using GT;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System;
using System.Net;

namespace GT
{
    public class UdpServerTransport : BaseServerTransport
    {
        /// <summary>
        /// Allow setting a cap on the maximum UDP message size
        /// as compared to the OS value normally used.
        /// 512 is the historical value supported by GT.
        /// </summary>
        public static int CappedMessageSize = 512;

        private UdpHandle handle;
        private List<byte[]> outstanding;
        private MemoryStream udpIn;

        public UdpServerTransport(UdpHandle h)
        {
            outstanding = new List<byte[]>();
            handle = h;
        }

        override public string Name
        {
            get { return "TCP"; }
        }

        override public MessageProtocol MessageProtocol
        {
            get { return MessageProtocol.Udp; }
        }

        override public bool Dead { get { return handle == null; } }

        override public int MaximumPacketSize
        {
            get
            {
                try
                {
                    return Math.Min(CappedMessageSize, handle.MaximumPacketSize);
                }
                catch (Exception)
                {
                    return CappedMessageSize;
                }
            }
        }

        override public void Dispose()
        {
            try
            {
                if (handle != null) { handle.Dispose(); }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Warning: exception when closing UDP handle: {1}",
                    DateTime.Now, e);
            }
            handle = null;
        }

        /// <summary>Sends a data via UDP.
        /// We don't care if it doesn't get through.</summary>
        /// <param name="buffer">Raw stuff to send.</param>
        override public void SendPacket(byte[] buffer, int offset, int length)
        {
            if (Dead) { throw new InvalidStateException("Cannot send: instance is dead"); }
            DebugUtils.DumpMessage(this + "SendPacket", buffer);
            if (offset != 0 || length != buffer.Length)
            {
                byte[] newBuffer = new byte[length];
                Array.Copy(buffer, offset, newBuffer, 0, length);
                buffer = newBuffer;
            }
            outstanding.Add(buffer);
            FlushOutstandingPackets();
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(Stream output)
        {
            if (Dead) { throw new InvalidStateException("Cannot send on a stopped client", this); }
            if (!(output is MemoryStream))
            {
                throw new ArgumentException("Transport provided different stream!");
            }
            outstanding.Add(((MemoryStream)output).ToArray());
            FlushOutstandingPackets();
        }

        virtual protected void FlushOutstandingPackets()
        {
            SocketError error = SocketError.Success;
            while (outstanding.Count > 0)
            {
                byte[] b = outstanding[0];
                handle.Send(b, 0, b.Length, out error);

                switch (error)
                {
                case SocketError.Success:
                    outstanding.RemoveAt(0);
                    break;
                case SocketError.WouldBlock:
                    //don't die, but try again next time
                    LastError = null;
                    NotifyError(null, error, this, "The UDP write buffer is full now, but the data will be saved and " +
                        "sent soon.  Send less data to reduce perceived latency.");
                    return;
                default:
                    //something terrible happened, but this is only UDP, so stick around.
                    LastError = null;
                    NotifyError(null, error, this, "Failed to Send UDP Message (" + b.Length + " bytes): " + b.ToString());
                    return;
                }
            }
        }

        /// <summary>Gets available data from UDP.</summary>
        override public void Update()
        {
            byte[] buffer, data;
            int length, cursor;
            byte id, type;

            if (Dead) { throw new InvalidStateException("Cannot send: is dead"); }

            CheckIncomingPackets();
            FlushOutstandingPackets();
        }

        virtual protected void CheckIncomingPackets()
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
                if (e.SocketErrorCode == SocketError.WouldBlock)
                {
                    LastError = e;
                    NotifyError(e, SocketError.NoRecovery, this, "Updating from UDP connection failed because of an exception");
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

        public UdpAcceptor(IPAddress address, int port)
            : base(address, port)
        {
        }

        #region IStartable

        public override bool Started
        {
            get { return udpMultiplexer != null && udpMultiplexer.Started; }
        }
        public override void Start()
        {
            if (Started) { return; }
            udpMultiplexer = new UdpMultiplexer(address, port);
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
            udpMultiplexer.Update();
        }


        public void PreviouslyUnseenUdpEndpoint(EndPoint ep, byte[] packet)
        {
            Console.WriteLine(this + ": Incoming unaddressed packet from " + ep);
            if (packet.Length < 5 || packet[0] != '?')
            {
                Console.WriteLine(this + ": UDP: Undecipherable packet");
                return;
            }
            int clientId = BitConverter.ToInt32(packet, 1);
            NotifyNewClient(new UdpServerTransport(new UdpHandle(ep, udpMultiplexer)), clientId);
        }

        public override string ToString()
        {
            return "UdpAcceptor(" + udpMultiplexer.LocalEndPoint + ")";
        }
    }
}
