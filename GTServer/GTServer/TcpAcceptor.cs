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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using Common.Logging;
using GT.Utils;

namespace GT.Net
{

    /// <summary>
    /// Accept incoming connections across a TCP socket.
    /// </summary>
    public class TcpAcceptor : IPBasedAcceptor
    {
        /// <summary>
        /// The listening backlog to use for the server socket.  
        /// Historically the maximum was 5; some newer OS' support
        /// up to 128.
        /// </summary>
        public static int LISTENER_BACKLOG = 10;

        private TcpListener bouncer;

        private List<NegotiationInProgress> pending;

        public TcpAcceptor(IPAddress address, int port)
            : base(address, port)
        {
        }

        public byte[] ProtocolDescriptor
        {
            get { return ASCIIEncoding.ASCII.GetBytes("GT10"); }
        }

        override public bool Active
        {
            get { return bouncer != null; }
        }

        override public void Start()
        {
            if (Active) { return; }
            pending = new List<NegotiationInProgress>();
            try
            {
                bouncer = new TcpListener(address, port);
                bouncer.Server.Blocking = false;
                try { bouncer.Server.LingerState = new LingerOption(false, 0); }
                catch (SocketException e)
                {
                    log.Debug("exception setting TCP listening socket's Linger = false (ignored)", e);
                }
                bouncer.Start(LISTENER_BACKLOG);
            }
            catch (ThreadAbortException t) { throw t; }
            catch (SocketException e)
            {
                string message = String.Format("Unable to create TCP listening socket on {0}/{1}",
                    address, port);
                log.Error(message, e);
                if(bouncer != null) { bouncer.Stop(); }
                bouncer = null;
                throw new TransportError(this, message, e);
            }
        }

        public override void Stop()
        {
            if (bouncer != null)
            {
                try { bouncer.Stop(); }
                catch (Exception e) { log.Info("Exception stopping TCP listener", e); }
                bouncer = null;
            }
        }

        public override void Update()
        {
            // Console.WriteLine(this + ": checking TCP listening socket...");
            while (bouncer.Pending())
            {
                //let them join us
                try
                {
                    // DebugUtils.WriteLine(this + ": accepting new TCP connection");
                    TcpClient connection = bouncer.AcceptTcpClient();
                    connection.NoDelay = true;
                    pending.Add(new NegotiationInProgress(this, connection));
                }
                catch (SocketException e)
                {
                    throw new TransportError(this, "Exception raised accepting new TCP connection", e);
                }
            }
            foreach (NegotiationInProgress nip in new List<NegotiationInProgress>(pending))
            {
                nip.Update();
            }
        }

        public override void Dispose()
        {
            Stop();
        }

        internal void Remove(NegotiationInProgress nip)
        {
            pending.Remove(nip);
        }

        internal void RemoveAndNotify(NegotiationInProgress nip, TcpTransport transport, 
            Dictionary<string,string> capabilities)
        {
            pending.Remove(nip);
            CheckAndNotify(transport, capabilities);
        }

        protected override void TransportRejected(ITransport transport, IDictionary<string, string> capabilities)
        {
            TransportPacket tp = new TransportPacket(LWMCFv11.EncodeHeader(MessageType.System,
                (byte)SystemMessageType.IncompatibleVersion, 0));
            transport.SendPacket(tp);
            base.TransportRejected(transport, capabilities);
        }
    }

    internal class NegotiationInProgress
    {
        protected ILog log;
        protected TcpAcceptor acceptor;
        protected TcpClient connection;
        protected byte[] data = null;
        protected int offset = 0;

        enum NIPState { TransportProtocol, DictionarySize, DictionaryContent };
        NIPState state;

        internal NegotiationInProgress(TcpAcceptor acc, TcpClient c)
        {
            log = LogManager.GetLogger(GetType());

            acceptor = acc;
            connection = c;
            state = NIPState.TransportProtocol;
            data = new byte[4]; // protocol descriptor is 4 bytes
            offset = 0;
            try
            {
                connection.NoDelay = true;
            }
            catch (Exception) {/*ignore*/}
        }

        internal void Update()
        {
            try
            {
                InternalUpdate();
            }
            catch (Exception e)
            {
                log.Info(String.Format("abandoned incoming connection: handshake failed: {0}", this), e);
                try { connection.Close(); }
                catch (Exception) { }
                acceptor.Remove(this);
            }
        }


        protected void InternalUpdate()
        {
            do
            {
                while (offset < data.Length)
                {
                    if (connection.Available <= 0)
                    {
                        // we will return!
                        return;
                    }
                    SocketError sockError;
                    int rc = connection.Client.Receive(data, offset, data.Length - offset,
                        SocketFlags.None, out sockError);
                    if (sockError == SocketError.WouldBlock) { return; }
                    if (rc == 0) { throw new CannotConnectException("unexpected EOF"); }
                    if (sockError != SocketError.Success) { throw new CannotConnectException(sockError.ToString()); }
                    offset += rc;
                }

                switch (state)
                {
                case NIPState.TransportProtocol:
                    if (!ByteUtils.Compare(data, 0, acceptor.ProtocolDescriptor, 0, 4))
                    {
                        throw new CannotConnectException("Unknown protocol version: "
                        + ByteUtils.DumpBytes(data, 0, 4) + " ["
                        + ByteUtils.AsPrintable(data, 0, 4) + "]");
                    }
                    state = NIPState.DictionarySize;
                    data = new byte[1];
                    offset = 0;
                    break;

                case NIPState.DictionarySize:
                    {
                        MemoryStream ms = new MemoryStream(data);
                        try
                        {
                            uint count = ByteUtils.DecodeLength(ms);
                            state = NIPState.DictionaryContent;
                            data = new byte[count];
                            offset = 0;
                        }
                        catch (InvalidDataException)
                        {
                            // we keep reading until we have an encoded length
                            byte[] newData = new byte[data.Length + 1];
                            Buffer.BlockCopy(data, 0, newData, 0, data.Length);
                            data = newData;
                            // and get that next byte!
                        }
                    }
                    break;

                case NIPState.DictionaryContent:
                    {
                        MemoryStream ms = new MemoryStream(data);
                        Dictionary<string, string> dict = ByteUtils.DecodeDictionary(ms);
                        acceptor.RemoveAndNotify(this, new TcpTransport(connection), dict);
                    }
                    return;
                }
            } while (connection.Available > 0);
        }
    }
}
