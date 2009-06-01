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

using System.Collections.Generic;
using System.Text;

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
    /// Guarantees or requirements for ordering of packets/messages. 
    /// If two packets are sent in a particular order, and both are
    /// received (see <see cref="Reliability"/>), does the transport
    /// guarantee that the packets will be received in that order?
    /// </summary>
    public enum Ordering
    {
	    /// <summary>There are no guarantees on packet order.</summary>
        Unordered = 0,
	    /// <summary>Packets are received in order, but there may be duplicates.</summary>
        Sequenced = 1,
	    /// <summary>Packets are received in order, and with no duplicates.</summary>
        Ordered = 2,
    }

    /// <summary>Can this message be aggregated?  This setting ties into 
    /// latency-sensitivity.</summary>
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

    /// <summary>
    /// Should receiving clients receive all messages sent on a channel or
    /// do they represent intermediate values only?  (This is only applicable with
    /// <see cref="MessageAggregation.Immediate"/>, since the other values of
    /// <see cref="MessageAggregation"/> cause the channels to be flushed upon send, 
    /// and thus there should be nothing in the channels.)
    /// </summary>
    public enum Freshness
    {
        /// <summary>All messages are relevant and should be included.</summary>
        IncludeAll = 0,
        /// <summary>Throw away old messages, including only the latest message.</summary>
        IncludeLatestOnly = 1,
    }

    // Transports provide guarantees.
    // Channels and messages specify requirements or expectations.

    /// <summary>
    /// Describes the expected QoS requirements for a particular message.  These requirements override
    /// the defaults specified by the sending channel as described by a
    /// <see cref="ChannelDeliveryRequirements"/>.
    /// 
    /// Note: users should pay close attention to the aggregation requirements!
    /// Any use of <see cref="MessageAggregation.Aggregatable"/> requires that the
    /// appliction <em>manually flush</em> the channel periodically.
    /// </summary>
    public class MessageDeliveryRequirements
    {
        /// <summary>
        /// A default constructor that uses the minimum requirements possible.
        /// </summary>
        protected MessageDeliveryRequirements()
        {
            Reliability = Reliability.Unreliable;
            Ordering = Ordering.Unordered;
            Aggregation = MessageAggregation.Aggregatable;
        }

        /// <summary>
        /// A constructor for specifying the most common requirements.
        /// </summary>
        /// <param name="d">the minimum reliability requirement</param>
        /// <param name="a">the minimum aggregation requirement</param>
        /// <param name="o">the minimum ordering requirement</param>
        public MessageDeliveryRequirements(Reliability d, MessageAggregation a, Ordering o)
        {
            Reliability = d;
            Aggregation = a;
            Ordering = o;
        }

        /// <summary>
        /// Get/set the minimum reliability requirement
        /// </summary>
        /// <seealso cref="Reliability"/>
        public Reliability Reliability {get; set; }

        /// <summary>
        /// Get/set the minimum ordering requirement
        /// </summary>
        /// <seealso cref="Ordering"/>
        public Ordering Ordering { get; set; }

        /// <summary>
        /// Get/set the minimum aggregation requirement
        /// </summary>
        /// <seealso cref="Aggregation"/>
        public MessageAggregation Aggregation { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Reliability);
            sb.Append(',');
            sb.Append(Ordering);
            sb.Append(',');
            sb.Append(Aggregation);
            return sb.ToString();
        }

        /// <summary>
        /// Select a transport meeting the requirements as specified by this instance.
        /// Assume that <see cref="candidates"/> is in a sorted order.
        /// </summary>
        /// <param name="candidates">the sorted list of available transports</param>
        public virtual ITransport SelectTransport(IList<ITransport> candidates) 
        {
            foreach(ITransport candidate in candidates) {
                // wouldn't it be cool to check if the transport is backlogged?
                if (MeetsRequirements(candidate)) { return candidate; }
            }
            return null;
        }

        /// <summary>
        /// Test whether a candidate meets the requirements as specified by this instance.
        /// </summary>
        /// <param name="candidate">the transport to test</param>
        public virtual bool MeetsRequirements(ITransport candidate)
        {
            if (candidate.Reliability < Reliability) { return false; }
            if (candidate.Ordering < Ordering) { return false; }
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

        /// <summary>
        /// A default constructor that specific the *LEAST* stringent possible values.
        /// </summary>
        protected ChannelDeliveryRequirements()
        {
            Reliability = Reliability.Unreliable;
            Ordering = Ordering.Unordered;
            Aggregation = MessageAggregation.Aggregatable;
            //aggregationTimeout = -1;
            Freshness = Freshness.IncludeAll;
        }

        /// <summary>
        /// Get/set the reliability
        /// </summary>
        /// <seealso cref="Reliability"/>
        public Reliability Reliability { get; set; }

        /// <summary>
        /// Get/set the ordering value.
        /// </summary>
        /// <seealso cref="Ordering"/>
        public Ordering Ordering { get; set; }

        /// <summary>
        /// Get/set the aggregation value.
        /// </summary>
        /// <seealso cref="Aggregation"/>
        public MessageAggregation Aggregation { get; set; }

        //public int AggregationTimeout { get; set; }

        /// <summary>
        /// Get/set the freshness value.
        /// </summary>
        /// <seealso cref="Freshness"/>
        public Freshness Freshness { get; set; }

        /// <summary>
        /// Constructor to set the 3 most common value
        /// </summary>
        /// <param name="d">the required reliability</param>
        /// <param name="a">the desired aggregation</param>
        /// <param name="o">the required ordered</param>
        public ChannelDeliveryRequirements(Reliability d, MessageAggregation a, Ordering o)
        {
            Reliability = d;
            Aggregation = a;
            Ordering = o;
            Freshness = Freshness.IncludeAll;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Reliability);
            sb.Append(',');
            sb.Append(Ordering);
            sb.Append(',');
            sb.Append(Aggregation);
            sb.Append(',');
            sb.Append(Freshness);
            return sb.ToString();
        }

        /// <summary>
        /// Select a transport meeting the requirements as specified by this instance. 
        /// Assume that <c>transports</c> is in a sorted order.
        /// </summary>
        /// <param name="transports">the sorted list of available transports</param>
        public virtual ITransport SelectTransport(IList<ITransport> transports)
        {
            // wouldn't it be cool to check if the transport is backlogged?
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
        public virtual bool MeetsRequirements(ITransport transport)
        {
            if (transport.Reliability < Reliability) { return false; }
            if (transport.Ordering < Ordering) { return false; }
            // could do something about flow characteristics or jitter
            return true;    // passed our test: go for gold!
        }

        #region Preconfigured QoS Channel Descriptors
        // 
        // This region defines some general examples of channel descriptors.
        // Note: users should pay close attention to the aggregation requirements!
        // Any use of MessageAggregation.Aggregatable requires periodically <em>manually flushing</em>
        // the channel.
        // 

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

        // 
        // These examples are taken from J Dyck, C Gutwin, TCN Graham, D Pinelle (2007). 
        // Beyond the LAN: Techniques from network games for improving groupware performance. 
        // In Proc. Int. Conf. on Supporting Group Work (GROUP), 291–300. New York, USA: ACM. 
        // doi:10.1145/1316624.1316669
        // 

        /// <summary>
        /// A descriptor for awareness data such as telepointers: such messages can be
        /// aggregated <b>for short time periods</b>, they should not be received
        /// out-of-order, but it's ok if they're lost (unreliable).  The application
        /// should ensure that such channels are either periodically flushed or have
        /// a lower ping time, to ensure these changes are pushed out.  Only the last 
        /// item placed in the channel will actually be sent 
        /// (Freshness.IncludeLatestOnly), replacing any previous values.
        /// </summary>
        /// <remarks>
        /// We have changed this definition slightly from Dyck et al. to actually
        /// recommend aggregation rather than being sent immediately.
        /// </remarks>
        public static ChannelDeliveryRequirements AwarenessLike
        {
            get
            {
                ChannelDeliveryRequirements cdr = new ChannelDeliveryRequirements();
                cdr.Reliability = Reliability.Unreliable;
                cdr.Ordering = Ordering.Sequenced;
                //cdr.Aggregation = MessageAggregation.Immediate;
                cdr.Aggregation = MessageAggregation.Aggregatable;
                cdr.Freshness = Freshness.IncludeLatestOnly;
                return cdr;
            }
        }

        /// <summary>
        /// A descriptor for chat messages: such messages should be received in order
        /// (e.g., after any previous chat messages) and must be received.  
        /// Such messages should be sent right away.
        /// </summary>
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

    /// <summary>
    /// A special <see cref="MessageDeliveryRequirements"/> variant to
    /// request a specific transport.
    /// </summary>
    public class SpecificTransportRequirement : MessageDeliveryRequirements
    {
        /// <summary>
        /// The specific transport requestd
        /// </summary>
        protected ITransport transport;

        /// <summary>
        /// Create an instance of an MDR that requires a specific transport.
        /// </summary>
        /// <param name="t">the specific transport to require</param>
        public SpecificTransportRequirement(ITransport t) 
            : this(t, MessageAggregation.Immediate)
        {
        }

        /// <summary>
        /// Create an instance of an MDR that requires a specific transport.
        /// </summary>
        /// <param name="t">the specific transport to require</param>
        /// <param name="aggr">the aggregation required</param>
        public SpecificTransportRequirement(ITransport t, MessageAggregation aggr)
            : base(t.Reliability, aggr, t.Ordering)
        {
            transport = t;
        }

        /// <summary>
        /// Verify whether the provided transport is the specific transport
        /// as required by this instance.
        /// </summary>
        /// <param name="candidate">the candidate transport</param>
        /// <returns>true if the candidate is the specific transport</returns>
        public override bool MeetsRequirements(ITransport candidate)
        {
            return transport == candidate;
        }
    }

    /// <summary>
    /// An interface describing the delivery characteristics of a transport.
    /// </summary>
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
        /// Return or update the observed delay on the transport (in milliseconds).
        /// When setting, the implementation may smooth the value.
        /// This property does not introduce nor control introduced delay.
        /// </summary>
        float Delay { get; set; }

        /// <summary>
        /// The maximum packet size supported by this transport instance (in bytes).
        /// </summary>
        uint MaximumPacketSize { get; }

        // loss?  would this be a percentage?
        // jitter?  in milliseconds?
    }
}
