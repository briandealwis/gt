using System.Collections.Generic;
namespace GT.Net {

    public abstract class BaseConfiguration : IComparer<ITransport> {

        /// <summary>
        /// Default transport orderer: orders by reliability, then sequencing, then delay.
        /// </summary>
        /// <param name="x">first transport</param>
        /// <param name="y">second transport</param>
        /// <returns>-1 if x < y, 0 if they're equivalent, and 1 if x > y</returns>
        public virtual int Compare(ITransport x, ITransport y)
        {
            if (x.Reliability < y.Reliability) { return -1; }
            if (x.Reliability > y.Reliability) { return 1; }
            if (x.Ordering < y.Ordering) { return -1; }
            if (x.Ordering > y.Ordering) { return 1; }
            if (x.Delay < y.Delay) { return -1; }
            if (x.Delay > y.Delay) { return 1; }
            return 0;
        }

        /// <summary>
        /// Select a transport meeting the requirements as specified by <c>mdr</c>
        /// and <c>cdr</c>.  <c>transports</c> is in a sorted order as defined by
        /// this instance.  Generally the <c>mdr</c> (if provided) takes precedence 
        /// over <c>cdr</c>.  The actual test of worthiness is implemented in
        /// <see cref="MeetsRequirements(ITransport,MessageDeliveryOptions,ChannelDeliveryOptions">
        /// MeetsRequirements</see>.
        /// </summary>
        /// <param name="transports">the sorted list of available transports</param>
        /// <param name="mdr">the requirements as specified by the message; may be null.</param>
        /// <param name="cdr">the requirements as configured by the channel</param>
        public virtual ITransport SelectTransport(IList<ITransport> transports, 
            MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr) 
        {
            foreach(ITransport t in transports) {
                if (MeetsRequirements(t, mdr, cdr)) { return t; }
            }
            return null;
        }

        /// <summary>
        /// Test whether a transport meets the requirements as specified by <c>mdr</c>
        /// and <c>cdr</c>.  Generally the <c>mdr</c> (if provided) takes precedence 
        /// over <c>cdr</c>.
        /// </summary>
        /// <param name="transport">the transport to test</param>
        /// <param name="mdr">the requirements as specified by the message; may be null.</param>
        /// <param name="cdr">the requirements as configured by the channel</param>
        protected virtual bool MeetsRequirements(ITransport transport, MessageDeliveryRequirements mdr, 
            ChannelDeliveryRequirements cdr)
        {
            Reliability r = mdr != null ? mdr.Reliability : cdr.Reliability;
            Ordering o = mdr != null ? mdr.Ordering : cdr.Ordering;
            if (transport.Reliability < r) { return false; }
            if (transport.Ordering < o) { return false; }
            // could do something about flow characteristics or jitter
            return true;    // passed our test: go for gold!
        }
    }
}
