using System;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Net;
using GT;
using System.Diagnostics;
using System.Threading;
using System.Text;

namespace GT
{
    public class TcpClientTransport : BaseClientTransport
    {
        /// <summary>
        /// Allow setting a cap on the maximum TCP message size
        /// as compared to the OS value normally used.
        /// 512 is the historical value supported by GT.
        /// </summary>
        public static int CappedMessageSize = 512;

        protected TcpClient tcpClient;
        protected PacketInProgress incomingInProgress;
        protected PacketInProgress outgoingInProgress;

        //If bits can't be written to the network now, try later
        //We use this so that we don't have to block on the writing to the network
        protected List<byte[]> outstanding = new List<byte[]>();

        public TcpClientTransport(TcpClient c) {
            PacketHeaderSize = 4;   // GT TCP 1.0 protocol has 4 bytes for packet length
            tcpClient = c;
        }

        public override string Name { get { return "TCP"; } }

        public override bool Active
        {
            get { return tcpClient != null; }
        }

        // FIXME: Stop-gap measure until we have QoS descriptors
        public override MessageProtocol MessageProtocol { get { return MessageProtocol.Tcp; } }

        public void Dispose() {
            if (!Active) { return; }
            //kill the connection as best we can
            lock (this)
            {
                try
                {
                    if (tcpClient != null)
                    {
                        tcpClient.Client.LingerState.Enabled = false;
                        tcpClient.Client.Close();
                    }
                }
                catch (SocketException e) {
                    // FIXME: logError(INFORM, "exception thrown when terminating socket", e);
                    Console.WriteLine(this + ": EXCEPTION closing tcp socket: " + e);
                }
                tcpClient = null;
            }
        }

        public override int MaximumPacketSize
        {
            get
            {
                try
                {
                    return Math.Min(CappedMessageSize,
                        tcpClient.Client.SendBufferSize);
                }
                catch (Exception)
                {
                    return CappedMessageSize;
                }
            }
        }
        
        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(byte[] buffer, int offset, int length)
        {
            Debug.Assert(Active, "Cannot send on a disposed client");

            DebugUtils.DumpMessage(this.ToString(), buffer);
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
            Debug.Assert(Active, "Cannot send on a disposed client");
            if (!(output is MemoryStream))
            {
                throw new ArgumentException("This stream is not the transport-provided stream!");
            }
            DebugUtils.DumpMessage(this + ": SendPacket(stream)", ((MemoryStream)output).ToArray());
            MemoryStream ms = (MemoryStream)output;
            ms.Position = 0;
            byte[] lb = BitConverter.GetBytes((int)(ms.Length - PacketHeaderSize));
            Debug.Assert(lb.Length == 4);
            ms.Write(lb, 0, lb.Length);

            //FIXME: should use a PacketInProgress with the stream length
            outstanding.Add(ms.ToArray());
            FlushOutstandingPackets();
        }

        /// <summary> Flushes out old messages that couldn't be sent because of exceptions</summary>
        /// <returns>True if there are bytes that still have to be sent out</returns>
        protected virtual bool FlushOutstandingPackets()
        {
            SocketError error = SocketError.Success;

            try
            {
                while (outstanding.Count > 0)
                {
                    if (outgoingInProgress == null)
                    {
                        //Console.WriteLine("Cli.Flush: " + outstanding[0].Length + " bytes");
                        outgoingInProgress = new PacketInProgress(outstanding[0]);
                    }
                    int bytesSent = tcpClient.Client.Send(outgoingInProgress.data, outgoingInProgress.position, 
                        outgoingInProgress.bytesRemaining, SocketFlags.None, out error);
                    //Console.WriteLine("  position=" + outgoingInProgress.position + " bR=" + outgoingInProgress.bytesRemaining
                    //    + ": sent " + bytesSent);

                    switch (error)
                    {
                    case SocketError.Success:
                        outgoingInProgress.Advance(bytesSent);
                        if (outgoingInProgress.bytesRemaining == 0)
                        {
                            outstanding.RemoveAt(0);
                            outgoingInProgress = null;
                        }
                        break;
                    case SocketError.WouldBlock:
                        //don't die, but try again next time
                        LastError = null;
                        // FIXME: this should *not* be sent as an error!
                        NotifyError(null, error,
                            "The TCP write buffer is full now, but the data will be saved and " +
                            "sent soon.  Send less data to reduce perceived latency.");
                        return true;
                    default:
                        //die, because something terrible happened
                        LastError = null;
                        NotifyError(null, error, "Failed to Send TCP Message (" + outgoingInProgress.Length + " bytes): " + 
                            outgoingInProgress.data.ToString());
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                LastError = e;
                NotifyError(e, SocketError.NoRecovery, "Trying to send saved TCP data failed because of an exception.");
                return true;
            }

            return false;
        }

        /// <summary>Gets one data from the tcp and interprets it.</summary>
        override public void Update()
        {
            Debug.Assert(Active, "Cannot update a disposed client");
            CheckIncomingPackets();
            FlushOutstandingPackets();
        }

        /// <summary>Get one packet from the tcp connection and interpret it.</summary>

        virtual protected void CheckIncomingPackets()
        {
            //if (tcpClient.Available > 0)
            //{
            //    Console.WriteLine(this + ": there appears to be some data available!");
            //}

            SocketError error = SocketError.Success;
            try
            {
                while (tcpClient.Available > 0)
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

                    int bytesReceived = tcpClient.Client.Receive(incomingInProgress.data, incomingInProgress.position,
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
                        Console.WriteLine("{0}: CheckIncomingPacket(): ERROR reading from socket: {1}", this, error);
                        NotifyError(null, SocketError.Fault, "Error reading TCP data header.");
                        return;
                    }
                    if (bytesReceived == 0)
                    {
                        // socket has been closed
                        // Stop(); // ??  Or should we try to reconnect?
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
                NotifyError(e, SocketError.NoRecovery, "Exception occured (not socket exception).");
            }
        }
    }
}
