using System;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Net;
using GT.Common;

namespace GT.Clients
{
    public class TcpTransport : BaseTransport
    {
        /// <summary>
        /// Allow setting a cap on the maximum UDP message size
        /// as compared to the OS value normally used.
        /// 512 is the historical value supported by GT.
        /// </summary>
        public static int CappedMessageSize = 512;

        protected TcpClient tcpClient;
        protected MemoryStream tcpIn;
        protected int tcpInBytesLeft;
        protected int tcpInMessageType;
        protected byte tcpInID;

        //If bits can't be written to the network now, try later
        //We use this so that we don't have to block on the writing to the network
        protected List<byte[]> tcpOut = new List<byte[]>();

        public TcpTransport() : base() {
        }

        public override string Name { get { return "TCP"; } }

        public override bool Started
        {
            get { return tcpClient != null; }
        }

        // FIXME: Stop-gap measure until we have QoS descriptors
        public override MessageProtocol MessageProtocol { get { return MessageProtocol.Tcp; } }

        public override void Start() {
            if(Started) { return; }
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
                    tcpClient.Client.LingerState.Enabled = false;
                    tcpClient.Client.Close();
                }
                catch (SocketException e) {
                    // FIXME: logError(INFORM, "exception thrown when terminating socket", e);
                    Console.WriteLine(this + ": EXCEPTION closing tcp socket: " + e);
                }
                tcpClient = null;
            }
        }

        protected void Restart()
        {
            Stop(); Start();
        }

        public override int MaximumMessageSize
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

        /// <summary>Reset the superstream and reconnect to the server (blocks)</summary>
        protected virtual void Reconnect()
        {
            lock (this)
            {
                if (tcpClient != null) { return; }

                tcpIn = new MemoryStream();
                IPHostEntry he = Dns.GetHostEntry(address);
                IPAddress[] addr = he.AddressList;
                TcpClient client = null;

                //try to connect to the address
                Exception error = null;
                for (int i = 0; i < addr.Length; i++)
                {
                    try
                    {
                        endPoint = new IPEndPoint(addr[0], Int32.Parse(port));
                        client = new TcpClient();
                        client.NoDelay = true;
                        client.ReceiveTimeout = 1;
                        client.SendTimeout = 1;
                        client.Connect(endPoint);
                        client.Client.Blocking = false;
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

                Console.WriteLine("Address resolved and contacted.  Now connected to " + endPoint.ToString());

                //we are now good to go
                tcpClient = client;
            }
        }

        /// <summary>Send a message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        public override void SendMessage(byte[] buffer)
        {
            DebugUtils.DumpMessage("TCPTransport.SendMessage", buffer);
            if (FlushRemainingBytes())
            {
                Console.WriteLine("{0}: SendMessage({1} bytes): outstanding bytes; adding message to queue", this, buffer.Length);
                tcpOut.Add(buffer); // FIXME: this means buffer isn't sent until next send, even if there is capacity!
                return;
            }

            SocketError error = SocketError.Success; //added
            try
            {
                Console.WriteLine("{0}: SendMessage({1} bytes): sending bytes...", this, buffer.Length);
                tcpClient.Client.Send(buffer, 0, buffer.Length, SocketFlags.None, out error);

                switch (error)
                {
                    case SocketError.Success:
                        Console.WriteLine("{0}: SendMessage({1} bytes): success", this, buffer.Length);
                        return;
                    case SocketError.WouldBlock:
                        tcpOut.Add(buffer);
                        LastError = null;
                        Console.WriteLine("{0}: SendMessage({1} bytes): EWOULDBLOCK: adding to outstanding bytes", this, buffer.Length);
                        // FIXME: this should *not* be sent as an error!
                        NotifyError(null, error,
                            "The TCP write buffer is full now, but the data will be saved and " +
                            "sent soon.  Send less data to reduce perceived latency.");
                        return;
                    default:
                        tcpOut.Add(buffer);
                        LastError = null;
                        Console.WriteLine("{0}: SendMessage({1} bytes): ERROR sending: {2}", this, buffer.Length, error);
                        NotifyError(null, error, "Failed to Send TCP Message.");
                        Restart();
                        return;
                }
            }
            catch (Exception e)
            {
                //something awful happened
                tcpOut.Add(buffer);
                LastError = e;
                Console.WriteLine("{0}: SendMessage({1} bytes): EXCEPTION sending: {2}", this, buffer.Length, e);
                NotifyError(e, SocketError.NoRecovery, "Failed to Send to TCP.  See Exception for details.");
                Restart();
            }
        }


        /// <summary> Flushes out old messages that couldn't be sent because of exceptions</summary>
        /// <returns>True if there are bytes that still have to be sent out</returns>
        protected virtual bool FlushRemainingBytes()
        {
            byte[] b;
            SocketError error = SocketError.Success;

            try
            {
                while (tcpOut.Count > 0)
                {
                    b = tcpOut[0];
                    tcpClient.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

                    switch (error)
                    {
                        case SocketError.Success:
                            tcpOut.RemoveAt(0);
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
                            NotifyError(null, error, "Failed to Send TCP Message (" + b.Length + " bytes): " + b.ToString());
                            Restart();
                            return true;
                    }


                }
            }
            catch (Exception e)
            {
                LastError = e;
                NotifyError(e, SocketError.NoRecovery, "Trying to send saved TCP data failed because of an exception.");
                Restart();
                return true;
            }

            return false;
        }

        /// <summary>Get one message from the tcp connection and interpret it.</summary>
        public override void Update()
        {
            byte[] buffer;
            int size;
            SocketError error;

            // FIXME: if a socket is closed prematurely then we may never
            // get >= 8 bytes.  This should be reimplemented as a state
            // machine.
            if (tcpInMessageType < 1)
            {
                //restart the counters to listen for a new message.
                if (tcpClient.Available < 8)
                    return;

                buffer = new byte[8];
                size = tcpClient.Client.Receive(buffer, 0, 8, SocketFlags.None, out error);
                switch (error)
                {
                    case SocketError.Success:
                        Console.WriteLine("{0}: Update(): read header ({1} bytes)", this, size);
                        break;
                    default:
                        LastError = null;
                        Console.WriteLine("{0}: Update(): error reading header: {1}", this, error);
                        NotifyError(null, error, "TCP Read Header Error.  See error for details.");
                        Restart();
                        return;
                }
                byte id = buffer[0];
                byte type = buffer[1];
                int length = BitConverter.ToInt32(buffer, 4);

                this.tcpInBytesLeft = length;
                this.tcpInMessageType = type;
                this.tcpInID = id;
                tcpIn.Position = 0;
            }

            buffer = new byte[MaximumMessageSize];
            int amountToRead = Math.Min(tcpInBytesLeft, buffer.Length);

            size = tcpClient.Client.Receive(buffer, 0, amountToRead, SocketFlags.None, out error);
            switch (error)
            {
                case SocketError.Success:
                    Console.WriteLine("{0}: Update(): read body ({1} bytes)", this, size);
                    break;
                default:
                    LastError = null;
                    Console.WriteLine("{0}: Update(): ERROR reading body: {1}", this, error);
                    NotifyError(null, error, "TCP Read Data Error.  See Exception for details.");
                    Restart();
                    return;
            }
            tcpInBytesLeft -= size;
            tcpIn.Write(buffer, 0, size);

            if (tcpInBytesLeft == 0)
            {
                //We have the entire message.  Pass it along.
                buffer = new byte[tcpIn.Position];
                tcpIn.Position = 0;
                tcpIn.Read(buffer, 0, buffer.Length);

                Console.WriteLine("{0}: Update(): received message id:{1} type:{2} #bytes:{3}", 
                    this, tcpInID, (MessageType)tcpInMessageType, buffer.Length);
                DebugUtils.DumpMessage("TCPTransport.Update", tcpInID, (MessageType)tcpInMessageType, buffer);
                server.Add(new MessageIn(tcpInID, (MessageType)tcpInMessageType, buffer, this, server));

                tcpInMessageType = 0;
            }
        }

    }
}
