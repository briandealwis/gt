using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace GT.Net
{

    public delegate void PacketReceivedHandler(byte[] buffer, int offset, int count, ITransport transport);

    /// <remarks>
    /// Represents a connection to either a server or a client.
    /// </remarks>
    public interface ITransport : IDisposable
    {
        /// <summary>
        /// A simple identifier for this transport
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Is this instance active?
        /// </summary>
        bool Active { get; }

        event PacketReceivedHandler PacketReceivedEvent;

        /* FIXME: Stop-gap solution until we have proper QoS descriptors */
        MessageProtocol MessageProtocol { get; }

        float Delay { get; set; }

        /// <summary>
        /// A set of tags describing the capabilities of the transport and of expectations/capabilities
        /// of the users of this transport.
        /// </summary>
        Dictionary<string,string> Capabilities { get; }

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
        private Dictionary<string, string> capabilities = new Dictionary<string, string>();
        public event ErrorEventHandler ErrorEvent;
        public event PacketReceivedHandler PacketReceivedEvent;
        public abstract string Name { get; }
        public abstract MessageProtocol MessageProtocol { get; }    // FIXME: temporary

        public abstract bool Active { get; }

        public void Dispose() { /* empty implementation */ }

        /// <summary>The average amount of latency between this server 
        /// and the client (in milliseconds).</summary>
        public float delay = 20f;

        public virtual float Delay
        {
            get { return delay; }
            set { delay = 0.95f * delay + 0.05f * delay; }
        }

        public Dictionary<string, string> Capabilities
        {
            get { return capabilities; }
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

        protected void NotifyError(Exception e, SocketError se, string explanation)
        {
            //FIXME: server.NotifyError(this, e, se, explanation);
            Console.WriteLine("{0} {1}: Error Occurred: {2}",
                DateTime.Now, this, explanation);
            if (e != null) { Console.WriteLine("  exception={0}", e.ToString()); }
            if (se != null) { Console.WriteLine("  socket error={1}", se.ToString()); }
        }
    }

}
