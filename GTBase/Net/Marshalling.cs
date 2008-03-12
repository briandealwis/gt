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

        void Marshal(Message message, Stream output, ITransport t);
        Message Unmarshal(Stream output, ITransport t);
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

        protected ObjectMessage UnmarshalObject(byte id, MessageType type, Stream output)
        {
            return new ObjectMessage(id, formatter.Deserialize(output));
        }

        protected BinaryMessage UnmarshalBinary(byte id, MessageType type, Stream output)
        {
            int length = (int)(output.Length - output.Position);
            byte[] result = new byte[length];
            output.Read(result, 0, length);
            return new BinaryMessage(id, result);
        }

        protected SessionMessage UnmarshalSessionAction(byte id, MessageType type, Stream output)
        {
            SessionAction ac = (SessionAction)output.ReadByte();
            byte[] idbytes = new byte[4];
            output.Read(idbytes, 0, 4);
            return new SessionMessage(id, BitConverter.ToInt32(idbytes, 0), ac);
        }

        #endregion

    }
}
