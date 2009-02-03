using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using GT.Net;

namespace GT.Net
{
    /// <summary>Delegate for tuples.</summary>
    public delegate void StreamedTupleReceivedDelegate<T_X>(RemoteTuple<T_X> tuple, int clientID);
    /// <summary>Delegate for tuples.</summary>
    public delegate void StreamedTupleReceivedDelegate<T_X, T_Y>(RemoteTuple<T_X, T_Y> tuple, int clientID);
    /// <summary>Delegate for tuples.</summary>
    public delegate void StreamedTupleReceivedDelegate<T_X, T_Y, T_Z>(RemoteTuple<T_X, T_Y, T_Z> tuple, int clientID);

    public interface IStreamedTuple<T_X> : IStream
        where T_X : IConvertible
    {
        /// <summary>X value</summary>
        T_X X { get; set; }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        event StreamedTupleReceivedDelegate<T_X> StreamedTupleReceived;
    }

    public interface IStreamedTuple<T_X, T_Y> : IStream
        where T_X : IConvertible
        where T_Y: IConvertible
    {
        /// <summary>X value</summary>
        T_X X { get; set; }

        /// <summary>Y value</summary>
        T_Y Y { get; set; }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        event StreamedTupleReceivedDelegate<T_X, T_Y> StreamedTupleReceived;
    }

    public interface IStreamedTuple<T_X, T_Y, T_Z> : IStream
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
    }


    internal abstract class AbstractStreamedTuple : AbstractBaseStream
    {
        protected bool changed;
        protected int updateDelay;     // milliseconds
        protected long lastTimeSent;    // milliseconds

        protected AbstractStreamedTuple(ConnexionToServer s, byte channel, int updateDelay,
                ChannelDeliveryRequirements cdr) : base(s, channel, cdr)
        {
            this.changed = false;
            this.updateDelay = updateDelay;
        }

        abstract internal void QueueMessage(Message m);

        override internal void Update(HPTimer hpTimer)
        {
            if (lastTimeSent + updateDelay < hpTimer.TimeInMilliseconds 
                && connexion.Identity != 0 && changed)
            {
                this.lastTimeSent = hpTimer.TimeInMilliseconds;
                Flush();
            }
        }
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
        /// <param name="connexion">The stream to send the tuples on</param>
        /// <param name="channel">the stream's channel</param>
        /// <param name="milliseconds">Send the tuple only once during this interval</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        internal StreamedTuple(ConnexionToServer connexion, byte channel,
            int milliseconds, ChannelDeliveryRequirements cdr)
            : base(connexion, channel, milliseconds, cdr)
        {
        }

        internal override void QueueMessage(Message message)
        {
            RemoteTuple<T_X, T_Y, T_Z> tuple = new RemoteTuple<T_X, T_Y, T_Z>();
            TupleMessage tm = (TupleMessage)message;
            tuple.X = (T_X)tm.X;
            tuple.Y = (T_Y)tm.Y;
            tuple.Z = (T_Z)tm.Z;

            if (StreamedTupleReceived != null) { StreamedTupleReceived(tuple, tm.ClientId); }
        }

        public override void Flush()
        {
            if (!changed) { return; }
            changed = false;
            connexion.Send(new TupleMessage(channel, Identity, X, Y, Z), 
                null, deliveryOptions);
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
        /// <param name="connexion">The stream to send the tuples on</param>
        /// <param name="channel">the stream's channel</param>
        /// <param name="milliseconds">Send the tuple only once during this interval</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        internal StreamedTuple(ConnexionToServer connexion, byte channel,
            int milliseconds, ChannelDeliveryRequirements cdr)
            : base(connexion, channel, milliseconds, cdr)
        {
        }

        internal override void QueueMessage(Message message)
        {
            RemoteTuple<T_X, T_Y> tuple = new RemoteTuple<T_X, T_Y>();
            TupleMessage tm = (TupleMessage)message;
            tuple.X = (T_X)tm.X;
            tuple.Y = (T_Y)tm.Y;

            if (StreamedTupleReceived != null) { StreamedTupleReceived(tuple, tm.ClientId); }
        }

        public override void Flush()
        {
            if (!changed) { return; }
            changed = false;
            connexion.Send(new TupleMessage(channel, Identity, X, Y),
                null, deliveryOptions);
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
        /// <param name="connexion">The stream to send the tuples on</param>
        /// <param name="channel">the stream's channel</param>
        /// <param name="milliseconds">Send the tuple only once during this interval</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        internal StreamedTuple(ConnexionToServer connexion, byte channel,
            int milliseconds, ChannelDeliveryRequirements cdr)
            : base(connexion, channel, milliseconds, cdr)
        {
        }

        internal override void QueueMessage(Message message)
        {
            RemoteTuple<T_X> tuple = new RemoteTuple<T_X>();
            TupleMessage tm = (TupleMessage)message;
            tuple.X = (T_X)tm.X;

            if (StreamedTupleReceived != null) { StreamedTupleReceived(tuple, tm.ClientId); }
        }

        public override void Flush()
        {
            if (!changed) { return; }
            changed = false;
            connexion.Send(new TupleMessage(channel, Identity, X), null, deliveryOptions);
        }
    }
}
