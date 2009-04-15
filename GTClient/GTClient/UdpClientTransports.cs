using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using GT.Net;
using System.IO;
using System.Diagnostics;
using System.Text;
using GT.Utils;

namespace GT.Net
{
    /// <summary>
    /// The client varient of <see cref="BaseUdpTransport"/>.  This
    /// varient uses a dedicated UDP socket.  This implementation uses
    /// the raw UDP protocol with no ordering or reliability enhancements.
    /// </summary>
    public class UdpClientTransport : BaseUdpTransport
    {
        /// <summary>
        /// The UDP socket instance.
        /// </summary>
        protected UdpClient udpClient;

        /// <summary>
        /// Create a new instance on the provided socket.
        /// </summary>
        /// <param name="udpc">the UDP socket to use</param>
        public UdpClientTransport(UdpClient udpc) 
            : base(0)   // GT UDP 1.0 doesn't need a packet length
        {
            udpClient = udpc;
        }

        /// <summary>
        /// Constructor provided for subclasses that may have a different PacketHeaderSize
        /// </summary>
        /// <param name="packetHeaderSize"></param>
        /// <param name="udpc"></param>
        protected UdpClientTransport(uint packetHeaderSize, UdpClient udpc)
            : base(packetHeaderSize)
        {
            udpClient = udpc;
        }

        public override bool Active
        {
            get { return udpClient != null; }
        }

        override public void Dispose() {
            if (!Active) { return; }
            //kill the connexion as best we can
            lock (this)
            {
                try
                {
                    // udpClient.Client.LingerState.Enabled = false; // FIXME: verify not supported on UDP
                    if (udpClient != null) { udpClient.Close(); }
                }
                catch (SocketException e)
                {
                    // FIXME: logError(INFORM, "exception thrown when terminating socket", e);
                    log.Info(this + ": exception thrown when terminating up socket", e);
                }
                udpClient = null;
            }
        }


        /// <summary> Flushes out old incomingMessages that couldn't be sent because of exceptions</summary>
        /// <returns>True if there are bytes that still have to be sent out</returns>
        protected override void FlushOutstandingPackets()
        {
            lock (this)
            {
                try
                {
                    while (outstanding.Count > 0 && udpClient.Client.Connected)
                    {
                        TransportPacket packet = outstanding.Peek();

                        ContractViolation.Assert(packet.Length > 0, "Cannot send 0-byte messages!");
                        ContractViolation.Assert(packet.Length - PacketHeaderSize <= MaximumPacketSize,
                            String.Format("Packet exceeds transport capacity: {0} > {1}",
                                packet.Length - PacketHeaderSize, MaximumPacketSize));

                        SocketError error;
                        udpClient.Client.Send(packet, SocketFlags.None, out error);

                        switch (error)
                        {
                        case SocketError.Success:
                            outstanding.Dequeue();
                            NotifyPacketSent(packet);
                            break;
                        case SocketError.WouldBlock:
                            // FIXME: Does UDP ever cause a WouldBlock?
                            NotifyError(new ErrorSummary(Severity.Information,
                                SummaryErrorCode.TransportBacklogged,
                                "Transport backlogged", this, null));
                            return;

                        default:
                            //something terrible happened, but this is only UDP, so stick around.
                            throw new TransportError(this, "Error sending UDP packet", error);
                        }
                    }
                }
                catch (SocketException e)
                {
                    throw new TransportError(this, "Error sending UDP packet", e);
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
                    if (udpClient.Client.Available > 0)
                    {
                        IPEndPoint ep = null;
                        byte[] buffer = udpClient.Receive(ref ep);

                        Debug.Assert(ep.Equals(udpClient.Client.RemoteEndPoint));
                        NotifyPacketReceived(new TransportPacket(buffer));
                        return true;
                    }
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode != SocketError.WouldBlock)
                    {
                        throw new TransportError(this, "Error sending UDP packet", e);
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            if (udpClient != null)
            {
                try
                {
                    return String.Format("{0}: {1} -> {2}", Name, 
                        udpClient.Client.LocalEndPoint, udpClient.Client.RemoteEndPoint);
                }
                catch (SocketException) { /* FALLTHROUGH */ }
            }
            return String.Format("{0}: disconnected?", Name);
        }
    }

    /// <summary>
    /// This UDP client implementation adds sequencing capabilities to the
    /// the raw UDP protocol to ensure that packets are received in-order,
    /// but with no guarantee on the reliability of packet delivery.
    /// </summary>
    public class UdpSequencedClientTransport : UdpClientTransport 
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

        public UdpSequencedClientTransport(UdpClient udpc)
            : base(4, udpc) // we use the first four bytes to encode the sequence 
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

}
