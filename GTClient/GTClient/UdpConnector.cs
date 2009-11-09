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
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Common.Logging;
using GT.Utils;

namespace GT.Net
{

    /// <summary>
    /// Class responsible for connecting (and negotiating) a connection to a
    /// remote server using UDP.
    /// </summary>
    /// <remarks>
    /// The use of <see cref="TransportFactory{T}"/> may seem to be a bit complicated,
    /// but it greatly simplifies testing.
    /// </remarks>
    public class UdpConnector : IConnector
    {
        protected ILog log;

        protected bool active = false;
        protected TransportFactory<UdpClient> factory;

        /// <summary>
        /// The default constructor is for an unordered, unsequenced UDP
        /// </summary>
        public UdpConnector() : this(Ordering.Unordered) {}

        /// <summary>
        /// Create a new instance for handling UDP connections providing
        /// specified ordering requirements.
        /// </summary>
        /// <param name="ordering"></param>
        public UdpConnector(Ordering ordering)
        {
            log = LogManager.GetLogger(GetType());

            switch (ordering)
            {
                case Ordering.Unordered:
                    factory = new TransportFactory<UdpClient>(
                        BaseUdpTransport.UnorderedProtocolDescriptor,
                        h => new UdpClientTransport(h),
                        t => t is UdpClientTransport);
                    return;
                case Ordering.Sequenced:
                    factory = new TransportFactory<UdpClient>(
                        BaseUdpTransport.SequencedProtocolDescriptor,
                        h => new UdpSequencedClientTransport(h),
                        t => t is UdpSequencedClientTransport);
                    return;
                default: throw new InvalidOperationException("Unsupported ordering type: " + ordering);
            }
        }

        /// <summary>
        /// Create a new instance using the provided transport factory
        /// </summary>
        /// <param name="factory"></param>
        public UdpConnector(TransportFactory<UdpClient> factory)
        {
            log = LogManager.GetLogger(GetType());

            this.factory = factory;
        }

        /// <summary>
        /// Return the protocol descriptor talked by this instance.
        /// </summary>
        public byte[] ProtocolDescriptor
        {
            get { return factory.ProtocolDescriptor; }
        }

        /// <summary>
        /// Number of times to retry handshake negotiation.
        /// </summary>
        protected virtual uint MaximumRetries { get { return 5; } }

        /// <summary>
        /// Amount of time to wait for a return on negotiation.
        /// </summary>
        protected virtual TimeSpan NegotiationTimeout { get { return TimeSpan.FromMilliseconds(500); } }

        public void Start() { active = true; }
        public void Stop() { active = false; }
        public bool Active { get { return active; } }
        public void Dispose() { Stop(); }

        public ITransport Connect(string address, string portDescription, 
            IDictionary<string, string> capabilities)
        {
            IPAddress[] addresses;
            try { addresses = Dns.GetHostAddresses(address); }
            catch (SocketException e)
            {
                throw new CannotConnectException("Cannot resolve hostname: " + address, e);
            }

            int port;
            try { port = Int32.Parse(portDescription); }
            catch (FormatException e)
            {
                throw new CannotConnectException("invalid port: " + portDescription, e);
            }

            //try to connect to the address
            CannotConnectException error = null;
            foreach(IPAddress addr in addresses)
            {
                UdpClient client = null;
                IPEndPoint endPoint = null;
                try
                {
                    endPoint = new IPEndPoint(addr, port);
                    client = new UdpClient();
                    client.DontFragment = true; // FIXME: what are the implications of setting this flag?
                    client.Client.Blocking = false;
                    client.Client.SendTimeout = 1;
                    client.Client.ReceiveTimeout = 1;
                    client.Connect(endPoint);
                    ShakeHands(client, capabilities);
                    log.Debug("Now connected to UDP: " + client.Client.RemoteEndPoint);
                    return factory.CreateTransport(client);
                }
                catch (SocketException e)
                {
                    String errorMsg = String.Format("Cannot connect to {0}: {1}",
                        endPoint, e.Message);
                    error = new CannotConnectException(errorMsg);
                    error.SourceComponent = this;
                    log.Debug(errorMsg);
                }
                catch (CannotConnectException e)
                {
                    log.Debug(String.Format("Could not connect to {0}: {1}",
                        endPoint, e.Message));
                    error = e;
                }
            }
            if (error == null)
            {
                error = new CannotConnectException(
                    String.Format("Unable to connect to remote {0}/{1}",
                                  address, port));
            }
            log.Info(error.Message);
            throw error;
        }

        /// <summary>
        /// Attempt connection negotiation with remote side.
        /// </summary>
        /// <exception cref="SocketException">thrown on socket error</exception>
        /// <exception cref="CannotConnectException">thrown if the remote endpoint
        /// cannot be resolved.</exception>
        private void ShakeHands(UdpClient client, IDictionary<string, string> capabilities)
        {
            byte[] offering = CreateHandshakeOffering(client, capabilities);

            IList readSockets = new ArrayList(1);
            int readTimeoutMicroseconds = (int)(NegotiationTimeout.TotalMilliseconds * 1000);
            bool invalidPackets = false;
            for (int i = 0; i < MaximumRetries; i++)
            {
                client.Send(offering, offering.Length);

                readSockets.Clear();
                readSockets.Add(client.Client);
                Socket.Select(readSockets, null, null, readTimeoutMicroseconds);
                if(readSockets.Count > 0)
                {
                    EndPoint remoteEP = client.Client.RemoteEndPoint;
                    IPEndPoint remote = (IPEndPoint)remoteEP;
                    byte[] reply = client.Receive(ref remote);
                    if (ValidateHandshakeReply(client, reply))
                    {
                        return;
                    }
                    invalidPackets = true;
                }
            }
            throw new CannotConnectException(invalidPackets
                ? "negotiation failed: invalid response data" 
                : "negotiation failed: timed out");
        }

        /// <summary>
        /// Create the handshake initiation message.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="capabilities"></param>
        /// <returns></returns>
        private byte[] CreateHandshakeOffering(UdpClient client, IDictionary<string, string> capabilities)
        {
            // This is the GT (UDP) protocol 1.0:
            //   bytes 0 - 3: the protocol version (the result from ProtocolDescriptor)
            //   bytes 4 - n: the number of bytes in the capability dictionary (see ByteUtils.EncodeLength)
            //   bytes n+1 - end: the capability dictionary
            // The # bytes in the dictionary isn't actually necessary in UDP, but oh well
            MemoryStream ms = new MemoryStream(4 + 60);
            // approx: 4 bytes for protocol, 60 for capabilities
            Debug.Assert(ProtocolDescriptor.Length == 4);
            ms.Write(ProtocolDescriptor, 0, 4);
            ByteUtils.EncodeLength(ByteUtils.EncodedDictionaryByteCount(capabilities), ms);
            ByteUtils.EncodeDictionary(capabilities, ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Validate the reply message to see if the connection was accepted.
        /// </summary>
        /// <param name="client">the socket on which the message was received</param>
        /// <param name="reply">the reply message received</param>
        /// <returns>true if accepted, false if the reply was incorrect</returns>
        /// <exception cref="CannotConnectException">if not accepted</exception>
        private bool ValidateHandshakeReply(UdpClient client, byte[] reply)
        {
            if(reply.Length < 6)
            {
                return false;
            }
            MessageType mt;
            byte sysMessage;
            uint length;
            LWMCFv11.DecodeHeader(out mt, out sysMessage, out length, reply, 0);
            if (mt == MessageType.System
                && (SystemMessageType)sysMessage == SystemMessageType.Acknowledged)
            {
                return length == ProtocolDescriptor.Length
                    && reply.Length - LWMCFv11.HeaderSize == length
                        && ByteUtils.Compare(ProtocolDescriptor, 0, reply, 
                                (int)LWMCFv11.HeaderSize, (int)length);
            }
            if (mt == MessageType.System
                && (SystemMessageType)sysMessage == SystemMessageType.IncompatibleVersion)
            {
                throw new CannotConnectException("connection was rejected by remote");
            }
            return false;
        }

        public bool Responsible(ITransport transport)
        {
            return factory.Responsible(transport);
        }


    }
}
