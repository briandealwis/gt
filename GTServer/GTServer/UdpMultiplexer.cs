using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Common.Logging;
using GT.Net;
using GT.Utils;

namespace GT.Net
{
    public delegate void NetPacketReceivedHandler(EndPoint ep, byte[] message);

    public class UdpMultiplexer : IStartable
    {
        protected ILog log;
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
            log = LogManager.GetLogger(GetType());

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
        
        public bool Active
        {
            get { return udpClient != null; }
        }

        public void Start()
        {
            udpClient = new UdpClient(new IPEndPoint(address, port));
            udpClient.Client.Blocking = false;
            DisableUdpConnectionResetBehaviour();
        }

        /// <summary>Hack to avoid the ConnectionReset/ECONNRESET problem described
        /// in <a href="https://papyrus.usask.ca/trac/gt/ticket/41">bug 41</a>.</summary>
        private void DisableUdpConnectionResetBehaviour()
        {
            /// Code from http://www.devnewsgroups.net/group/microsoft.public.dotnet.framework/topic47566.aspx
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                byte[] inValue = new byte[4];   // zeroes = false
                udpClient.Client.IOControl(SIO_UDP_CONNRESET, inValue, null);
                log.Info("INFO: installed SIO_UDP_CONNRESET hack for UdpMultiplexer");
            }
            catch (Exception e) {
                log.Info("INFO: unable to install SIO_UDP_CONNRESET hack for UdpMultiplexer: {0}", e);
            }
        }

        public void Stop()
        {
            if (udpClient == null) { return; }
            udpClient.Close();
            udpClient = null;
        }

        public void Dispose()
        {
            Stop();
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

        /// <summary>
        /// Process any incoming messages from the UDP socket.
        /// </summary>
        /// <exception cref="SocketException">thrown if there is a socket error</exception>
        public void Update()
        {
            while (udpClient.Available > 0)
            {
                EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                // any SocketExceptions will be caught by callers
                int rc = udpClient.Client.ReceiveFrom(buffer, ref remote);
                // log.Debug(String.Format("{0}: received {1} bytes from {2}", this, rc, remote));
                NetPacketReceivedHandler h;
                if (!handlers.TryGetValue(remote, out h) || h == null)
                {
                    h = defaultHandler;
                    if (h == null)
                    {
                        log.Warn(String.Format("{0}: WARNING: no default handler for {1}: ignoring incoming packet", this, remote));
                        continue;
                    }
                    if (log.IsTraceEnabled)
                    {
                        log.Trace(String.Format("{0}: no handler found for {1}; using default handler",
                                this, remote));
                    }
                }
                else
                {
                    if (log.IsTraceEnabled)
                    {
                        log.Trace(String.Format("{0}: found handler: {1}", this, h));
                    }
                }
                h.Invoke(remote, new MemoryStream(buffer, 0, rc).ToArray());
            }
        }

        /// <summary>
        /// Send a packet on the UDP socket.
        /// </summary>
        /// <exception cref="SocketException">thrown if there is a socket error</exception>
        public int Send(byte[] buffer, int offset, int length, EndPoint remote)
        {
            return udpClient.Client.SendTo(buffer, offset, length, SocketFlags.None, remote);
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

        /// <summary>
        /// Send a packet on the UDP socket.
        /// </summary>
        /// <exception cref="SocketException">thrown if there is a socket error</exception>
        public void Send(byte[] buffer, int offset, int length)
        {
            mux.Send(buffer, offset, length, remote);
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
