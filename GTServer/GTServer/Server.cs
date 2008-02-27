using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using GT.Common;

namespace GT.Servers
{

    #region Delegates

    /// <summary>Handles a tick event, which is one loop of the server</summary>
    public delegate void TickHandler();

    /// <summary>Handles a Message event, when a data arrives</summary>
    /// <param name="id">The id of the channel the data was sent along</param>
    /// <param name="type">The type of data sent</param>
    /// <param name="data">The data sent</param>
    /// <param name="client">Who sent the data</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void MessageHandler(byte id, MessageType type, byte[] data, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a SessionMessage event, when a SessionMessage arrives.</summary>
    /// <param name="e">The action performed.</param>
    /// <param name="id">The id of the channel the data was sent along.</param>
    /// <param name="client">Who sent the data.</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void SessionMessageHandler(SessionAction e, byte id, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a StringMessage event, when a StringMessage arrives.</summary>
    /// <param name="s">The string sent.</param>
    /// <param name="id">The id of the channel the data was sent along.</param>
    /// <param name="client">Who sent the data.</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void StringMessageHandler(byte[] s, byte id, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a ObjectMessage event, when a ObjectMessage arrives.</summary>
    /// <param name="o">The object sent.</param>
    /// <param name="id">The id of the channel the data was sent along.</param>
    /// <param name="client">Who sent the data.</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void ObjectMessageHandler(byte[] o, byte id, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a BinaryMessage event, when a BinaryMessage arrives.</summary>
    /// <param name="b">The byte array sent.</param>
    /// <param name="id">The id of the channel the data was sent along.</param>
    /// <param name="client">Who sent the data.</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void BinaryMessageHandler(byte[] b, byte id, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles when clients leave the server.</summary>
    /// <param name="list">The clients who've left.</param>
    public delegate void ClientsRemovedHandler(List<Server.Client> list);

    /// <summary>Handles when clients join the server.</summary>
    /// <param name="list">The clients who've joined.</param>
    public delegate void ClientsJoinedHandler(List<Server.Client> list);

    /// <summary>Handles when there is an internal error that the application should know about.</summary>
    /// <param name="e">The exception that occurred</param>
    /// <param name="se">The type of networking error</param>
    /// <param name="c">The client where the exception occurred</param>
    /// <param name="explanation">An explanation of the error encountered</param>
    public delegate void ErrorClientHandler(Exception e, SocketError se, Server.Client c, string explanation);


    #endregion


    /// <summary>Represents a server.</summary>
    public class Server
    {
        #region Variables and Properties

        /// <summary>The listening backlog to use for the server socket.  Historically
        /// the maximum was 5; some newer OS' support up to 128.</summary>
        public static int LISTENER_BACKLOG = 10;

        private static Random random = new Random();
        private static BinaryFormatter formatter = new BinaryFormatter();

        private bool running = false;
        private TcpListener bouncer;
        private UdpMultiplexer udpMultiplexor;

        private int lastPingTime = 0;
        private int port;

        /// <summary>Last exception encountered.</summary>
        public Exception LastError = null;

        /// <summary>All of the clients that this server knows about.</summary>
        public List<Client> clientList = new List<Client>();

        /// <summary>All of the clientIDs that this server knows about.  
        /// Hide this so that users cannot cause mischief.  I accept that this list may 
        /// not be accurate because the users have direct access to the clientList.</summary>
        private Dictionary<int, Client> clientIDs = new Dictionary<int, Client>();

        /// <summary>Time in milliseconds between keep-alive pings.</summary>
        public int PingInterval = 10000;

        /// <summary>Time in milliseconds between server ticks.</summary>
        public int Interval = 10;

        #endregion

        #region Events

        /// <summary>Invoked each cycle of the server.</summary>
        public event TickHandler Tick;

        /// <summary>Invoked each time a data is received.</summary>
        public event MessageHandler MessageReceived;

        /* Note: the specialized data handlers are provided the data payloads
         * as raw uninterpreted byte arrays so as to avoid introducing latency
         * from unneeded latency.  If your server needs to use
         * the data content, then the server should perform the interpretation. */

        /// <summary>Invoked each time a session data is received.</summary>
        public event SessionMessageHandler SessionMessageReceived;

        /// <summary>Invoked each time a string data is received.
        /// Strings are encoded as bytes and can be decoded using variants of BytesToString().</summary>
        public event StringMessageHandler StringMessageReceived;

        /// <summary>Invoked each time a object data is received.
        /// Objects are encoded as bytes and can be decoded using variants of BytesToObject().</summary>
        /// FIXME: document the format.</summary>
        public event ObjectMessageHandler ObjectMessageReceived;

        /// <summary>Invoked each time a binary data is received.</summary>
        public event BinaryMessageHandler BinaryMessageReceived;

        /// <summary>Invoked each time a client disconnects.</summary>
        public event ClientsRemovedHandler ClientsRemoved;

        /// <summary>Invoked each time a client connects.</summary>
        public event ClientsJoinedHandler ClientsJoined;

        /// <summary>Invoked each time an error occurs.</summary>
        public event ErrorClientHandler ErrorEvent;

        #endregion

        #region Static

        /// <summary>Converts bytes into a string, in a way consistant with the clients and this server.</summary>
        /// <param name="b">The bytes</param>
        /// <returns>The string</returns>
        public static string BytesToString(byte[] b)
        {
            return System.Text.ASCIIEncoding.ASCII.GetString(b);
        }

        /// <summary>Converts bytes into a string, in a way consistant with the clients and this server.</summary>
        /// <param name="b">The bytes</param>
        /// <param name="index">Where to start in the array</param>
        /// <param name="length">How many *bytes* to decode</param>
        /// <returns>The string</returns>
        public static string BytesToString(byte[] b, int index, int length)
        {
            return System.Text.ASCIIEncoding.ASCII.GetString(b, index, length);
        }

        /// <summary>Converts a string into bytes, in a way consistant with the clients and this server.</summary>
        /// <param name="s">The string</param>
        /// <returns>The bytes</returns>
        public static byte[] StringToBytes(string s)
        {
            return System.Text.ASCIIEncoding.ASCII.GetBytes(s);
        }

        /// <summary>Converts bytes into an object, in a way consistant with the clients and this server.</summary>
        /// <param name="b">The bytes</param>
        /// <returns>The object</returns>
        public static object BytesToObject(byte[] b)
        {
            MemoryStream ms = new MemoryStream(b);
            ms.Position = 0;
            return Server.formatter.Deserialize(ms);
        }

        /// <summary>Converts an object into bytes, in a way consistant with the clients and this server.</summary>
        /// <param name="o">The object</param>
        /// <returns>The bytes</returns>
        public static byte[] ObjectToBytes(object o)
        {
            MemoryStream ms = new MemoryStream();
            Server.formatter.Serialize(ms, o);
            byte[] buffer = new byte[ms.Position];
            ms.Position = 0;
            ms.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        /// <summary>Converts a remote tuple into bytes in a system-consistent way</summary>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <typeparam name="C"></typeparam>
        /// <param name="tuple"></param>
        /// <param name="clientID"></param>
        /// <returns></returns>
        public static byte[] RemoteTupleToBytes<A, B, C>(RemoteTuple<A, B, C> tuple, int clientID)
            where A : IConvertible
            where B : IConvertible
            where C : IConvertible
        {
            MemoryStream ms = new MemoryStream(28);  //the maximum size this tuple could possibly be
            byte[] b;

            //convert values into bytes
            b = Converter<A>(tuple.X);
            ms.Write(b, 0, b.Length);
            b = Converter<B>(tuple.Y);
            ms.Write(b, 0, b.Length);
            b = Converter<C>(tuple.Z);
            ms.Write(b, 0, b.Length);

            //along with whose tuple it is
            ms.Write(BitConverter.GetBytes(clientID), 0, 4);

            b = new byte[ms.Position];
            ms.Position = 0;
            ms.Read(b, 0, b.Length);

            return b;
        }

        /// <summary>Converts bytes into a remote tuple in a system-consistent way</summary>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <typeparam name="C"></typeparam>
        /// <param name="b">The bytes to be converted</param>
        /// <param name="clientID"></param>
        /// <returns></returns>
        public static RemoteTuple<A, B, C> BytesToRemoteTuple<A, B, C>(byte[] b, out int clientID)
            where A : IConvertible
            where B : IConvertible
            where C : IConvertible
        {
            int cursor, length;
            RemoteTuple<A, B, C> tuple = new RemoteTuple<A, B, C>();

            tuple.X = Converter<A>(b, 0, out length);
            cursor = length;
            tuple.Y = Converter<B>(b, cursor, out length);
            cursor += length;
            tuple.Z = Converter<C>(b, cursor, out length);
            cursor += length;

            clientID = BitConverter.ToInt32(b, cursor);

            return tuple;
        }

        /// <summary>Converts a remote tuple into bytes in a system-consistent way</summary>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <param name="tuple"></param>
        /// <param name="clientID"></param>
        /// <returns></returns>
        public static byte[] RemoteTupleToBytes<A, B>(RemoteTuple<A, B> tuple, int clientID)
            where A : IConvertible
            where B : IConvertible
        {
            MemoryStream ms = new MemoryStream(28);  //the maximum size this tuple could possibly be
            byte[] b;

            //convert values into bytes
            b = Converter<A>(tuple.X);
            ms.Write(b, 0, b.Length);
            b = Converter<B>(tuple.Y);
            ms.Write(b, 0, b.Length);

            //along with whose tuple it is
            ms.Write(BitConverter.GetBytes(clientID), 0, 4);

            b = new byte[ms.Position];
            ms.Position = 0;
            ms.Read(b, 0, b.Length);

            return b;
        }

        /// <summary>Converts bytes into a remote tuple in a system-consistent way</summary>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <param name="b">The bytes to be converted</param>
        /// <param name="clientID"></param>
        /// <returns></returns>
        public static RemoteTuple<A, B> BytesToRemoteTuple<A, B>(byte[] b, out int clientID)
            where A : IConvertible
            where B : IConvertible
        {
            int cursor, length;
            RemoteTuple<A, B> tuple = new RemoteTuple<A, B>();

            tuple.X = Converter<A>(b, 0, out length);
            cursor = length;
            tuple.Y = Converter<B>(b, cursor, out length);
            cursor += length;

            clientID = BitConverter.ToInt32(b, cursor);

            return tuple;
        }

        /// <summary>Converts a remote tuple into bytes in a system-consistent way</summary>
        /// <typeparam name="A"></typeparam>
        /// <param name="tuple"></param>
        /// <param name="clientID"></param>
        /// <returns></returns>
        public static byte[] RemoteTupleToBytes<A>(RemoteTuple<A> tuple, int clientID)
            where A : IConvertible
        {
            MemoryStream ms = new MemoryStream(28);  //the maximum size this tuple could possibly be
            byte[] b;

            //convert values into bytes
            b = Converter<A>(tuple.X);
            ms.Write(b, 0, b.Length);

            //along with whose tuple it is
            ms.Write(BitConverter.GetBytes(clientID), 0, 4);

            b = new byte[ms.Position];
            ms.Position = 0;
            ms.Read(b, 0, b.Length);

            return b;
        }

        /// <summary>Converts bytes into a remote tuple in a system-consistent way</summary>
        /// <typeparam name="A"></typeparam>
        /// <param name="b">The bytes to be converted</param>
        /// <param name="clientID"></param>
        /// <returns></returns>
        public static RemoteTuple<A> BytesToRemoteTuple<A>(byte[] b, out int clientID)
            where A : IConvertible
        {
            int cursor, length;
            RemoteTuple<A> tuple = new RemoteTuple<A>();

            tuple.X = Converter<A>(b, 0, out length);
            cursor = length;

            clientID = BitConverter.ToInt32(b, cursor);

            return tuple;
        }

        /// <summary>Converts a IConvertible type into a byte array.</summary>
        /// <typeparam name="A">The IConvertible type.</typeparam>
        /// <param name="value">The value</param>
        /// <returns></returns>
        private static byte[] Converter<A>(A value)
            where A : IConvertible
        {
            switch (Type.GetTypeCode(typeof(A)))
            {
                case TypeCode.Byte: byte[] b = new byte[1]; b[0] = value.ToByte(null); return b;
                case TypeCode.Char: return BitConverter.GetBytes(value.ToChar(null));
                case TypeCode.Single: return BitConverter.GetBytes(value.ToSingle(null));
                case TypeCode.Double: return BitConverter.GetBytes(value.ToDouble(null));
                case TypeCode.Int16: return BitConverter.GetBytes(value.ToInt16(null));
                case TypeCode.Int32: return BitConverter.GetBytes(value.ToInt32(null));
                case TypeCode.Int64: return BitConverter.GetBytes(value.ToInt64(null));
                case TypeCode.UInt16: return BitConverter.GetBytes(value.ToUInt16(null));
                case TypeCode.UInt32: return BitConverter.GetBytes(value.ToUInt32(null));
                case TypeCode.UInt64: return BitConverter.GetBytes(value.ToUInt64(null));
                default: return BitConverter.GetBytes(value.ToDouble(null)); //if not recognized, make it a double
            }
        }

        /// <summary>Converts a portion of a byte array into some IConvertible type.</summary>
        /// <typeparam name="A">The IConvertible type to convert the byte array into.</typeparam>
        /// <param name="b">The byte array</param>
        /// <param name="index">The byte in the array at which to begin</param>
        /// <param name="length">The length of the type</param>
        /// <returns>The converted type.</returns>
        private static A Converter<A>(byte[] b, int index, out int length)
            where A : IConvertible
        {
            switch (Type.GetTypeCode(typeof(A)))
            {
                case TypeCode.Byte: length = 1; return (A)Convert.ChangeType(b[index], typeof(Byte));
                case TypeCode.Char: length = 2; return (A)Convert.ChangeType(BitConverter.ToChar(b, index), typeof(Char));
                case TypeCode.Single: length = 4; return (A)Convert.ChangeType(BitConverter.ToSingle(b, index), typeof(Single));
                case TypeCode.Double: length = 8; return (A)Convert.ChangeType(BitConverter.ToDouble(b, index), typeof(Double));
                case TypeCode.Int16: length = 2; return (A)Convert.ChangeType(BitConverter.ToInt16(b, index), typeof(Int16));
                case TypeCode.Int32: length = 4; return (A)Convert.ChangeType(BitConverter.ToInt32(b, index), typeof(Int32));
                case TypeCode.Int64: length = 8; return (A)Convert.ChangeType(BitConverter.ToInt64(b, index), typeof(Int64));
                case TypeCode.UInt16: length = 2; return (A)Convert.ChangeType(BitConverter.ToUInt16(b, index), typeof(UInt16));
                case TypeCode.UInt32: length = 4; return (A)Convert.ChangeType(BitConverter.ToUInt32(b, index), typeof(UInt32));
                case TypeCode.UInt64: length = 8; return (A)Convert.ChangeType(BitConverter.ToUInt64(b, index), typeof(UInt64));
                default: length = 8; return (A)Convert.ChangeType(BitConverter.ToDouble(b, index), typeof(Double)); //if not recognized, make it a double
            }
        }

        #endregion

        #region Vital Server Mechanics

        /// <summary>Creates a new Server object.</summary>
        /// <param name="port">The port to listen on.</param>
        public Server(int port)
        {
            this.port = port;
        }

        /// <summary>Creates a new Server object.</summary>
        /// <param name="port">The port to listen on.</param>
        /// <param name="interval">The interval in milliseconds at which to check 
        /// for new connections or new data.</param>
        public Server(int port, int interval)
        {
            this.port = port;
            this.Interval = interval;
        }


        /// <summary>Starts a new thread that listens for new clients or new data.
        /// Abort returned thread at any time to stop listening.
        /// <param name="interval">The interval in milliseconds at which to check 
        /// for new connections or new data.</param> </summary>
        public Thread StartSeparateListeningThread(int interval)
        {
            this.Interval = interval;

            Thread t = new Thread(new ThreadStart(StartListening));
            t.Name = "Server Thread[" + this.ToString() + "]";
            t.IsBackground = true;
            t.Start();
            return t;
        }

        private void ErrorClientHandlerMethod(Exception e, SocketError se, Client client, string ex)
        {
            if (ErrorEvent != null)
                ErrorEvent(e, se, client, ex);
        }

        public string ToString()
        {
            return "port=" + port + ", " + clientList.Count + " clients";
        }

        /// <summary>One tick of the server, manually
        /// Use if not using Start
        /// </summary>
        public void Update()
        {
            Console.WriteLine(this + ": Server.Update(): shall we take a turn about the room?");

            if (bouncer == null)
                RestartBouncers();
            else
            {
                //add new clients
                CheckNewTcpClients();

                Console.WriteLine(this + ": Server.Update(): checking udpMultiplexor");
                udpMultiplexor.Update();
            }


            //ping, if needed
            if (lastPingTime + PingInterval < System.Environment.TickCount)
            {
                Console.WriteLine(this + ": Server.Update(): pinging existing clients (" + clientList.Count + ")");
                lastPingTime = System.Environment.TickCount;
                foreach (Client c in clientList)
                    c.Ping();
            }

            Console.WriteLine(this + ": Server.Update(): checking existing clients");
            //update all clients, reading from the network
            foreach (Client c in clientList)
            {
                c.Update();
            }

            //if anyone is listening, tell them we're done one cycle
            if (Tick != null)
                Tick();

            //remove dead clients
            List<Client> listD = clientList.FindAll(Client.isDead);
            if (listD.Count > 0)
            {
                Console.WriteLine(this + ": Server.Update(): removing seemingly dead clients");
                foreach (Client c in listD)
                {
                    clientList.Remove(c);
                    c.MessageReceived -= c.MessageReceivedDelegate;
                    c.ErrorEvent -= c.ErrorEventDelegate;
                    clientIDs.Remove(c.UniqueIdentity);
                    c.Dead = true;  //make sure it's gone
                }
                if (ClientsRemoved != null)
                    ClientsRemoved(listD);
            }

            Console.WriteLine(this + ": Server.Update(): done this turn");
        }

        private void CheckNewTcpClients()
        {
            List<Client> listA = new List<Client>();
            TcpClient connection;
            Console.WriteLine(this + ": checking TCP listening socket...");
            while (bouncer.Pending())
            {
                //let them join us
                try
                {
                    Console.WriteLine(this + ": accepting new TCP connection");
                    connection = bouncer.AcceptTcpClient();
                }
                catch (Exception e)
                {
                    LastError = e;
                    Console.WriteLine(this + ": EXCEPTION accepting new TCP connection: " + e);
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.Fault, null, "An error occurred when trying to accept new client.");
                    bouncer = null;
                    break;
                }

                //set them up with a session
                Client client = CreateNewClient();
                client.SetTcpHandle(connection);
                listA.Add(client);
            }
            if (listA.Count > 0 && ClientsJoined != null)
            {
                ClientsJoined(listA);
            }
        }

        /// <summary>Returns the client matching that unique identity number.</summary>
        /// <param name="uniqueIdentity">The unique identity of this client</param>
        /// <returns>The client with that unique identity.  If the number doesn't 
        /// match a client, then it returns null.</returns>
        public Client GetClientFromUniqueIdentity(int uniqueIdentity)
        {
            Client c;
            if (clientIDs.TryGetValue(uniqueIdentity, out c))
                return c;
            return null;
        }

        protected Client CreateNewClient()
        {
            Client client = new Client(this, GenerateUniqueIdentity());
            client.MessageReceivedDelegate = new MessageHandler(client_MessageReceived);
            client.MessageReceived += client.MessageReceivedDelegate;
            client.ErrorEventDelegate = new ErrorClientHandler(ErrorClientHandlerMethod);
            client.ErrorEvent += client.ErrorEventDelegate;

            clientList.Add(client);
            clientIDs.Add(client.UniqueIdentity, client);
            Console.WriteLine(this + ": Created new client: " + client.UniqueIdentity);
            return client;
        }

        /// <summary>Starts a new thread that listens for new clients or new data on the current thread.</summary>
        public void StartListening()
        {
            int oldTickCount;
            int newTickCount;

            Start();

            //check this server for new connections or new data forevermore
            while (running)
            {
                try
                {
                    oldTickCount = System.Environment.TickCount;

                    Console.WriteLine(this + ": Server.Update()");
                    Update();

                    newTickCount = System.Environment.TickCount;
                    int sleepCount = Math.Max(this.Interval - (newTickCount - oldTickCount), 0);
                    Console.WriteLine(this + ": sleeping for " + sleepCount + " ticks");

                    Sleep(sleepCount);
                }
                catch (ThreadAbortException) 
                {
                    Console.WriteLine(this + ": listening loop stopped");
                    Stop();
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine(this + ": exception in listening loop: " + e);
                    LastError = e;
                    if(ErrorEvent != null)
                        ErrorEvent(e, SocketError.Fault, null, "An error occurred in the server.");
                }
            }
        }

        public void Sleep(int milliseconds)
        {
            // FIXME: This should be more clever and use Socket.Select()
            Thread.Sleep(milliseconds);
        }

        public void Start()
        {
            //start up the listeners
            RestartBouncers();
            running = true;
        }

        public void Stop()
        {
            // we were told to die.  die gracefully.
            running = false;
            KillBouncers();
            KillAll();
        }

        /// <summary>Handle a data that was received by a client.</summary>
        /// <param name="id">Channel of the data.</param>
        /// <param name="type">Type of Message sent.</param>
        /// <param name="data">The data of the Message.</param>
        /// <param name="client">Which client sent it.</param>
        /// <param name="protocol">How the data was sent</param>
        void client_MessageReceived(byte id, MessageType type, byte[] data, Client client, MessageProtocol protocol)
        {
            Console.WriteLine("Server {0}: MessageReceived id:{1} type:{2} #bytes:{3} from:{4} protocol:{5}",
                this, id, type, data.Length, client, protocol);
            DebugUtils.DumpMessage("client_MessageReceived", id, type, data);
            //sort to the correct data type
            switch (type)
            {
                case MessageType.Binary:
                    if (BinaryMessageReceived != null) BinaryMessageReceived(data, id, client, protocol); break;
                case MessageType.Object:
                    if (ObjectMessageReceived != null) ObjectMessageReceived(data, id, client, protocol); break;
                case MessageType.Session:
                    if (SessionMessageReceived != null) SessionMessageReceived((SessionAction)data[4], id, client, protocol); break;
                case MessageType.String:
                    if (StringMessageReceived != null) StringMessageReceived(data, id, client, protocol); break;
                default:
                    break;
            }

            //send to this
            if (MessageReceived != null) MessageReceived(id, type, data, client, protocol);
        }

        private void KillAll()
        {
            for (int i = 0; i < clientList.Count; i++) {
                clientList[i].Dead = true;
            }
        }

        private void RestartBouncers()
        {
            RestartTcpBouncer();
            RestartUdpMultiplexor();
        }

        private void RestartTcpBouncer()
        {
            if (bouncer != null)
            {
                try { bouncer.Stop(); }
                catch (ThreadAbortException t) { throw t; }
                catch (Exception e) {
                    Console.WriteLine(this + ": exception closing TCP listening socket: " + e);
                }
            }

            bouncer = null;
            try
            {
                bouncer = new TcpListener(IPAddress.Any, this.port);
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
                LastError = e;
                if (ErrorEvent != null)
                    ErrorEvent(e, SocketError.Fault, null, "A socket exception occurred when we tried to start listening for incoming connections.");
                bouncer = null;
            }
            catch (Exception e)
            {
                LastError = e;
                if (ErrorEvent != null)
                    ErrorEvent(e, SocketError.Fault, null, "A non-socket exception occurred when we tried to start listening for incoming connections.");
                bouncer = null;
            }
        }

        private void KillBouncers()
        {
            // don't throw any exceptions
            if (bouncer != null)
            {
                try { bouncer.Stop(); }
                catch (Exception e) { Console.WriteLine("Exception stopping TCP listener: " + e); }
                bouncer = null;
            }
            if (udpMultiplexor != null)
            {
                try { udpMultiplexor.Stop(); } 
                catch (Exception e) { Console.WriteLine("Exception stopping UDP listener: " + e); }
                try { udpMultiplexor.Dispose(); }
                catch (Exception e) { Console.WriteLine("Exception disposing UDP listener: " + e); }
                udpMultiplexor = null;
            }
        }

        public void RestartUdpMultiplexor()
        {
            if (udpMultiplexor != null)
            {
                try { udpMultiplexor.Stop(); }
                catch (Exception e) { Console.WriteLine("Exception stopping UDP listener: " + e); }
                try { udpMultiplexor.Dispose(); }
                catch (Exception e) { Console.WriteLine("Exception disposing UDP listener: " + e); }
            }
            udpMultiplexor = new UdpMultiplexer(port);
            udpMultiplexor.SetDefaultMessageHandler(new MessageReceivedHandler(PreviouslyUnseenUdpMessage));
            udpMultiplexor.Start();
        }

        public void PreviouslyUnseenUdpMessage(EndPoint ep, byte[] message)
        {
            Console.WriteLine(this + ": Incoming unaddressed message from " + ep);
            if (message.Length < 5 || message[0] != '?')
            {
                Console.WriteLine(this + ": UDP: Undecipherable message");
                return;
            }
            int clientId = BitConverter.ToInt32(message, 1);
            Client c = GetClientFromUniqueIdentity(clientId);
            if (c == null)
            {
                Console.WriteLine("Unknown client: " + clientId + " (remote: " + ep + ")");
                c = CreateNewClient();
                c.SetUdpHandle(new UdpHandle(ep, udpMultiplexor, c));
            }
            else
            {
                Console.WriteLine(this + ": UDP: found client: " + clientId + " (remote: " + ep + ")");
                c.SetUdpHandle(new UdpHandle(ep, udpMultiplexor, c));
            }
        }

        /// <summary>Generates a unique identity number that clients can use to identify each other.</summary>
        /// <returns>The unique identity number</returns>
        public int GenerateUniqueIdentity()
        {
            int clientId = 0;
            DateTime timeStamp = DateTime.Now;
            do
            {
                clientId = (timeStamp.Hour * 100 + timeStamp.Minute) * 100 + timeStamp.Second;
                clientId = clientId * 1000 + random.Next(0, 1000);

            } while (clientIDs.ContainsKey(clientId));
            return clientId;
        }

        #endregion

        #region List Sending

        /// <summary>Sends a byte array as a data on the id binary channel to many clients in an efficient way.</summary>
        /// <param name="buffer">The byte array to send</param>
        /// <param name="id">The id of the channel to send it on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="reli">How to send it</param>
        public void Send(byte[] buffer, byte id, List<Client> list, MessageProtocol reli)
        {
            byte[] data = Client.FromMessageToBytes(buffer, id);
            SendToList(data, list, reli);
        }

        /// <summary>Sends a string as a data on the id binary channel to many clients in an efficient way.</summary>
        /// <param name="s">The byte array to send</param>
        /// <param name="id">The id of the channel to send it on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="reli">How to send it</param>
        public void Send(string s, byte id, List<Client> list, MessageProtocol reli)
        {
            byte[] data = Client.FromMessageToBytes(s, id);
            SendToList(data, list, reli);
        }

        /// <summary>Sends a byte array as a data on the id binary channel to many clients in an efficient way.</summary>
        /// <param name="o">The byte array to send</param>
        /// <param name="id">The id of the channel to send it on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="reli">How to send it</param>
        public void Send(object o, byte id, List<Client> list, MessageProtocol reli)
        {
            byte[] data = Client.FromMessageToBytes(o, id);
            SendToList(data, list, reli);
        }

        /// <summary>Packs a series of messages together, then sends them to a list of clients in an efficient way.</summary>
        /// <param name="messagesOut">The list of messages</param>
        /// <param name="list">The list of clients</param>
        /// <param name="reli">How to send them</param>
        public void Send(List<Message> messagesOut, List<Client> list, MessageProtocol reli)
        {
            byte[] data = Client.FromMessageToBytes(messagesOut);
            SendToList(data, list, reli);
        }

        /// <summary>Sends raw binary data to each client in the list.  This is private for a very good reason.</summary>
        /// <param name="buffer">The raw data</param>
        /// <param name="list">Who to send it to</param>
        /// <param name="reli">How to send it</param>
        private void SendToList(byte[] buffer, List<Client> list, MessageProtocol reli)
        {
            //if (!running)
            //{
            //    throw new InvalidStateException("Cannot send on a stopped server", this);
            //}
            if (reli == MessageProtocol.Tcp)
                foreach (Client c in list)
                    c.SendTcpMessage(buffer);
            else
                foreach (Client c in list)
                    c.SendUdpMessage(buffer);
        }

        #endregion

        /// <summary>Represents a client using the server.</summary>
        public class Client
        {
            #region Internal classes and structures

            /// <summary>
            /// Record information necessary for reading a data from the wire.
            /// If <value>messageType</value> is 0, then this represents a header.
            /// </summary>
            class MessageInProgress : Message
            {
                public int position;
                public int bytesRemaining;

                /// <summary>
                /// Construct a Message-in-Progress for reading in a data header.
                /// </summary>
                /// <param name="headerSize"></param>
                public MessageInProgress(int headerSize) 
                    : base(0, 0, new byte[headerSize])
                {
                    // assert messageType == 0;  // indicates a data header
                    this.position = 0;
                    this.bytesRemaining = headerSize;
                }

                /// <summary>
                /// Construct a Message-in-Progress for a data body
                /// </summary>
                /// <param name="id">the channel id for the in-progress data</param>
                /// <param name="messageType">the type of the in-progress data</param>
                /// <param name="size">the size of the in-progress data</param>
                public MessageInProgress(byte id, byte messageType, int size)
                    : base(id, (MessageType)messageType, new byte[size])
                {
                    // assert messageType != 0;
                    this.position = 0;
                    this.bytesRemaining = size;
                }

                /// <summary>
                /// Distinguish between a data header and a data body
                /// </summary>
                /// <returns>true if this instance represents a data header</returns>
                public bool IsMessageHeader()
                {
                    return type == 0;
                }
            }

            #endregion

            #region Variables and Properties

            /// <summary>All of the received Binary Messages that we have kept.</summary>
            public List<byte[]> BinaryMessages;
            /// <summary>All of the received Object Messages that we have kept.</summary>
            public List<byte[]> ObjectMessages;
            /// <summary>All of the received String Messages that we have kept.</summary>
            public List<byte[]> StringMessages;

            /// <summary>Triggered when a data is received.</summary>
            public event MessageHandler MessageReceived;
            internal MessageHandler MessageReceivedDelegate;

            /// <summary>Triggered when an error occurs in this client.</summary>
            public event ErrorClientHandler ErrorEvent;
            internal ErrorClientHandler ErrorEventDelegate;

            /// <summary>Last exception encountered.</summary>
            public Exception LastError;

            private int uniqueIdentity;
            private Server server;
            private float delay = 20;
            private TcpClient tcpHandle;
            private UdpHandle udpHandle;

            private List<byte[]> tcpOut;
            private List<byte[]> udpOut;
            private bool dead;
            private short udpPort = 0;
            private const int bufferSize = 512;
            private MemoryStream udpIn;

            private MessageInProgress tcpInProgress;

            /// <summary>
            /// Is this client dead?
            /// If set to true, kills client.
            /// </summary>
            public bool Dead
            {
                get { return dead; }
                set 
                { 
                    if (value) Kill(this);
                    else if (ErrorEvent != null)
                        ErrorEvent(null, SocketError.Fault, this, 
                            "You should not resurrect clients on server side.  Make the client responsible for reconnecting.");
                }
            }

            /// <summary>Average amount of latency between the server and this particular client.</summary>
            public float Delay
            {
                get { return delay; }
            }
            /// <summary>The UDP port in use, 0 if not set.</summary>
            public short UdpPort
            {
                get { return udpPort; }
            }
            /// <summary>The remote computer's IP address, or null if not set.</summary>
            public string RemoteIP
            {
                get
                {
                    return ((IPEndPoint)tcpHandle.Client.RemoteEndPoint).Address.ToString();
                }
            }
            /// <summary>The remote computer's Port, or null if not set.</summary>
            public string RemotePort
            {
                get
                {
                    return ((IPEndPoint)tcpHandle.Client.RemoteEndPoint).Port.ToString();
                }
            }
            /// <summary>The unique id of this client</summary>
            public int UniqueIdentity
            {
                get { return uniqueIdentity; }
            }

            #endregion


            #region Constructors and Destructors

            /// <summary>Creates a new Client to communicate with.</summary>
            /// <param name="tcpHandle">The tcpHandle to communicate over.</param>
            /// <param name="id">The unique identity of this new Client.</param>
            public Client(Server s, int id)
            {
                server = s;
                uniqueIdentity = id;
                tcpOut = new List<byte[]>();
                udpOut = new List<byte[]>();
                BinaryMessages = new List<byte[]>();
                ObjectMessages = new List<byte[]>();
                StringMessages = new List<byte[]>();

                dead = false;
            }

            #endregion


            #region Static

            /// <summary>Is this Client dead?  (Thread-safe)</summary>
            /// <param name="c">The client to check.</param>
            /// <returns>True if the Client is dead.</returns>
            public static bool isDead(Client c)
            {
                lock (c) { return c.dead; }
            }

            /// <summary>Kills a client and makes it dead.</summary>
            /// <param name="c">The client to kill.</param>
            public static void Kill(Client c)
            {
                try { c.Stop(); }
                catch { }
            }

            public void Stop()
            {
                dead = true;

                try
                {
                    if (tcpHandle != null) { tcpHandle.Close(); }
                }
                catch (Exception) { }
                tcpHandle = null;

                try
                {
                    //c.udpHandle.Client.LingerState.Enabled = false;
                    //c.udpHandle.Client.Close();
                    if (udpHandle != null) { udpHandle.Dispose(); }
                }
                catch (Exception) { }
                udpHandle = null;
            }

            public string ToString()
            {
                return "Server.Client(" + uniqueIdentity + ")";
            }

            #region Internal

            /// <summary>Add the id and the data type to the front of a raw binary data</summary>
            /// <param name="data"></param>
            /// <param name="id"></param>
            /// <param name="type"></param>
            /// <returns></returns>
            internal static byte[] FromMessageToBytes(byte[] data, byte id, MessageType type)
            {
                DebugUtils.DumpMessage("Server.FromMessageToBytes", id, type, data);

                byte[] buffer = new byte[data.Length + 8];
                buffer[0] = id;
                buffer[1] = (byte)type;
                BitConverter.GetBytes(data.Length).CopyTo(buffer, 4);
                data.CopyTo(buffer, 8);
                return buffer;
            }

            /// <summary>Give bytes to be sent a data header and return as bytes</summary>
            /// <param name="data"></param>
            /// <param name="id"></param>
            /// <returns></returns>
            internal static byte[] FromMessageToBytes(byte[] data, byte id)
            {
                return FromMessageToBytes(data, id, MessageType.Binary);
            }

            /// <summary>Give a string to be sent a data header and return as bytes</summary>
            /// <param name="s"></param>
            /// <param name="id"></param>
            /// <returns></returns>
            internal static byte[] FromMessageToBytes(string s, byte id)
            {
                byte[] buffer = System.Text.ASCIIEncoding.ASCII.GetBytes(s);
                return FromMessageToBytes(buffer, id, MessageType.String);
            }

            /// <summary>Give an object to be sent a data header and return as bytes</summary>
            /// <param name="o"></param>
            /// <param name="id"></param>
            /// <returns></returns>
            internal static byte[] FromMessageToBytes(object o, byte id)
            {
                MemoryStream ms = new MemoryStream();
                Server.formatter.Serialize(ms, o);
                byte[] buffer = new byte[ms.Position];
                ms.Position = 0;
                ms.Read(buffer, 0, buffer.Length);
                return FromMessageToBytes(buffer, id, MessageType.Object);
            }

            /// <summary>Convert a list of messages into the raw bytes that will be sent into the network.
            /// The bytes include the 2 byte data headers.</summary>
            /// <param name="messagesOut"></param>
            /// <returns></returns>
            internal static byte[] FromMessageToBytes(List<Message> messagesOut)
            {
                int sizeMax = 0;
                int size = 0;

                foreach (Message message in messagesOut)
                    sizeMax += message.data.Length + 8;

                byte[] buffer = new byte[sizeMax];
                byte[] data;
                foreach (Message message in messagesOut)
                {
                    data = FromMessageToBytes(message.data, message.id, message.type);
                    data.CopyTo(buffer, size);
                    size += data.Length;
                }

                return buffer;
            }

            #endregion

            #endregion

            internal void SetUdpHandle(UdpHandle h)
            {
                Console.WriteLine(this + ": set UDP handle: " + h);
                udpHandle = h;
            }

            internal void SetTcpHandle(TcpClient c)
            {
                Console.WriteLine(this + ": set TCP handle: " + c.Client.RemoteEndPoint);
                c.NoDelay = true;
                c.Client.Blocking = false;
                tcpHandle = c;
            }

            #region Send

            /// <summary>Send SessionAction.</summary>
            /// <param name="clientId">Client who is doing the action.</param>
            /// <param name="e">SessionEvent data to send.</param>
            /// <param name="id">Channel to send on.</param>
            /// <param name="protocol">What protocol the data should use.</param>
            public void Send(int clientId, SessionAction e, byte id, MessageProtocol protocol)
            {
                //Session messages are unique in that we ALWAYS want to know who the action refers to, 
                //therefore we built it into the system.
                byte[] buffer = new byte[5];
                BitConverter.GetBytes(clientId).CopyTo(buffer, 0);
                buffer[4] = (byte)e;
                this.Send(buffer, id, MessageType.Session, protocol);
            }

            /// <summary>Send byte array.</summary>
            /// <param name="buffer">Binary data to send.</param>
            /// <param name="id">Channel to send on.</param>
            /// <param name="protocol">What protocol the data should use.</param>
            public void Send(byte[] buffer, byte id, MessageProtocol protocol)
            {
                this.Send(buffer, id, MessageType.Binary, protocol);
            }

            /// <summary>Send object.</summary>
            /// <param name="o">Object data to send.</param>
            /// <param name="id">Channel to send on.</param>
            /// <param name="protocol">What protocol the data should use.</param>
            public void Send(Object o, byte id, MessageProtocol protocol)
            {
                MemoryStream ms = new MemoryStream();
                Server.formatter.Serialize(ms, o);
                byte[] buffer = new byte[ms.Position];
                ms.Position = 0;
                ms.Read(buffer, 0, buffer.Length);
                this.Send(buffer, id, MessageType.Object, protocol);
            }

            /// <summary>Send string.</summary>
            /// <param name="s">String data to send.</param>
            /// <param name="id">Channel to send on.</param>
            /// <param name="protocol">What protocol the data should use.</param>
            public void Send(String s, byte id, MessageProtocol protocol)
            {
                byte[] buffer = System.Text.ASCIIEncoding.ASCII.GetBytes(s);
                this.Send(buffer, id, MessageType.String, protocol);
            }

            /// <summary>Send SessionAction.</summary>
            /// <param name="clientId">client who is doing the action.</param>
            /// <param name="s">SessionEvent data to send.</param>
            /// <param name="id">Channel to send on.</param>
            public void Send(int clientId, SessionAction s, byte id)
            {
                this.Send(clientId, s, id, MessageProtocol.Tcp);
            }

            /// <summary>Send byte array.</summary>
            /// <param name="buffer">Binary data to send.</param>
            /// <param name="id">Channel to send on.</param>
            public void Send(byte[] buffer, byte id)
            {
                this.Send(buffer, id, MessageType.Binary, MessageProtocol.Tcp);
            }

            /// <summary>Send object.</summary>
            /// <param name="o">Object data to send.</param>
            /// <param name="id">Channel to send on.</param>
            public void Send(Object o, byte id)
            {
                this.Send(o, id, MessageProtocol.Tcp);
            }

            /// <summary>Send string.</summary>
            /// <param name="s">String data to send.</param>
            /// <param name="id">Channel to send on.</param>
            public void Send(String s, byte id)
            {
                this.Send(s, id, MessageProtocol.Tcp);
            }

            /// <summary>Sends a raw byte array of any data type using these parameters.  
            /// This can be used to manipulate received messages without converting it to another 
            /// format, for example, a string or an object.  Using this method, received messages 
            /// can be repeated, compared, or passed along faster than otherwise.</summary>
            /// <param name="data">Raw bytes to be sent.</param>
            /// <param name="id">The channel to send on.</param>
            /// <param name="type">The raw data type.</param>
            /// <param name="protocol">What protocol the data should use.</param>
            public void Send(byte[] data, byte id, MessageType type, MessageProtocol protocol)
            {
                lock (this)
                {
                    if (dead)
                        return;

                    byte[] buffer = new byte[data.Length + 8];
                    buffer[0] = id;
                    buffer[1] = (byte)type;
                    BitConverter.GetBytes(data.Length).CopyTo(buffer, 4);
                    data.CopyTo(buffer, 8);


                    if (protocol == MessageProtocol.Tcp)
                        SendTcpMessage(buffer);
                    else
                        SendUdpMessage(buffer);
                }
            }

            /// <summary>Sends a data via UDP.
            /// We don't care if it doesn't get through.</summary>
            /// <param name="buffer">Raw stuff to send.</param>
            internal void SendUdpMessage(byte[] buffer)
            {
                if (this.dead) return;

                SocketError error = SocketError.Success;
                if (TryAndFlushOldUdpBytesOut())
                {
                    Console.WriteLine("{0}: SendUdpMessage({1} bytes): sending...", this, buffer.Length);
                    udpOut.Add(buffer);
                    return;
                }

                try
                {
                    udpHandle.Send(buffer, 0, buffer.Length, out error);
                    switch (error)
                    {
                        case SocketError.Success:
                            Console.WriteLine("{0}: SendMessage({1} bytes): success", this, buffer.Length);
                            return;
                        case SocketError.WouldBlock:
                            Console.WriteLine("{0}: SendMessage({1} bytes): EWOULDBLOCK", this, buffer.Length);
                            udpOut.Add(buffer);
                            if (ErrorEvent != null)
                                ErrorEvent(null, error, this, "The TCP write buffer is full now, but the data will be saved and " +
                                    "sent soon.  Send less data to reduce perceived latency.");
                            return;
                        default:
                            udpOut.Add(buffer);
                            Console.WriteLine("{0}: SendMessage({1} bytes): ERROR: {2}", this, buffer.Length, error);
                            if (ErrorEvent != null)
                                ErrorEvent(null, error, this, "Error occurred while trying to send TCP to client.");
                            return;
                    }
                }
                catch (Exception e)
                {
                    udpOut.Add(buffer);
                    LastError = e;
                    Console.WriteLine("{0}: SendMessage({1} bytes): EXCEPTION: {2}", this, buffer.Length, e);
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.NoRecovery, this, "Exception occurred while trying to send TCP to client.");
                }
                
            }

            /// <summary>Sends a data via TCP.
            /// We DO care if it doesn't get through; we throw an exception.
            /// </summary>
            /// <param name="buffer">Raw stuff to send.</param>
            internal void SendTcpMessage(byte[] buffer)
            {
                if (dead || tcpHandle == null) { return; }

                if (TryAndFlushOldTcpBytesOut())
                {
                    Console.WriteLine("{0}: SendTcpMessage({1} bytes): adding to outstanding bytes", this, buffer.Length);
                    tcpOut.Add(buffer);
                    return;
                }

                SocketError error = SocketError.Success;
                try
                {
                    tcpHandle.Client.Send(buffer, 0, buffer.Length, SocketFlags.None, out error);
                    switch (error)
                    {
                        case SocketError.Success:
                            Console.WriteLine("{0}: SendMessage({1} bytes): success", this, buffer.Length);
                            return;
                        case SocketError.WouldBlock:
                            tcpOut.Add(buffer);
                            Console.WriteLine("{0}: SendMessage({1} bytes): EWOULDBLOCK", this, buffer.Length);
                            if (ErrorEvent != null)
                                ErrorEvent(null, error, this, "The TCP write buffer is full now, but the data will be saved and " +
                                    "sent soon.  Send less data to reduce perceived latency.");
                            return;
                        default:
                            Console.WriteLine("{0}: SendMessage({1} bytes): ERROR: {2}", this, buffer.Length, error);
                            this.dead = true;
                            tcpOut.Add(buffer);
                            if (ErrorEvent != null)
                                ErrorEvent(null, error, this, "Error occurred while trying to send TCP to client.");
                            return;
                    }
                }
                catch (Exception e)
                {
                    this.dead = true;
                    tcpOut.Add(buffer);
                    Console.WriteLine("{0}: SendMessage({1} bytes): EXCEPTION: {2}", this, buffer.Length, e);
                    LastError = e;
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.NoRecovery, this, "Exception occurred while trying to send TCP to client.");
                }
            }

            /// <summary> Flushes out old messages that couldn't be sent because of exceptions</summary>
            /// <returns>True if there are bytes that still have to be sent out</returns>
            internal bool TryAndFlushOldUdpBytesOut()
            {
                byte[] b;
                SocketError error = SocketError.Success;
                try
                {
                    while (udpOut.Count > 0)
                    {
                        b = udpOut[0];
                        udpHandle.Send(b, 0, b.Length, out error);

                        switch (error)
                        {
                            case SocketError.Success:
                                udpOut.RemoveAt(0);
                                break;
                            case SocketError.WouldBlock:
                                //don't die, but try again next time
                                LastError = null;
                                if (ErrorEvent != null)
                                    ErrorEvent(null, error, this, "The UDP write buffer is full now, but the data will be saved and " +
                                        "sent soon.  Send less data to reduce perceived latency.");
                                return true;
                            default:
                                //something terrible happened, but this is only UDP, so stick around.
                                LastError = null;
                                if (ErrorEvent != null)
                                    ErrorEvent(null, error, this, "Failed to Send UDP Message (" + b.Length + " bytes): " + b.ToString());
                                return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    LastError = e;
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.NoRecovery, this, "Trying to send saved UDP data failed because of an exception.");
                    return true;
                }
                return false;
            }

            /// <summary> Flushes out old messages that couldn't be sent because of exceptions</summary>
            /// <returns>True if there are bytes that still have to be sent out</returns>
            internal bool TryAndFlushOldTcpBytesOut()
            {
                byte[] b;
                SocketError error = SocketError.Success;

                try
                {
                    while (tcpOut.Count > 0)
                    {
                        b = tcpOut[0];
                        tcpHandle.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

                        switch (error)
                        {
                            case SocketError.Success:
                                tcpOut.RemoveAt(0);
                                break;
                            case SocketError.WouldBlock:
                                //don't die, but try again next time
                                LastError = null;
                                if (ErrorEvent != null)
                                    ErrorEvent(null, error, this, "The TCP write buffer is full now, but the data will be saved and " +
                                        "sent soon.  Send less data to reduce perceived latency.");
                                return true;
                            default:
                                //die, because something terrible happened
                                dead = true;
                                LastError = null;
                                if (ErrorEvent != null)
                                    ErrorEvent(null, error, this, "Failed to Send TCP Message (" + b.Length + " bytes): " + b.ToString());

                                return true;
                        }


                    }
                }
                catch (Exception e)
                {
                    dead = true;
                    LastError = e;
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.NoRecovery, this, "Trying to send saved TCP data failed because of an exception.");
                    return true;
                }

                return false;
            }

            /// <summary>Handles a system data in that it takes the information and does something with it.</summary>
            /// <param name="data">The data we received.</param>
            /// <param name="id">What channel it came in on.</param>
            private void HandleSystemMessage(byte[] data, byte id)
            {
                byte[] buffer = new byte[4];

                if (id == (byte)SystemMessageType.UniqueIDRequest)
                {
                    //they want to know their own id
                    BitConverter.GetBytes(UniqueIdentity).CopyTo(buffer, 0);
                    Send(buffer, (byte)SystemMessageType.UniqueIDRequest, MessageType.System, MessageProtocol.Tcp);
                }
                else if (id == (byte)SystemMessageType.UDPPortRequest)
                {
                    //they want to receive udp messages on this port
                    short port = BitConverter.ToInt16(data, 0);
                    // FIXME: the following is a *monstrous* hack
                    IPEndPoint remoteUdp = new IPEndPoint(((IPEndPoint)tcpHandle.Client.RemoteEndPoint).Address, port);
                    //udpHandle.Connect(remoteUdp);
                    SetUdpHandle(new UdpHandle(remoteUdp, server.udpMultiplexor, this));

                    //they want the udp port.  Send it.
                    BitConverter.GetBytes(UdpPort).CopyTo(buffer, 0);
                    Send(buffer, (byte)SystemMessageType.UDPPortResponse, MessageType.System, MessageProtocol.Tcp);
                }
                else if (id == (byte)SystemMessageType.UDPPortResponse)
                {
                    // We should never see this data as a server!
                    Console.WriteLine("WARNING: Server should never receive UDPPortResponse messages!");
                    //they want to receive udp messages on this port
                    //short port = BitConverter.ToInt16(data, 0);
                    //IPEndPoint remoteUdp = new IPEndPoint(((IPEndPoint)tcpHandle.Client.RemoteEndPoint).Address, port);
                    //udpHandle.Connect(remoteUdp);
                }
                else if (id == (byte)SystemMessageType.ServerPingAndMeasure)
                {
                    //record the difference; half of it is the latency between this client and the server
                    int newDelay = (System.Environment.TickCount - BitConverter.ToInt32(data, 0)) / 2;
                    this.delay = this.delay * 0.95f + newDelay * 0.05f;
                }
                else if (id == (byte)SystemMessageType.ClientPingAndMeasure)
                {
                    Send(data, (byte)SystemMessageType.ClientPingAndMeasure, MessageType.System, MessageProtocol.Tcp);
                }
            }

            /// <summary>Send a ping to a client to see if it responds.</summary>
            internal void Ping()
            {
                // FIXME: ping each of the transports...
                byte[] buffer = BitConverter.GetBytes(System.Environment.TickCount);
                Send(buffer, (byte)SystemMessageType.ServerPingAndMeasure, MessageType.System, MessageProtocol.Tcp);
            }

            #endregion


            #region Receive

            /// <summary>Go through one tick of this client.</summary>
            public void Update()
            {
                lock (this)
                {
                    if (dead) { return; }
                    UpdateFromNetworkTcp();
                    UpdateFromNetworkUdp();
                }
            }

            /// <summary>Gets available data from UDP.</summary>
            private void UpdateFromNetworkUdp()
            {
                byte[] buffer, data;
                int length, cursor;
                byte id, type;

                if (udpHandle == null) { return; }

                try
                {
                    //while there are more packets to read
                    while (udpHandle.Available > 0)
                    {
                        //get a packet
                        buffer = udpHandle.Receive();
                        cursor = 0;
                        Console.WriteLine("{0}: UpdateFromNetworkUdp(): received packet ({1} bytes)", this, buffer.Length);

                        //while there are more messages in this packet
                        while(cursor < buffer.Length)
                        {
                            id = buffer[cursor];
                            type = buffer[cursor + 1];
                            length = BitConverter.ToInt32(buffer, cursor + 4);
                            data = new byte[length];
                            Array.Copy(buffer, 8, data, 0, length);

                            PostNewlyReceivedMessage(id, (MessageType)type, data, MessageProtocol.Udp);
                            cursor += length + 8;
                        }
                    }
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.WouldBlock)
                    {
                        LastError = e;
                        if (ErrorEvent != null)
                            ErrorEvent(e, SocketError.NoRecovery, this, "Updating from UDP connection failed because of an exception");
                    }
                }
                catch (Exception e)
                {
                    LastError = e;
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.Fault, this, "Error in interpretting UDP data.  This data has been lost.");
                }
            }

            /// <summary>Gets one data from the tcp and interprets it.</summary>
            private void UpdateFromNetworkTcp()
            {
                if (tcpHandle == null) {
                    Console.WriteLine(this + ": a pity: I have no tcpHandle");
                    return; 
                }
                if (tcpHandle.Available > 0)
                {
                    Console.WriteLine(this + ": there appears to be some data available!");
                }

                SocketError error = SocketError.Success;
                try
                {
                    while (tcpHandle.Available > 0)
                    {
                        // This is a simple state machine: we're either:
                        // (a) reading a data header (tcpInProgress.IsMessageHeader())
                        // (b) reading a data body (!tcpInProgress.IsMessageHeader())
                        // (c) finished and about to start reading in a header (tcpInProgress == null)

                        if (tcpInProgress == null)
                        {
                            //restart the counters to listen for a new data.
                            tcpInProgress = new MessageInProgress(8);
                            // assert tcpInProgress.IsMessageHeader();
                        }

                        int size = tcpHandle.Client.Receive(tcpInProgress.data, tcpInProgress.position,
                            tcpInProgress.bytesRemaining, SocketFlags.None, out error);
                        switch (error)
                        {
                        case SocketError.Success:
                            // Console.WriteLine("{0}: UpdateFromNetworkTcp(): received header", this);
                            break;
                        default:
                            dead = true;
                            Console.WriteLine("{0}: UpdateFromNetworkTcp(): ERROR reading from socket: {1}", this, error);
                            if (ErrorEvent != null)
                                ErrorEvent(null, SocketError.Fault, this, "Error reading TCP data header.");
                            return;
                        }

                        tcpInProgress.position += size;
                        tcpInProgress.bytesRemaining -= size;
                        if (tcpInProgress.bytesRemaining == 0)
                        {
                            if (tcpInProgress.IsMessageHeader())
                            {
                                // byte 0: id
                                // byte 1: data type
                                // bytes 2,3: unused
                                // bytes 4-7: data length
                                tcpInProgress = new MessageInProgress(tcpInProgress.data[0], 
                                    tcpInProgress.data[1], BitConverter.ToInt32(tcpInProgress.data, 4));
                                // assert tcpInProgress.IsMessageHeader()
                            }
                            else
                            {
                                PostNewlyReceivedMessage(tcpInProgress.id, (MessageType)tcpInProgress.type, tcpInProgress.data, MessageProtocol.Tcp);
                                tcpInProgress = null;
                            }
                        }
                    }
                }
                catch (SocketException e)
                {   // FIXME: can this clause even happen?
                    dead = true;
                    LastError = e;
                    Console.WriteLine("{0}: UpdateFromNetworkTcp(): SocketException reading from socket: {1}", this, e);
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.NoRecovery, this, "Updating from TCP connection failed because of a socket exception.");
                }
                catch (Exception e)
                {
                    // We shouldn't catch ThreadAbortExceptions!  (FIXME: should we really be
                    // catching anything other than SocketExceptions from here?)
                    if (e is ThreadAbortException) { throw e; }
                    LastError = e;
                    Console.WriteLine("{0}: UpdateFromNetworkTcp(): EXCEPTION: {1}", this, e);
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.NoRecovery, this, "Exception occured (not socket exception).");
                }
            }

            private void PostNewlyReceivedMessage(byte id, MessageType type, byte[] buffer, MessageProtocol messageProtocol)
            {
                Console.WriteLine("{0}: posting newly received message id:{1} type:{2} protocol:{3} #bytes:{4}",
                    this, id, type, messageProtocol, buffer.Length);
                DebugUtils.DumpMessage("Server.Client.PostNewlyReceivedMessage", id, type, buffer);

                if (type == MessageType.Session)
                {
                    //session messages are special!  Weee!
                    buffer = ConvertIncomingSessionMessageToNormalForm(buffer);
                }

                if (type == MessageType.System)
                {
                    //System messages are special!  Yay!
                    HandleSystemMessage(buffer, id);
                }
                else
                {
                    MessageReceived(id, type, buffer, this, messageProtocol);
                }
            }

            private byte[] ConvertIncomingSessionMessageToNormalForm(byte[] b)
            {
                byte[] buffer = new byte[5];
                buffer[4] = b[0];
                BitConverter.GetBytes(this.uniqueIdentity).CopyTo(buffer, 0);
                return buffer;
            }

            #endregion

        }
    }
}
