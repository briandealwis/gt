using System.Collections.Generic;
using System.Diagnostics;

namespace GT.Net
{
    /// <summary>
    /// Packet schedulers are provided the opportunity to come up with alternative
    /// packet scheduling schemes, such as round-robin or weighted fair queueing.
    /// </summary>
    public interface IPacketScheduler
    {
        event Action<ITransport, TransportPacket> PacketScheduled;

        /// <summary>
        /// Return this scheduler's associated transport.
        /// </summary>
        public ITransport { get; }

        /// <summary>
        /// Schedule the packets forming the provided message.  When the message
        /// has been sent, be sure to dispose of the marshalled result.
        /// </summary>
        /// <param name="m">the message being sent</param>
        /// <param name="mr">the message's marshalled representation</param>
        /// <param name="mdr">the message's delivery requirements (overrides 
        ///     <see cref="cdr"/> if not null)</param>
        /// <param name="cdr">the message's channel's delivery requirements</param>
        void Schedule(Message m, MarshalledResult mr, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr);

        /// <summary>
        /// There are no more messages to be scheduled; flush any remaining packets.
        /// </summary>
        void Flush();
    }

    /// <summary>
    /// A FIFO scheduler.
    /// </summary>
    public abstract class AbstractPacketScheduler
    {
        public event Action<ITransport, TransportPacket> PacketScheduled;

        public ITransport Transport { get; private set; }

        public AbstractPacketScheduler(ITransport t)
        {
            Transport = t;
        }

        public abstract void Schedule(Message m, MarshalledResult mr, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr);

        public abstract void Flush();

        /// <summary>
        /// Utility method to trigger the packet-scheduled event.
        /// </summary>
        /// <param name="tp"></param>
        protected void SchedulePacket(TransportPacket tp)
        {
            if(PacketScheduled == null) { return; }
            PacketScheduled(Transport, tp);
        }
    }


    /// <summary>
    /// A FIFO scheduler.
    /// </summary>
    public class FIFOPacketScheduler : AbstractPacketScheduler
    {
        public FIFOPacketScheduler(ITransport t) : base(t)
        {
        }

        public override void Schedule(Message m, MarshalledResult mr, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            TransportPacket tp;
            while((tp = mr.RemovePacket()) != null) 
            {
                SchedulePacket(tp);
            }
            mr.Dispose();
        }

        /// <summary>
        /// In a FIFO, we schedule 'em as they come in.
        /// </summary>
        public override void Flush()
        {
        }

    }

    /// <summary>
    /// A round-robin scheduler: we alternate between each channel.
    /// We rotate on a per-channel basis to ensure a channel sending many 
    /// packets (e.g., a video stream) won't starve other channels.
    /// </summary>
    public class RoundRobinPacketScheduler : AbstractPacketScheduler
    {
        static protected Pool<IList<Message>> _messages = new Pool<IList<Message>>(1, 5,
            () => new List<Message>(1), (m) => m.Clear(), null);

        protected int nextChannelIndex = 0;     // where we start from
        protected IList<byte> channels = new List<byte>();
        protected IDictionary<byte,int> channelIndices = 
            new Dictionary<byte, int>();
        protected IDictionary<byte, Queue<Message>> channelMessages = 
            new Dictionary<byte, Queue<Message>>();
        protected IDictionary<Message, MarshalledResult> marshalledResults =
            new Dictionary<Message, MarshalledResult>();
        protected int outstandingPacketCount = 0;
        protected IList<Message> finished = new List<Message>();
        protected IDictionary<TransportPacket,IList<Message>> sentMessages =
            new Dictionary<TransportPacket,IList<Message>>();

        protected TransportPacket activePacket = null;

        public RoundRobinPacketScheduler(ITransport t) : base(t)
        {
            t.PacketSent += _transport_PacketSent;
        }

        public override void Schedule(Message m, MarshalledResult mr, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            Debug.Assert(mr.HasPackets);

            // First register this message
            marshalledResults[m] = mr;

            Queue<Message> messageQueue;
            if(!channelMessages.TryGetValue(m.Id, out messageQueue)) {
                channelMessages[m.Id] = messageQueue = new Queue<Message>();
            }
            messageQueue.Enqueue(m);
            
            int channelIndex;
            if(!channelIndices.TryGetValue(m.Id, out channelIndex)) {
                channel.Add(m.Id);
                channelIndices[m.Id] = channelIndex = channels.Count - 1;
            }
            packetCount += mr.PacketCount;
        }

        protected void ProcessMessage()
        {
            // Now try adding packets
            int startChannel = nextChannelIndex;
             while(outstandingPacketCount > 0 && Transport.Backlog != 0) {
                byte channel = channels[nextChannelIndex];
                nextChannelIndex = (nextChannelIndex + 1) % channels.Count;
                if(channelMessages[channel].Count > 0) {
                    Message message = channelMessages[channel].Peek();
                    MarshalledResult mr = marshalledResults[message];
                    // To be still listed, there must be a packet
                    TransportPacket other = mr.RemovePacket();
                    outstandingPacketCount--;
                    if(!mr.HasPackets) { channelMessages[channel].Dequeue(); }
                    if(activePacket == null) {
                        activePacket = other;
                    if(!mr.HasPackets) { finished.Add(message); }
                    } 
                    else if(activePacket.Length + other.Length <= Transport.MaximumPacketSize)
                    {
                        activePacket.Add(other)
                        if(!mr.HasPackets) { finished.Add(message); }
                    }
                    else
                    {
                        TransportPacket toBeScheduled = activePacket;
                        activePacket = other;
                        sentMessages[p] = finished;
                        finished = _messages.Obtain();
                        SchedulePacket(toBeScheduled);
                    }
                }
            }
            if(Transport.Backlog != 0 && activePacket.Length > 0)
            {
                Debug.Assert(outstandingPacketCount == 0);
                TransportPacket toBeScheduled = activePacket;
                activePacket = other;
                sentMessages[p] = finished;
                finished = _messages.Obtain();
                SchedulePacket(activePacket);
            }
        }

        public override void Flush()
        {
            if(Transport.Backlog != 0 && outstandingPacketCount > 0)
            {
                ProcessMessage();
            }
        }

        // FIXME: I really don't know about this
        protected override void SchedulePacket(TransportPacket p)
        {
            try
            {
                base.SchedulePacket(p);
            }
            catch(TransportError e)
            {
                throw new CannotSendMessagesError(this, e, finished);
            }
            NotifyMessagesSent(finished, transport);
        }

        protected void _transport_SentPacket(TransportPacket p, ITransport t)
        {
            Debug.Assert(t == Transport);
            IList<Message> messages;
            if(!sentMessages.TryGetValue(p, out messages)) {
                Debug.Assert(false, "uh oh, unknown messages!");
            }
            foreach(Message m in messages) {
                MarshalledResult mr = marshalledResults[m].Remove(m);
                mr.Dispose();
            }
            // FIXME: pool.Return(messages);
        }
    }

}