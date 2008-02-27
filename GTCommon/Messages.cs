namespace GT.Common
{
    #region Message Classes

    /// <summary>A message</summary>
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
    #endregion
}
