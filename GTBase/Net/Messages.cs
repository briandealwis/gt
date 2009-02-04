using System;
using System.Diagnostics;
using System.Text;
using GT.Utils;
namespace GT.Net
{
    #region Message Classes

    /// <remarks>A GT message; the contents of the message have not been unmarshalled.</remarks>
    public abstract class Message
    {
        /// <summary>The channel that this message is on.</summary>
        virtual public byte Channel { get { return channel; } }

        /// <summary>The type of message.</summary>
        virtual public MessageType MessageType { get { return type; } }

        protected byte channel;
        protected MessageType type;

        /// <summary>Creates a new outbound message.</summary>
        /// <param name="channel">The channel that this message is on.</param>
        /// <param name="type">The type of message.</param>
        public Message(byte channel, MessageType type)
        {
            this.channel = channel;
            this.type = type;
        }

        public override string ToString()
        {
            return GetType().Name + "(type:" + type + " channel:" + channel + ")";
        }
    }

    public class BinaryMessage : Message
    {
        /// <summary>The binary byte content.</summary>
        public byte[] Bytes { get { return bytes; } }

        protected byte[] bytes;

        public BinaryMessage(byte channel, byte[] bytes)
            : base(channel, MessageType.Binary)
        {
            this.bytes = bytes;
        }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder(GetType().Name);
            result.Append(": ");
            result.Append(bytes.LongLength);
            result.Append(" bytes");
            for (int i = 0; i < bytes.Length; i += 16)
            {
                result.Append("\n  ");
                result.Append(i.ToString("X3"));
                result.Append(": ");
                result.Append(ByteUtils.DumpBytes(bytes, i, 16));
                result.Append(ByteUtils.AsPrintable(bytes, i, 16));
            }
            return result.ToString();
        }
    }

    public class StringMessage : Message
    {
        /// <summary>The string text.</summary>
        public string Text { get { return text; } }

        protected string text;

        public StringMessage(byte channel, string text)
            : base(channel, MessageType.String)
        {
            this.text = text;
        }

        public override string ToString()
        {
            return GetType().Name + "(type:" + type + " channel:" + channel + " text:\"" + text + "\")";
        }

    }

    public class ObjectMessage : Message
    {
        /// <summary>The message's object.</summary>
        public object Object { get { return obj; } }

        protected object obj;

        public ObjectMessage(byte channel, object obj)
            : base(channel, MessageType.Object)
        {
            this.obj = obj;
        }

        public override string ToString()
        {
            return GetType().Name + "(type:" + type + " channel:" + channel + " object:\"" + obj + "\")";
        }
    }

    /// <summary>A message from a session about a particular client</summary>
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
        public SessionMessage(byte channel, int clientId, SessionAction e)
            : base(channel, MessageType.Session)
        {
            this.clientId = clientId;
            this.action = e;
        }

        public override string ToString()
        {
            return GetType().Name + "(type:" + type + " channel:" + channel + " client:" + clientId + " " + action + ")";
        }
    }

    /// <summary>A GT control message.  System messages aren't sent
    /// on a channel; the descriptor (the type of system message) is 
    /// instead encoded as the channel.</summary>
    public class SystemMessage : Message
    {
        public byte[] data;

        /// <summary>Create a new SystemMessage</summary>
        public SystemMessage(SystemMessageType t)
            : base((byte)t, MessageType.System)
        {
            data = new byte[0];
        }

        /// <summary>Create a new SystemMessage</summary>
        public SystemMessage(SystemMessageType t, byte[] data)
            : base((byte)t, MessageType.System)
        {
            this.data = data;
        }

        /// <summary>
        /// Return the system message descriptor.  System messages aren't sent
        /// on a channel; the descriptor is instead encoded as the channel.
        /// </summary>
        public SystemMessageType Descriptor { get { return (SystemMessageType)channel; } }

        //public override byte Channel
        //{
        //    get
        //    {
        //        throw new NotSupportedException("system messages are not sent on a channel; use SystemMessage.Descriptor");
        //    }
        //}

        public override string ToString()
        {
            return GetType().Name + "(" + Descriptor +
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
