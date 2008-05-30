using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Diagnostics;
using GT.Net;
using GT.Utils;

namespace GT.Net
{
    public class TcpTransport : BaseTransport
    {
        /// <summary>
        /// Allow setting a cap on the maximum TCP message size
        /// as compared to the OS value normally used.
        /// 512 is the historical value supported by GT.
        /// </summary>
        public static int CappedMessageSize = 512;

        private TcpClient handle;
        private Queue<byte[]> outstanding;

        private PacketInProgress incomingInProgress;
        private PacketInProgress outgoingInProgress;

        public TcpTransport(TcpClient h)
        {
            PacketHeaderSize = 4;   // GT TCP 1.0 protocol has 4 bytes for packet length
            outstanding = new Queue<byte[]>();
            h.NoDelay = true;
            h.Client.Blocking = false;
            handle = h;
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
                Console.WriteLine("{0} Warning: exception when closing TCP handle: {1}",
                    DateTime.Now, e);
            }
            handle = null;
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(byte[] buffer, int offset, int length)
        {
            if (!Active) { throw new InvalidStateException("Cannot send on disposed transport"); }
            ContractViolation.Assert(length > 0, "Cannot send 0-byte messages!");
            ContractViolation.Assert(length - PacketHeaderSize <= MaximumPacketSize, String.Format(
                    "Packet exceeds transport capacity: {0} > {1}", length - PacketHeaderSize, MaximumPacketSize));

            DebugUtils.DumpMessage(this + "SendPacket", buffer, offset, length);
            Debug.Assert(PacketHeaderSize > 0);
            byte[] wireFormat = new byte[length + PacketHeaderSize];
            BitConverter.GetBytes(length).CopyTo(wireFormat, 0);
            Array.Copy(buffer, offset, wireFormat, PacketHeaderSize, length);

            outstanding.Enqueue(wireFormat);
            FlushOutstandingPackets();
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(Stream output)
        {
            if (!Active) { throw new InvalidStateException("Cannot send on disposed transport"); }
            Debug.Assert(PacketHeaderSize > 0);
            ContractViolation.Assert(output.Length > 0, "Cannot send 0-byte messages!");
            ContractViolation.Assert(output.Length - PacketHeaderSize <= MaximumPacketSize, String.Format(
                    "Packet exceeds transport capacity: {0} > {1}", output.Length - PacketHeaderSize, MaximumPacketSize));
            if (!(output is MemoryStream))  // could check header bytes that it's the one we provided
            {
                throw new ArgumentException("Transport provided different stream!");
            }

            MemoryStream ms = (MemoryStream)output;
            DebugUtils.DumpMessage(this + ": SendPacket(stream)", ms.ToArray(), PacketHeaderSize,
                (int)(ms.Length - PacketHeaderSize));
            ms.Position = 0;
            byte[] lb = BitConverter.GetBytes((int)(ms.Length - PacketHeaderSize));
            Debug.Assert(lb.Length == 4);
            ms.Write(lb, 0, lb.Length);

            //FIXME: should use a PacketInProgress with the stream length
            outstanding.Enqueue(ms.ToArray());
            FlushOutstandingPackets();
        }

        virtual protected void FlushOutstandingPackets()
        {
            SocketError error = SocketError.Success;

            while (outstanding.Count > 0)
            {
                if (outgoingInProgress == null)
                {
                    //DebugUtils.WriteLine(this + ": Flush: " + outstanding.Peek().Length + " bytes");
                    outgoingInProgress = new PacketInProgress(outstanding.Peek());
                    Debug.Assert(outgoingInProgress.Length > 0);
                }
                int bytesSent = handle.Client.Send(outgoingInProgress.data, outgoingInProgress.position,
                    outgoingInProgress.bytesRemaining, SocketFlags.None, out error);
                //DebugUtils.WriteLine("{0}: position={1} bR={2}: sent {3}", Name, 
                //    outgoingInProgress.position, outgoingInProgress.bytesRemaining, bytesSent);

                switch (error)
                {
                case SocketError.Success:
                    outgoingInProgress.Advance(bytesSent);
                    if (outgoingInProgress.bytesRemaining <= 0)
                    {
                        outstanding.Dequeue();
                        outgoingInProgress = null;
                    }
                    break;
                case SocketError.WouldBlock:
                    //don't die, but try again next time
                    // FIXME: Some how push back to indicate that flow has been choked off
                    return;
                default:
                    //die, because something terrible happened
                    throw new FatalTransportError(this, String.Format("Error sending TCP Message ({0} bytes): {1}",
                        outgoingInProgress.Length, error), error);
                }
            }
        }

        override public void Update()
        {
            if (!Active) { throw new InvalidStateException("Cannot send on disposed transport"); }
            CheckIncomingPackets();
            FlushOutstandingPackets();
        }

        virtual protected void CheckIncomingPackets()
        {
            SocketError error = SocketError.Success;
            try
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
                        incomingInProgress = new PacketInProgress(PacketHeaderSize, true);
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
                        throw new FatalTransportError(this, String.Format("Error reading from socket: {0}", error), error);
                    }
                    if (bytesReceived == 0)
                    {
                        throw new TransportDecomissionedException(this);
                    }

                    incomingInProgress.Advance(bytesReceived);
                    if (incomingInProgress.bytesRemaining == 0)
                    {
                        if (incomingInProgress.IsMessageHeader())
                        {
                            int payloadLength = BitConverter.ToInt32(incomingInProgress.data, 0);
                            if (payloadLength <= 0)
                            {
                                Console.WriteLine("{0}: WARNING: received packet with 0-byte payload!", this);
                                incomingInProgress = null;
                            }
                            else
                            {
                                incomingInProgress = new PacketInProgress(payloadLength, false);
                            }
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
                throw new FatalTransportError(this, String.Format("Error reading from socket: {0}", e.SocketErrorCode), e);
            }
        }

        public override string ToString()
        {
            return "TcpTransport(" + handle.Client.LocalEndPoint + " -> " + handle.Client.RemoteEndPoint + ")";
        }
    }

}