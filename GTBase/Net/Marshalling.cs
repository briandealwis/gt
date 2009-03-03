using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Diagnostics;
using GT.Net;
using System.Collections.Generic;
using GT.Utils;

namespace GT.Net
{
    /// <remarks>
    /// A marshaller is responsible for transforming an object to a sequence of bytes,
    /// and later restoring an equivalent object from that sequence of bytes.
    /// GT marshallers must handle strings, session objects, byte arrays, and tuples.
    /// </remarks>
    public interface IMarshaller : IDisposable
    {
        /// <summary>
        /// Return a set of descriptions identifying this marshaller and its capabilities.
        /// The descriptions should not have any white-space.
        /// </summary>
        string[] Descriptors { get; }

        /// <summary>
        /// Marshal the provided message in an appropriate form for the provided transport.
        /// </summary>
        /// <param name="senderIdentity">the server-unique id of the sender of this message
        /// (i.e., the local client or server's server-unique identifier).  This
        /// should generally be the same number across different invocations.</param>
        /// <param name="message">the message</param>
        /// <param name="t">the transport on which the packet will be sent</param>
        /// <returns>the marshalled result, containing a set of transport packets;
        ///     this result <b>must</b> be disposed of when finished with</returns>
        /// <exception cref="MarshallingException">on a marshalling error, or if the
        /// message cannot be encoded within the transport's packet capacity</exception>
        MarshalledResult Marshal(int senderIdentity, Message message, ITransport t);

        /// <summary>
        /// Unmarshal one message (or possibly more) as encoded in the transport-specific 
        /// form from <see cref="input"/>.   Messages are notified using the provided 
        /// <see cref="messageAvailable"/> delegate; a message may not be immediately 
        /// available, for example, if decoding their byte-content depends on information 
        /// carried in a not-yet-seen message.  The marshaller <b>must</b> not access
        /// <see cref="input"/> once this call has returned: any required information 
        /// must be retrieved from the stream during this call and cached.
        /// </summary>
        /// <param name="input">the stream with the packet content</param>
        /// <param name="t">the transport from which the packet was received</param>
        /// <param name="messageAvailable">a callback for when a message becomes available
        /// from the stream.</param>
        void Unmarshal(TransportPacket input, ITransport t, 
            EventHandler<MessageEventArgs> messageAvailable);
    }

    /// <summary>
    /// Represents the event information from a message becoming available.
    /// Some marshallers may choose to subclass this to provide additional information.
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(ITransport transport, Message message)
        {
            Transport = transport;
            Message = message;
        }

        /// <summary>
        /// The transport on which this message was received.
        /// </summary>
        public ITransport Transport { get; set; }

        /// <summary>
        /// The message received
        /// </summary>
        public Message Message { get; set; }
    }

    /// <summary>
    /// An exception thrown in case of a error or warning during marshalling or unmarshalling.
    /// </summary>
    public class MarshallingException : GTException {
        protected MarshallingException() : base(Severity.Error) { }
        public MarshallingException(string message) : base(Severity.Error, message) {}
        public MarshallingException(string message, Exception inner) : base(Severity.Error, message, inner) { }
        public MarshallingException(Severity severity, string message, Exception inner) : base(severity, message, inner) { }

        public static void Assert(bool condition, string explanation)
        {
            if (!condition)
            {
                throw new MarshallingException("assertion failed: " + explanation);
            }
        }
    }

    /// <summary>
    /// Represents a byte-array with appropriately marshalled content ready to send
    /// across a particular transport.
    /// </summary>
    public class TransportPacket : IList<ArraySegment<byte>>
    {
        /// <summary>
        /// An ordered set of byte arrays; the marshalled packet is
        /// made up of these segments laid one after the other.
        /// </summary>
        protected List<ArraySegment<byte>> list;

        /// <summary>
        /// The total number of bytes in this marshalled packet (should be 
        /// equal to the sum of the <see cref="ArraySegment{T}.Count"/> for
        /// each segment in <see cref="list"/>).
        /// </summary>
        protected int length = 0;

        public TransportPacket()
        {
            list = new List<ArraySegment<byte>>();
        }

        /// <summary>
        /// Create an instance expecting <see cref="expectedSegments"/> segments.
        /// </summary>
        /// <param name="expectedSegments">the expected number of segments</param>
        public TransportPacket(int expectedSegments)
        {
            list = new List<ArraySegment<byte>>(expectedSegments);
        }

        public TransportPacket(byte[] bytes, int offset, int count)
            : this(new ArraySegment<byte>(bytes, offset, count)) { }

        public TransportPacket(MemoryStream ms)
            : this(ms.GetBuffer(), 0, (int)ms.Length)
        {
        }

        public TransportPacket(ArraySegment<byte> segment) 
            : this(1)
        {
            Add(segment);
        }

        public TransportPacket(params byte[][] bytesArrays) 
            : this(bytesArrays.Length)
        {
            foreach (byte[] bytes in bytesArrays)
            {
                Add(bytes, 0, bytes.Length);
            }
        }


        /// <summary>
        /// Create a new marshalled packet as a subset of another packet <see cref="source"/>
        /// </summary>
        /// <param name="source">the provided marshalled packet</param>
        /// <param name="offset">the start position of the subset to include</param>
        /// <param name="count">the number of bytes of the subset to include</param>
        public TransportPacket(TransportPacket source, int offset, int count)
        {
            list = new List<ArraySegment<byte>>(source.Count);

            // We proceed through the segments of source, copying those portions
            // that fall in our defined area of interest.
            int sourceStart = offset;           // index of first byte of AOI
            int sourceEnd = offset + count - 1; // index of last byte of AOI
            int segmentStart = 0;               // index of first byte of current <segment>
            foreach(ArraySegment<byte> segment in source)
            {
                int segmentEnd = segmentStart + segment.Count - 1;  // index of last byte
                // if this segment appears after the area of interest then we're finished:
                // none of the remaining segments can possibly be in our AOI
                if (sourceEnd < segmentStart)
                {
                    break;
                }
                // but it this segment is at least partially contained within our area of interest
                if (sourceStart <= segmentEnd)
                {
                    int segOffset = Math.Max(segmentStart, sourceStart) - segmentStart;
                    int segLen = Math.Min(segmentEnd, sourceEnd) - segmentStart;
                    Add(new ArraySegment<byte>(segment.Array, segment.Offset + segOffset,
                        segment.Offset + segOffset + segLen));
                }
                segmentStart += segment.Count;
            }
        }

        /// <summary>
        /// Return the number of bytes in this packet.
        /// </summary>
        public int Length { get { return length; } }

        /// <summary>
        /// Return a subset of this marshalled packet; this is non-destructive.
        /// </summary>
        /// <param name="offset">the start position of the subset</param>
        /// <param name="count">the number of bytes in the subset</param>
        /// <returns></returns>
        public TransportPacket Subset(int offset, int count)
        {
            return new TransportPacket(this, offset, count);
        }

        /// <summary>
        /// Prepend the byte segment to this item.
        /// </summary>
        /// <param name="item"></param>
        public void Prepend(ArraySegment<byte> item)
        {
            list.Insert(0, item);
            length += item.Count;
        }

        /// <summary>
        /// Append the byte segment to this item.  This adds a reference
        /// to <see cref="item"/>; any changes made to <see cref="item"/>
        /// will be reflected in this packet's contents.
        /// </summary>
        /// <param name="item"></param>
        public void Add(ArraySegment<byte> item)
        {
            list.Add(item);
            length += item.Count;
        }

        /// <summary>
        /// Append the byte segment to this item.  This adds a reference
        /// to <see cref="source"/>; any changes made to <see cref="source"/>
        /// will be reflected in this packet's contents.
        /// </summary>
        /// <param name="source">source array</param>
        /// <param name="offset">offset into <see cref="source"/></param>
        /// <param name="count">number of bytes from <see cref="source"/> starting 
        ///     at <see cref="offset"/></param>
        public void Add(byte[] source, int offset, int count)
        {
            Add(new ArraySegment<byte>(source, offset, count));
        }

        public void Clear()
        {
            list.Clear();
            length = 0;
        }

        public bool Contains(ArraySegment<byte> item)
        {
            return list.Contains(item);
        }

        public void CopyTo(ArraySegment<byte>[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public bool Remove(ArraySegment<byte> item)
        {
            length -= item.Count;
            return list.Remove(item);
        }

        public int Count
        {
            get { return list.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public int IndexOf(ArraySegment<byte> item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, ArraySegment<byte> item)
        {
            list.Insert(index, item);
            length += item.Count;
        }

        public void RemoveAt(int index)
        {
            length -= list[index].Count;
            list.RemoveAt(index);
        }

        public ArraySegment<byte> this[int index]
        {
            get { return list[index]; }
            set {
                length -= list[index].Count;
                list[index] = value;
                length += value.Count;
            }
        }

        public IEnumerator<ArraySegment<byte>> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }


        public byte[] ToArray()
        {
            MemoryStream ms = new MemoryStream(length);
            foreach (ArraySegment<byte> segment in list)
            {
                ms.Write(segment.Array, segment.Offset, segment.Count);
            }
            return ms.ToArray();
        }
    }

    /// <summary>The lightweight marshaller is a provides the message payloads as raw 
    /// uninterpreted byte arrays so as to avoid introducing latency from unneeded processing.
    /// Should your server need to use the message content, then you server should 
    /// use the DotNetSerializingMarshaller.</summary>
    /// <seealso cref="T:GT.Net.DotNetSerializingMarshaller"/>
    public class LightweightDotNetSerializingMarshaller : IMarshaller
    {
        /*
         * Messages have an 8-byte header (MessageHeaderByteSize):
         * byte 0 is the channel
         * byte 1 is the message type
         * bytes 2-5 encode the message data length
         */

        public virtual string[] Descriptors
        {
            get
            {
                return new string[] { ".NET-1.0" };
            }
        }

        public virtual void Dispose()
        {
        }

        #region Marshalling

        virtual public void Marshal(int senderIdentity, Message m, Stream output, ITransport t)
        {
            // This marshaller doesn't use <see cref="senderIdentity"/>.
            Debug.Assert(output.CanSeek);

            long startPosition = output.Position;
            output.WriteByte((byte)m.MessageType);
            output.WriteByte(m.Channel);    // NB: SystemMessages use Channel to encode the sysmsg descriptor
            if (m is RawMessage)
            {
                RawMessage rm = (RawMessage)m;
                ByteUtils.EncodeLength((int)rm.Bytes.Length, output);
                output.Write(rm.Bytes, 0, rm.Bytes.Length);
            }
            else
            {
                // MarshalContents, and its callers, are responsible for putting on
                // the payload length
                MarshalContents(m, output, t);
            }
            if (output.Position - startPosition > t.MaximumPacketSize)
            {
                throw new MarshallingException(
                    String.Format("marshalled message exceeds transport's capacity ({0} vs {1} max)",
                    output.Position - startPosition, t.MaximumPacketSize));
            }
        }

        virtual protected void MarshalContents(Message m, Stream output, ITransport t)
        {
            // Individual marshalling methods are responsible for first encoding
            // the payload length
            switch (m.MessageType)
            {
            case MessageType.Session:
                MarshalSessionAction((SessionMessage)m, output);
                break;
            case MessageType.System:
                // channel is the system message type
                MarshalSystemMessage((SystemMessage)m, output);
                break;
            default:
                throw new MarshallingException(String.Format("ERROR: {0} cannot handle messages of type {1}",
                    this.GetType().Name, m.GetType().Name));
            }
        }

        protected void MarshalSessionAction(SessionMessage sm, Stream output)
        {
            ByteUtils.EncodeLength(1 + 4, output);
            output.WriteByte((byte)sm.Action);
            output.Write(BitConverter.GetBytes(sm.ClientId), 0, 4);
        }

        protected void MarshalSystemMessage(SystemMessage systemMessage, Stream output)
        {
            // SystemMessageType is the channel
            ByteUtils.EncodeLength(systemMessage.data.Length, output);
            output.Write(systemMessage.data, 0, systemMessage.data.Length);
        }

        #endregion

        #region Unmarshalling

        virtual public void Unmarshal(Stream input, ITransport t, EventHandler<MessageEventArgs> messageAvailable)
        {
            Debug.Assert(messageAvailable != null, "callers must provide a messageAvailale handler");
            // Could check the version or something here?
            MessageType type = (MessageType)input.ReadByte();
            byte channel = (byte)input.ReadByte();
            int length = ByteUtils.DecodeLength(input);
            Message m = UnmarshalContent(channel, type, new WrappedStream(input, (uint)length));
            messageAvailable(this, new MessageEventArgs(t, m));
        }

        virtual protected Message UnmarshalContent(byte channel, MessageType type, Stream input)
        {
            switch (type)
            {
            case MessageType.Session:
                return UnmarshalSessionAction(channel, type, input);
            case MessageType.System:
                return UnmarshalSystemMessage(channel, type, input);
            default:
                int length = (int)(input.Length - input.Position);
                byte[] data = new byte[length];
                input.Read(data, 0, length);
                return new RawMessage(channel, type, data);
            }
        }

        protected Message UnmarshalSystemMessage(byte channel, MessageType messageType, Stream input)
        {
            // SystemMessageType is the channel
            byte[] contents = new byte[input.Length - input.Position];
            input.Read(contents, 0, contents.Length);
            return new SystemMessage((SystemMessageType)channel, contents);
        }

        protected SessionMessage UnmarshalSessionAction(byte channel, MessageType type, Stream input)
        {
            SessionAction ac = (SessionAction)input.ReadByte();
            return new SessionMessage(channel, BitConverter.ToInt32(ReadBytes(input, 4), 0), ac);
        }

        #endregion

        protected byte[] ReadBytes(Stream input, int length)
        {
            byte[] result = new byte[length];
            input.Read(result, 0, length);
            return result;
        }
    }

    public class DotNetSerializingMarshaller :
            LightweightDotNetSerializingMarshaller
    {
        protected BinaryFormatter formatter = new BinaryFormatter();

        override public void Dispose()
        {
            formatter = null;
            base.Dispose();
        }

        #region Marshalling

        override protected void MarshalContents(Message m, Stream output, ITransport t)
        {
            // Individual marshalling methods are responsible for first encoding
            // the payload length
            switch (m.MessageType)
            {
            case MessageType.Binary:
                MarshalBinary(((BinaryMessage)m).Bytes, output);
                break;
            case MessageType.String:
                MarshalString(((StringMessage)m).Text, output);
                break;
            case MessageType.Object:
                MarshalObject(((ObjectMessage)m).Object, output);
                break;
            case MessageType.Tuple1D:
            case MessageType.Tuple2D:
            case MessageType.Tuple3D:
                MarshalTupleMessage((TupleMessage)m, output);
                break;
            case MessageType.Session:
                MarshalSessionAction((SessionMessage)m, output);
                break;
            case MessageType.System:
                MarshalSystemMessage((SystemMessage)m, output);
                break;
            default:
                throw new InvalidOperationException("unknown message type: " + m.MessageType);
            }
        }

        protected void MarshalString(string s, Stream output)
        {
            StreamWriter w = new StreamWriter(output, UTF8Encoding.UTF8);
            ByteUtils.EncodeLength(UTF8Encoding.UTF8.GetByteCount(s), output);
            w.Write(s);
            w.Flush();
        }

        protected void MarshalObject(object o, Stream output)
        {
            // adaptive length-encoding has one major downside: we
            // can't predict how many bytes the length will require.  
            // So all the hard work for trying to minimize copies buys
            // us little :-(
            // FIXME: could just reserve a fixed 3 bytes instead?
            MemoryStream ms = new MemoryStream(64);    // guestimate
            formatter.Serialize(ms, o);
            ByteUtils.EncodeLength((int)ms.Length, output);
            ms.WriteTo(output);
        }

        protected void MarshalBinary(byte[] bytes, Stream output)
        {
            ByteUtils.EncodeLength(bytes.Length, output);
            output.Write(bytes, 0, bytes.Length);
        }

        protected void MarshalTupleMessage(TupleMessage tm, Stream output)
        {
            // adaptive length-encoding has one major downside: we
            // can't predict how many bytes the length will require.  
            // So all the hard work for trying to minimize copies buys
            // us little :-(
            // FIXME: could just reserve a fixed 3 bytes instead?
            MemoryStream ms = new MemoryStream(64);    // guestimate

            ms.Write(BitConverter.GetBytes(tm.ClientId), 0, 4);
            if (tm.Dimension >= 1) { EncodeConvertible(tm.X, ms); }
            if (tm.Dimension >= 2) { EncodeConvertible(tm.Y, ms); }
            if (tm.Dimension >= 3) { EncodeConvertible(tm.Z, ms); }
            
            ByteUtils.EncodeLength((int)ms.Length, output);
            ms.WriteTo(output);
        }

        protected void EncodeConvertible(IConvertible value, Stream output) {
            output.WriteByte((byte)value.GetTypeCode());
            byte[] result;
            switch (value.GetTypeCode())
            {
            case TypeCode.Byte: output.WriteByte(value.ToByte(null)); return;
            case TypeCode.Char: result = BitConverter.GetBytes(value.ToChar(null)); break;
            case TypeCode.Single: result = BitConverter.GetBytes(value.ToSingle(null)); break;
            case TypeCode.Double: result = BitConverter.GetBytes(value.ToDouble(null)); break;
            case TypeCode.Int16: result = BitConverter.GetBytes(value.ToInt16(null)); break;
            case TypeCode.Int32: result = BitConverter.GetBytes(value.ToInt32(null)); break;
            case TypeCode.Int64: result = BitConverter.GetBytes(value.ToInt64(null)); break;
            case TypeCode.UInt16: result = BitConverter.GetBytes(value.ToUInt16(null)); break;
            case TypeCode.UInt32: result = BitConverter.GetBytes(value.ToUInt32(null)); break;
            case TypeCode.UInt64: result = BitConverter.GetBytes(value.ToUInt64(null)); break;
            default: throw new MarshallingException("Unhandled form of IConvertible: " + value.GetTypeCode());
            }
            output.Write(result, 0, result.Length);
        }

        #endregion

        #region Unmarshalling

        override protected Message UnmarshalContent(byte channel, MessageType type, Stream input)
        {
            switch (type)
            {
            case MessageType.Binary:
                return UnmarshalBinary(channel, (MessageType)type, input);
            case MessageType.String:
                return UnmarshalString(channel, (MessageType)type, input);
            case MessageType.Object:
                return UnmarshalObject(channel, (MessageType)type, input);
            case MessageType.Tuple1D:
            case MessageType.Tuple2D:
            case MessageType.Tuple3D:
                return UnmarshalTuple(channel, (MessageType)type, input);
            case MessageType.Session:
                return UnmarshalSessionAction(channel, type, input);
            case MessageType.System:
                return UnmarshalSystemMessage(channel, type, input);

            default:
                throw new InvalidOperationException("unknown message type: " + (MessageType)type);
            }
        }

        protected StringMessage UnmarshalString(byte channel, MessageType type, Stream input)
        {
            StreamReader sr = new StreamReader(input, System.Text.UTF8Encoding.UTF8);
            return new StringMessage(channel, sr.ReadToEnd());
        }

        protected ObjectMessage UnmarshalObject(byte channel, MessageType type, Stream input)
        {
            return new ObjectMessage(channel, formatter.Deserialize(input));
        }

        protected BinaryMessage UnmarshalBinary(byte channel, MessageType type, Stream input)
        {
            int length = (int)(input.Length - input.Position);
            byte[] result = new byte[length];
            input.Read(result, 0, length);
            return new BinaryMessage(channel, result);
        }

        protected TupleMessage UnmarshalTuple(byte channel, MessageType type, Stream input)
        {
            int clientId = BitConverter.ToInt32(ReadBytes(input, 4), 0);
            switch (type)
            {
            case MessageType.Tuple1D:
                return new TupleMessage(channel, clientId, DecodeConvertible(input));
            case MessageType.Tuple2D:
                return new TupleMessage(channel, clientId, DecodeConvertible(input), DecodeConvertible(input));
            case MessageType.Tuple3D:
                return new TupleMessage(channel, clientId, DecodeConvertible(input),
                    DecodeConvertible(input), DecodeConvertible(input));
            }
            throw new MarshallingException("MessageType is not a tuple: " + type);
        }

        protected IConvertible DecodeConvertible(Stream input)
        {
            TypeCode tc = (TypeCode)input.ReadByte();
            switch (tc)
            {
            case TypeCode.Byte: return input.ReadByte();
            case TypeCode.Char: return BitConverter.ToChar(ReadBytes(input, 2), 0);
            case TypeCode.Single: return BitConverter.ToSingle(ReadBytes(input, 4), 0);
            case TypeCode.Double: return BitConverter.ToDouble(ReadBytes(input, 8), 0);
            case TypeCode.Int16: return BitConverter.ToInt16(ReadBytes(input, 2), 0);
            case TypeCode.Int32: return BitConverter.ToInt32(ReadBytes(input, 4), 0);
            case TypeCode.Int64: return BitConverter.ToInt64(ReadBytes(input, 8), 0);
            case TypeCode.UInt16: return BitConverter.ToUInt16(ReadBytes(input, 2), 0);
            case TypeCode.UInt32: return BitConverter.ToUInt32(ReadBytes(input, 4), 0);
            case TypeCode.UInt64: return BitConverter.ToUInt64(ReadBytes(input, 8), 0);
            default: throw new MarshallingException("Unknown IConvertible type: " + tc);
            }
        }

        #endregion

    }
}
