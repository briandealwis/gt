using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

namespace GTServer
{
    public delegate void MessageReceivedHandler(EndPoint ep, byte[] message);

    public class UdpMultiplexer : IDisposable
    {
        protected readonly int port;
        protected UdpClient udpClient;
        protected Dictionary<EndPoint,MessageReceivedHandler> handlers = 
            new Dictionary<EndPoint,MessageReceivedHandler>();
        protected MessageReceivedHandler defaultHandler;

        // theoretical maximum is 65535 bytes; practical limit is 65507
        // <http://en.wikipedia.org/wiki/User_Datagram_Protocol>
        protected byte[] buffer = new byte[65535];

        public UdpMultiplexer(int port)
        {
            this.port = port;
        }

        public void Start()
        {
            udpClient = new UdpClient(port);
            udpClient.Client.Blocking = false;
            // udp sockets don't support LingerState/SO_LINGER
        }

        public void SetDefaultMessageHandler(MessageReceivedHandler handler)
        {
            defaultHandler = handler;
        }

        public MessageReceivedHandler RemoveDefaultMessageHandler() {
            MessageReceivedHandler old = defaultHandler;
            defaultHandler = null;
            return old;
        }

        public void SetMessageHandler(EndPoint ep, MessageReceivedHandler handler)
        {
            handlers[ep] = handler;
        }

        public MessageReceivedHandler RemoveMessageHandler(EndPoint ep) {
            MessageReceivedHandler hdl;
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
                    MessageReceivedHandler h;
                    Console.WriteLine(this + ": received " + rc + " bytes from " + remote);
                    Server.DumpMessage("UDP received", buffer);
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

    }

    public class UdpHandle : IDisposable
    {
        protected EndPoint remote;
        protected UdpMultiplexer mux;
        protected Server.Client client;
        protected Stopwatch lastMessage;
        protected List<byte[]> messages;

        public UdpHandle(EndPoint ep, UdpMultiplexer udpMux, Server.Client cli)
        {
            remote = ep;
            mux = udpMux;
            client = cli;
            lastMessage = new Stopwatch();
            messages = new List<byte[]>();

            mux.SetMessageHandler(ep, new MessageReceivedHandler(ReceivedMessage));
        }

        public string ToString()
        {
            return "UDP[" + Remote + "]";
        }

        public EndPoint Remote
        {
            get { return remote; }
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