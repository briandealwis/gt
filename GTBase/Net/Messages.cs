using System.Diagnostics;
using GT.Utils;
namespace GT.Net
{
    #region Message Classes

    /// <remarks>A GT message; the contents of the message have not been unmarshalled.</remarks>
    public abstract class Message
    {
        /// <summary>The channel that this message is on.</summary>
        virtual public byte Id { get { return id; } }

        /// <summary>The type of message.</summary>
        virtual public MessageType MessageType { get { return type; } }

        protected byte id;
        protected MessageType type;

        /// <summary>Creates a new outbound message.</summary>
        /// <param name="id">The channel that this message is on.</param>
        /// <param name="type">The type of message.</param>
        public Message(byte id, MessageType type)
        {
            this.id = id;
            this.type = type;
        }

        public override string ToString()
        {
            return GetType().Name + "(type:" + type + " id:" + id + ")";
        }
    }

    public class BinaryMessage : Message
    {
        /// <summary>The binary byte content.</summary>
        public byte[] Bytes { get { return bytes; } }

        protected byte[] bytes;

        public BinaryMessage(byte id, byte[] bytes)
            : base(id, MessageType.Binary)
        {
            this.bytes = bytes;
        }
    }

    public class StringMessage : Message
    {
        /// <summary>The string text.</summary>
        public string Text { get { return text; } }

        protected string text;

        public StringMessage(byte id, string text)
            : base(id, MessageType.String)
        {
            this.text = text;
        }

        public override string ToString()
        {
            return GetType().Name + "(type:" + type + " id:" + id + " text:\"" + text + "\")";
        }

    }

    public class ObjectMessage : Message
    {
        /// <summary>The message's object.</summary>
        public object Object { get { return obj; } }

        protected object obj;

        public ObjectMessage(byte id, object obj)
            : base(id, MessageType.Object)
        {
            this.obj = obj;
        }

        public override string ToString()
        {
            return GetType().Name + "(type:" + type + " id:" + id + " object:\"" + obj + "\")";
        }
    }

    /// <summary>A message from a session about id</summary>
    public class SessionMessage : Message
    {
        /// <summary>What occurred on the session.</summary>
        public SessionAction Action { get { return action; } }

        /// <summary>Which client was affected.</summary>
        public int ClientId { get { return clientId; } }

        protected SessionAction action;
        protected int clientId;

        /// <summary>Create a new SessionMessage</summary>
        /// <param name="clientId">The subject of the session action.</param>
        /// <param name="e">The session action.</param>
        public SessionMessage(byte id, int clientId, SessionAction e)
            : base(id, MessageType.Session)
        {
            this.clientId = clientId;
            this.action = e;
        }

        public override string ToString()
        {
            return GetType().Name + "(type:" + type + " id:" + id + " client:" + clientId + " " + action + ")";
        }
    }

    /// <summary>A message from a session about id</summary>
    public class SystemMessage : Message
    {
        public byte[] data;

        /// <summary>Create a new SystemMessage</summary>
        public SystemMessage(SystemMessageType t)
            : base((byte)t, MessageType.System)
        {
            this.data = new byte[0];
        }

        /// <summary>Create a new SystemMessage</summary>
        public SystemMessage(SystemMessageType t, byte[] data)
            : base((byte)t, MessageType.System)
        {
            this.data = data;
        }

        public override string ToString()
        {
            return GetType().Name + "(type:" + type + " id:" + id + 
                " data:[" + ByteUtils.DumpBytes(data) + "])";
        }
    }

    /// <summary>
    /// An uninterpreted message
    /// </summary>
    public class RawMessage : Message
    {
        /// <summary>The binary byte content.</summary>
        public byte[] Bytes { get { return bytes; } }

        protected byte[] bytes;

        public RawMessage(byte id, MessageType t, byte[] data)
            : base(id, t)
        {
            this.bytes = data;
        }
    }


    /// <remarks>
    /// Record information necessary for reading or writing a packet from the wire.
    /// Assumes reading is a two-phase process, of first reading in a packet header 
    /// and then reading in the packet body.
    /// </remarks>
    public class PacketInProgress
    {
        public bool isHeader;

        /// <summary>The packet payload.</summary>
        public byte[] data;

        public uint position;
        public uint bytesRemaining;

        /// <summary>
        /// Construct a Packet-in-Progress for reading in a packet.
        /// </summary>
        public PacketInProgress(uint size, bool isHeader)
        {
            data = new byte[size];
            this.isHeader = isHeader;
            this.position = 0;
            this.bytesRemaining = size;
        }

        /// <summary>
        /// Construct a Packet-in-Progress for sending a packet.
        /// </summary>
        public PacketInProgress(byte[] packet)
        {
            data = packet;
            this.isHeader = false;
            this.position = 0;
            this.bytesRemaining = (uint)packet.Length;
        }


        /// <summary>
        /// For incoming packets, distinguish whether this instance represents a packet header 
        /// or a packet body.
        /// </summary>
        /// <returns>true if this instance represents a header</returns>
        public bool IsMessageHeader()
        {
            return isHeader;
        }

        public void Advance(uint increment)
        {
            Debug.Assert(increment > 0);
            position += increment;
            bytesRemaining -= increment;
        }

        public uint Length { get { return (uint)data.Length; } }
    }

    #endregion

    public class PendingMessage
    {
        protected Message message;
        protected MessageDeliveryRequirements mdr;
        protected ChannelDeliveryRequirements cdr;

        public PendingMessage(Message m, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            this.message = m;
            this.mdr = mdr;
            this.cdr = cdr;
        }

        public Message Message { get { return message; } }
        public MessageDeliveryRequirements MDR { get { return mdr; } }
        public ChannelDeliveryRequirements CDR { get { return cdr; } }
    }
}
