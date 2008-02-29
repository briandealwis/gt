using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using GT.Common;

namespace GT.Servers
{
    public delegate void NetPacketReceivedHandler(EndPoint ep, byte[] message);

    public class UdpMultiplexer : IStartable
    {
        protected readonly IPAddress address;
        protected readonly int port;
        protected UdpClient udpClient;
        protected Dictionary<EndPoint,NetPacketReceivedHandler> handlers = 
            new Dictionary<EndPoint,NetPacketReceivedHandler>();
        protected NetPacketReceivedHandler defaultHandler;

        // theoretical maximum is 65535 bytes; practical limit is 65507
        // <http://en.wikipedia.org/wiki/User_Datagram_Protocol>
        protected byte[] buffer = new byte[65535];

        public UdpMultiplexer(IPAddress address, int port)
        {
            this.address = address;
            this.port = port;
        }

        public int MaximumPacketSize {
            get { return udpClient.Client.SendBufferSize; }
        }

        public IPEndPoint LocalEndPoint
        {
            get { return new IPEndPoint(address, port); }
        }
        
        public bool Started
        {
            get { return udpClient != null; }
        }

        public void Start()
        {
            udpClient = new UdpClient(new IPEndPoint(address, port));
            udpClient.Client.Blocking = false;
            // udp sockets don't support LingerState/SO_LINGER
        }

        public void Stop()
        {
            if (udpClient == null) { return; }
            udpClient.Close();
            udpClient = null;
        }

        public void Dispose()
        {
            defaultHandler = null;
            handlers = null;
            udpClient = null;
        }

        public void SetDefaultMessageHandler(NetPacketReceivedHandler handler)
        {
            defaultHandler = handler;
        }

        public NetPacketReceivedHandler RemoveDefaultMessageHandler() {
            NetPacketReceivedHandler old = defaultHandler;
            defaultHandler = null;
            return old;
        }

        public void SetMessageHandler(EndPoint ep, NetPacketReceivedHandler handler)
        {
            handlers[ep] = handler;
        }

        public NetPacketReceivedHandler RemoveMessageHandler(EndPoint ep) {
            NetPacketReceivedHandler hdl;
            if (handlers == null || !handlers.TryGetValue(ep, out hdl)) { return null; }
            handlers.Remove(ep);
            return hdl;
        }

        public void Update()
        {
            while (udpClient.Available > 0)
            {
                EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    int rc = udpClient.Client.ReceiveFrom(buffer, ref remote);
                    NetPacketReceivedHandler h;
                    Console.WriteLine(this + ": received " + rc + " bytes from " + remote);
                    DebugUtils.DumpMessage("UDP received", buffer);
                    if (!handlers.TryGetValue(remote, out h) || h == null)
                    {
                        h = defaultHandler;
                        if (h == null)
                        {
                            /* FIXME: do something! */
                            Console.WriteLine(this + ": WARNING: no handler available for " + remote);
                            continue;
                        }
                        else
                        {
                            Console.WriteLine(this + ": no handler found; using default");
                        }
                    }
                    else
                    {
                        Console.WriteLine(this + ": found handler: " + h);
                    }
                    h.Invoke(remote, new MemoryStream(buffer, 0, rc).ToArray());
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode != SocketError.WouldBlock) { throw e; }
                }
            }
        }

        public int Send(byte[] buffer, int offset, int length, EndPoint remote, out SocketError error)
        {
            error = SocketError.Success;
            try
            {
                return udpClient.Client.SendTo(buffer, offset, length, SocketFlags.None, remote);
            }
            catch (SocketException e)
            {
                error = e.SocketErrorCode;
                return 0;
            }
        }

    }

    public class UdpHandle : IDisposable
    {
        protected EndPoint remote;
        protected UdpMultiplexer mux;
        protected Stopwatch lastMessage;
        protected List<byte[]> messages;

        public UdpHandle(EndPoint ep, UdpMultiplexer udpMux)
        {
            remote = ep;
            mux = udpMux;
            lastMessage = new Stopwatch();
            messages = new List<byte[]>();

            mux.SetMessageHandler(ep, new NetPacketReceivedHandler(ReceivedMessage));
        }

        override public string ToString()
        {
            return "UDP[" + RemoteEndPoint + "]";
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return (IPEndPoint)remote; }
        }

        public int MaximumPacketSize
        {
            get { return mux.MaximumPacketSize; }
        }

        public void Dispose()
        {
            mux.RemoveMessageHandler(remote);
            messages = null;
        }

        protected void ReceivedMessage(EndPoint ep, byte[] message)
        {
            lastMessage.Reset();
            messages.Add(message);
        }

        public void Send(byte[] buffer, int offset, int length, out SocketError error)
        {
            mux.Send(buffer, offset, length, remote, out error);
        }

        public int Available { get { return messages.Count; } }

        public byte[] Receive()
        {
            byte[] msg = messages[0];
            messages.RemoveAt(0);
            return msg;
        }

    }

}
