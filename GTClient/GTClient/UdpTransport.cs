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
    /// varient uses a dedicated UDP socket.
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
        public UdpClientTransport(UdpClient udpc) {
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
                    Console.WriteLine(this + ": EXCEPTION thrown when terminating up socket: " + e);
                }
                udpClient = null;
            }
        }


        /// <summary> Flushes out old incomingMessages that couldn't be sent because of exceptions</summary>
        /// <returns>True if there are bytes that still have to be sent out</returns>
        protected override void FlushOutstandingPackets()
        {
            byte[] b;
            SocketError error = SocketError.Success;

            lock (this)
            {
                try
                {
                    while (outstanding.Count > 0 && udpClient.Client.Connected)
                    {
                        b = outstanding.Peek();
                        ContractViolation.Assert(b.Length > 0, "Cannot send 0-byte messages!");
                        ContractViolation.Assert(b.Length - PacketHeaderSize <= MaximumPacketSize,
                            String.Format(
                                "Packet exceeds transport capacity: {0} > {1}",
                                b.Length - PacketHeaderSize, MaximumPacketSize));

                        udpClient.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

                        switch (error)
                        {
                        case SocketError.Success:
                            outstanding.Dequeue();
                            NotifyPacketSent(b, 0, b.Length);
                            break;
                        case SocketError.WouldBlock:
                            //don't die, but try again next time
                            // FIXME: This should not be an error!
                            // FIXME: Does UDP ever cause a WouldBlock?
                            //NotifyError(null, error, "The UDP write buffer is full now, but the data will be saved and " +
                            //        "sent soon.  Send less data to reduce perceived latency.");
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
            byte[] packet;
            while ((packet = FetchIncomingPacket()) != null)
            {
                NotifyPacketReceived(packet, 0, packet.Length);
            }
        }

        virtual protected byte[] FetchIncomingPacket()
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

                        DebugUtils.DumpMessage(this + ": Update()", buffer);
                        Debug.Assert(ep.Equals(udpClient.Client.RemoteEndPoint));
                        return buffer;
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
            return null;
        }

        public override int MaximumPacketSize
        {
            get { return CappedMessageSize; }
        }

    }
}
