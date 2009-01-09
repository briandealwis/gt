using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Common.Logging;
using GT.Utils;

namespace GT.Net
{

    public delegate void PacketHandler(byte[] buffer, int offset, int count, ITransport transport);

    /// <remarks>
    /// Represents a connection to either a server or a client.
    /// Errors should be notified by throwing an instanceof TransportError.
    /// Should the transport have been cleanly shutdown by the remote side, then
    /// throw a TransportDecomissionedException.
    /// </remarks>
    public interface ITransport : ITransportDeliveryCharacteristics, IDisposable
    {
        /// <summary>
        /// A simple identifier for this transport.  This name should uniquely identify this
        /// transport.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Is this instance active?
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// How many packets are backlogged waiting to be sent?
        /// </summary>
        uint Backlog { get; }

        event PacketHandler PacketReceivedEvent;
        event PacketHandler PacketSentEvent;

        /// <summary>
        /// A set of tags describing the capabilities of the transport and of expectations/capabilities
        /// of the users of this transport.
        /// </summary>
        IDictionary<string,string> Capabilities { get; }

        /// <summary>
        /// Send the given message to the server.
        /// </summary>
        /// <param name="packet">the packet of message(s) to send</param>
        /// <param name="offset">the offset into the packet to send</param>
        /// <param name="count">the number of bytes within the packet to send</param>
        /// <exception cref="TransportError">thrown on a fatal transport error.</exception>
        void SendPacket(byte[] packet, int offset, int count);

        /// <summary>
        /// Send the given message to the server.  The stream is sent <b>from the stream's current 
        /// position</b> to the end of the stream.  <b>It is not sent from position 0.</b></b>
        /// </summary>
        /// <param name="stream">the stream encoding the packet</param>
        /// <exception cref="TransportError">thrown on a fatal transport error.</exception>
        void SendPacket(Stream stream);

        /// <summary>
        /// Get a suitable stream for the transport.
        /// </summary>
        Stream GetPacketStream();

        /// <exception cref="TransportError">thrown on a fatal transport error.</exception>
        void Update();

        /// <summary>
        /// The maximum packet size supported by this transport instance (in bytes).
        /// </summary>
        int MaximumPacketSize { get; }
    }

    public abstract class BaseTransport : ITransport
    {
        protected ILog log;

        private Dictionary<string, string> capabilities = new Dictionary<string, string>();
        public event PacketHandler PacketReceivedEvent;
        public event PacketHandler PacketSentEvent;
        public abstract string Name { get; }
        public abstract uint Backlog { get; }
        public abstract bool Active { get; }

        protected readonly uint PacketHeaderSize;
        protected uint AveragePacketSize = 64; // guestimate on avg packet size

        protected BaseTransport(uint packetHeaderSize)
        {
            log = LogManager.GetLogger(GetType());

            PacketHeaderSize = packetHeaderSize;
        }

        virtual public void Dispose() { /* empty implementation */ }

        #region Transport Characteristics

        public abstract Reliability Reliability { get; }
        public abstract Ordering Ordering { get; }
        public abstract int MaximumPacketSize { get; }

        /// <summary>The average amount of latency between this server 
        /// and the client (in milliseconds).</summary>
        protected float delay = 20f;
        protected float delayMemory = 0.95f;
        protected StatisticalMoments delayStats = new StatisticalMoments();

        public virtual float Delay
        {
            get { return delay; }
            set {
                delayStats.Accumulate(value);
                Debug.Assert(delayMemory >= 0f && delayMemory <= 1.0f);
                delay = delayMemory * delay + (1f - delayMemory) * value; 
            }
        }

        #endregion

        public IDictionary<string, string> Capabilities
        {
            get { return capabilities; }
        }

        public abstract void SendPacket(byte[] packet, int offset, int count);
        public abstract void SendPacket(Stream packetStream);

        // Must be kept in sync with CheckValidPacketStream()
        virtual public Stream GetPacketStream()
        {
            MemoryStream ms = new MemoryStream((int)(PacketHeaderSize + AveragePacketSize));

            // Encode a known byte pattern to verify that is same stream provided to SendPacket.
            byte[] name = Encoding.UTF8.GetBytes(Name);
            for (int i = 0; i < PacketHeaderSize; i++)
            {
                ms.WriteByte(name[i % name.Length]);
            }

            Debug.Assert(ms.Position == PacketHeaderSize);
            return ms;
        }

        /// <summary>
        /// Check that this stream is a stream as obtained from <see cref="GetPacketStream"/>.
        /// </summary>
        /// <param name="stream">the stream to check</param>
        protected virtual void CheckValidPacketStream(Stream stream)
        {
            ContractViolation.Assert(stream is MemoryStream, "stream should be a MemoryStream");
            // Encode a known byte pattern to verify that is same stream provided to SendPacket.
            byte[] name = Encoding.UTF8.GetBytes(Name);
            long savedPosition = stream.Position;
            stream.Position = 0;
            for (int i = 0; i < PacketHeaderSize; i++)
            {
                ContractViolation.Assert(stream.ReadByte() == name[i % name.Length], "Stream header appears to have been overwritten");
            }
            stream.Position = savedPosition;
        }

        public abstract void Update();

        protected virtual void NotifyPacketReceived(byte[] buffer, int offset, int count)
        {
            // DebugUtils.DumpMessage(this.ToString() + " notifying of received message", buffer, offset, count);
            if (PacketReceivedEvent == null)
            {
                Debug.WriteLine(DateTime.Now + " WARNING: transport has nobody to receive incoming messages!");
                return;
            }
            PacketReceivedEvent(buffer, offset, count, this);
        }

        protected virtual void NotifyPacketSent(byte[] buffer, int offset, int count)
        {
            if (PacketSentEvent == null) { return; }
            PacketSentEvent(buffer, offset, count, this);
        }

    }
}
