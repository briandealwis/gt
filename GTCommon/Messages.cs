namespace GT.Common
{
    #region Message Classes

    /// <remarks>A GT message; the contents of the message have not been unmarshalled.</remarks>
    public class Message
    {
        /// <summary>The channel that this message is on.</summary>
        public byte id;

        /// <summary>The type of message.</summary>
        public MessageType type;

        /// <summary>The message payload.</summary>
        public byte[] data;

        /// <summary>Creates a new outbound message.</summary>
        /// <param name="id">The channel that this message is on.</param>
        /// <param name="type">The type of message.</param>
        /// <param name="data">The data of the message.</param>
        public Message(byte id, MessageType type, byte[] data)
        {
            this.id = id;
            this.type = type;
            this.data = data;
        }
    }

    /// <remarks>
    /// Record information necessary for reading a data from the wire.
    /// If <value>messageType</value> is 0, then this represents a header.
    /// </remarks>
    public class MessageInProgress : Message
    {
        public int position;
        public int bytesRemaining;

        /// <summary>
        /// Construct a Message-in-Progress for reading in a data header.
        /// </summary>
        /// <param name="headerSize"></param>
        public MessageInProgress(int headerSize)
            : base(0, 0, new byte[headerSize])
        {
            // assert messageType == 0;  // indicates a data header
            this.position = 0;
            this.bytesRemaining = headerSize;
        }

        /// <summary>
        /// Construct a Message-in-Progress for a data body
        /// </summary>
        /// <param name="id">the channel id for the in-progress data</param>
        /// <param name="messageType">the type of the in-progress data</param>
        /// <param name="size">the size of the in-progress data</param>
        public MessageInProgress(byte id, byte messageType, int size)
            : base(id, (MessageType)messageType, new byte[size])
        {
            // assert messageType != 0;
            this.position = 0;
            this.bytesRemaining = size;
        }

        /// <summary>
        /// Distinguish between a data header and a data body
        /// </summary>
        /// <returns>true if this instance represents a data header</returns>
        public bool IsMessageHeader()
        {
            return type == 0;
        }
    }

    #endregion
}
