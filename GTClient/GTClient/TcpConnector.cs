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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Common.Logging;
using GT.Utils;

namespace GT.Net
{
    /// <summary>
    /// A connector for initiating and negotiation a new
    /// transport connection across TCP.  
    /// See also GT.Net.TcpAcceptor.
    /// </summary>
    public class TcpConnector : IConnector
    {
        protected ILog log;

        protected bool active = false;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public TcpConnector()
        {
            log = LogManager.GetLogger(GetType());
        }

        /// <summary>
        /// The on-wire protocol version used by this connector
        /// </summary>
        public byte[] ProtocolDescriptor
        {
            get { return ASCIIEncoding.ASCII.GetBytes("GT10"); }
        }

        public void Start() { active = true;  }
        public void Stop() { active = false; }
        public bool Active { get { return active; } }
        public void Dispose() { Stop(); }

        public ITransport Connect(string address, string portDescription,
            IDictionary<string, string> capabilities)
        {
            IPAddress[] addresses;
            int port;

            try { addresses = Dns.GetHostAddresses(address); }
            catch (SocketException e)
            {
                throw new CannotConnectException("Cannot resolve hostname: " + address, e);
            }
            if (addresses.Length == 0)
            {
                throw new CannotConnectException(
                    String.Format("Cannot resolve address", address));
            }

            try { port = Int32.Parse(portDescription); }
            catch (FormatException e)
            {
                throw new CannotConnectException("invalid port: " + portDescription, e);
            }

            //try to connect to the address
            TcpClient client = null;
            IPEndPoint endPoint = null;
            CannotConnectException error = null;
            foreach (IPAddress addr in addresses)
            {
                // in case hanging around from last iteration
                if (client != null)
                {
                    try
                    {
                        client.Close();
                        client = null;
                    }
                    catch (Exception) {/*ignore*/}
                }
                try
                {
                    endPoint = new IPEndPoint(addr, port);
                    client = new TcpClient();
                    client.NoDelay = true;
                    client.ReceiveTimeout = 1;
                    client.SendTimeout = 1;
                    client.Connect(endPoint);
                    // must set to blocking *after* the connect
                    client.Client.Blocking = false;
                    error = null;
                    break;
                }
                catch (SocketException e)
                {
                    String errorMsg = String.Format("Cannot connect to {0}: {1}",
                        endPoint, e.Message);
                    error = new CannotConnectException(errorMsg);
                    error.SourceComponent = this;
                    log.Debug(errorMsg);
                }
            }

            if (error != null)
            {
                log.Info(error.Message);
                throw error;
            }

            // FIXME: a handshake is between two people; we assume that if they don't want
            // to talk to us then they'll close the connexion.

            // This is the GT (UDP) protocol 1.0:
            // bytes 0 - 3: the protocol version (ASCII for "GT10")
            // bytes 4 - n: the number of bytes in the capability dictionary (see ByteUtils.EncodeLength)
            // bytes n+1 - end: the capability dictionary
            MemoryStream ms = new MemoryStream(4 + 60); // approx: 4 bytes for protocol, 50 for capabilities
            Debug.Assert(ProtocolDescriptor.Length == 4);
            ms.Write(ProtocolDescriptor, 0, 4);
            ByteUtils.EncodeLength(ByteUtils.EncodedDictionaryByteCount(capabilities), ms);
            ByteUtils.EncodeDictionary(capabilities, ms);
            client.Client.Send(ms.GetBuffer(), 0, (int)ms.Length, SocketFlags.None);

            log.Debug("Now connected via TCP: " + endPoint);
            return new TcpTransport(client);
        }

        public bool Responsible(ITransport transport)
        {
            return transport is TcpTransport;
        }

    }
}
