using System;
using System.Collections.Generic;
using System.Text;
using GT.Net;
using System.IO;
using GT.Net;

namespace GT.Net
{
    /// <summary>Delegate for tuples.</summary>
    public delegate void StreamedTupleReceivedDelegate<T>(RemoteTuple<T> tuple, int clientID);
    /// <summary>Delegate for tuples.</summary>
    public delegate void StreamedTupleReceivedDelegate<T, K>(RemoteTuple<T, K> tuple, int clientID);
    /// <summary>Delegate for tuples.</summary>
    public delegate void StreamedTupleReceivedDelegate<T, K, J>(RemoteTuple<T, K, J> tuple, int clientID);

    internal static class StreamedTupleUtilities
    {
        /// <summary>Converts a IConvertible type into a byte array.</summary>
        /// <typeparam name="A">The IConvertible type.</typeparam>
        /// <param name="value">The value</param>
        /// <returns></returns>
        public static byte[] Converter<A>(A value)
            where A : IConvertible
        {
            switch (Type.GetTypeCode(typeof(A)))
            {
                case TypeCode.Byte: byte[] b = new byte[1]; b[0] = value.ToByte(null); return b;
                case TypeCode.Char: return BitConverter.GetBytes(value.ToChar(null));
                case TypeCode.Single: return BitConverter.GetBytes(value.ToSingle(null));
                case TypeCode.Double: return BitConverter.GetBytes(value.ToDouble(null));
                case TypeCode.Int16: return BitConverter.GetBytes(value.ToInt16(null));
                case TypeCode.Int32: return BitConverter.GetBytes(value.ToInt32(null));
                case TypeCode.Int64: return BitConverter.GetBytes(value.ToInt64(null));
                case TypeCode.UInt16: return BitConverter.GetBytes(value.ToUInt16(null));
                case TypeCode.UInt32: return BitConverter.GetBytes(value.ToUInt32(null));
                case TypeCode.UInt64: return BitConverter.GetBytes(value.ToUInt64(null));
                default: return BitConverter.GetBytes(value.ToDouble(null)); //if not recognized, make it a double
            }
        }

        /// <summary>Converts a portion of a byte array into some IConvertible type.</summary>
        /// <typeparam name="A">The IConvertible type to convert the byte array into.</typeparam>
        /// <param name="b">The byte array</param>
        /// <param name="index">The byte in the array at which to begin</param>
        /// <param name="length">The length of the type</param>
        /// <returns>The converted type.</returns>
        public static A Converter<A>(byte[] b, int index, out int length)
            where A : IConvertible
        {
            switch (Type.GetTypeCode(typeof(A)))
            {
                case TypeCode.Byte: length = 1; return (A)Convert.ChangeType(b[index], typeof(Byte));
                case TypeCode.Char: length = 2; return (A)Convert.ChangeType(BitConverter.ToChar(b, index), typeof(Char));
                case TypeCode.Single: length = 4; return (A)Convert.ChangeType(BitConverter.ToSingle(b, index), typeof(Single));
                case TypeCode.Double: length = 8; return (A)Convert.ChangeType(BitConverter.ToDouble(b, index), typeof(Double));
                case TypeCode.Int16: length = 2; return (A)Convert.ChangeType(BitConverter.ToInt16(b, index), typeof(Int16));
                case TypeCode.Int32: length = 4; return (A)Convert.ChangeType(BitConverter.ToInt32(b, index), typeof(Int32));
                case TypeCode.Int64: length = 8; return (A)Convert.ChangeType(BitConverter.ToInt64(b, index), typeof(Int64));
                case TypeCode.UInt16: length = 2; return (A)Convert.ChangeType(BitConverter.ToUInt16(b, index), typeof(UInt16));
                case TypeCode.UInt32: length = 4; return (A)Convert.ChangeType(BitConverter.ToUInt32(b, index), typeof(UInt32));
                case TypeCode.UInt64: length = 8; return (A)Convert.ChangeType(BitConverter.ToUInt64(b, index), typeof(UInt64));
                default: length = 8; return (A)Convert.ChangeType(BitConverter.ToDouble(b, index), typeof(Double)); //if not recognized, make it a double
            }
        }
    }

    public abstract class AbstractStreamedTuple : AbstractBaseStream
    {
        protected bool changed;
        protected int milliseconds;
        protected long interval;
        protected long lastTimeSent;

        protected AbstractStreamedTuple(ServerStream s, byte id)
            : base(s, id)
        {
            this.changed = false;
            this.milliseconds = milliseconds;
            this.interval = 9999999;
        }

        abstract internal void QueueMessage(Message m);
        abstract internal void Update(HPTimer hpTImer);
    }

    /// <summary>A three-tuple that is automatically streamed to the other clients.</summary>
    /// <typeparam name="T">X value</typeparam>
    /// <typeparam name="K">Y value</typeparam>
    /// <typeparam name="J">Z value</typeparam>
    public class StreamedTuple<T, K, J> : AbstractStreamedTuple
        where T : IConvertible
        where K : IConvertible
        where J : IConvertible
    {
        private T x;
        private K y;
        private J z;

        /// <summary>X value</summary>
        public T X { get { return x; } set { x = value; changed = true; } }

        /// <summary>Y value</summary>
        public K Y { get { return y; } set { y = value; changed = true; } }

        /// <summary>Z value</summary>
        public J Z { get { return z; } set { z = value; changed = true; } }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        public event StreamedTupleReceivedDelegate<T, K, J> StreamedTupleReceived;

        /// <summary>Creates a streaming tuple</summary>
        /// <param name="connection">The stream to send the tuples on</param>
        /// <param name="id">the stream id</param>
        /// <param name="milliseconds">Send the tuple only once during this interval</param>
        internal StreamedTuple(ServerStream connection, byte id, int milliseconds) : base(connection, id)
        {
        }

        internal override void QueueMessage(Message message)
        {
            int clientID, cursor, length;
            RemoteTuple<T, K, J> tuple = new RemoteTuple<T, K, J>();

            //tuple.X = StreamedTupleUtilities.Converter<T>(message.data, 0, out length);
            //cursor = length;
            //tuple.Y = StreamedTupleUtilities.Converter<K>(message.data, cursor, out length);
            //cursor += length;
            //tuple.Z = StreamedTupleUtilities.Converter<J>(message.data, cursor, out length);
            //cursor += length;

            //clientID = BitConverter.ToInt32(message.data, cursor);

            //if (StreamedTupleReceived != null)
            //    StreamedTupleReceived(tuple, clientID);
        }

        internal override void Update(HPTimer hpTimer)
        {
            this.interval = hpTimer.Frequency * milliseconds / 1000;
            if (this.lastTimeSent + this.interval < hpTimer.Time && connection.UniqueIdentity != 0 && changed)
            {
                this.lastTimeSent = hpTimer.Time;
                MemoryStream ms = new MemoryStream(28);  //the maximum size this tuple could possibly be
                byte[] b;

                //convert values into bytes
                b = StreamedTupleUtilities.Converter<T>(this.X);
                ms.Write(b, 0, b.Length);
                b = StreamedTupleUtilities.Converter<K>(this.Y);
                ms.Write(b, 0, b.Length);
                b = StreamedTupleUtilities.Converter<J>(this.Z);
                ms.Write(b, 0, b.Length);

                //along with whose tuple it is
                ms.Write(BitConverter.GetBytes(connection.UniqueIdentity), 0, 4);

                b = new byte[ms.Position];
                ms.Position = 0;
                ms.Read(b, 0, b.Length);

                changed = false;
                //connection.Send(b, id, MessageType.Tuple3D, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
                throw new InvalidProgramException("FIXME");
            }
        }
    }

    /// <summary>A two-tuple that is automatically streamed to the other clients.</summary>
    /// <typeparam name="T">X value</typeparam>
    /// <typeparam name="K">Y value</typeparam>
    public class StreamedTuple<T, K> : AbstractStreamedTuple
        where T : IConvertible
        where K : IConvertible
    {
        private T x;
        private K y;

        /// <summary>X value</summary>
        public T X { get { return x; } set { x = value; changed = true; } }
        /// <summary>Y value</summary>
        public K Y { get { return y; } set { y = value; changed = true; } }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        public event StreamedTupleReceivedDelegate<T, K> StreamedTupleReceived;

        /// <summary>Creates a streaming tuple</summary>
        /// <param name="connection">The stream to send the tuples on</param>
        /// <param name="id">the stream id</param>
        /// <param name="milliseconds">Send the tuple only once during this interval</param>
        public StreamedTuple(ServerStream connection, byte id, int milliseconds) : base(connection, id)
        {
        }

        internal override void QueueMessage(Message message)
        {
            int clientID, cursor, length;

            RemoteTuple<T, K> tuple = new RemoteTuple<T, K>();

            //tuple.X = StreamedTupleUtilities.Converter<T>(message.data, 0, out length);
            //cursor = length;
            //tuple.Y = StreamedTupleUtilities.Converter<K>(message.data, cursor, out length);
            //cursor += length;

            //clientID = BitConverter.ToInt32(message.data, cursor);

            //if (StreamedTupleReceived != null)
            //    StreamedTupleReceived(tuple, clientID);
        }

        internal override void Update(HPTimer hpTimer)
        {
            this.interval = hpTimer.Frequency * milliseconds / 1000;
            if (this.lastTimeSent + this.interval < hpTimer.Time && connection.UniqueIdentity != 0 && changed)
            {
                this.lastTimeSent = hpTimer.Time;
                MemoryStream ms = new MemoryStream(28);  //the maximum size this tuple could possibly be
                byte[] b;

                //convert values into bytes
                b = StreamedTupleUtilities.Converter<T>(this.X);
                ms.Write(b, 0, b.Length);
                b = StreamedTupleUtilities.Converter<K>(this.Y);
                ms.Write(b, 0, b.Length);

                //along with whose tuple it is
                ms.Write(BitConverter.GetBytes(connection.UniqueIdentity), 0, 4);

                b = new byte[ms.Position];
                ms.Position = 0;
                ms.Read(b, 0, b.Length);

                changed = false;
                //connection.Send(b, id, MessageType.Tuple2D, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
                throw new InvalidProgramException("FIXME");
            }
        }
    }

    /// <summary>A one-tuple that is automatically streamed to the other clients.</summary>
    /// <typeparam name="T">X value</typeparam>
    public class StreamedTuple<T> : AbstractStreamedTuple
        where T : IConvertible
    {
        private T x;

        /// <summary>X value</summary>
        public T X { get { return x; } set { x = value; changed = true; } }

        /// <summary>Occurs when we receive a tuple from someone else.</summary>
        public event StreamedTupleReceivedDelegate<T> StreamedTupleReceived;

        /// <summary>Creates a streaming tuple</summary>
        /// <param name="connection">The stream to send the tuples on</param>
        /// <param name="id">the stream id</param>
        /// <param name="milliseconds">Send the tuple only once during this interval</param>
        public StreamedTuple(ServerStream connection, byte id, int milliseconds) : base(connection, id)
        {
        }

        internal override void QueueMessage(Message message)
        {
            int clientID, cursor, length;

            RemoteTuple<T> tuple = new RemoteTuple<T>();

            //tuple.X = StreamedTupleUtilities.Converter<T>(message.data, 0, out length);
            //cursor = length;

            //clientID = BitConverter.ToInt32(message.data, cursor);

            //if (StreamedTupleReceived != null)
            //    StreamedTupleReceived(tuple, clientID);
        }

        internal override void Update(HPTimer hpTimer)
        {
            this.interval = hpTimer.Frequency * milliseconds / 1000;
            if (this.lastTimeSent + this.interval < hpTimer.Time && connection.UniqueIdentity != 0 && changed)
            {
                this.lastTimeSent = hpTimer.Time;
                MemoryStream ms = new MemoryStream(28);  //the maximum size this tuple could possibly be
                byte[] b;

                //convert values into bytes
                b = StreamedTupleUtilities.Converter<T>(this.X);
                ms.Write(b, 0, b.Length);

                //along with whose tuple it is
                ms.Write(BitConverter.GetBytes(connection.UniqueIdentity), 0, 4);

                b = new byte[ms.Position];
                ms.Position = 0;
                ms.Read(b, 0, b.Length);

                changed = false;
                //connection.Send(b, id, MessageType.Tuple1D, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
                throw new InvalidProgramException("FIXME");
            }
        }
    }
}
