using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;

namespace GTClient
{
    public class UdpTransport : BaseTransport
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
        protected List<byte[]> udpOut = new List<byte[]>();

        public UdpTransport() : base()
        {
        }

        public override string Name { get { return "UDP"; } }

        public override bool Dead
        {
            get { return udpClient == null; }
        }

        // FIXME: Stop-gap measure until we have QoS descriptors
        public override MessageProtocol MessageProtocol { get { return MessageProtocol.Udp; } }

        public override void Start() {
            if (!Dead) { return; }
            //we are dead, but they want us to live again.  Reconnect!
            Reconnect();
        }

        public override void Stop() {
            if (Dead) { return; }
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
                        error = new Exception("There was a problem connecting to the server you specified. " +
                        "The address or port you provided may be improper, the receiving server may be down, " +
                        "full, or unavailable, or your system's host file may be corrupted. " +
                        "See inner exception for details.", e);
                    }
                }

                if (error != null)
                    throw error;

                //we are now good to go
                udpClient = client;
                Console.WriteLine("Address resolved and contacted.  Now connected to " + endPoint.ToString());
                SendIdentification();
            }
        }

        protected void SendIdentification()
        {
            Console.WriteLine("{0}: sending identification (id={1})", this, server.UniqueIdentity);
            byte[] startMessage = new byte[5];
            startMessage[0] = (byte)'?'; // FIXME: use ASCII encoder, just in case?
            BitConverter.GetBytes(server.UniqueIdentity).CopyTo(startMessage, 1);
            udpClient.Send(startMessage, startMessage.Length);
        }

        public override void SendMessage(byte[] buffer)
        {
            if (!udpClient.Client.Connected)
            {
                throw new NotSupportedException("ERROR: UdpTransport's should always be connected");
            }
            Client.DumpMessage("UDPTransport.SendMessage", buffer);

            //if there is old stuff to send yet, try the old stuff first
            //before sending the new stuff
            if (FlushRemainingBytes())
            {
                Console.WriteLine("{0}: SendMessage({1} bytes): flushing remaining bytes", this, buffer.Length);
                udpOut.Add(buffer); // FIXME: this means <buffer> isn't sent now!  Even if there is capacity!
                return;
            }
            //if it's all clear, send the new stuff

            SocketError error = SocketError.Success; //added
            try
            {
                udpClient.Client.Send(buffer, 0, buffer.Length, SocketFlags.None, out error);
                switch (error)
                {
                    case SocketError.Success:
                        Console.WriteLine("{0}: SendMessage({1} bytes): success", this, buffer.Length);
                        return;
                    case SocketError.WouldBlock:
                        udpOut.Add(buffer);
                        LastError = null;
                        Console.WriteLine("{0}: SendMessage({1} bytes): EWOULDBLOCK", this, buffer.Length);
                        // FIXME: This should not be an error!
                        // FIXME: Does UDP ever cause a WouldBlock?
                        NotifyError(null, error, "The UDP write buffer is full now, but the data will be saved and " +
                                "sent soon.  Send less data to reduce perceived latency.");
                        return;
                    default:
                        udpOut.Add(buffer);
                        LastError = null;
                        Console.WriteLine("{0}: SendMessage({1} bytes): ERROR: {2}", this, buffer.Length, error);
                        NotifyError(null, error, "Failed to Send UDP Message.");
                        return;
                }
            }
            catch (Exception e)
            {
                //something awful happened
                udpOut.Add(buffer);
                LastError = e;
                Console.WriteLine("{0}: SendMessage({1} bytes): Exception: {2}", this, buffer.Length, e);
                NotifyError(e, SocketError.Fault, "Failed to Send UDP Message (" + buffer.Length +
                        " bytes) because of an exception: " + buffer.ToString());
            }

            // FIXME: This should no longer be necessary
            //}
            //else
            //{
            //    //save this data to be sent later
            //    udpOut.Add(buffer);
            //    //request the server for the port to send to
            //    byte[] data = BitConverter.GetBytes((short)(((IPEndPoint)udpClient.Client.LocalEndPoint).Port));
            //    try
            //    {
            //        server.Send(data, (byte)SystemMessageType.UDPPortRequest, MessageType.System, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
            //    }
            //    catch (Exception e)
            //    {
            //        //don't save this or die, but still throw an error
            //        LastError = e;
            //        NotifyError(e, SocketError.Fault, "Failed to Request UDP port from server.");
            //    }
            //}
        }

        /// <summary> Flushes out old messages that couldn't be sent because of exceptions</summary>
        /// <returns>True if there are bytes that still have to be sent out</returns>
        protected virtual bool FlushRemainingBytes()
        {
            byte[] b;
            SocketError error = SocketError.Success;

            try
            {
                while (udpOut.Count > 0 && udpClient.Client.Connected)
                {
                    b = udpOut[0];
                    udpClient.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

                    switch (error)
                    {
                        case SocketError.Success:
                            udpOut.RemoveAt(0);
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
            byte[] buffer, data;
            byte id, type;
            int length, cursor;
            IPEndPoint ep = null;

            try
            {
                //while there are more packets to read
                while (udpClient.Client.Available > 8)
                {
                    buffer = udpClient.Receive(ref ep);
                    cursor = 0;

                    //while there are more messages in this packet
                    while (cursor < buffer.Length)
                    {
                        id = buffer[cursor + 0];
                        type = buffer[cursor + 1];
                        length = BitConverter.ToInt32(buffer, cursor + 4);
                        data = new byte[length];
                        Array.Copy(buffer, 8, data, 0, length);

                        Console.WriteLine("{0}: Update(): received message id:{1} type:{2} #bytes:{3}",
                            this, id, (MessageType)type, length);
                        Client.DumpMessage("UDPTransport.Update", id, (MessageType)type, data);
                        server.Add(new MessageIn(id, (MessageType)type, data, this, server));

                        cursor += length + 8;
                    }
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

        public override int MaximumMessageSize
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

    }
}