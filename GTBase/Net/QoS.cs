using System;
using System.Net;
using GT;
using System.Collections.Generic;

namespace GT.Net
{
    /// <summary>Guarantees on message delivery.</summary>
    public enum Reliability
    {
        /// <summary>The selected transport need not guarantee reliable delivery.</summary>
        Unreliable = 0,
        /// <summary>The selected transport must guarantee reliable delivery.</summary>
        Reliable = 1,
    }

    /// <summary>
    /// Guarantees or requirements for ordering of packets/messages.  If two packets are
    /// sent in a particular order, does the transport guarantee that the packets
    /// will be received in that order?
    /// </summary>
    public enum Ordering
    {
        Unordered = 0,
        Sequenced = 1,
        Ordered = 2,
    }

    /// <summary>Can this message be aggregated?  This ties into latency-sensitivity.</summary>
    public enum MessageAggregation
    {
        /// <summary>This message can be saved, and sent depending on the 
        /// specified message ordering</summary>
        Aggregatable = 0,

       /// <summary>This message will be sent immediately, without worrying 
        /// about any saved-to-be-aggregated messages</summary>
        Immediate = 1,

        /// <summary>This message will flush all other saved-to-be-aggregated 
        /// messages on this channel out beforehand</summary>
        FlushChannel = 2,

        /// <summary>This message will flush all other saved-to-be-aggregated 
        /// messages out beforehand</summary>
        FlushAll = 3,
    }

    ///// <summary>Should receiving clients keep old messages?</summary>
    ///// FIXME: Is this the same as freshness?
    //public enum MessageTimeliness
    //{
    //    /// <summary>Keep old messages</summary>
    //    NonRealTime = 0,
    //    /// <summary>Throw away old messages</summary>
    //    RealTime = 1,
    //}

    // Transports provide guarantees.
    // Channels and messages specify requirements or expectations.

    /// <summary>
    /// Describes the expected QoS requirements for a particular message.  These requirements override
    /// the defaults specified by the sending channel as described by a
    /// <see cref="ChannelDeliveryRequirements"/>.
    /// 
    /// Note: users should pay close attention to the aggregation requirements!
    /// Any use of MessageAggregation.Aggregatable requires periodically <em>manually flushing</em>
    /// the channel.
    /// </summary>
    public class MessageDeliveryRequirements
    {
        // NOTE: default values should be for the *LEAST* stringent possible.
        protected Reliability reliability = Reliability.Unreliable;
        protected Ordering ordering = Ordering.Unordered;
        protected MessageAggregation aggregation = MessageAggregation.Aggregatable;
        //protected MessageTimeliness timeliness;

        public Reliability Reliability
        {
            get { return reliability; }
            set { reliability = value; }
        }
        public Ordering Ordering
        {
            get { return ordering; }
            set { ordering = value; }
        }
        public MessageAggregation Aggregation 
        {
            get { return aggregation; }
            set { aggregation = value; }
        }
        //public MessageTimeliness Timeliness { get { return timeliness; } }

        public MessageDeliveryRequirements(Reliability d, MessageAggregation a, Ordering o)
            //, MessageTimeliness t)
        {
            reliability = d;
            aggregation = a;
            ordering = o;
            //timeliness = t;
        }

        protected MessageDeliveryRequirements() {}

        /// <summary>
        /// Select a transport meeting the requirements as specified by this instance.
        /// Assume that <c>transports</c> is in a sorted order.
        /// </summary>
        /// <param name="transports">the sorted list of available transports</param>
        public virtual ITransport SelectTransport(IList<ITransport> transports) 
        {
            foreach(ITransport t in transports) {
                if (MeetsRequirements(t)) { return t; }
            }
            return null;
        }

        /// <summary>
        /// Test whether a transport meets the requirements as specified by this instance.
        /// </summary>
        /// <param name="transport">the transport to test</param>
        protected virtual bool MeetsRequirements(ITransport transport)
        {
            if (transport.Reliability < Reliability) { return false; }
            if (transport.Ordering < Ordering) { return false; }
            return true;    // passed our test: go for gold!
        }

        /// <summary>
        /// An instance representing the most strict requirements possible.
        /// </summary>
        public static MessageDeliveryRequirements MostStrict
        {
            get
            {
                MessageDeliveryRequirements mdr = new MessageDeliveryRequirements();
                mdr.Reliability = Reliability.Reliable;
                mdr.Ordering = Ordering.Ordered;
                mdr.Aggregation = MessageAggregation.FlushAll;
                return mdr;
            }
        }

        /// <summary>
        /// An instance representing the least strict requirements possible.
        /// </summary>
        public static MessageDeliveryRequirements LeastStrict
        {
            get { return new MessageDeliveryRequirements(); }
        }

    }

    /// <summary>
    /// Describes the expected QoS requirements for a channel.  These requirements form
    /// the default for any message sent on the configured channel; these defaults can
    /// be overridden on a per-message basis by providing a <see cref="MessageDeliveryRequirements"/>.
    /// 
    /// Note: users should pay close attention to the aggregation requirements!
    /// Any use of MessageAggregation.Aggregatable requires periodically <em>manually flushing</em>
    /// the channel.
    /// </summary>
    public class ChannelDeliveryRequirements
    {
        // FIXME: minimum delay, flow rates (min, max), maximum jitter
        // NOTE: default values should be for the *LEAST* stringent possible.

        protected Reliability reliability = Reliability.Unreliable;
        protected Ordering ordering = Ordering.Unordered;
        protected MessageAggregation aggregation = MessageAggregation.Aggregatable;
        protected int aggregationTimeout = -1;
        //protected MessageTimeliness timeliness;
        //protected MessageOrder ordering;

        public Reliability Reliability
        {
            get { return reliability; }
            set { reliability = value; }
        }
        public Ordering Ordering
        {
            get { return ordering; }
            set { ordering = value; }
        }
        public MessageAggregation Aggregation 
        {
            get { return aggregation; }
            set { aggregation = value; }
        }
        public int AggregationTimeout
        {
            get { return aggregationTimeout; }
            set { aggregationTimeout = value; }
        }
        //public MessageOrder Ordering { get { return ordering; } }
        //public MessageTimeliness Timeliness { get { return timeliness; } }

        public ChannelDeliveryRequirements(Reliability d, MessageAggregation a, Ordering o)
            //, MessageTimeliness t)
        {
            reliability = d;
            aggregation = a;
            ordering = o;
            //timeliness = t;
        }

        protected ChannelDeliveryRequirements() {}

        /// <summary>
        /// Select a transport meeting the requirements as specified by this instance. 
        /// Assume that <c>transports</c> is in a sorted order.
        /// </summary>
        /// <param name="transports">the sorted list of available transports</param>
        public virtual ITransport SelectTransport(IList<ITransport> transports)
        {
            foreach (ITransport t in transports)
            {
                if (MeetsRequirements(t)) { return t; }
            }
            return null;
        }

        /// <summary>
        /// Test whether a transport meets the requirements as specified by this instance
        /// </summary>
        /// <param name="transport">the transport to test</param>
        protected virtual bool MeetsRequirements(ITransport transport)
        {
            if (transport.Reliability < Reliability) { return false; }
            if (transport.Ordering < Ordering) { return false; }
            // could do something about flow characteristics or jitter
            return true;    // passed our test: go for gold!
        }

        #region Preconfigured QoS Channel Descriptors
        /// 
        /// This region defines some general examples of channel descriptors.
        /// Note: users should pay close attention to the aggregation requirements!
        /// Any use of MessageAggregation.Aggregatable requires periodically <em>manually flushing</em>
        /// the channel.
        /// 

        /// <summary>A descriptor with the strictest possible requirements.</summary>
        public static ChannelDeliveryRequirements MostStrict
        {
            get
            {
                ChannelDeliveryRequirements cdr = new ChannelDeliveryRequirements();
                cdr.Reliability = Reliability.Reliable;
                cdr.Ordering = Ordering.Ordered;
                cdr.Aggregation = MessageAggregation.FlushAll;
                return cdr;
            }
        }

        /// <summary>A descriptor with the least strict requirements possible.</summary>
        public static ChannelDeliveryRequirements LeastStrict
        {
            get { return new ChannelDeliveryRequirements(); }
        }

        /// 
        /// These examples are taken from J Dyck, C Gutwin, TCN Graham, D Pinelle (2007). 
        /// Beyond the LAN: Techniques from network games for improving groupware performance. 
        /// In Proc. Int. Conf. on Supporting Group Work (GROUP), 291–300. New York, USA: ACM. 
        /// doi:10.1145/1316624.1316669
        /// 

        /// <summary>A descriptor for telepointer data: these should be sent immediately,
        /// they should not be out-of-order, but it's ok if they're lost (unreliable).</summary>
        public static ChannelDeliveryRequirements TelepointerLike
        {
            get
            {
                ChannelDeliveryRequirements cdr = new ChannelDeliveryRequirements();
                cdr.Reliability = Reliability.Unreliable;
                cdr.Ordering = Ordering.Sequenced;
                cdr.Aggregation = MessageAggregation.Immediate;
                return cdr;
            }
        }

        /// <summary>A descriptor for chat messages: such messages should be received in order
        /// (e.g., after any previous chat messages) and must be received.  They should be
        /// sent right away.</summary>
        public static ChannelDeliveryRequirements ChatLike
        {
            get
            {
                ChannelDeliveryRequirements cdr = new ChannelDeliveryRequirements();
                cdr.Reliability = Reliability.Reliable;
                cdr.Ordering = Ordering.Ordered;
                cdr.Aggregation = MessageAggregation.FlushChannel;
                return cdr;
            }
        }

        /// <summary>A descriptor for command messages: such messages should be received in order
        /// (as they may depend on the results of previous commands) and must be received.  
        /// They should be sent right away.</summary>
        public static ChannelDeliveryRequirements CommandsLike
        {
            get
            {
                ChannelDeliveryRequirements cdr = new ChannelDeliveryRequirements();
                cdr.Reliability = Reliability.Reliable;
                cdr.Ordering = Ordering.Ordered;
                cdr.Aggregation = MessageAggregation.FlushChannel;
                return cdr;
            }
        }

        /// <summary>A descriptor for session notification messages: such messages must be 
        /// received but may be received out of order.  They should cause all other pending
        /// messages to be sent first.</summary>
        public static ChannelDeliveryRequirements SessionLike
        {
            get
            {
                ChannelDeliveryRequirements cdr = new ChannelDeliveryRequirements();
                cdr.Reliability = Reliability.Reliable;
                cdr.Ordering = Ordering.Unordered;  // ?
                cdr.Aggregation = MessageAggregation.FlushAll;
                return cdr;
            }
        }

        /// <summary>A descriptor for data messages: such messages must be 
        /// received and in a strict order.  <em>NOTE: data messages can be
        /// aggregated and require the channel to be periodically flushed.</em></summary>
        public static ChannelDeliveryRequirements Data
        {
            get
            {
                ChannelDeliveryRequirements cdr = new ChannelDeliveryRequirements();
                cdr.Reliability = Reliability.Reliable;
                cdr.Ordering = Ordering.Ordered;
                cdr.Aggregation = MessageAggregation.Aggregatable;
                return cdr;
            }
        }
        #endregion
    }

    public interface ITransportDeliveryCharacteristics
    {
        /// <summary>
        /// Are packets sent using this transport guaranteed to reach the other side?
        /// </summary>
        Reliability Reliability { get; }

        /// <summary>
        /// Are packets sent using this transport received in the same order on the other side?
        /// </summary>
        Ordering Ordering { get; }

        /// <summary>
        /// Delay on the transport in milliseconds.
        /// </summary>
        float Delay { get; set; }

        // loss?
        // jitter?

    }
}
