using System;
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
        /// <param name="message">the message</param>
        /// <param name="output">the stream collecting the packet content</param>
        /// <param name="t">the transport on which the packet will be sent</param>
        void Marshal(Message message, Stream output, ITransport t);

        /// <summary>
        /// Unmarshal the message as encoded in the transport-specific form from the given
        /// stream.
        /// </summary>
        /// <param name="input">the stream with the packet content</param>
        /// <param name="t">the transport from which the packet was received</param>
        /// <returns>the message, or null if no message was present</returns>
        Message Unmarshal(Stream input, ITransport t);
    }

    public class MarshallingException : GTException {
        public MarshallingException(string message) 
            : base(message) 
        {
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
         * byte 0 is the channel id
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

        public void Dispose()
        {
        }

        #region Marshalling

        virtual public void Marshal(Message m, Stream output, ITransport t)
        {
            Debug.Assert(output.CanSeek);

            output.WriteByte(m.Id);
            output.WriteByte((byte)m.MessageType);
            Debug.Assert(m.GetType().Equals(typeof(RawMessage)));
            RawMessage rm = (RawMessage)m;
            ByteUtils.EncodeLength((int)rm.Bytes.Length, output);
            output.Write(rm.Bytes, 0, rm.Bytes.Length);
        }

        #endregion

        #region Unmarshalling

        virtual public Message Unmarshal(Stream input, ITransport t)
        {
            // Could check the version or something here?
            byte id = (byte)input.ReadByte();
            byte type = (byte)input.ReadByte();
            int length = ByteUtils.DecodeLength(input);
            byte[] data = new byte[length];
            input.Read(data, 0, length);
            return new RawMessage(id, (MessageType)type, data);
        }

        #endregion

    }

    public class DotNetSerializingMarshaller :
            LightweightDotNetSerializingMarshaller
    {
        protected BinaryFormatter formatter = new BinaryFormatter();

        public void Dispose()
        {
            formatter = null;
            base.Dispose();
        }

        #region Marshalling

        override public void Marshal(Message m, Stream output, ITransport t)
        {
            Debug.Assert(output.CanSeek);

            output.WriteByte(m.Id);
            output.WriteByte((byte)m.MessageType);
            // ok, this is crappy: because the length-encoding is adaptive, we
            // can't predict how many bytes it will require.  So all the
            // hard work for trying to minimize copies comes to nothing :-(
            MemoryStream ms = new MemoryStream(64);    // guestimate
            MarshalContents(m, ms, t);
            ByteUtils.EncodeLength((int)ms.Length, output);
            ms.WriteTo(output);
        }

        protected void MarshalContents(Message m, Stream output, ITransport t)
        {
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
            case MessageType.Session:
                MarshalSessionAction((SessionMessage)m, output);
                break;
            case MessageType.System:
                // channel Id is the system message type
                MarshalSystemMessage((SystemMessage)m, output);
                break;
            case MessageType.Tuple1D:
            case MessageType.Tuple2D:
            case MessageType.Tuple3D:
                MarshalTupleMessage((TupleMessage)m, output);
                break;
            default:
                throw new InvalidOperationException("unknown message type: " + m.MessageType);
            }
        }

        protected void MarshalSystemMessage(SystemMessage systemMessage, Stream output)
        {
            // SystemMessageType is the channel id
            output.Write(systemMessage.data, 0, systemMessage.data.Length);
        }

        protected void MarshalString(string s, Stream output)
        {
            StreamWriter w = new StreamWriter(output, System.Text.UTF8Encoding.UTF8);
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

        protected void MarshalSessionAction(SessionMessage sm, Stream output)
        {
            output.WriteByte((byte)sm.Action);
            output.Write(BitConverter.GetBytes(sm.ClientId), 0, 4);
        }

        protected void MarshalTupleMessage(TupleMessage tm, Stream output)
        {
            output.Write(BitConverter.GetBytes(tm.ClientId), 0, 4);
            if (tm.Dimension >= 1) { MarshalConvertible(tm.X, output); }
            if (tm.Dimension >= 2) { MarshalConvertible(tm.Y, output); }
            if (tm.Dimension >= 3) { MarshalConvertible(tm.Z, output); }
        }

        protected void MarshalConvertible(IConvertible value, Stream output) {
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

        override public Message Unmarshal(Stream input, ITransport t)
        {
            // Could check the version or something here?
            byte id = (byte)input.ReadByte();
            byte type = (byte)input.ReadByte();
            int length = ByteUtils.DecodeLength(input);
            return UnmarshalContent(id, (MessageType)type, new WrappedStream(input, (uint)length));
        }

        protected Message UnmarshalContent(byte id, MessageType type, Stream input)
        {
            switch (type)
            {
            case MessageType.Binary:
                return UnmarshalBinary(id, (MessageType)type, input);
            case MessageType.String:
                return UnmarshalString(id, (MessageType)type, input);
            case MessageType.Object:
                return UnmarshalObject(id, (MessageType)type, input);
            case MessageType.Session:
                return UnmarshalSessionAction(id, (MessageType)type, input);
            case MessageType.System:
                return UnmarshalSystemMessage(id, (MessageType)type, input);
            case MessageType.Tuple1D:
            case MessageType.Tuple2D:
            case MessageType.Tuple3D:
                return UnmarshalTuple(id, (MessageType)type, input);
            default:
                throw new InvalidOperationException("unknown message type: " + (MessageType)type);
            }
        }

        protected Message UnmarshalSystemMessage(byte id, MessageType messageType, Stream input)
        {
            // SystemMessageType is the channel id
            byte[] contents = new byte[input.Length - input.Position];
            input.Read(contents, 0, contents.Length);
            return new SystemMessage((SystemMessageType)id, contents);
        }

        protected StringMessage UnmarshalString(byte id, MessageType type, Stream input)
        {
            StreamReader sr = new StreamReader(input, System.Text.UTF8Encoding.UTF8);
            return new StringMessage(id, sr.ReadToEnd());
        }

        protected ObjectMessage UnmarshalObject(byte id, MessageType type, Stream input)
        {
            return new ObjectMessage(id, formatter.Deserialize(input));
        }

        protected BinaryMessage UnmarshalBinary(byte id, MessageType type, Stream input)
        {
            int length = (int)(input.Length - input.Position);
            byte[] result = new byte[length];
            input.Read(result, 0, length);
            return new BinaryMessage(id, result);
        }

        protected SessionMessage UnmarshalSessionAction(byte id, MessageType type, Stream input)
        {
            SessionAction ac = (SessionAction)input.ReadByte();
            return new SessionMessage(id, BitConverter.ToInt32(ReadBytes(input, 4), 0), ac);
        }

        protected TupleMessage UnmarshalTuple(byte id, MessageType type, Stream input)
        {
            int clientId = BitConverter.ToInt32(ReadBytes(input, 4), 0);
            switch (type)
            {
            case MessageType.Tuple1D:
                return new TupleMessage(id, clientId, UnmarshalConvertible(input));
            case MessageType.Tuple2D:
                return new TupleMessage(id, clientId, UnmarshalConvertible(input), UnmarshalConvertible(input));
            case MessageType.Tuple3D:
                return new TupleMessage(id, clientId, UnmarshalConvertible(input),
                    UnmarshalConvertible(input), UnmarshalConvertible(input));
            }
            throw new MarshallingException("MessageType is not a tuple: " + type);
        }

        protected IConvertible UnmarshalConvertible(Stream input)
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

        protected byte[] ReadBytes(Stream input, int length)
        {
            byte[] result = new byte[length];
            input.Read(result, 0, length);
            return result;
        }

        #endregion

    }
}
