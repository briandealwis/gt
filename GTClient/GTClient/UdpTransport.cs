using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using GT.Net;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace GT.Net
{
    public class UdpClientTransport : BaseClientTransport
    {
        /// <summary>
        /// Allow setting a cap on the maximum UDP message size
        /// as compared to the OS value normally used.
        /// 512 is the historical value supported by GT.
        /// </summary>
        public static int CappedMessageSize = 512;

        protected UdpClient udpClient;

        //If bits can't be written to the network now, try later
        //We use this so that we don't have to block on the writing to the network
        protected List<byte[]> outstanding = new List<byte[]>();

        public UdpClientTransport(UdpClient udpc) {
            udpClient = udpc;
        }

        public override string Name { get { return "UDP"; } }

        public override bool Active
        {
            get { return udpClient != null; }
        }

        // FIXME: Stop-gap measure until we have QoS descriptors
        public override MessageProtocol MessageProtocol { get { return MessageProtocol.Udp; } }

        public void Dispose() {
            if (!Active) { return; }
            //kill the connection as best we can
            lock (this)
            {
                try
                {
                    // udpClient.Client.LingerState.Enabled = false; // FIXME: verify not supported on UDP
                    if (udpClient != null) { udpClient.Close(); }
                }
                catch (SocketException e)
                {
                    // FIXME: logError(INFORM, "exception thrown when terminating socket", e);
                    Console.WriteLine(this + ": EXCEPTION thrown when terminating up socket: " + e);
                }
                udpClient = null;
            }
        }


        public override void SendPacket(byte[] buffer, int offset, int length)
        {
            if (!Active)
            {
                throw new InvalidStateException("Cannot send on a stopped client", this);
            }
            Debug.Assert(udpClient.Client.Connected, "ERROR: UdpTransport's should always be connected");
            DebugUtils.DumpMessage(this + "SendPacket", buffer);
            if (offset != 0 || length != buffer.Length)
            {
                byte[] newBuffer = new byte[length];
                Array.Copy(buffer, offset, newBuffer, 0, length);
                buffer = newBuffer;
            }
            outstanding.Add(buffer);
            FlushOutstandingPackets();        
        }

        /// <summary> Flushes out old messages that couldn't be sent because of exceptions</summary>
        /// <returns>True if there are bytes that still have to be sent out</returns>
        protected virtual bool FlushOutstandingPackets()
        {
            byte[] b;
            SocketError error = SocketError.Success;

            try
            {
                while (outstanding.Count > 0 && udpClient.Client.Connected)
                {
                    b = outstanding[0];
                    udpClient.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

                    switch (error)
                    {
                        case SocketError.Success:
                            outstanding.RemoveAt(0);
                            break;
                        case SocketError.WouldBlock:
                            //don't die, but try again next time
                            LastError = null;
                            // FIXME: This should not be an error!
                            // FIXME: Does UDP ever cause a WouldBlock?
                            NotifyError(null, error, "The UDP write buffer is full now, but the data will be saved and " +
                                    "sent soon.  Send less data to reduce perceived latency.");
                            return true;
                        default:
                            //something terrible happened, but this is only UDP, so stick around.
                            LastError = null;
                            NotifyError(null, error, "Failed to Send UDP Message (" + b.Length + " bytes): " + b.ToString());
                            return true;
                    }
                }
            }
            catch (Exception e)
            {
                LastError = e;
                NotifyError(e, SocketError.NoRecovery, "Trying to send saved UDP data failed because of an exception.");
                return true;
            }

            return false;
        }

        /// <summary>Get data from the UDP connection and interpret them.</summary>
        public override void Update()
        {
            Debug.Assert(Active, "Cannot update an inactive client");

            try
            {
                //while there are more packets to read
                while (udpClient.Client.Available > 0)
                {
                    IPEndPoint ep = null;
                    byte[] buffer = udpClient.Receive(ref ep);

                    DebugUtils.DumpMessage(this + ": Update()", buffer);
                    Debug.Assert(ep.Equals(udpClient.Client.RemoteEndPoint));
                    NotifyPacketReceived(buffer, 0, buffer.Length);
                }
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.WouldBlock)
                {
                    LastError = e;
                    NotifyError(e, SocketError.NoRecovery, "Updating from UDP failed because of a socket exception.");
                }
            }
            catch (Exception e)
            {
                LastError = e;
                NotifyError(e, SocketError.Fault, "UDP Data Interpretation Error.  There must have been a mistake in a message header. " +
                        "Data has been lost.");
                //Don't die on a mistake interpreting the data, because we can always 
                //try to interpret more data starting from a new set of packets. 
                //However, the data we were currently working on is lost.
                return;
            }
        }

        public override int MaximumPacketSize
        {
            get
            {
                try
                {
                    return Math.Min(CappedMessageSize, 
                        udpClient.Client.SendBufferSize);
                }
                catch (Exception)
                {
                    return CappedMessageSize;
                }
            }
        }

        override public Stream GetPacketStream()
        {
            MemoryStream ms = new MemoryStream();
            return ms;
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendPacket(Stream ms)
        {
            if (!Active)
            {
                throw new InvalidStateException("Cannot send on a stopped client", this);
            }
            if (!(ms is MemoryStream))
            {
                throw new ArgumentException("Transport provided different stream!");
            }
            MemoryStream output = (MemoryStream)ms;
            outstanding.Add(output.ToArray());
            FlushOutstandingPackets();
        }

    }
}
