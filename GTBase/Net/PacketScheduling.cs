﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Common.Logging;
using GT.Utils;
using GT.Weak;

namespace GT.Net
{
    /// <summary>
    /// Packet schedulers are provided the opportunity to come up with alternative
    /// packet scheduling schemes, such as round-robin or weighted fair queueing.
    /// Packet schedulers are created with two delegates for (1) obtaining the
    /// list of active transports, and (2) to marshal a message for a particular
    /// transport.
    /// </summary>
    /// <remarks>
    /// Packet schedulers are not thread safe.
    /// </remarks>
    public interface IPacketScheduler : IDisposable
    {
        event ErrorEventNotication ErrorEvent;
        event Action<ICollection<Message>, ITransport> MessagesSent;

        /// <summary>
        /// Schedule the packets forming the provided message.  When the message
        /// has been sent, be sure to dispose of the marshalled result.
        /// </summary>
        /// <param name="m">the message being sent</param>
        /// <param name="mdr">the message's delivery requirements (overrides 
        ///     <see cref="cdr"/> if not null)</param>
        /// <param name="cdr">the message's channel's delivery requirements</param>
        void Schedule(Message m, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr);

        /// <summary>
        /// Flush all remaining messages.
        /// </summary>
        void Flush();

        /// <summary>
        /// Flush all remaining messages for the specific channel.
        /// </summary>
        void FlushChannelMessages(byte channel);

        /// <summary>
        /// Reset the instance; throw away all data.
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Provides useful base for other schedulers.
    /// </summary>
    public abstract class AbstractPacketScheduler : IPacketScheduler
    {
        static protected Pool<SingleItem<Message>> _singleMessage = 
            new Pool<SingleItem<Message>>(1, 5,
                () => new SingleItem<Message>(), (l) => l.Clear(), null);

        protected ILog log;

        protected IConnexion cnx;
        public event ErrorEventNotication ErrorEvent;
        public event Action<ICollection<Message>, ITransport> MessagesSent;

        public AbstractPacketScheduler(IConnexion cnx)
        {
            log = LogManager.GetLogger(GetType());

            this.cnx = cnx;
        }

        public abstract void Schedule(Message m, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr);

        public abstract void Flush();
        public abstract void FlushChannelMessages(byte channel);

        protected void NotifyMessagesSent(ICollection<Message> messages, ITransport transport)
        {
            if(MessagesSent != null)
            {
                MessagesSent(messages, transport);
            }
        }

        protected void NotifyMessageSent(Message message, ITransport transport)
        {
            if (MessagesSent == null)
            {
                return;
            }
            SingleItem<Message> list = _singleMessage.Obtain();
            try
            {
                list[0] = message;
                MessagesSent(list, transport);
            }
            finally
            {
                _singleMessage.Return(list);
            }
        }

        protected virtual void FastpathSendMessage(ITransport t, Message m)
        {
            MarshalledResult mr = cnx.Marshal(m, t);
            try
            {
                TransportPacket tp;
                while ((tp = mr.RemovePacket()) != null)
                {
                    cnx.SendPacket(t, tp);
                }
                NotifyMessageSent(m, t);
            }
            finally
            {
                mr.Dispose();
            }
        }

        protected void NotifyError(ErrorSummary es)
        {
            if(ErrorEvent != null) { ErrorEvent(es); }
        }

        public virtual void Reset()
        {
            // nothing to dipose of here
        }

        public virtual void Dispose()
        {
            // nothing to dipose of here
        }
    }


    /// <summary>
    /// A FIFO scheduler where each message is shipped out as it is received
    /// </summary>
    public class ImmediatePacketScheduler : AbstractPacketScheduler
    {
        public ImmediatePacketScheduler(IConnexion cnx) : base(cnx) { }

        public override void Schedule(Message m, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            try
            {
                ITransport t = cnx.FindTransport(mdr, cdr);
                FastpathSendMessage(t, m);
            }
            catch (NoMatchingTransport e)
            {
                NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.MessagesCannotBeSent,
                    e.Message, new PendingMessage(m, mdr, cdr), e));
            }
        }

        public override void Flush()
        {
            /// In a FIFO, we send 'em as they are scheduled.
        }

        public override void FlushChannelMessages(byte channel)
        {
            /// In a FIFO, we send 'em as they are scheduled.
        }
    }

    /// <summary>
    /// A round-robin scheduler: we alternate between each channel.
    /// We rotate on a per-channel basis to ensure a channel sending many 
    /// packets (e.g., a video stream) won't starve other channels.
    /// </summary>
    public class RoundRobinPacketScheduler : AbstractPacketScheduler
    {
        protected Pool<PendingMessage> pmPool = new Pool<PendingMessage>(1, 5,
            () => new PendingMessage(), m => m.Clear(), null);

        protected List<PendingMessage> pending = new List<PendingMessage>();

        protected int nextChannelIndex = 0;     // where we start from
        protected IList<byte> channels = new List<byte>();
        protected IDictionary<byte,int> channelIndices = 
            new Dictionary<byte, int>();

        protected IDictionary<byte, ChannelSendingState> channelSendingStates = new Dictionary<byte, ChannelSendingState>();
        WeakKeyDictionary<ITransport, TransportPacket> packetsInProgress = 
            new WeakKeyDictionary<ITransport, TransportPacket>();
        WeakKeyDictionary<ITransport, IList<Message>> messagesInProgress =
            new WeakKeyDictionary<ITransport, IList<Message>>();
        WeakKeyDictionary<ITransport, IList<Message>> sentMessages =
            new WeakKeyDictionary<ITransport, IList<Message>>();

        public RoundRobinPacketScheduler(IConnexion cnx) : base(cnx) {}

        public override void Schedule(Message msg, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr) 
        {
            MessageAggregation aggr = mdr == null ? cdr.Aggregation : mdr.Aggregation;

            // Place the message, performing any channel-compaction if so specified
            // by cdr.Freshness
            Aggregate(msg, mdr, cdr);

            if (aggr == MessageAggregation.FlushChannel)
            {
                // make sure ALL other messages on this CHANNEL are sent, and then send <c>msg</c>.
                FlushChannelMessages(msg.Channel);
            }
            else if (aggr == MessageAggregation.FlushAll)
            {
                // make sure ALL messages are sent, then send <c>msg</c>.
                Flush();
            }
            else if (aggr == MessageAggregation.Immediate)
            {
                // bundle <c>msg</c> first and then cram on whatever other messages are waiting.
                Flush();
            }
        }

        /// <summary>Adds the message to a list, waiting to be sent out.</summary>
        /// <param name="newMsg">The message to be aggregated</param>
        /// <param name="mdr">How it should be sent out (potentially null)</param>
        /// <param name="cdr">General delivery instructions for this message's channel.</param>
        private void Aggregate(Message newMsg, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Debug.Assert(mdr != null || cdr != null);
            if (newMsg.MessageType != MessageType.System && cdr != null
                && cdr.Freshness == Freshness.IncludeLatestOnly)
            {
                pending.RemoveAll(pendingMsg => pendingMsg.Message.Channel == newMsg.Channel
                    && pendingMsg.Message.MessageType != MessageType.System);
            }

            if (!channelIndices.ContainsKey(newMsg.Channel))
            {
                channelIndices[newMsg.Channel] = channels.Count;
                channels.Add(newMsg.Channel);
            }

            PendingMessage pm = pmPool.Obtain();
            pm.Message = newMsg;
            pm.MDR = mdr;
            pm.CDR = cdr;

            MessageAggregation aggr = mdr == null ? cdr.Aggregation : mdr.Aggregation;
            if (aggr == MessageAggregation.Immediate)
            {
                pending.Insert(0, pm);
            }
            else
            {
                pending.Add(pm);
            }
        }

        public override void Flush()
        {
            CannotSendMessagesError csme = new CannotSendMessagesError(cnx);
            while (channels.Count > 0)
            {
                byte channel = channels[nextChannelIndex];

                if(ProcessNextPacket(channel, csme) && channels.Count > 0)
                {
                    nextChannelIndex = (nextChannelIndex + 1) % channels.Count;
                }
            }

            FlushPendingPackets(csme);
            if (csme.IsApplicable)
            {
                NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.MessagesCannotBeSent,
                    "Unable to send messages", csme));
            }
            Debug.Assert(channels.Count == 0);
            Debug.Assert(pending.Count == 0);
            //Debug.Assert(messagesInProgress.Count == 0);
            Debug.Assert(packetsInProgress.Count == 0);
        }

        public override void FlushChannelMessages(byte channel)
        {
            int channelIndex;
            if(!channelIndices.TryGetValue(channel, out channelIndex))
            {
                return;
            }

            CannotSendMessagesError csme = new CannotSendMessagesError(cnx);
            while (ProcessNextPacket(channel, csme));

            FlushPendingPackets(csme);
            if (csme.IsApplicable)
            {
                NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.MessagesCannotBeSent,
                    "Unable to send messages", csme));
            }
            //Debug.Assert(messagesInProgress.Count == 0);
            Debug.Assert(packetsInProgress.Count == 0);
        }

        protected virtual bool ProcessNextPacket(byte channel, CannotSendMessagesError csme)
        {
            ChannelSendingState cs = default(ChannelSendingState);
            if (!FindNextPacket(channel, csme, ref cs)) { return false; }
            Debug.Assert(cs.MarshalledForm != null && cs.MarshalledForm.HasPackets);
            TransportPacket tp;
            if(!packetsInProgress.TryGetValue(cs.Transport, out tp) || tp == null)
            {
                tp = packetsInProgress[cs.Transport] = new TransportPacket();
            }
            if (!sentMessages.ContainsKey(cs.Transport)) { sentMessages[cs.Transport] = new List<Message>(); }
            if (!messagesInProgress.ContainsKey(cs.Transport)) { messagesInProgress[cs.Transport] = new List<Message>(); }

            TransportPacket next = cs.MarshalledForm.RemovePacket();
            if (tp.Length + next.Length >= cs.Transport.MaximumPacketSize)
            {
                try 
                {
                    cnx.SendPacket(cs.Transport, tp);
                }
                catch(TransportError e)
                {
                    csme.AddAll(e, sentMessages[cs.Transport]);
                    csme.AddAll(e, messagesInProgress[cs.Transport]);
                    sentMessages[cs.Transport].Clear();
                    messagesInProgress[cs.Transport].Clear();
                    packetsInProgress.Remove(cs.Transport);
                    return true;
                }
                NotifyMessagesSent(sentMessages[cs.Transport], cs.Transport);
                sentMessages[cs.Transport].Clear();
                packetsInProgress[cs.Transport] = tp = next;
            }
            else
            {
                tp.Add(next, 0, next.Length);
                next.Dispose();
            }
            if (cs.MarshalledForm.Finished)
            {
                messagesInProgress[cs.Transport].Remove(cs.PendingMessage.Message);
                sentMessages[cs.Transport].Add(cs.PendingMessage.Message);
                pmPool.Return(cs.PendingMessage);
                cs.MarshalledForm.Dispose();
                cs.PendingMessage = null;
                cs.MarshalledForm = null;
            }
            else
            {
                messagesInProgress[cs.Transport].Add(cs.PendingMessage.Message);
            }
            return true;
        }

        protected virtual void FlushPendingPackets(CannotSendMessagesError csme)
        {
            /// And send any pending stuff
            foreach (ITransport t in packetsInProgress.Keys)
            {
                TransportPacket tp;
                if(!packetsInProgress.TryGetValue(t, out tp) || tp == null
                    || tp.Length == 0)
                {
                    continue;
                }
                try
                {
                    cnx.SendPacket(t, tp);
                    NotifyMessagesSent(sentMessages[t], t);
                    sentMessages[t].Clear();
                }
                catch(TransportError e)
                {
                    csme.AddAll(e, sentMessages[t]);
                    csme.AddAll(e, messagesInProgress[t]);
                }
            }
            packetsInProgress.Clear();
        }

        protected virtual bool FindNextPacket(byte channel, CannotSendMessagesError csme, 
            ref ChannelSendingState cs)
        {
            if (channelSendingStates.TryGetValue(channel, out cs)) {
                if (cs.MarshalledForm != null)
                {
                    if(!cs.MarshalledForm.Finished) { return true; }
                    pmPool.Return(cs.PendingMessage);
                    cs.PendingMessage = null;
                    cs.MarshalledForm.Dispose();
                    cs.MarshalledForm = null;
                }
            }
            PendingMessage pm;
            while (DetermineNextPendingMessage(channel, out pm))
            {
                try
                {
                    ITransport transport = cnx.FindTransport(pm.MDR, pm.CDR);
                    MarshalledResult mr = cnx.Marshal(pm.Message, transport);
                    if (mr.Finished)
                    {
                        // this shouldn't happen
                        mr.Dispose();
                        pmPool.Return(pm);
                        continue;
                    }
                    cs.MarshalledForm = mr;
                    cs.PendingMessage = pm;
                    cs.Transport = transport;
                    return true;
                }
                catch(NoMatchingTransport e)
                {
                    csme.Add(e, pm);
                    continue;
                }
                catch (MarshallingException e)
                {
                    csme.Add(e, pm);
                    continue;
                }
            }
            channels.Remove(channel);
            channelIndices.Remove(channel);
            channelSendingStates.Remove(channel);
            return false;
        }

        protected virtual bool DetermineNextPendingMessage(byte channel, out PendingMessage next)
        {
            next = default(PendingMessage);
            for (int index = 0; index < pending.Count; index++) {
                next = pending[index];
                if (next.Message.MessageType == MessageType.System
                    || next.Message.Channel == channel) 
                {
                    pending.RemoveAt(index);
                    return true;
                }
            }
            return false;
        }

        public override void Dispose()
        {
            Reset();
        }

        public override void Reset()
        {
            foreach (ChannelSendingState cs in channelSendingStates.Values)
            {
                if (cs.PendingMessage != null)
                {
                    pmPool.Return(cs.PendingMessage);
                }
                if (cs.MarshalledForm != null)
                {
                    cs.MarshalledForm.Dispose();
                }
            }
            Debug.Assert(pmPool.Out == 0);
            foreach (TransportPacket tp in packetsInProgress.Values)
            {
                tp.Dispose();
            }
            packetsInProgress.Clear();
            channelSendingStates.Clear();
            pending.Clear();
            channels.Clear();
            channelIndices.Clear();
            nextChannelIndex = 0;
        }
    }

    public struct ChannelSendingState {
        public MarshalledResult MarshalledForm;
        public PendingMessage PendingMessage;
        public ITransport Transport;
    }
}