using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using GT.Common;
using System.IO;
using System.Diagnostics;

namespace GT.Clients
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

        public UdpClientTransport() { }

        public override string Name { get { return "UDP"; } }

        public override bool Started
        {
            get { return udpClient != null; }
        }

        // FIXME: Stop-gap measure until we have QoS descriptors
        public override MessageProtocol MessageProtocol { get { return MessageProtocol.Udp; } }

        public override void Start() {
            if (Started) { return; }
            //we are dead, but they want us to live again.  Reconnect!
            Reconnect();
        }

        public override void Stop() {
            if (!Started) { return; }
            //kill the connection as best we can
            lock (this)
            {
                try
                {
                    // udpClient.Client.LingerState.Enabled = false; // FIXME: verify not supported on UDP
                    udpClient.Close();
                }
                catch (SocketException e)
                {
                    // FIXME: logError(INFORM, "exception thrown when terminating socket", e);
                    Console.WriteLine(this + ": EXCEPTION thrown when terminating up socket: " + e);
                }
                udpClient = null;
            }
        }

        /// <summary>Reset the superstream and reconnect to the server (blocks)</summary>
        protected virtual void Reconnect()
        {
            lock (this)
            {
                if (udpClient != null) { return; }

                IPHostEntry he = Dns.GetHostEntry(address);
                IPAddress[] addr = he.AddressList;
                UdpClient client = new UdpClient();

                //try to connect to the address
                Exception error = null;
                for (int i = 0; i < addr.Length; i++)
                {
                    try
                    {
                        endPoint = new IPEndPoint(addr[0], Int32.Parse(port));
                        client = new UdpClient();
                        client.DontFragment = true; // FIXME: what are the implications of setting this flag?
                        client.Client.Blocking = false;
                        client.Client.SendTimeout = 1;
                        client.Client.ReceiveTimeout = 1;
                        client.Connect(endPoint);
                        error = null;
                        break;
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }
                }

                if (error != null)
                {
                    throw new CannotConnectToRemoteException(error);
                }

                //we are now good to go
                udpClient = client;
                Console.WriteLine("Address resolved and contacted.  Now connected to " + endPoint.ToString());
                SendIdentification();
            }
        }

        protected void SendIdentification()
        {
            // FIXME: need a proper way of doing a handshake!
            DebugUtils.WriteLine(this + ": sending identification id=" + server.UniqueIdentity);
            byte[] startMessage = new byte[5];
            startMessage[0] = (byte)'?'; // FIXME: use ASCII encoder, just in case?
            BitConverter.GetBytes(server.UniqueIdentity).CopyTo(startMessage, 1);
            udpClient.Send(startMessage, startMessage.Length);
        }

        public override void SendPacket(byte[] buffer, int offset, int length)
        {
            if (!Started)
            {
                throw new InvalidStateException("Cannot send on a stopped client", this);
            } else if (!udpClient.Client.Connected)
            {
                throw new NotSupportedException("ERROR: UdpTransport's should always be connected");
            }
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
            try
            {
                //while there are more packets to read
                while (udpClient.Client.Available > 8)
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
            if (!Started)
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
