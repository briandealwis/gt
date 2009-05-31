//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using Common.Logging;

namespace GT.Net
{
    /// <summary>
    /// A delegate for notifying of the reception of a new packet from some remote
    /// endpoint.
    /// </summary>
    /// <param name="remote">the remote endpoint</param>
    /// <param name="packet">the packet received</param>
    public delegate void NetPacketReceivedHandler(EndPoint remote, TransportPacket packet);

    /// <summary>
    /// The UdpMultiplexor demultiplexes traffic from a UDP socket configured on 
    /// a particular address/port pair.  <see cref="UdpHandle"/> instances generally
    /// register to receive messages received from a particular endpoint.  There is a 
    /// default handler that is triggered for messages received from previously-unknown
    /// remote endpoints (e.g., a new connection); the default handler generally creates
    /// a new <see cref="UdpHandle"/> to handle subsequent messages from that remote
    /// endpoint.
    /// </summary>
    public class UdpMultiplexer : IStartable
    {
        protected ILog log;
        protected readonly IPAddress address;
        protected readonly int port;
        protected UdpClient udpClient;
        protected Dictionary<EndPoint,NetPacketReceivedHandler> handlers = 
            new Dictionary<EndPoint,NetPacketReceivedHandler>();
        protected NetPacketReceivedHandler defaultHandler;

        /// <summary>
        /// Create a new instance listening on the specified address/port combination.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        public UdpMultiplexer(IPAddress address, int port)
        {
            log = LogManager.GetLogger(GetType());

            this.address = address;
            this.port = port;
        }

        /// <summary>
        /// Return the maximum packet size that can be sent via this multiplexor
        /// </summary>
        public int MaximumPacketSize {
            get { return udpClient.Client.SendBufferSize; }
        }

        /// <summary>
        /// Return the description of this local endpoint.
        /// </summary>
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
            try
            {
                udpClient = new UdpClient(new IPEndPoint(address, port));
            }
            catch (SocketException e)
            {
                string message = String.Format("Unable to create UDP listening socket on {0}/{1}",
                    address, port);
                log.Error(message, e);
                throw new TransportError(this, message, e);
            }
            udpClient.Client.Blocking = false;
            DisableUdpConnectionResetBehaviour();
        }

        /// <summary>Hack to avoid the ConnectionReset/ECONNRESET problem described
        /// in <a href="https://papyrus.usask.ca/trac/gt/ticket/41">bug 41</a>.</summary>
        private void DisableUdpConnectionResetBehaviour()
        {
            // Code from http://www.devnewsgroups.net/group/microsoft.public.dotnet.framework/topic47566.aspx
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                byte[] inValue = new byte[4];   // zeroes = false
                udpClient.Client.IOControl(SIO_UDP_CONNRESET, inValue, null);
                log.Debug("installed SIO_UDP_CONNRESET hack for UdpMultiplexer");
            }
            catch (Exception e) {
                log.Debug("unable to install SIO_UDP_CONNRESET hack for UdpMultiplexer: {0}", e);
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

        /// <summary>
        /// Configure a default message handler, called for packets received
        /// from remote endpoints that have not been sent previously.
        /// </summary>
        /// <param name="handler">the message handler</param>
        public void SetDefaultPacketHandler(NetPacketReceivedHandler handler)
        {
            defaultHandler = handler;
        }

        /// <summary>
        /// Discard the default message handler.
        /// </summary>
        /// <returns>the message handler, or null if there was no handler previously</returns>
        public NetPacketReceivedHandler RemoveDefaultPacketHandler() {
            NetPacketReceivedHandler old = defaultHandler;
            defaultHandler = null;
            return old;
        }

        /// <summary>
        /// Associate a message handler for packets received from the specified 
        /// remote endpoint.
        /// </summary>
        /// <param name="ep">the remote endpoint</param>
        /// <param name="handler">the packet handler</param>
        public void SetPacketHandler(EndPoint ep, NetPacketReceivedHandler handler)
        {
            handlers[ep] = handler;
        }

        /// <summary>
        /// Discard the message handler configured for the specified remote endpoint
        /// </summary>
        /// <param name="ep">the remote endpoint</param>
        /// <returns>the old handler, or null if none</returns>
        public NetPacketReceivedHandler RemovePacketHandler(EndPoint ep) {
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
            while (Active && udpClient.Available > 0)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                // any SocketExceptions will be caught by callers
                byte[] buffer = udpClient.Receive(ref remote);
                // log.Debug(String.Format("{0}: received {1} bytes from {2}", this, rc, remote));
                NetPacketReceivedHandler h;
                if (!handlers.TryGetValue(remote, out h) || h == null)
                {
                    h = defaultHandler;
                    if (h == null)
                    {
                        log.Warn(String.Format("{0}: no default handler for {1}: ignoring incoming packet", this, remote));
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
                h.Invoke(remote, TransportPacket.On(buffer));
            }
        }

        /// <summary>
        /// Send a packet on the UDP socket.
        /// </summary>
        /// <exception cref="SocketException">thrown if there is a socket error</exception>
        public int Send(TransportPacket packet, EndPoint remote)
        {
            // Is our throwing a SocketException considered out of line?
            if (!Active) { throw new SocketException((int)SocketError.Shutdown); }
            // Sadly SentTo does not support being provided a IList<ArraySegment<byte>>
            packet.Consolidate();   // try to reduce to a single segment
            IList<ArraySegment<byte>> bytes = packet;
            if (bytes.Count == 1)
            {
                return udpClient.Client.SendTo(bytes[0].Array, bytes[0].Offset, bytes[0].Count,
                    SocketFlags.None, remote);
            }
            // hopefully this won't happen often; we could maintain our own bytearray pool
            return udpClient.Client.SendTo(packet.ToArray(), SocketFlags.None, remote);
        }

    }

    /// <summary>
    /// A simple class to send and receive packets to/from a remote endpoint.
    /// Works in cooperation with the <see cref="UdpMultiplexer"/>.
    /// </summary>
    public class UdpHandle : IDisposable
    {
        protected EndPoint remote;
        protected UdpMultiplexer mux;
        protected Queue<TransportPacket> messages;

        /// <summary>
        /// Create and configure a UDP handle for the specified demultiplexor.
        /// </summary>
        /// <param name="mux"></param>
        /// <param name="ep"></param>
        /// <returns></returns>
        public static UdpHandle Bind(UdpMultiplexer mux, EndPoint ep)
        {
            UdpHandle h = new UdpHandle(ep);
            h.Bind(mux);
            return h;
        }

        /// <summary>
        /// Create a new instance to send and receive messages from the specified 
        /// remote endpoint.  This instance is unbound.
        /// </summary>
        /// <param name="ep">the remote endpoint to be associated with</param>
        protected UdpHandle(EndPoint ep)
        {
            remote = ep;
            messages = new Queue<TransportPacket>();
        }

        protected void Bind(UdpMultiplexer udpMux)
        {
            mux = udpMux;
            mux.SetPacketHandler(remote, ReceivedMessage);
        }

        override public string ToString()
        {
            return "UDP[" + RemoteEndPoint + "]";
        }

        /// <summary>
        /// Return the remote's address information
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get { return (IPEndPoint)remote; }
        }

        /// <summary>
        /// Return the maximum packet size supported
        /// </summary>
        public int MaximumPacketSize
        {
            get { return mux.MaximumPacketSize; }
        }

        public void Dispose()
        {
            mux.RemovePacketHandler(remote);
            messages = null;
        }

        protected void ReceivedMessage(EndPoint ep, TransportPacket packet)
        {
            messages.Enqueue(packet);
        }

        /// <summary>
        /// Send a packet on the UDP socket.
        /// </summary>
        /// <exception cref="SocketException">thrown if there is a socket error</exception>
        public void Send(TransportPacket packet)
        {
            mux.Send(packet, remote);
        }

        /// <summary>
        /// Return the number of messages available to be received.
        /// </summary>
        public int Available { get { return messages.Count; } }

        /// <summary>
        /// Pull out a received packet
        /// </summary>
        /// <returns></returns>
        public TransportPacket Receive()
        {
            return messages.Dequeue();
        }

    }

}
