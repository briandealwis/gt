using System;
using System.Net;
using GT;

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

        public static MessageDeliveryRequirements LeastStrict
        {
            get { return new MessageDeliveryRequirements(); }
        }

    }

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

        public static ChannelDeliveryRequirements LeastStrict
        {
            get { return new ChannelDeliveryRequirements(); }
        }

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

        public static ChannelDeliveryRequirements SessionLike
        {
            get
            {
                ChannelDeliveryRequirements cdr = new ChannelDeliveryRequirements();
                cdr.Reliability = Reliability.Reliable;
                cdr.Ordering = Ordering.Unordered;
                cdr.Aggregation = MessageAggregation.FlushAll;
                return cdr;
            }
        }

        public static ChannelDeliveryRequirements Data
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
