//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Common.Logging;
using GT.Utils;

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
        /// 
        /// </summary>
        void Update();

        /// <summary>
        /// Flush all remaining messages.
        /// </summary>
        void Flush();

        /// <summary>
        /// Flush all remaining messages for the specific channelId.
        /// </summary>
        void FlushChannelMessages(byte channelId);

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
        public abstract void Update();
        public abstract void Flush();
        public abstract void FlushChannelMessages(byte channelId);

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
            IMarshalledResult mr = cnx.Marshal(m, t);
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

        public override void Update()
        {
            /// In a FIFO, we send 'em as they are scheduled.
        }

        public override void Flush()
        {
            /// In a FIFO, we send 'em as they are scheduled.
        }

        public override void FlushChannelMessages(byte channelId)
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
        protected bool disposed = false;

        protected Pool<PendingMessage> pmPool = new Pool<PendingMessage>(1, 5,
            () => new PendingMessage(), m => m.Clear(), null);

        // Could make this a LinkedList and avoid array copies; then again,
        // we're not expecting the backlog to get very high.
        protected List<PendingMessage> pending = new List<PendingMessage>();

        /// <summary>
        /// These values hold metadata for the round-robin.  
        /// </summary>
        /// <remarks>
        /// nextChannelIndex is an index
        /// into channels, indicating the channelId from which to take the next message.  
        /// </remarks>
        protected int nextChannelIndex = 0;
        /// <remarks>
        /// channels is a list of channel ids in first-come-first-served order;
        /// note that SystemMessages encode the system request as the channel id,
        /// and so will be inserted as if from a particular channel.  Finally,
        /// </remarks>
        protected IList<byte> channels = new List<byte>();
        /// <remarks>
        /// channelIndices is a reversee mapping of channel id to index within
        /// channels; it acts as a fast-lookup.
        /// </remarks>
        protected IDictionary<byte, int> channelIndices = 
            new Dictionary<byte, int>();

        /// <summary></summary>
        /// These dictionaries record the current sending state during the
        /// execution of Schedule() and Flush*().  They should otherwise be empty.
        /// </summary>
        /// <remarks>
        /// records the current message, its marshalled form, and the transport
        /// selected as per its MDR/CDR.
        /// </remarks>
        protected IDictionary<byte, ChannelSendingState> channelSendingStates = 
            new Dictionary<byte, ChannelSendingState>();
        /// <remarks>
        /// the accumulated packet for sending on a transport
        /// </remarks>
        WeakKeyDictionary<ITransport, TransportPacket> packetsInProgress = 
            new WeakKeyDictionary<ITransport, TransportPacket>();
        /// <remarks>
        /// Records messages that span multiple packets
        /// </remarks>
        WeakKeyDictionary<ITransport, IList<Message>> messagesInProgress =
            new WeakKeyDictionary<ITransport, IList<Message>>();
        /// <remarks>
        /// Records messages where all packets are in the process of being sent
        /// </remarks>
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
                FlushChannelMessages(msg.ChannelId);
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

        public override void Update()
        {
            // we could check whether there are too many messages pending and
            // call an interim flush, I suppose?
            // Note: we should do this only if we cease sending messages on transport backlog
            // Otherwise there should be no messages backed up at this point.
            // Leave this for GT 3.1
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
                pending.RemoveAll(pendingMsg => pendingMsg.Message.ChannelId == newMsg.ChannelId
                    && pendingMsg.Message.MessageType != MessageType.System);
            }

            if (!channelIndices.ContainsKey(newMsg.ChannelId))
            {
                channelIndices[newMsg.ChannelId] = channels.Count;
                channels.Add(newMsg.ChannelId);
            }

            PendingMessage pm = pmPool.Obtain();
            pm.Message = newMsg;
            pm.MDR = mdr;
            pm.CDR = cdr;

            MessageAggregation aggr = mdr == null ? cdr.Aggregation : mdr.Aggregation;
            if (aggr == MessageAggregation.Immediate)
            {
                pending.Insert(0, pm);
                nextChannelIndex = channelIndices[newMsg.ChannelId];
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
                byte channelId = channels[nextChannelIndex];

                if(ProcessNextPacket(channelId, csme) && channels.Count > 0)
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

        public override void FlushChannelMessages(byte channelId)
        {
            int channelIndex;
            if(!channelIndices.TryGetValue(channelId, out channelIndex))
            {
                return;
            }

            CannotSendMessagesError csme = new CannotSendMessagesError(cnx);
            // Process all the packets on the channel
            while (ProcessNextPacket(channelId, csme)) { /* keep going */ }

            FlushPendingPackets(csme);
            if (csme.IsApplicable)
            {
                NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.MessagesCannotBeSent,
                    "Unable to send messages", csme));
            }
            //Debug.Assert(messagesInProgress.Count == 0);
            Debug.Assert(packetsInProgress.Count == 0);
        }

        /// <summary>
        /// Process the next packet to be sent on <see cref="channelId"/>.  Return true
        /// if there was a packet to be processed on the channel, or false otherwise.
        /// </summary>
        /// <param name="channelId">the channel to process</param>
        /// <param name="csme">accumulated errors/exceptions</param>
        /// <returns>true if a packet was processed on the channel, or false if there are no packets
        /// to process or some error/exception arose</returns>
        protected virtual bool ProcessNextPacket(byte channelId, CannotSendMessagesError csme)
        {
            ChannelSendingState cs = default(ChannelSendingState);
            if (!FindNextPacket(channelId, csme, ref cs)) { return false; }
            Debug.Assert(cs.MarshalledForm != null && cs.MarshalledForm.HasPackets);
            // we could do something here to check if the transport was backlogged...
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
                tp.Append(next, 0, next.Length);
                next.Dispose();
            }
            if (cs.MarshalledForm.Finished)
            {
                messagesInProgress[cs.Transport].Remove(cs.PendingMessage.Message);
                sentMessages[cs.Transport].Add(cs.PendingMessage.Message);
                pmPool.Return(cs.PendingMessage);
                cs.PendingMessage = null;
                cs.MarshalledForm.Dispose();
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
                if (disposed) { return; }
            }
            packetsInProgress.Clear();
        }

        /// <summary>
        /// Find the next packet to be processed; return true if there are packets to 
        /// process on channelId.  The channel sending state is returned in <see cref="cs"/>.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="csme"></param>
        /// <param name="cs"></param>
        /// <returns></returns>
        protected virtual bool FindNextPacket(byte channelId, CannotSendMessagesError csme, 
            ref ChannelSendingState cs)
        {
            if (channelSendingStates.TryGetValue(channelId, out cs)) {
                if (cs.MarshalledForm != null)
                {
                    if(!cs.MarshalledForm.Finished) { return true; }
                    pmPool.Return(cs.PendingMessage);
                    cs.PendingMessage = null;
                    cs.MarshalledForm.Dispose();
                    cs.MarshalledForm = null;
                }
            }
            else
            {
                channelSendingStates[channelId] = cs = new ChannelSendingState();
            }
            PendingMessage pm;
            while (DetermineNextPendingMessage(channelId, out pm))
            {
                try
                {
                    ITransport transport = cnx.FindTransport(pm.MDR, pm.CDR);
                    IMarshalledResult mr = cnx.Marshal(pm.Message, transport);
                    Debug.Assert(!mr.Finished, "Marshallers shouldn't produce an empty result");
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
            // There were no messages for channelId.  So remove it from contention
            // and advance the nextChannelIndex to the next channel (actually, by
            // virtue of removing this channel, nextChannelIndex does point to the
            // next channel, so we only need to check for wrapping)
            channels.Remove(channelId);
            channelIndices.Remove(channelId);
            channelSendingStates.Remove(channelId);
            cs = null;
            if (nextChannelIndex >= channels.Count) { nextChannelIndex = 0; }
            return false;
        }

        protected virtual bool DetermineNextPendingMessage(byte channelId, out PendingMessage next)
        {
            next = null;
            for (int index = 0; index < pending.Count; index++) {
                next = pending[index];
                if (next.Message.MessageType == MessageType.System
                    || next.Message.ChannelId == channelId) 
                {
                    pending.RemoveAt(index);
                    return true;
                }
            }
            return false;
        }

        public override void Dispose()
        {
            disposed = true;
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

    public class ChannelSendingState {
        public IMarshalledResult MarshalledForm;
        public PendingMessage PendingMessage;
        public ITransport Transport;
    }
}
