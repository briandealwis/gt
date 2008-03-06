using System.Diagnostics;
using System;
using System.IO;
namespace GT
{

    public delegate void PacketReceivedHandler(byte[] buffer, int offset, int count, ITransport transport);

    /// <remarks>
    /// Represents a connection to either a server or a client.
    /// </remarks>
    public interface ITransport
    {
        /// <summary>
        /// A simple identifier for this transport
        /// </summary>
        string Name { get; }

        event PacketReceivedHandler PacketReceivedEvent;

        /* FIXME: Stop-gap solution until we have proper QoS descriptors */
        MessageProtocol MessageProtocol { get; }

        float Delay { get; set; }

        /// <summary>
        /// Send the given message to the server.
        /// </summary>
        /// <param name="packet">the packet of message(s) to send</param>
        /// <param name="offset">the offset into the packet to send</param>
        /// <param name="length">the number of bytes within the packet to send</param>
        void SendPacket(byte[] packet, int offset, int count);

        /// <summary>
        /// Send the given message to the server.  The stream is sent <b>from the stream's current 
        /// position</b> to the end of the stream.  <b>It is not sent from position 0.</b></b>
        /// </summary>
        /// <param name="stream">the stream encoding the packet</param>
        void SendPacket(Stream stream);

        /// <summary>
        /// Get a suitable stream for the transport.
        /// </summary>
        Stream GetPacketStream();

        void Update();

        int MaximumPacketSize { get; }
    }

    public abstract class BaseTransport : ITransport
    {
        public event PacketReceivedHandler PacketReceivedEvent;
        public abstract string Name { get; }

        public abstract MessageProtocol MessageProtocol { get; }

        /// <summary>The average amount of latency between this server 
        /// and the client (in milliseconds).</summary>
        public float delay = 20f;

        public virtual float Delay
        {
            get { return delay; }
            set { delay = 0.95f * delay + 0.05f * delay; }
        }

        public abstract void SendPacket(byte[] message, int offset, int count);
        public abstract void SendPacket(Stream p);

        protected int PacketHeaderSize = 0;
        protected int AveragePacketSize = 64; // guestimate on avg packet size

        virtual public Stream GetPacketStream()
        {
            MemoryStream ms = new MemoryStream(PacketHeaderSize + AveragePacketSize);
            ms.Write(new byte[PacketHeaderSize], 0, PacketHeaderSize);
            return ms;
        }

        public abstract void Update();
        public abstract int MaximumPacketSize { get; }

        protected void NotifyPacketReceived(byte[] buffer, int offset, int count)
        {
            DebugUtils.DumpMessage(this.ToString() + " notifying of received message", buffer, offset, count);
            if (PacketReceivedEvent == null)
            {
                Debug.WriteLine(DateTime.Now + " ERROR: transport has nobody to receive incoming messages!");
                return;
            }
            PacketReceivedEvent(buffer, offset, count, this);
        }
    }

}
