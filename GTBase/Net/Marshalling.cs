using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using GT.Utils;

namespace GT.Net
{
    /// <remarks>
    /// A marshaller is responsible for transforming an object to a sequence of bytes,
    /// and later restoring an equivalent object from that sequence of bytes.
    /// GT marshallers must handle strings, session objects, byte arrays, and tuples.
    /// Marshaller implementations have special responsibilities with regards to
    /// handling the <see cref="TransportPacket"/> instances used for storing the
    /// marshalling results, which are documented in the method comments.
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
        /// Returns the results as a list of <see cref="TransportPacket"/> instances,
        /// as encoded in a <see cref="IMarshalledResult"/>.  It is the caller's
        /// responsibility to ensure the <see cref="IMarshalledResult"/> is properly
        /// disposed of using <see cref="IMarshalledResult.Dispose"/>.
        /// Although it is also the caller's responsibility to separately dispose of 
        /// these packets (through <see cref="TransportPacket.Dispose"/>), this 
        /// responsibility is typically transferred once these packets are sent 
        /// across an <see cref="ITransport"/>. 
        /// Unless the marshaller has support for fragmenting a message across
        /// multiple packets, the marshaller should not perform any checks
        /// that the resulting packet fits within the 
        /// <see cref="ITransportDeliveryCharacteristics.MaximumPacketSize"/>.
        /// </summary>
        /// <param name="senderIdentity">the server-unique id of the sender of this message
        /// (i.e., the local client or server's server-unique identifier).  This
        /// should generally be the same number across different invocations.</param>
        /// <param name="message">the message</param>
        /// <param name="tdc">the characteristics of the transport on which the packet 
        ///     will be sent; this may be the actual transport</param>
        /// <returns>the marshalled result, containing a set of transport packets;
        ///     this result <b>must</b> be disposed of with <see cref="IMarshalledResult.Dispose"/>
        ///     once finished with</returns>
        /// <exception cref="MarshallingException">on a marshalling error</exception>
        IMarshalledResult Marshal(int senderIdentity, Message message, ITransportDeliveryCharacteristics tdc);

        /// <summary>
        /// Unmarshal one message (or possibly more) as encoded in the transport-specific 
        /// form from <see cref="input"/>.   Messages are notified using the provided 
        /// <see cref="messageAvailable"/> delegate; a message may not be immediately 
        /// available, for example, if decoding their byte-content depends on information 
        /// carried in a not-yet-seen message.  The marshaller <b>must</b> destructively 
        /// modify the packet to remove the bytes consituting the message; this it typically 
        /// done using either <see cref="TransportPacket.RemoveBytes"/> or 
        /// <see cref="TransportPacket.AsReadStream"/>.  Should the marshaller require 
        /// access to the contents of <see cref="input"/> after this call has returned
        /// then the marshaller should request for a subset of the packet using
        /// <see cref="TransportPacket.Subset"/>.
        /// </summary>
        /// <param name="input">the stream with the packet content</param>
        /// <param name="tdc">the characteristics of transport from which the packet was 
        ///     received; this may be the actual transport</param>
        /// <param name="messageAvailable">a callback for when a message becomes available
        /// from the stream.</param>
        void Unmarshal(TransportPacket input, ITransportDeliveryCharacteristics tdc, 
            EventHandler<MessageEventArgs> messageAvailable);
    }

    /// <summary>
    /// A representation of a marshalled message.  The instance should
    /// have its <see cref="Dispose"/> called when finished with.
    /// </summary>
    /// <summary>
    /// A representation of a marshalled message.  The instance should
    /// have its <see cref="Dispose"/> called when finished with.
    /// </summary>
    public interface IMarshalledResult : IDisposable
    {
        /// <summary>
        /// Triggered when the marshalled result instance has been instructed
        /// to be disposed.
        /// </summary>
        event Action<IMarshalledResult> Disposed;

        /// <summary>
        /// Does this instance have packets *at this moment*?
        /// This is different from having exhausted all available packets.
        /// </summary>
        /// <seealso cref="Finished"/>
        bool HasPackets { get; }

        /// <summary>
        /// Are there any packets remaining from this instance?
        /// </summary>
        bool Finished { get; }

        /// <summary>
        /// Remove the next packet.  Return null if there is no packet available;
        /// this does not necessarily entail that there are no more packets
        /// (i.e., that this instance is <see cref="Finished"/>).
        /// </summary>
        /// <returns></returns>
        TransportPacket RemovePacket();

        /// <summary>
        /// Dispose of this instance.
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// A simple <see cref="IMarshalledResult"/> implementation for
    /// marshallers returning a fixed-number of packets.  Not intended
    /// for marshallers that may produce very long sequences of packets,
    /// such as something that is streaming out a large number of bytes.
    /// </summary>
    public class MarshalledResult : IMarshalledResult
    {
        public event Action<IMarshalledResult> Disposed;
        protected IList<TransportPacket> packets = new List<TransportPacket>(2);

        /// <summary>
        /// Return the list of remaining packets
        /// </summary>
        public IList<TransportPacket> Packets { get { return packets; } }

        public bool HasPackets { get { return packets.Count > 0; } }

        public bool Finished { get { return packets.Count == 0; } }

        public void AddPacket(TransportPacket packet)
        {
            packets.Add(packet);
        }

        public TransportPacket RemovePacket()
        {
            if (!HasPackets) { return null; }
            TransportPacket p = packets[0];
            packets.RemoveAt(0);
            return p;
        }

        public void Dispose()
        {
            if (Disposed != null) { Disposed(this); }
            // this instance doesn't actually do anything
            packets.Clear();
        }
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
    /// Support utilities for the Lightweight Message Container Format 1.1.
    /// (aka LWMCFv1.1).
    /// </summary>
    public class LWMCFv11
    {
        /// <summary>
        /// Descriptor for the Lightweight Message Container Format v1.1:
        /// This format prefix each marshaller with a 6 byte header where:
        /// </para>
        /// <list>
        /// <item> byte 0 is the message type</item>
        /// <item> byte 1 is the channel</item>
        /// <item> bytes 2-6 encode the message data length using the 
        ///     <see cref="DataConverter.Converter.GetBytes(uint)"/> format.</item>
        /// </list>
        /// </summary>
        public const string Descriptor = "LWMCF-1.1";

        /// <summary>
        /// The # bytes in the v1.1 header.
        /// </summary>
        public const uint HeaderSize = 6;

        /// <summary>
        /// Encode a v1.1 compliant header.
        /// </summary>
        /// <returns>the header bytes</returns>
        public static byte[] EncodeHeader(MessageType type, byte channel, uint length)
        {
            byte[] header = new byte[HeaderSize];
            header[0] = (byte)type;
            header[1] = channel;
            byte[] encoded = DataConverter.Converter.GetBytes(length);
            Debug.Assert(encoded.Length == 4);
            encoded.CopyTo(header, 2);
            return header;
        }

        /// <summary>
        /// Encode a v1.1 compliant header.
        /// </summary>
        public static void EncodeHeader(MessageType type, byte channel, uint length, Stream stream)
        {
            stream.WriteByte((byte)type);
            stream.WriteByte(channel);
            byte[] encoded = DataConverter.Converter.GetBytes(length);
            Debug.Assert(encoded.Length == 4);
            stream.Write(encoded, 0, 4);
        }

        public static void DecodeHeader(out MessageType type, out byte channel, out uint length, Stream stream)
        {
            type = (MessageType)stream.ReadByte();
            channel = (byte)stream.ReadByte();
            byte[] encoded = new byte[4];
            stream.Read(encoded, 0, 4);
            length = DataConverter.Converter.ToUInt32(encoded, 0);
        }

        public static void DecodeHeader(out MessageType type, out byte channel, out uint length, byte[] bytes, int offset)
        {
            type = (MessageType)bytes[offset + 0];
            channel = bytes[offset + 1];
            length = DataConverter.Converter.ToUInt32(bytes, offset + 2);
        }
    }

    /// <summary>
    /// The lightweight marshaller provides the message payloads as raw 
    /// uninterpreted byte arrays so as to avoid introducing latency from unneeded processing.
    /// Should your server need to use the message content, then you server should 
    /// use a <see cref="DotNetSerializingMarshaller"/>.
    /// This marshaller adheres to the Lightweight Message Container Format 1.1.
    /// </summary>
    /// <seealso cref="T:GT.Net.DotNetSerializingMarshaller"/>
    public class LightweightDotNetSerializingMarshaller : IMarshaller
    {

        public virtual string[] Descriptors
        {
            get { return new[] { LWMCFv11.Descriptor }; }
        }

        public virtual void Dispose()
        {
        }

        #region Marshalling

        /// <summary>
        /// Marshal the given message to the provided stream in a form suitable to
        /// be sent out on the provided transport.
        /// </summary>
        /// <param name="senderIdentity">the identity of the sender</param>
        /// <param name="msg">the message being sent, that is to be marshalled</param>
        /// <param name="tdc">the characteristics of the transport that will send the marshalled form</param>
        /// <returns>the marshalled representation</returns>
        virtual public IMarshalledResult Marshal(int senderIdentity, Message msg, ITransportDeliveryCharacteristics tdc)
        {
            // This marshaller doesn't use <see cref="senderIdentity"/>.
            MarshalledResult mr = new MarshalledResult();
            TransportPacket tp = new TransportPacket();

            // NB: SystemMessages use Channel to encode the sysmsg descriptor
            if (msg is RawMessage)
            {
                RawMessage rm = (RawMessage)msg;
                tp.Prepend(LWMCFv11.EncodeHeader(msg.MessageType, msg.Channel, (uint)rm.Bytes.Length));
                tp.Add(rm.Bytes);
            }
            else
            {
                //  We use TransportPacket.Prepend to add the marshalling header in-place
                // after the marshalling.
                Stream output = tp.AsWriteStream();
                Debug.Assert(output.CanSeek);
                MarshalContents(msg, output, tdc);
                output.Flush();
                tp.Prepend(LWMCFv11.EncodeHeader(msg.MessageType, msg.Channel, (uint)output.Position));
            }
            mr.AddPacket(tp);
            return mr;
        }

        /// <summary>
        /// Marshal the contents of the message <see cref="m"/> onto the stream <see cref="output"/>.
        /// The channel and message type have already been placed on <see cref="output"/>.
        /// This method is **not responsible** for encoding the message payload length.
        /// </summary>
        /// <param name="m">the message contents to be marshalled</param>
        /// <param name="output">the destination for the marshalled message payload</param>
        /// <param name="tdc">the characteristics of the transport that is to be used for sending</param>
        virtual protected void MarshalContents(Message m, Stream output, ITransportDeliveryCharacteristics tdc)
        {
            // Individual marshalling methods are **NO LONGER RESPONSIBLE** for encoding
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
            output.WriteByte((byte)sm.Action);
            output.Write(DataConverter.Converter.GetBytes(sm.ClientId), 0, 4);
        }

        protected void MarshalSystemMessage(SystemMessage systemMessage, Stream output)
        {
            // SystemMessageType is the channel
            output.Write(systemMessage.data, 0, systemMessage.data.Length);
        }

        #endregion

        #region Unmarshalling

        virtual public void Unmarshal(TransportPacket tp, ITransportDeliveryCharacteristics tdc, EventHandler<MessageEventArgs> messageAvailable)
        {
            Debug.Assert(messageAvailable != null, "callers must provide a messageAvailale handler");
            Stream input = tp.AsReadStream();
            MessageType type;
            byte channel;
            uint length;
            LWMCFv11.DecodeHeader(out type, out channel, out length, input);
            Message m = UnmarshalContent(channel, type, new WrappedStream(input, length), length);
            messageAvailable(this, new MessageEventArgs(tdc as ITransport, m)); // FIXME!!!
        }

        /// <summary>
        /// Unmarshal the content from the provided stream.  The message payload was marshalled as
        /// <see cref="length"/> bytes, is of type <see cref="type"/>, and is intended for 
        /// channel <see cref="channel"/>.
        /// </summary>
        /// <param name="channel">the channel received on</param>
        /// <param name="type">the type of message</param>
        /// <param name="input">the marshalled contents</param>
        /// <param name="length">the number of bytes available</param>
        /// <returns>the unmarshalled message</returns>
        virtual protected Message UnmarshalContent(byte channel, MessageType type, Stream input, uint length)
        {
            switch (type)
            {
            case MessageType.Session:
                return UnmarshalSessionAction(channel, type, input, length);
            case MessageType.System:
                return UnmarshalSystemMessage(channel, type, input, length);
            default:
                return new RawMessage(channel, type, ReadBytes(input, length));
            }
        }

        protected Message UnmarshalSystemMessage(byte channel, MessageType messageType, 
            Stream input, uint length)
        {
            // SystemMessageType is the channel
            return new SystemMessage((SystemMessageType)channel, ReadBytes(input, length));
        }

        protected SessionMessage UnmarshalSessionAction(byte channel, MessageType type, 
            Stream input, uint length)
        {
            Debug.Assert(length == 5);
            SessionAction ac = (SessionAction)input.ReadByte();
            return new SessionMessage(channel, DataConverter.Converter.ToInt32(ReadBytes(input, 4), 0), ac);
        }

        #endregion

        protected byte[] ReadBytes(Stream input, uint length)
        {
            byte[] result = new byte[length];
            input.Read(result, 0, (int)length);
            return result;
        }

        /// <summary>
        /// An uninterpreted, length-prefixed message
        /// </summary>
        public class RawMessage : Message
        {
            /// <summary>The binary byte content.</summary>
            public byte[] Bytes { get { return bytes; } }

            protected byte[] bytes;

            public RawMessage(byte channel, MessageType t, byte[] data)
                : base(channel, t)
            {
                this.bytes = data;
            }

            public override string ToString()
            {
                return "Raw Message (uninterpreted bytes)";
            }
        }
    }

    /// <summary>
    /// A standard-issue marshaller that marshals strings, byte arrays, and objects.
    /// Strings are marshalled as UTF-8, byte arrays are marshalled as-is, and objects
    /// are marshalled using the .NET Serialization framework.
    /// </summary>
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

        override protected void MarshalContents(Message m, Stream output, ITransportDeliveryCharacteristics tdc)
        {
            // Individual marshalling methods are **NO LONGER RESPONSIBLE** for encoding
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
            default:
                base.MarshalContents(m, output, tdc);
                break;
            }
        }

        protected void MarshalString(string s, Stream output)
        {
            StreamWriter w = new StreamWriter(output, Encoding.UTF8);
            w.Write(s);
            w.Flush();
        }

        protected void MarshalObject(object o, Stream output)
        {
            formatter.Serialize(output, o);
        }

        protected void MarshalBinary(byte[] bytes, Stream output)
        {
            output.Write(bytes, 0, bytes.Length);
        }

        protected void MarshalTupleMessage(TupleMessage tm, Stream output)
        {
            output.Write(DataConverter.Converter.GetBytes(tm.ClientId), 0, 4);
            if (tm.Dimension >= 1) { EncodeConvertible(tm.X, output); }
            if (tm.Dimension >= 2) { EncodeConvertible(tm.Y, output); }
            if (tm.Dimension >= 3) { EncodeConvertible(tm.Z, output); }
        }

        protected void EncodeConvertible(IConvertible value, Stream output) {
            output.WriteByte((byte)value.GetTypeCode());
            byte[] result;
            switch (value.GetTypeCode())
            {
            // the following encode directly on the stream
            case TypeCode.Boolean: output.WriteByte((byte)((bool)value ? 1 : 0)); return;
            case TypeCode.Byte: output.WriteByte(value.ToByte(null)); return;
            case TypeCode.SByte: output.WriteByte((byte)(value.ToSByte(null) + 128)); return;

            case TypeCode.Object:
                formatter.Serialize(output, value);
                return;

            case TypeCode.String: {
                long lengthPosition = output.Position;
                output.Write(new byte[4], 0, 4);
                StreamWriter w = new StreamWriter(output, Encoding.UTF8);
                w.Write((string)value);
                w.Flush();
                long savedPosition = output.Position;
                uint payloadLength = (uint)(output.Position - lengthPosition - 4);
                output.Position = lengthPosition;
                output.Write(DataConverter.Converter.GetBytes(payloadLength), 0, 4);
                output.Position = savedPosition;
                return;
            }

            // the following obtain byte arrays which are dumped below
            case TypeCode.Char: result = DataConverter.Converter.GetBytes(value.ToChar(null)); break;
            case TypeCode.Single: result = DataConverter.Converter.GetBytes(value.ToSingle(null)); break;
            case TypeCode.Double: result = DataConverter.Converter.GetBytes(value.ToDouble(null)); break;
            case TypeCode.Int16: result = DataConverter.Converter.GetBytes(value.ToInt16(null)); break;
            case TypeCode.Int32: result = DataConverter.Converter.GetBytes(value.ToInt32(null)); break;
            case TypeCode.Int64: result = DataConverter.Converter.GetBytes(value.ToInt64(null)); break;
            case TypeCode.UInt16: result = DataConverter.Converter.GetBytes(value.ToUInt16(null)); break;
            case TypeCode.UInt32: result = DataConverter.Converter.GetBytes(value.ToUInt32(null)); break;
            case TypeCode.UInt64: result = DataConverter.Converter.GetBytes(value.ToUInt64(null)); break;
            case TypeCode.DateTime: result = DataConverter.Converter.GetBytes(((DateTime)value).ToBinary()); break;

            default: throw new MarshallingException("Unhandled form of IConvertible: " + value.GetTypeCode());
            }
            output.Write(result, 0, result.Length);
        }

        #endregion

        #region Unmarshalling

        override protected Message UnmarshalContent(byte channel, MessageType type, 
            Stream input, uint length)
        {
            switch (type)
            {
            case MessageType.Binary:
                return UnmarshalBinary(channel, type, input);
            case MessageType.String:
                return UnmarshalString(channel, type, input);
            case MessageType.Object:
                return UnmarshalObject(channel, type, input);
            case MessageType.Tuple1D:
            case MessageType.Tuple2D:
            case MessageType.Tuple3D:
                return UnmarshalTuple(channel, type, input);
            default:
                return base.UnmarshalContent(channel, type, input, length);
            }
        }

        protected StringMessage UnmarshalString(byte channel, MessageType type, Stream input)
        {
            StreamReader sr = new StreamReader(input, Encoding.UTF8);
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
            int clientId = DataConverter.Converter.ToInt32(ReadBytes(input, 4), 0);
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
            case TypeCode.Boolean: return input.ReadByte() == 0 ? false : true;
            case TypeCode.SByte: return (sbyte)(input.ReadByte() - 128);
            case TypeCode.Byte: return input.ReadByte();
            case TypeCode.Char: return DataConverter.Converter.ToChar(ReadBytes(input, 2), 0);
            case TypeCode.Single: return DataConverter.Converter.ToSingle(ReadBytes(input, 4), 0);
            case TypeCode.Double: return DataConverter.Converter.ToDouble(ReadBytes(input, 8), 0);
            case TypeCode.Int16: return DataConverter.Converter.ToInt16(ReadBytes(input, 2), 0);
            case TypeCode.Int32: return DataConverter.Converter.ToInt32(ReadBytes(input, 4), 0);
            case TypeCode.Int64: return DataConverter.Converter.ToInt64(ReadBytes(input, 8), 0);
            case TypeCode.UInt16: return DataConverter.Converter.ToUInt16(ReadBytes(input, 2), 0);
            case TypeCode.UInt32: return DataConverter.Converter.ToUInt32(ReadBytes(input, 4), 0);
            case TypeCode.UInt64: return DataConverter.Converter.ToUInt64(ReadBytes(input, 8), 0);
            case TypeCode.DateTime: return DateTime.FromBinary(DataConverter.Converter.ToInt64(ReadBytes(input, 8), 0));

            case TypeCode.Object: return (IConvertible)formatter.Deserialize(input);
            case TypeCode.String: {
                uint length = DataConverter.Converter.ToUInt32(ReadBytes(input, 4), 0);
                StreamReader sr = new StreamReader(new WrappedStream(input, length), Encoding.UTF8);
                return sr.ReadToEnd();
            }
            default: throw new MarshallingException("Unknown IConvertible type: " + tc);
            }
        }

        #endregion

    }
}
