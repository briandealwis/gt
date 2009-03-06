using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Net.Sockets;
using System;
using System.Net;
using System.Text;
using System.Diagnostics;
using GT.Utils;

namespace GT.Net
{
    public class UdpServerTransport : BaseUdpTransport
    {
        private UdpHandle handle;

        /// <summary>
        /// Create a new instance on the provided socket.
        /// </summary>
        /// <param name="h">the UDP handle to use</param>
        public UdpServerTransport(UdpHandle h)
            : base(0)   // GT UDP 1.0 doesn't need a packet length
        {
            handle = h;
        }

    
        /// <summary>
        /// Constructor provided for subclasses that may have a different PacketHeaderSize
        /// </summary>
        /// <param name="packetHeaderSize"></param>
        /// <param name="h"></param>
        protected UdpServerTransport(uint packetHeaderSize, UdpHandle h)
            : base(packetHeaderSize)
        {
            handle = h;
        }

        override public bool Active { get { return handle != null; } }

        public override void Dispose()
        {
            lock (this)
            {
                try
                {
                    if (handle != null)
                    {
                        handle.Dispose();
                    }
                }
                catch (Exception e)
                {
                    log.Info("exception when closing UDP handle", e);
                }
                handle = null;
            }
        }

        protected override void FlushOutstandingPackets()
        {
            lock (this)
            {
                while (outstanding.Count > 0)
                {
                    TransportPacket packet = outstanding.Peek();
                    try
                    {
                        handle.Send(packet);
                    }
                    catch (SocketException e)
                    {
                        switch (e.SocketErrorCode)
                        {
                        case SocketError.Success: // this can't happen, right?
                            break;
                        case SocketError.WouldBlock:
                            //don't die, but try again next time; not clear if this does (can) happen with UDP
                            // NotifyError(null, error, this, "The UDP write buffer is full now, but the data will be saved and " +
                            ///    "sent soon.  Send less data to reduce perceived latency.");
                            return;
                        default:
                            //something terrible happened, but this is only UDP, so stick around.
                            throw new TransportError(this,
                                String.Format("Error sending UDP message ({0} bytes): {1}",
                                    packet.Length, e), e);
                        }
                    }
                    outstanding.Dequeue();
                    NotifyPacketSent(packet);
                }
            }
        }

        protected override void CheckIncomingPackets()
        {
            while (FetchIncomingPacket()) { /* do nothing */ }
        }

        virtual protected bool FetchIncomingPacket()
        {
            lock (this)
            {
                try
                {
                    //while there are more packets to read
                    while (handle.Available > 0)
                    {
                        // get a packet
                        TransportPacket packet = handle.Receive();
                        NotifyPacketReceived(packet);
                        return true;
                    }
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode != SocketError.WouldBlock)
                    {
                        throw new TransportError(this,
                            String.Format("Error reading UDP message: {0}",
                                e.SocketErrorCode), e);
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            if (handle != null)
            {
                try
                {
                    return String.Format("{0}: {1}", Name, handle.RemoteEndPoint);
                }
                catch (SocketException) { /* FALLTHROUGH */ }
            }
            return String.Format("{0}", Name);
        }
    }

    /// <summary>
    /// This UDP client implementation adds sequencing capabilities to the
    /// the raw UDP protocol to ensure that packets are received in-order,
    /// but with no guarantee on the reliability of packet delivery.
    /// </summary>
    public class UdpSequencedServerTransport : UdpServerTransport
    {
        public override Ordering Ordering { get { return Ordering.Sequenced; } }

        /// <summary>
        /// The sequence number expected for the next packet received.
        /// </summary>
        protected uint nextIncomingPacketSeqNo = 0;

        /// <summary>
        /// The sequence number for the next outgoing packet.
        /// </summary>
        protected uint nextOutgoingPacketSeqNo = 0;

        public UdpSequencedServerTransport(UdpHandle h)
            : base(4, h) // we use the first four bytes to encode the sequence 
        {
        }

        protected override void NotifyPacketReceived(TransportPacket packet)
        {
            if (packet.Length < PacketHeaderSize)
            {
                throw new TransportError(this,
                    "should not receive datagrams whose size is less than PacketHeaderSize bytes", packet);
            }
            uint packetSeqNo = 0;
            packet.BytesAt(0, 4, (b,offset) => packetSeqNo = BitConverter.ToUInt32(b, offset));
            packet.RemoveBytes(0, 4);

            // We handle wrap around by checking if the difference between the
            // packet-seqno and the expected next packet-seqno > uint.MaxValue / 2
            // After all, it's unlikely that 2 billion packets will mysteriously disappear!
            if (packetSeqNo < nextIncomingPacketSeqNo
                && nextIncomingPacketSeqNo - packetSeqNo < uint.MaxValue / 2) { return; }
            nextIncomingPacketSeqNo = packetSeqNo + 1;

            // pass it on
            base.NotifyPacketReceived(packet);
        }

        protected override void WritePacketHeader(TransportPacket packet)
        {
            packet.Prepend(BitConverter.GetBytes(nextOutgoingPacketSeqNo++));
        }
    }

    /// <summary>
    /// An acceptor for incoming UDP connections.
    /// </summary>
    /// <remarks>
    /// The use of <see cref="TransportFactory{T}"/> may seem to be a bit complicated,
    /// but it greatly simplifies testing.
    /// </remarks>
    public class UdpAcceptor : IPBasedAcceptor
    {
        protected TransportFactory<UdpHandle> factory;
        protected UdpMultiplexer udpMultiplexer;

        /// <summary>
        /// Create an acceptor to accept incoming UDP connections, with no guarantees
        /// on ordering or reliability
        /// </summary>
        /// <param name="address">the local address on which to wait; usually
        ///     <see cref="IPAddress.Any"/></param>
        /// <param name="port">the local port on which to wait</param>
        public UdpAcceptor(IPAddress address, int port)
            : this(address, port, Ordering.Unordered) {}

        /// <summary>
        /// Create an acceptor to accept incoming UDP connections satisfying the
        /// given ordering requirements.
        /// </summary>
        /// <param name="address">the local address on which to wait; usually
        ///     <see cref="IPAddress.Any"/></param>
        /// <param name="port">the local port on which to wait</param>
        /// <param name="ordering">the expected ordering to support</param>
        public UdpAcceptor(IPAddress address, int port, Ordering ordering)
            : base(address, port)
        {
            switch (ordering)
            {
                case Ordering.Unordered:
                    factory = new TransportFactory<UdpHandle>(
                        BaseUdpTransport.UnorderedProtocolDescriptor,
                        h => new UdpServerTransport(h),
                        t => t is UdpServerTransport);
                    return;
                case Ordering.Sequenced:
                    factory = new TransportFactory<UdpHandle>(
                        BaseUdpTransport.SequencedProtocolDescriptor,
                        h => new UdpSequencedServerTransport(h),
                        t => t is UdpSequencedServerTransport);
                    return;
                default: throw new InvalidOperationException("Unsupported ordering type: " + ordering);
            }
        }

        /// <summary>
        /// A constructor more intended for testing purposes
        /// </summary>
        /// <param name="address">the local address on which to wait; usually
        ///     <see cref="IPAddress.Any"/></param>
        /// <param name="port">the local port on which to wait</param>
        /// <param name="factory">the factory responsible for creating an appropriate
        ///     <see cref="ITransport"/> instance</param>
        public UdpAcceptor(IPAddress address, int port, TransportFactory<UdpHandle> factory)
            : base(address, port)
        {
            this.factory = factory;
        }

        #region IStartable

        public override bool Active
        {
            get { return udpMultiplexer != null && udpMultiplexer.Active; }
        }

        public override void Start()
        {
            if (Active) { return; }
            if (udpMultiplexer == null) { udpMultiplexer = new UdpMultiplexer(address, port); }
            udpMultiplexer.SetDefaultMessageHandler(PreviouslyUnseenUdpEndpoint);
            udpMultiplexer.Start();
        }

        public override void Stop()
        {
            if (udpMultiplexer != null)
            {
                try { udpMultiplexer.Stop(); }
                catch (Exception e) { log.Warn("exception stopping UDP listener", e); }
            }
        }

        public override void Dispose()
        {
            Stop();
            try { udpMultiplexer.Dispose(); }
            catch (Exception e) { log.Warn("exception disposing UDP listener", e); }
            udpMultiplexer = null;
        }
        #endregion

        public override void Update()
        {
            try
            {
                lock (this)
                {
                    udpMultiplexer.Update();
                }
            }
            catch (SocketException e)
            {
                throw new TransportError(this, "Exception raised by UDP multiplexor", e);
            }
        }

        protected byte[] ProtocolDescriptor
        {
            get { return factory.ProtocolDescriptor; }
        }

        protected void PreviouslyUnseenUdpEndpoint(EndPoint ep, TransportPacket packet)
        {
            TransportPacket response;
            Stream ms;

            // Console.WriteLine(this + ": Incoming unaddressed packet from " + ep);
            if (packet.Length < ProtocolDescriptor.Length)
            {
                response = new TransportPacket();
                ms = response.AsWriteStream();
                log.Info("Undecipherable packet (ignored)");
                // NB: following follows the format used by the LightweightDotNetSerializingMarshaller 
                ms.WriteByte((byte)MessageType.System);
                ms.WriteByte((byte)SystemMessageType.UnknownConnexion);
                ByteUtils.EncodeLength(ProtocolDescriptor.Length, ms);
                ms.Write(ProtocolDescriptor, 0, ProtocolDescriptor.Length);
                ms.Flush();
                udpMultiplexer.Send(response, ep);
                return;
            }

            if (!ByteUtils.Compare(packet.ToArray(0, ProtocolDescriptor.Length), ProtocolDescriptor))
            {
                response = new TransportPacket();
                ms = response.AsWriteStream(); 
                log.Info("Unknown protocol version: "
                    + ByteUtils.DumpBytes(packet.ToArray(), 0, 4) + " [" 
                    + ByteUtils.AsPrintable(packet.ToArray(), 0, 4) + "]");
                // NB: following follows the format used by the LightweightDotNetSerializingMarshaller 
                ms.WriteByte((byte)MessageType.System);
                ms.WriteByte((byte)SystemMessageType.IncompatibleVersion);
                ByteUtils.EncodeLength(ProtocolDescriptor.Length, ms);
                ms.Write(ProtocolDescriptor, 0, ProtocolDescriptor.Length);
                ms.Flush();
                udpMultiplexer.Send(response, ep);
                return;
            }

            packet.RemoveBytes(0, ProtocolDescriptor.Length);
            ms = packet.AsReadStream();
            Dictionary<string, string> dict = null;
            try
            {
                int count = ByteUtils.DecodeLength(ms); // we don't use it
                dict = ByteUtils.DecodeDictionary(ms);
                if (ms.Position != ms.Length)
                {
                    byte[] rest = packet.ToArray();
                    log.Info(String.Format("{0} bytes still left at end of UDP handshake packet: {1} ({2})",
                        rest.Length, ByteUtils.DumpBytes(rest, 0, rest.Length),
                        ByteUtils.AsPrintable(rest, 0, rest.Length)));
                }
                //Debug.Assert(ms.Position != ms.Length, "crud left at end of UDP handshake packet");
                NotifyNewClient(factory.CreateTransport(new UdpHandle(ep, udpMultiplexer)), dict);
            }
            catch (Exception e)
            {
                log.Warn(String.Format("Error decoding handshake from remote {0}", ep), e);

                response = new TransportPacket();
                ms = response.AsWriteStream();
                // NB: following follows the format used by the LightweightDotNetSerializingMarshaller 
                ms.WriteByte((byte)MessageType.System);
                ms.WriteByte((byte)SystemMessageType.IncompatibleVersion);
                ByteUtils.EncodeLength(ProtocolDescriptor.Length, ms);
                ms.Write(ProtocolDescriptor, 0, ProtocolDescriptor.Length);
                ms.Flush();
                udpMultiplexer.Send(response, ep);
                return;
            }
        }
    }
}
