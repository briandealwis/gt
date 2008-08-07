using System.Collections.Generic;
using System.IO;
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

        public UdpServerTransport(UdpHandle h)
        {
            handle = h;
        }

        override public bool Active { get { return handle != null; } }

        override public int MaximumPacketSize
        {
            get { return CappedMessageSize; }
        }

        virtual public void Dispose()
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
                    Console.WriteLine("{0} Warning: exception when closing UDP handle: {1}",
                        DateTime.Now, e);
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
                    byte[] b = outstanding.Peek();
                    try
                    {
                        handle.Send(b, 0, b.Length);
                        outstanding.Dequeue();
                    }
                    catch (SocketException e)
                    {
                        switch (e.SocketErrorCode)
                        {
                        case SocketError.Success: // this can't happen, right?
                            outstanding.Dequeue();
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
                                    b.Length, e), e);
                        }
                    }
                }
            }
        }

        protected override void CheckIncomingPackets()
        {
            lock (this)
            {
                try
                {
                    //while there are more packets to read
                    while (handle.Available > 0)
                    {
                        //get a packet
                        byte[] buffer = handle.Receive();
                        NotifyPacketReceived(buffer, 0, buffer.Length);
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
        }

        public override string ToString()
        {
            return "UdpServerTransport(" + handle.RemoteEndPoint + ")";
        }
    }

    public class UdpAcceptor : BaseAcceptor
    {
        internal protected UdpMultiplexer udpMultiplexer;
        protected byte[] protocolDescriptor = ASCIIEncoding.ASCII.GetBytes("GT10");

        public UdpAcceptor(IPAddress address, int port)
            : base(address, port)
        {
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
            udpMultiplexer.SetDefaultMessageHandler(new NetPacketReceivedHandler(PreviouslyUnseenUdpEndpoint));
            udpMultiplexer.Start();
        }

        public override void Stop()
        {
            if (udpMultiplexer != null)
            {
                try { udpMultiplexer.Stop(); }
                catch (Exception e) { Console.WriteLine("Exception stopping UDP listener: " + e); }
            }
        }

        public override void Dispose()
        {
            Stop();
            try { udpMultiplexer.Dispose(); }
            catch (Exception e) { Console.WriteLine("Exception disposing UDP listener: " + e); }
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
                throw new TransportError(this, "UDP multiplexor is throwing a fit", e);
            }
        }

        public byte[] ProtocolDescriptor
        {
            get { return protocolDescriptor; }
        }

        public void PreviouslyUnseenUdpEndpoint(EndPoint ep, byte[] packet)
        {
            MemoryStream ms = null;
            // Console.WriteLine(this + ": Incoming unaddressed packet from " + ep);
            if (packet.Length < 4)
            {
                Console.WriteLine(DateTime.Now + " " + this + ": Undecipherable packet");
                ms = new MemoryStream();
                // NB: following follows the format used by the LightweightDotNetSerializingMarshaller 
                ms.WriteByte((byte)MessageType.System);
                ms.WriteByte((byte)SystemMessageType.UnknownConnexion);
                ByteUtils.EncodeLength(ProtocolDescriptor.Length, ms);
                ms.Write(ProtocolDescriptor, 0, ProtocolDescriptor.Length);
                udpMultiplexer.Send(ms.ToArray(), 0, (int)ms.Length, ep);
                return;
            }

            if (!ByteUtils.Compare(packet, 0, ProtocolDescriptor, 0, 4))
            {
                Console.WriteLine(DateTime.Now + " " + this + ": Unknown protocol version: "
                    + ByteUtils.DumpBytes(packet, 0, 4) + " [" 
                    + ByteUtils.AsPrintable(packet, 0, 4) + "]");
                ms = new MemoryStream();
                // NB: following follows the format used by the LightweightDotNetSerializingMarshaller 
                ms.WriteByte((byte)MessageType.System);
                ms.WriteByte((byte)SystemMessageType.IncompatibleVersion);
                ByteUtils.EncodeLength(ProtocolDescriptor.Length, ms);
                ms.Write(ProtocolDescriptor, 0, ProtocolDescriptor.Length);
                udpMultiplexer.Send(ms.ToArray(), 0, (int)ms.Length, ep);
                return;
            }

            ms = new MemoryStream(packet, 4, packet.Length - 4);
            Dictionary<string, string> dict = null;
            try
            {
                int count = ByteUtils.DecodeLength(ms); // we don't use it
                dict = ByteUtils.DecodeDictionary(ms);
                if (ms.Position != ms.Length)
                {
                    Console.WriteLine("{0} bytes still left at end of UDP handshake packet: ({1} vs {2})",
                        ms.Length - ms.Position, ms.Position, ms.Length);
                    byte[] rest = new byte[ms.Length - ms.Position];
                    ms.Read(rest, 0, (int)rest.Length);
                    Console.WriteLine(" " + ByteUtils.DumpBytes(rest, 0, rest.Length) + "   " +
                        ByteUtils.AsPrintable(rest, 0, rest.Length));
                }
                //Debug.Assert(ms.Position != ms.Length, "crud left at end of UDP handshake packet");
                NotifyNewClient(new UdpServerTransport(new UdpHandle(ep, udpMultiplexer)), dict);
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} {1}: Error decoding handshake from remote {2}: {3}",
                    DateTime.Now, this, ep, e);

                ms = new MemoryStream();
                // NB: following follows the format used by the LightweightDotNetSerializingMarshaller 
                ms.WriteByte((byte)MessageType.System);
                ms.WriteByte((byte)SystemMessageType.IncompatibleVersion);
                ByteUtils.EncodeLength(ProtocolDescriptor.Length, ms);
                ms.Write(ProtocolDescriptor, 0, ProtocolDescriptor.Length);
                udpMultiplexer.Send(ms.ToArray(), 0, (int)ms.Length, ep);
                return;
            }
        }

        public override string ToString()
        {
            return "UdpAcceptor(" + udpMultiplexer.LocalEndPoint + ")";
        }
    }
}
