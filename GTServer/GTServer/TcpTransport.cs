using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using GT.Common;
using System.IO;
using System.Diagnostics;

namespace GT.Servers {
    public class TcpServerTransport : BaseServerTransport
    {
        /// <summary>
        /// Allow setting a cap on the maximum TCP message size
        /// as compared to the OS value normally used.
        /// 512 is the historical value supported by GT.
        /// </summary>
        public static int CappedMessageSize = 512;

        private TcpClient handle;
        private List<byte[]> outstanding;

        private PacketInProgress incomingInProgress;
        private PacketInProgress outgoingInProgress;

        public TcpServerTransport(TcpClient h)
        {
            PacketHeaderSize = 4;   // 4 bytes for packet length
            outstanding = new List<byte[]>();
            h.NoDelay = true;
            h.Client.Blocking = false;
            handle = h;
        }

        override public string Name
        {
            get { return "TCP"; }
        }

        override public MessageProtocol MessageProtocol
        {
            get { return MessageProtocol.Tcp; }
        }

        public override int MaximumPacketSize
        {
            get
            {
                try
                {
                    return Math.Min(CappedMessageSize,
                        handle.Client.SendBufferSize);
                }
                catch (Exception)
                {
                    return CappedMessageSize;
                }
            }
        }

        public IPAddress Address
        {
            get { return ((IPEndPoint)handle.Client.RemoteEndPoint).Address; }
        }

        override public bool Dead { get { return handle == null; } }

        override public void Dispose()
        {
            try
            {
                if (handle != null) { handle.Close(); }
                outgoingInProgress = null;
                incomingInProgress = null;
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Warning: exception when closing TCP handle: {1}",
                    DateTime.Now, e);
            }
            handle = null;
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(byte[] buffer, int offset, int length)
        {
            if (Dead) { throw new InvalidStateException("Cannot send: is dead"); }

            DebugUtils.DumpMessage(this + "SendPacket", buffer);
            byte[] wireFormat = new byte[length + 4];
            BitConverter.GetBytes(length).CopyTo(wireFormat, 0);
            Array.Copy(buffer, offset, wireFormat, 4, length);

            outstanding.Add(wireFormat);
            FlushOutstandingPackets();
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(Stream output)
        {
            if (Dead)
            {
                throw new InvalidStateException("Cannot send on a stopped client", this);
            }
            if (!(output is MemoryStream))
            {
                throw new ArgumentException("Transport provided different stream!");
            }
            MemoryStream ms = (MemoryStream)output;
            DebugUtils.DumpMessage(this + ": SendPacket(stream)", ms.ToArray());
            ms.Position = 0;
            byte[] lb = BitConverter.GetBytes((int)(ms.Length - PacketHeaderSize));
            Debug.Assert(lb.Length == 4);
            ms.Write(lb, 0, lb.Length);

            //FIXME: should use a PacketInProgress with the stream length
            outstanding.Add(ms.ToArray());
            FlushOutstandingPackets();
        }

        virtual protected void FlushOutstandingPackets() 
        {
            SocketError error = SocketError.Success;

            while (outstanding.Count > 0)
            {
                if (outgoingInProgress == null)
                {
                    //Console.WriteLine("Srv.Flush: " + outstanding[0].Length + " bytes");
                    outgoingInProgress = new PacketInProgress(outstanding[0]);
                }
                int bytesSent = handle.Client.Send(outgoingInProgress.data, outgoingInProgress.position, 
                    outgoingInProgress.bytesRemaining, SocketFlags.None, out error);
                //Console.WriteLine("  position=" + outgoingInProgress.position + " bR=" + 
                //  outgoingInProgress.bytesRemaining + ": sent " + bytesSent);

                switch (error)
                {
                case SocketError.Success:
                    outgoingInProgress.Advance(bytesSent);
                    if (outgoingInProgress.bytesRemaining <= 0)
                    {
                        outstanding.RemoveAt(0);
                        outgoingInProgress = null;
                    }
                    break;
                case SocketError.WouldBlock:
                    //don't die, but try again next time
                    LastError = null;
                    NotifyError(null, error, this, "The TCP write buffer is full now, but the data will be saved and " +
                            "sent soon.  Send less data to reduce perceived latency.");
                    return;
                default:
                    //die, because something terrible happened
                    //dead = true;
                    LastError = null;
                    NotifyError(null, error, this, "Failed to Send TCP Message (" + outgoingInProgress.Length + " bytes): " + outgoingInProgress.data.ToString());
                    return;
                }
            }
        }

        /// <summary>Gets one data from the tcp and interprets it.</summary>
        override public void Update()
        {
            if (Dead) { throw new InvalidStateException("Cannot update: instance is dead"); }
            CheckIncomingPackets();
            FlushOutstandingPackets();
        }

        virtual protected void CheckIncomingPackets() {
            //if (handle.Available > 0)
            //{
            //    Console.WriteLine(this + ": there appears to be some data available!");
            //}

            SocketError error = SocketError.Success;
            try
            {
                while (handle.Available > 0)
                {
                    // This is a simple state machine: we're either:
                    // (a) reading a data header (incomingInProgress.IsMessageHeader())
                    // (b) reading a data body (!incomingInProgress.IsMessageHeader())
                    // (c) finished and about to start reading in a header (incomingInProgress == null)

                    if (incomingInProgress == null)
                    {
                        //restart the counters to listen for a new data.
                        incomingInProgress = new PacketInProgress(4, true);
                        // assert incomingInProgress.IsMessageHeader();
                    }

                    int bytesReceived = handle.Client.Receive(incomingInProgress.data, incomingInProgress.position,
                        incomingInProgress.bytesRemaining, SocketFlags.None, out error);
                    switch (error)
                    {
                    case SocketError.Success:
                        // Console.WriteLine("{0}: CheckIncomingPacket(): received header", this);
                        break;

                    case SocketError.WouldBlock:
                        // Console.WriteLine("{0}: CheckIncomingPacket(): would block", this);
                        return;

                    default:
                        //dead = true;
                        DebugUtils.WriteLine(this + ": CheckIncomingPacket(): ERROR reading from socket: " + error);
                        NotifyError(null, SocketError.Fault, this, "Error reading TCP data header.");
                        return;
                    }
                    if (bytesReceived == 0)
                    {
                        return;
                    }

                    incomingInProgress.Advance(bytesReceived);
                    if (incomingInProgress.bytesRemaining == 0)
                    {
                        if (incomingInProgress.IsMessageHeader())
                        {
                            incomingInProgress = new PacketInProgress(BitConverter.ToInt32(incomingInProgress.data,0), false);
                            // assert incomingInProgress.IsMessageHeader()
                        }
                        else
                        {
                            DebugUtils.DumpMessage(this + ": CheckIncomingMessage", incomingInProgress.data);
                            NotifyPacketReceived(incomingInProgress.data, 0, incomingInProgress.data.Length);
                            incomingInProgress = null;
                        }
                    }
                }
            }
            catch (SocketException e)
            {   // FIXME: can this clause even happen?
                //dead = true;
                LastError = e;
                Console.WriteLine("{0}: UpdateFromNetworkTcp(): SocketException reading from socket: {1}", this, e);
                //if (ErrorEvent != null)
                //    ErrorEvent(e, SocketError.NoRecovery, this, "Updating from TCP connection failed because of a socket exception.");
            }
            catch (Exception e)
            {
                // We shouldn't catch ThreadAbortExceptions!  (FIXME: should we really be
                // catching anything other than SocketExceptions from here?)
                if (e is ThreadAbortException) { throw e; }
                LastError = e;
                Console.WriteLine("{0}: UpdateFromNetworkTcp(): EXCEPTION: {1}", this, e);
                NotifyError(e, SocketError.NoRecovery, this, "Exception occured (not socket exception).");
            }
        }

        public override string ToString()
        {
            return "TcpServerTransport(" + handle.Client.RemoteEndPoint + ")";
        }
    }


    public class TcpAcceptor : BaseAcceptor
    {
        /// <summary>The listening backlog to use for the server socket.  Historically
        /// the maximum was 5; some newer OS' support up to 128.</summary>
        public static int LISTENER_BACKLOG = 10;

        private TcpListener bouncer;

        public TcpAcceptor(IPAddress address, int port)
            : base(address, port)
        {
        }

        override public bool Started
        {
            get { return bouncer != null; }
        }

        override public void Start()
        {
            if (Started) { return; }
            try
            {
                bouncer = new TcpListener(address, port);
                bouncer.Server.Blocking = false;
                try { bouncer.Server.LingerState = new LingerOption(false, 0); }
                catch (SocketException e)
                {
                    Console.WriteLine(this + ": exception setting TCP listening socket's Linger = false (ignored): " + e);
                }
                bouncer.Start(LISTENER_BACKLOG);
            }
            catch (ThreadAbortException t) { throw t; }
            catch (SocketException e)
            {
                //LastError = e;
                //if (ErrorEvent != null)
                //    ErrorEvent(e, SocketError.Fault, null, "A socket exception occurred when we tried to start listening for incoming connections.");
                Console.WriteLine(this + ": exception creating TCP listening socket: " + e);
                bouncer = null;
            }
        }

        public override void Stop()
        {
            if (bouncer != null)
            {
                try { bouncer.Stop(); }
                catch (Exception e) { Console.WriteLine("Exception stopping TCP listener: " + e); }
                bouncer = null;
            }
        }

        public override string ToString()
        {
            return "TcpAcceptor(" + bouncer.LocalEndpoint + ")";
        }

        public override void Update()
        {
            TcpClient connection;
            // Console.WriteLine(this + ": checking TCP listening socket...");
            while (bouncer.Pending())
            {
                //let them join us
                try
                {
                    DebugUtils.WriteLine(this + ": accepting new TCP connection");
                    connection = bouncer.AcceptTcpClient();
                }
                catch (Exception e)
                {
                    //LastError = e;
                    Console.WriteLine(this + ": EXCEPTION accepting new TCP connection: " + e);
                    //if (ErrorEvent != null)
                    //    ErrorEvent(e, SocketError.Fault, null, "An error occurred when trying to accept new client.");
                    bouncer = null;
                    break;
                }

                NotifyNewClient(new TcpServerTransport(connection), 0);
            }
        }

        public override void Dispose()
        {
            Stop();
        }
    }
}