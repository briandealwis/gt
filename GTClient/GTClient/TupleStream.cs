using System;

namespace GT.Net
{
    /// <summary>Delegate for tuples.</summary>
    public delegate void StreamedTupleReceivedDelegate<T_X>(RemoteTuple<T_X> tuple, int clientID);
    /// <summary>Delegate for tuples.</summary>
    public delegate void StreamedTupleReceivedDelegate<T_X, T_Y>(RemoteTuple<T_X, T_Y> tuple, int clientID);
    /// <summary>Delegate for tuples.</summary>
    public delegate void StreamedTupleReceivedDelegate<T_X, T_Y, T_Z>(RemoteTuple<T_X, T_Y, T_Z> tuple, int clientID);

    public interface IStreamedTuple<T_X> : IChannel
        where T_X : IConvertible
    {
        /// <summary>X value</summary>
        T_X X { get; set; }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        event StreamedTupleReceivedDelegate<T_X> StreamedTupleReceived;

        /// <summary>
        /// The frequency at which this tuple's changes should be propagated.
        /// </summary>
        TimeSpan UpdatePeriod { get; set; }
    }

    public interface IStreamedTuple<T_X, T_Y> : IChannel
        where T_X : IConvertible
        where T_Y: IConvertible
    {
        /// <summary>X value</summary>
        T_X X { get; set; }

        /// <summary>Y value</summary>
        T_Y Y { get; set; }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        event StreamedTupleReceivedDelegate<T_X, T_Y> StreamedTupleReceived;

        /// <summary>
        /// The frequency at which this tuple's changes should be propagated.
        /// </summary>
        TimeSpan UpdatePeriod { get; set; }
    }

    public interface IStreamedTuple<T_X, T_Y, T_Z> : IChannel
        where T_X : IConvertible
        where T_Y : IConvertible
        where T_Z : IConvertible
    {
        /// <summary>X value</summary>
        T_X X { get; set; }

        /// <summary>Y value</summary>
        T_Y Y { get; set; }

        /// <summary>Z value</summary>
        T_Z Z { get; set; }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        event StreamedTupleReceivedDelegate<T_X, T_Y, T_Z> StreamedTupleReceived;

        /// <summary>
        /// The frequency at which this tuple's changes should be propagated.
        /// </summary>
        TimeSpan UpdatePeriod { get; set; }
    }


    internal abstract class AbstractStreamedTuple : AbstractChannel
    {
        protected bool changed;
        protected long updateDelayMS;     // milliseconds
        protected long lastTimeSentMS;    // milliseconds
        protected TimeSpan updatePeriod;

        /// <summary>
        /// The frequency at which this tuple's changes should be propagated.
        /// </summary>
        public TimeSpan UpdatePeriod
        {
            get { return updatePeriod; }
            set
            {
                updatePeriod = value;
                updateDelayMS = (long)value.TotalMilliseconds;
            }
        }

        /// <summary>
        /// Create a new streamed tuple for the given connexion and on the given
        /// channel.  This tuple should propagate any changes every <see cref="updateDelay"/>.
        /// </summary>
        /// <param name="s">the given connexion</param>
        /// <param name="channelId">the channel on which to send and receive updates</param>
        /// <param name="updateDelay">the frequency to send our updates</param>
        /// <param name="cdr">the delivery requirements to be used in sending or updates</param>
        protected AbstractStreamedTuple(IConnexion s, byte channelId,
                TimeSpan updateDelay, ChannelDeliveryRequirements cdr) 
            : base(s, channelId, cdr)
        {
            changed = false;
            UpdatePeriod = updateDelay;
        }

        public override void Update(HPTimer hpTimer)
        {
            if (changed && connexion.Identity != 0 
                && hpTimer.TimeInMilliseconds - lastTimeSentMS > updateDelayMS)
            {
                lastTimeSentMS = hpTimer.TimeInMilliseconds;
                Flush();
            }
        }

        public override void Flush()
        {
            if (changed) { connexion.Send(AsTupleMessage(), null, deliveryOptions); }
            changed = false;
            base.Flush();
        }

        protected abstract TupleMessage AsTupleMessage();

    }

    /// <summary>A three-tuple that is automatically streamed to the other clients.</summary>
    /// <typeparam name="T_X">X value</typeparam>
    /// <typeparam name="T_Y">Y value</typeparam>
    /// <typeparam name="T_Z">Z value</typeparam>
    internal class StreamedTuple<T_X, T_Y, T_Z> : AbstractStreamedTuple, IStreamedTuple<T_X, T_Y, T_Z>
        where T_X : IConvertible
        where T_Y : IConvertible
        where T_Z : IConvertible
    {
        private T_X x;
        private T_Y y;
        private T_Z z;

        /// <summary>X value</summary>
        public T_X X { get { return x; } set { x = value; changed = true; } }

        /// <summary>Y value</summary>
        public T_Y Y { get { return y; } set { y = value; changed = true; } }

        /// <summary>Z value</summary>
        public T_Z Z { get { return z; } set { z = value; changed = true; } }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        public event StreamedTupleReceivedDelegate<T_X, T_Y, T_Z> StreamedTupleReceived;

        /// <summary>Creates a streaming tuple</summary>
        /// <param name="connexion">The connexion to send the tuples on</param>
        /// <param name="channelId">the channel</param>
        /// <param name="updateTime">Update time for changed tuple</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        internal StreamedTuple(IConnexion connexion, byte channelId,
            TimeSpan updateTime, ChannelDeliveryRequirements cdr)
            : base(connexion, channelId, updateTime, cdr)
        {
        }

        protected override void _cnx_MessageReceived(Message message, IConnexion client, ITransport transport)
        {
            /// We can't register for fine-grained events, so we have to do the processing 
            /// ourselves to ensure the message is actually intended for this channel
            if (message.ChannelId != ChannelId || !(message is TupleMessage)) { return; }
            TupleMessage tm = (TupleMessage)message;
            if (!(tm.Dimension == 3 && tm.X is T_X && tm.Y is T_Y && tm.Z is T_Z)) { return; }
            RemoteTuple<T_X, T_Y, T_Z> tuple = new RemoteTuple<T_X, T_Y, T_Z>();
            tuple.X = (T_X)tm.X;
            tuple.Y = (T_Y)tm.Y;
            tuple.Z = (T_Z)tm.Z;

            // Perhaps should be done in Update()?
            if (StreamedTupleReceived != null) { StreamedTupleReceived(tuple, tm.ClientId); }
        }

        protected override TupleMessage AsTupleMessage()
        {
            return new TupleMessage(channelId, Identity, X, Y, Z);
        }
    }

    /// <summary>A two-tuple that is automatically streamed to the other clients.</summary>
    /// <typeparam name="T_X">X value</typeparam>
    /// <typeparam name="T_Y">Y value</typeparam>
    internal class StreamedTuple<T_X, T_Y> : AbstractStreamedTuple, IStreamedTuple<T_X, T_Y>
        where T_X : IConvertible
        where T_Y : IConvertible
    {
        private T_X x;
        private T_Y y;

        /// <summary>X value</summary>
        public T_X X { get { return x; } set { x = value; changed = true; } }
        /// <summary>Y value</summary>
        public T_Y Y { get { return y; } set { y = value; changed = true; } }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        public event StreamedTupleReceivedDelegate<T_X, T_Y> StreamedTupleReceived;

        /// <summary>Creates a streaming tuple</summary>
        /// <param name="connexion">The connexion to send the tuples on</param>
        /// <param name="channelId">the channel</param>
        /// <param name="updateTime">Update time for changed tuple</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        internal StreamedTuple(IConnexion connexion, byte channelId,
            TimeSpan updateTime, ChannelDeliveryRequirements cdr)
            : base(connexion, channelId, updateTime, cdr)
        {
        }

        protected override void _cnx_MessageReceived(Message message, IConnexion client, ITransport transport)
        {
            /// We can't register for fine-grained events, so we have to do the processing 
            /// ourselves to ensure the message is actually intended for this channel
            if (message.ChannelId != ChannelId || !(message is TupleMessage)) { return; }
            TupleMessage tm = (TupleMessage)message;
            if (!(tm.Dimension == 2 && tm.X is T_X && tm.Y is T_Y)) { return; }
            RemoteTuple<T_X, T_Y> tuple = new RemoteTuple<T_X, T_Y>();
            tuple.X = (T_X)tm.X;
            tuple.Y = (T_Y)tm.Y;

            // Perhaps should be done in Update()?
            if (StreamedTupleReceived != null) { StreamedTupleReceived(tuple, tm.ClientId); }
        }

        protected override TupleMessage AsTupleMessage()
        {
            return new TupleMessage(channelId, Identity, X, Y);
        }
    }

    /// <summary>A one-tuple that is automatically streamed to the other clients.</summary>
    /// <typeparam name="T_X">X value</typeparam>
    internal class StreamedTuple<T_X> : AbstractStreamedTuple, IStreamedTuple<T_X>
        where T_X : IConvertible
    {
        private T_X x;

        /// <summary>X value</summary>
        public T_X X { get { return x; } set { x = value; changed = true; } }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        public event StreamedTupleReceivedDelegate<T_X> StreamedTupleReceived;

        /// <summary>Creates a streaming tuple</summary>
        /// <param name="connexion">The connexion to send the tuples on</param>
        /// <param name="channelId">the channel</param>
        /// <param name="updateTime">Update time for changed tuple</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        internal StreamedTuple(IConnexion connexion, byte channelId,
            TimeSpan updateTime, ChannelDeliveryRequirements cdr)
            : base(connexion, channelId, updateTime, cdr)
        {
        }

        protected override void _cnx_MessageReceived(Message message, IConnexion client, ITransport transport)
        {
            /// We can't register for fine-grained events, so we have to do the processing 
            /// ourselves to ensure the message is actually intended for this channel
            if (message.ChannelId != ChannelId || !(message is TupleMessage)) { return; }
            TupleMessage tm = (TupleMessage)message;
            if (tm.Dimension != 1 || !(tm.X is T_X)) { return; }
            RemoteTuple<T_X> tuple = new RemoteTuple<T_X>();
            tuple.X = (T_X)tm.X;

            // Perhaps should be done in Update()?
            if (StreamedTupleReceived != null) { StreamedTupleReceived(tuple, tm.ClientId); }
        }

        protected override TupleMessage AsTupleMessage()
        {
            return new TupleMessage(channelId, Identity, X);
        }
    }
}
