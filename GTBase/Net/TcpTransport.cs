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
        private List<byte[]> outstanding;

        private PacketInProgress incomingInProgress;
        private PacketInProgress outgoingInProgress;

        public TcpTransport(TcpClient h)
        {
            PacketHeaderSize = 4;   // GT TCP 1.0 protocol has 4 bytes for packet length
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

        override public bool Active { get { return handle != null; } }

        virtual public void Dispose()
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

            DebugUtils.DumpMessage(this + "SendPacket", buffer, offset, length);
            Debug.Assert(PacketHeaderSize == 4);
            byte[] wireFormat = new byte[length + PacketHeaderSize];
            BitConverter.GetBytes(length).CopyTo(wireFormat, 0);
            Array.Copy(buffer, offset, wireFormat, PacketHeaderSize, length);

            outstanding.Add(wireFormat);
            FlushOutstandingPackets();
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(Stream output)
        {
            if (!Active) { throw new InvalidStateException("Cannot send on disposed transport"); }
            if (!(output is MemoryStream))  // could check header bytes that it's the one we provided
            {
                throw new ArgumentException("Transport provided different stream!");
            }
            MemoryStream ms = (MemoryStream)output;
            DebugUtils.DumpMessage(this + ": SendPacket(stream)", ms.ToArray(), PacketHeaderSize,
                (int)(ms.Length - PacketHeaderSize));
            ms.Position = 0;
            Debug.Assert(PacketHeaderSize == 4);
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
                    DebugUtils.WriteLine(this + ": Flush: " + outstanding[0].Length + " bytes");
                    outgoingInProgress = new PacketInProgress(outstanding[0]);
                }
                int bytesSent = handle.Client.Send(outgoingInProgress.data, outgoingInProgress.position,
                    outgoingInProgress.bytesRemaining, SocketFlags.None, out error);
                DebugUtils.WriteLine(Name + ": position=" + outgoingInProgress.position + " bR=" + 
                  outgoingInProgress.bytesRemaining + ": sent " + bytesSent);

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
                    // NotifyError(null, error, this, "The TCP write buffer is full now, but the data will be saved and " +
                    //        "sent soon.  Send less data to reduce perceived latency.");
                    return;
                default:
                    //die, because something terrible happened
                    //dead = true;
                    NotifyError("Failed to Send TCP Message (" + outgoingInProgress.Length + " bytes): " 
                        + outgoingInProgress.data.ToString(), error);
                    return;
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
                        DebugUtils.WriteLine(this + ": CheckIncomingPacket(): ERROR reading from socket: " + error);
                        NotifyError("Error reading TCP data", error);
                        return;
                    }
                    if (bytesReceived == 0)
                    {
                        NotifyError("Received EOF", "EOF");
                        return;
                    }

                    incomingInProgress.Advance(bytesReceived);
                    if (incomingInProgress.bytesRemaining == 0)
                    {
                        if (incomingInProgress.IsMessageHeader())
                        {
                            incomingInProgress = new PacketInProgress(BitConverter.ToInt32(incomingInProgress.data, 0), false);
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
                Console.WriteLine("{0}: CheckIncomingPacket(): SocketException reading from socket: {1}", this, e);
                //if (ErrorEvent != null)
                //    ErrorEvent(e, SocketError.NoRecovery, this, "Updating from TCP connection failed because of a socket exception.");
                NotifyError("Exception reading TCP data", e);
            }
        }

        public override string ToString()
        {
            return "TcpTransport(" + handle.Client.LocalEndPoint + " -> " + handle.Client.RemoteEndPoint + ")";
        }
    }

}