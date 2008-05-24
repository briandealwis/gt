using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using GT.Utils;

namespace GT.Net
{

    public delegate void PacketReceivedHandler(byte[] buffer, int offset, int count, ITransport transport);

    /// <remarks>
    /// Represents a connection to either a server or a client.
    /// Errors should be notified by throwing an instanceof FatalTransportError.
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

        event PacketReceivedHandler PacketReceivedEvent;

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
        /// <exception cref="FatalTransportError">thrown on a fatal transport error.</exception>
        /// <exception cref="TransportDecomissionedException">thrown should the transport be decommissioned.</exception>
        void SendPacket(byte[] packet, int offset, int count);

        /// <summary>
        /// Send the given message to the server.  The stream is sent <b>from the stream's current 
        /// position</b> to the end of the stream.  <b>It is not sent from position 0.</b></b>
        /// </summary>
        /// <param name="stream">the stream encoding the packet</param>
        /// <exception cref="FatalTransportError">thrown on a fatal transport error.</exception>
        /// <exception cref="TransportDecomissionedException">thrown should the transport be decommissioned.</exception>
        void SendPacket(Stream stream);

        /// <summary>
        /// Get a suitable stream for the transport.
        /// </summary>
        Stream GetPacketStream();

        /// <exception cref="FatalTransportError">thrown on a fatal transport error.</exception>
        /// <exception cref="TransportDecomissionedException">thrown should the transport be decommissioned.</exception>
        void Update();

        int MaximumPacketSize { get; }
    }

    public abstract class BaseTransport : ITransport
    {
        private Dictionary<string, string> capabilities = new Dictionary<string, string>();
        public event PacketReceivedHandler PacketReceivedEvent;
        public abstract string Name { get; }

        public abstract bool Active { get; }

        virtual public void Dispose() { /* empty implementation */ }

        #region Transport Characteristics

        public abstract Reliability Reliability { get; }
        public abstract Ordering Ordering { get; }

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

        public Dictionary<string, string> Capabilities
        {
            get { return capabilities; }
        }

        public abstract void SendPacket(byte[] packet, int offset, int count);
        public abstract void SendPacket(Stream packetStream);

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
