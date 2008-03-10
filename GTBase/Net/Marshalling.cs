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

    public class DotNetSerializingMarshaller : IMarshaller
    {
        /*
         * Messages have an 8-byte header (MessageHeaderByteSize):
         * byte 0 is the channel id
         * byte 1 is the message type
         * bytes 2 and 3 are unused
         * bytes 4-7 encode the message data length
         */
        public static readonly int MessageHeaderByteSize = 8;

        protected BinaryFormatter formatter = new BinaryFormatter();

        public virtual string[] Descriptors
        {
            get
            {
                return new string[] { ".NET-1.0" };
            }
        }

        public void Dispose()
        {
            formatter = null;
        }

        #region Marshalling

        public void Marshal(Message m, Stream output, ITransport t)
        {
            Debug.Assert(output.CanSeek);

            output.WriteByte(m.Id);
            output.WriteByte((byte)m.MessageType);
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

        public Message Unmarshal(Stream input, ITransport t)
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
