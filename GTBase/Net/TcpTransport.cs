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
        private EndPoint remoteEndPoint;
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

        public override int MaximumPacketSize
        {
            get { return CappedMessageSize; }
        }

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
                Console.WriteLine("{0} Warning: exception when closing TCP handle: {1}",
                    DateTime.Now, e);
            }
            handle = null;
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(byte[] buffer, int offset, int length)
        {
            InvalidStateException.Assert(Active, "Cannot send on disposed transport", this);
            ContractViolation.Assert(length > 0, "Cannot send 0-byte messages!");
            ContractViolation.Assert(length - PacketHeaderSize <= MaximumPacketSize, String.Format(
                    "Packet exceeds transport capacity: {0} > {1}", length - PacketHeaderSize, MaximumPacketSize));

            DebugUtils.DumpMessage(this + "SendPacket", buffer, offset, length);
            Debug.Assert(PacketHeaderSize > 0);
            byte[] wireFormat = new byte[length + PacketHeaderSize];
            BitConverter.GetBytes(length).CopyTo(wireFormat, 0);
            Array.Copy(buffer, offset, wireFormat, PacketHeaderSize, length);

            lock (this)
            {
                outstanding.Enqueue(wireFormat);
            }
            FlushOutstandingPackets();
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(Stream output)
        {
            InvalidStateException.Assert(Active, "Cannot send on disposed transport", this);
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
            lock (this)
            {
                outstanding.Enqueue(ms.ToArray());
            }
            FlushOutstandingPackets();
        }

        virtual protected void FlushOutstandingPackets()
        {
            SocketError error = SocketError.Success;
            lock (this)
            {
                while (outstanding.Count > 0)
                {
                    if (outgoingInProgress == null)
                    {
                        //DebugUtils.WriteLine(this + ": Flush: " + outstanding.Peek().Length + " bytes");
                        outgoingInProgress = new PacketInProgress(outstanding.Peek());
                        Debug.Assert(outgoingInProgress.Length > 0);
                    }
                    int bytesSent = handle.Client.Send(outgoingInProgress.data,
                        outgoingInProgress.position,
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
                        throw new TransportBackloggedWarning(this);

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
            byte[] packet;
            while ((packet = FetchIncomingPacket()) != null)
            {
                NotifyPacketReceived(packet, 0, packet.Length);
            }
        }

        virtual protected byte[] FetchIncomingPacket()
        {
            lock (this)
            {
                SocketError error = SocketError.Success;
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

                    int bytesReceived = handle.Client.Receive(incomingInProgress.data,
                        incomingInProgress.position,
                        incomingInProgress.bytesRemaining, SocketFlags.None, out error);
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

                    incomingInProgress.Advance(bytesReceived);
                    if (incomingInProgress.bytesRemaining == 0)
                    {
                        if (incomingInProgress.IsMessageHeader())
                        {
                            int payloadLength = BitConverter.ToInt32(incomingInProgress.data, 0);
                            if (payloadLength <= 0)
                            {
                                Console.WriteLine(
                                    "{0}: WARNING: received packet with 0-byte payload!", this);
                                incomingInProgress = null;
                            }
                            else
                            {
                                incomingInProgress = new PacketInProgress(payloadLength, false);
                            }
                        }
                        else
                        {
                            DebugUtils.DumpMessage(this + ": CheckIncomingMessage",
                                incomingInProgress.data);
                            byte[] packet = incomingInProgress.data;
                            incomingInProgress = null;
                            return packet;
                        }
                    }
                }
            }
            return null;
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
