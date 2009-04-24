using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using GT.Utils;

namespace GT.Net
{
    public class TcpTransport : BaseTransport
    {
        /// <summary>
        /// 512 is the historical value supported by GT.
        /// </summary>
        public static uint DefaultMaximumMessageSize = 512;

        private TcpClient handle;
        private readonly EndPoint remoteEndPoint;
        private uint maximumPacketSize = DefaultMaximumMessageSize;
        private Queue<TransportPacket> outstanding = new Queue<TransportPacket>();

        private TransportPacket incomingInProgress;
        private bool incomingReadingHeader;
        private uint incomingOffset;
        private uint incomingRemaining;

        private TransportPacket outgoingInProgress;

        public TcpTransport(TcpClient h)
            : base(4)   // GT TCP 1.0 protocol has 4 bytes for packet length
        {
            h.NoDelay = true;
            h.Client.Blocking = false;
            handle = h;
            remoteEndPoint = handle.Client.RemoteEndPoint;
        }

        override public string Name
        {
            get { return "TCP"; }
        }

        override public Reliability Reliability
        {
            get { return Reliability.Reliable; }
        }

        public override Ordering Ordering
        {
            get { return Ordering.Ordered; }
        }

        public override uint MaximumPacketSize
        {
            get { return maximumPacketSize; }
            set
            {
                maximumPacketSize = Math.Max(PacketHeaderSize, 
                    Math.Min(value, uint.MaxValue));    // can't exceed 2^32
                try
                {
                    if (handle.SendBufferSize < maximumPacketSize)
                    {
                        handle.SendBufferSize = (int)maximumPacketSize;
                    }
                }
                catch (SocketException e)
                {
                    log.Debug(
                        String.Format("Unable to change send-buffer size to {0}", maximumPacketSize), e);
                }
            }
        }


        public override uint Backlog { get { return (uint)outstanding.Count; } }

        public IPAddress Address
        {
            get { return ((IPEndPoint)remoteEndPoint).Address; }
        }

        override public bool Active { get { return handle != null; } }

        override public void Dispose()
        {
            try
            {
                if (!Active) { return; }
                if (handle != null) { handle.Close(); }
                outgoingInProgress = null;
                incomingInProgress = null;
            }
            catch (Exception e)
            {
                log.Info("exception when closing TCP handle", e);
            }
            handle = null;
        }

        /// <summary>Send a packet to server.</summary>
        /// <param name="packet">The packet to send.</param>
        public override void SendPacket(TransportPacket packet)
        {
            InvalidStateException.Assert(Active, "Cannot send on disposed transport", this);
            ContractViolation.Assert(packet.Length > 0, "Cannot send 0-byte messages!");
            ContractViolation.Assert(packet.Length - PacketHeaderSize <= MaximumPacketSize, 
                String.Format("Packet exceeds transport capacity: {0} > {1}; try increasing transport's MaximumPacketSize",
                    packet.Length - PacketHeaderSize, MaximumPacketSize));

            //DebugUtils.DumpMessage(this + "SendPacket", buffer, offset, length);
            Debug.Assert(PacketHeaderSize > 0);
            packet.Prepend(DataConverter.Converter.GetBytes((uint)packet.Length));

            lock (this)
            {
                outstanding.Enqueue(packet);
            }
            FlushOutstandingPackets();
        }

        virtual protected void FlushOutstandingPackets()
        {
            lock (this)
            {
                while (outstanding.Count > 0)
                {
                    if (outgoingInProgress == null)
                    {
                        //DebugUtils.WriteLine(this + ": Flush: " + outstanding.Peek().Length + " bytes");
                        outgoingInProgress = outstanding.Peek();
                        Debug.Assert(outgoingInProgress.Length > 0);
                    }

                    SocketError error;
                    int bytesSent = handle.Client.Send(outgoingInProgress, SocketFlags.None, out error);
                    //DebugUtils.WriteLine("{0}: position={1} bR={2}: sent {3}", Name, 
                    //    outgoingInProgress.position, outgoingInProgress.bytesRemaining, bytesSent);

                    switch (error)
                    {
                    case SocketError.Success:
                        outgoingInProgress.RemoveBytes(0, bytesSent);
                        if (outgoingInProgress.Length == 0)
                        {
                            outstanding.Dequeue();
                            TransportPacket oip = outgoingInProgress;
                            outgoingInProgress = null;
                            // ok, strictly speaking this won't be right if we've sent a
                            // subsequence of the oip's data array
                            NotifyPacketSent(oip);
                        }
                        break;

                    case SocketError.WouldBlock:
                        NotifyError(new ErrorSummary(Severity.Information,
                            SummaryErrorCode.TransportBacklogged, 
                            "Transport backlogged: too much data",
                            this, null));
                        return;

                    default:
                        //die, because something terrible happened
                        throw new TransportError(this,
                            String.Format("Error sending TCP Message ({0} bytes): {1}",
                                outgoingInProgress.Length, error), error);
                    }
                }
            }
        }

        override public void Update()
        {
            InvalidStateException.Assert(Active, "Cannot send on disposed transport", this);
            CheckIncomingPackets();
            FlushOutstandingPackets();
        }

        virtual protected void CheckIncomingPackets()
        {
            TransportPacket packet;
            while ((packet = FetchIncomingPacket()) != null)
            {
                // NotifyPacketReceived will ensure the packet is disposed of
                NotifyPacketReceived(packet);
            }
        }

        virtual protected TransportPacket FetchIncomingPacket()
        {
            lock (this)
            {
                while (handle.Available > 0)
                {
                    // This is a simple state machine: we're either:
                    // (a) reading a packet header (incomingInProgress.IsMessageHeader())
                    // (b) reading a packet body (!incomingInProgress.IsMessageHeader())
                    // (c) finished and about to start reading in a header (incomingInProgress == null)

                    if (incomingInProgress == null)
                    {
                        //restart the counters to listen for a new packet.
                        incomingInProgress = new TransportPacket(PacketHeaderSize);
                        incomingReadingHeader = true;
                        incomingOffset = 0;
                        incomingRemaining = PacketHeaderSize;
                        // assert incomingInProgress.IsMessageHeader();
                    }

                    SocketError error;
                    int bytesReceived = handle.Client.Receive(
                        incomingOffset == 0 ? incomingInProgress
                            : incomingInProgress.Subset((int)incomingOffset, (int)incomingRemaining),
                        SocketFlags.None, out error);
                    switch (error)
                    {
                    case SocketError.Success:
                        // Console.WriteLine("{0}: CheckIncomingPacket(): received header", this);
                        break;

                    case SocketError.WouldBlock:
                        return null; // nothing to do!

                    default:
                        //dead = true;
                        throw new TransportError(this,
                            String.Format("Error reading from socket: {0}", error), error);
                    }
                    if (bytesReceived == 0)
                    {
                        throw new TransportError(this, "Socket was closed",
                            SocketError.Disconnecting);
                    }

                    incomingOffset += (uint)bytesReceived;
                    incomingRemaining -= (uint)bytesReceived;
                    if (incomingRemaining == 0)
                    {
                        if (incomingReadingHeader)
                        {
                            incomingRemaining = ProcessHeader(incomingInProgress);
                            if (incomingRemaining == 0)
                            {
                                log.Warn("received packet with 0-byte payload!");
                                incomingInProgress = null;
                            }
                            else
                            {
                                incomingReadingHeader = false;
                                incomingOffset = 0;
                                incomingInProgress.Grow((int)incomingRemaining);
                            }
                        }
                        else
                        {
                            TransportPacket packet = incomingInProgress;
                            incomingInProgress = null;
                            return packet;
                        }
                    }
                }
            }
            return null;
        }

        protected virtual uint ProcessHeader(TransportPacket header)
        {
            uint headerLength = 0;
            header.BytesAt(0, 4,
                (b, offset) => headerLength = DataConverter.Converter.ToUInt32(b, offset));
            return headerLength;
        }

        public override string ToString()
        {
            if (handle != null)
            {
		        try
		        {
		            return String.Format("{0}: {1} -> {2}", Name,
			        handle.Client.LocalEndPoint,
			        handle.Client.RemoteEndPoint);
		        }
		        catch(SocketException) { /* FALLTHROUGH */ }
            }
    	    return String.Format("{0}: {1}", Name, remoteEndPoint);
        }
    }

}
