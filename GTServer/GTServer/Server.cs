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
using GT;

namespace GT
{

    #region Delegates

    /// <summary>Handles a tick event, which is one loop of the server</summary>
    public delegate void TickHandler();

    /// <summary>Handles a Message event, when a data arrives</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the data</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void MessageHandler(Message m, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a SessionMessage event, when a SessionMessage arrives.</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the data.</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void SessionMessageHandler(Message m, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a StringMessage event, when a StringMessage arrives.</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the data.</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void StringMessageHandler(Message m, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a ObjectMessage event, when a ObjectMessage arrives.</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the data.</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void ObjectMessageHandler(Message m, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a BinaryMessage event, when a BinaryMessage arrives.</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the data.</param>
    /// <param name="protocol">How the data was sent</param>
    public delegate void BinaryMessageHandler(Message m, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles when clients leave the server.</summary>
    /// <param name="list">The clients who've left.</param>
    public delegate void ClientsRemovedHandler(ICollection<Server.Client> list);

    /// <summary>Handles when clients join the server.</summary>
    /// <param name="list">The clients who've joined.</param>
    public delegate void ClientsJoinedHandler(ICollection<Server.Client> list);

    /// <summary>Handles when there is an internal error that the application should know about.</summary>
    /// <param name="e">The exception that occurred</param>
    /// <param name="se">The type of networking error</param>
    /// <param name="c">The client where the exception occurred</param>
    /// <param name="explanation">An explanation of the error encountered</param>
    public delegate void ErrorClientHandler(Exception e, SocketError se, Server.Client c, string explanation);


    #endregion


    /// <summary>Represents traditional server.</summary>
    public class Server : IStartable
    {
        #region Variables and Properties

        private static Random random = new Random();
        private static BinaryFormatter formatter = new BinaryFormatter();

        private bool running = false;

        private List<IAcceptor> acceptors = new List<IAcceptor>();
        private IMarshaller marshaller = new DotNetSerializingMarshaller();

        private int lastPingTime = 0;
        private int port;

        /// <summary>Last exception encountered.</summary>
        public Exception LastError = null;

        /// <summary>All of the clientIDs that this server knows about.  
        /// Hide this so that users cannot cause mischief.  I accept that this list may 
        /// not be accurate because the users have direct access to the clientList.</summary>
        private Dictionary<int, Client> clientIDs = new Dictionary<int, Client>();
        private ICollection<Client> newlyAddedClients = new List<Client>();

        public ICollection<Client> Clients { get { return clientIDs.Values; } }

        /// <summary>Time in milliseconds between keep-alive pings.</summary>
        public int PingInterval = 10000;

        /// <summary>Time in milliseconds between server ticks.</summary>
        public int Interval = 10;

        public IMarshaller Marshaller { get { return marshaller; } }

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

        /// <summary>
        /// Create a descriptive string representation 
        /// </summary>
        /// <returns>a descriptive string representation</returns>
        override public string ToString()
        {
            return "Server(port=" + port + ", " + Clients.Count + " clients)";
        }

        /// <summary>One tick of the server, manually
        /// Use if not using Start
        /// </summary>
        public void Update()
        {
            DebugUtils.WriteLine(">>>> Server.Update() started");

            if (!Active) { Start(); }

            newlyAddedClients.Clear();
            foreach(BaseAcceptor acc in acceptors) {
                DebugUtils.WriteLine("Server.Update(): checking acceptor " + acc);
                acc.Update();
            }
            if (newlyAddedClients.Count > 0 && ClientsJoined != null)
            {
                ClientsJoined(newlyAddedClients);
            }

            //ping, if needed
            if (lastPingTime + PingInterval < System.Environment.TickCount)
            {
                DebugUtils.WriteLine("Server.Update(): pinging clients");
                lastPingTime = System.Environment.TickCount;
                foreach (Client c in Clients) { c.Ping(); }
            }

            DebugUtils.WriteLine("Server.Update(): Clients.Update()");
            //update all clients, reading from the network
            foreach (Client c in Clients) { c.Update(); }

            //if anyone is listening, tell them we're done one cycle
            if (Tick != null) { Tick(); }

            //remove dead clients
            List<Client> listD = FindAll(Clients, Client.IsDead);
            if (listD.Count > 0)
            {
                DebugUtils.WriteLine("Server.Update(): removing dead clients");
                foreach (Client c in listD)
                {
                    clientIDs.Remove(c.UniqueIdentity);
                    c.Dispose();  //make sure it's gone
                }
                if (ClientsRemoved != null)
                {
                    ClientsRemoved(listD);
                }
            }

            DebugUtils.WriteLine("<<<< Server.Update() finished");
        }

        private List<T> FindAll<T>(ICollection<T> list, Predicate<T> pred)
        {
            List<T> results = new List<T>();
            foreach (T t in list)
            {
                if (pred(t))
                {
                    results.Add(t);
                }
            }
            return results;
        }

        protected void NewClient(ITransport t, Dictionary<string,string> capabilities)
        {
            Guid clientId;
            try
            {
                clientId = new Guid(capabilities[GTConstants.CAPABILITIES_CLIENT_ID]);
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Exception when decoding client's GUID: {1}",
                    DateTime.Now, e);
                t.Dispose();
                return;
            }
            Client c = GetClientFromUniqueIdentity(clientId);
            if (c == null)
            {
                Console.WriteLine("{0}: unknown client: {1} from {2}", this, clientId, t);
                c = CreateNewClient(clientId);
                newlyAddedClients.Add(c);
            }
            else
            {
                Console.WriteLine("{0}: found client {1} from {2}", this, clientId, t);
            }
            c.AddTransport(t);
        }

        /// <summary>Returns the client matching that unique identity number.</summary>
        /// <param name="uniqueIdentity">The unique identity of this client</param>
        /// <returns>The client with that unique identity.  If the number doesn't 
        /// match a client, then it returns null.</returns>
        public Client GetClientFromUniqueIdentity(Guid id)
        {
            foreach (Client c in Clients)
            {
                if (c.ClientIdentity.Equals(id)) { return c; }
            }
            return null;
        }

        protected Client CreateNewClient(Guid clientId)
        {
            Client client = new Client(this, clientId, GenerateUniqueIdentity());
            client.MessageReceivedDelegate = new MessageHandler(client_MessageReceived);
            client.MessageReceived += client.MessageReceivedDelegate;
            client.ErrorEventDelegate = new ErrorClientHandler(ErrorClientHandlerMethod);
            client.ErrorEvent += client.ErrorEventDelegate;

            clientIDs.Add(client.UniqueIdentity, client);
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
                    // tick count is in milliseconds
                    oldTickCount = System.Environment.TickCount;

                    Update();

                    newTickCount = System.Environment.TickCount;
                    int sleepCount = Math.Max(this.Interval - (newTickCount - oldTickCount), 0);

                    Sleep(sleepCount);
                }
                catch (ThreadAbortException) 
                {
                    Console.WriteLine("{0}: listening loop stopped", this);
                    Stop();
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0}: exception in listening loop: {1}", this, e);
                    LastError = e;
                    if(ErrorEvent != null)
                        ErrorEvent(e, SocketError.Fault, null, "An error occurred in the server.");
                }
            }
        }

        public void Sleep(int milliseconds)
        {
            Trace.TraceInformation("{0}: sleeping for {1}ms", this, milliseconds);

            // FIXME: This should be more clever and use Socket.Select()
            Thread.Sleep(milliseconds);
        }

        public void Start()
        {
            if (acceptors.Count == 0)
            {
                acceptors.Add(new TcpAcceptor(IPAddress.Any, port));
                acceptors.Add(new UdpAcceptor(IPAddress.Any, port));
            }
            foreach (IAcceptor acc in acceptors)
            {
                acc.NewClientEvent += new NewClientHandler(NewClient);
                acc.Start();
            }
            running = true;
        }

        public void Stop()
        {
            // we were told to die.  die gracefully.
            running = false;
            foreach (IAcceptor acc in acceptors)
            {
                acc.Stop();
            }
            KillAll();
        }

        public void Dispose()
        {
            Stop();
            foreach (IAcceptor acc in acceptors)
            {
                acc.Dispose();
            }
            acceptors = null;
        }

        public bool Active
        {
            get { return running; }
        }

        /// <summary>Handle a data that was received by a client.</summary>
        /// <param name="id">Channel of the data.</param>
        /// <param name="type">Type of Message sent.</param>
        /// <param name="data">The data of the Message.</param>
        /// <param name="client">Which client sent it.</param>
        /// <param name="protocol">How the data was sent</param>
        void client_MessageReceived(Message m, Client client, MessageProtocol protocol)
        {
            DebugUtils.DumpMessage(this + ": MessageReceived from " + client, m);
            //send to this
            if (MessageReceived != null) MessageReceived(m, client, protocol);

            //sort to the correct data type
            switch (m.type)
            {
                case MessageType.Binary:
                    if (BinaryMessageReceived != null) BinaryMessageReceived(m, client, protocol); break;
                case MessageType.Object:
                    if (ObjectMessageReceived != null) ObjectMessageReceived(m, client, protocol); break;
                case MessageType.Session:
                    if (SessionMessageReceived != null) SessionMessageReceived(m, client, protocol); break;
                case MessageType.String:
                    if (StringMessageReceived != null) StringMessageReceived(m, client, protocol); break;
                default:
                    break;
            }

        }

        private void KillAll()
        {
            foreach(Client c in Clients) {
                try {
                    c.Dispose();
                } catch(Exception e) {
                    Console.WriteLine("{0} EXCEPTION while disposing client: {1}",
                        DateTime.Now, e);
                }
            }
            clientIDs.Clear();
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

        #region Sending

        /// <summary>Sends a byte array on channel <c>id</c> to many clients in an efficient manner.</summary>
        /// <param name="buffer">The byte array to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="reli">How to send it</param>
        public void Send(byte[] buffer, byte id, ICollection<Client> list, MessageProtocol reli)
        {
            List<Message> messages = new List<Message>(1);
            messages.Add(new BinaryMessage(id, buffer));
            Send(messages, list, reli);
        }

        /// <summary>Sends a string on channel <c>id</c> to many clients in an efficient manner.</summary>
        /// <param name="s">The byte array to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="reli">How to send it</param>
        public void Send(string s, byte id, ICollection<Client> list, MessageProtocol reli)
        {
            List<Message> messages = new List<Message>(1);
            messages.Add(new StringMessage(id, s));
            Send(messages, list, reli);
        }

        /// <summary>Sends an object on channel <c>id</c> to many clients in an efficient manner.</summary>
        /// <param name="o">The bject to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="reli">How to send it</param>
        public void Send(object o, byte id, ICollection<Client> list, MessageProtocol reli)
        {
            List<Message> messages = new List<Message>(1);
            messages.Add(new ObjectMessage(id, o));
            Send(messages, list, reli);
        }

        public void Send(Message message, ICollection<Client> list, MessageProtocol reli)
        {
            foreach (Client c in list)
            {
                try
                {
                    c.Send(message, reli);
                }
                catch (Exception e)
                {
                    ErrorClientHandlerMethod(e, SocketError.Fault, c, "exception when sending");
                }
            }
        }

        /// <summary>Sends a collection of messages in an efficient way to a list of clients.</summary>
        /// <param name="messages">The list of messages</param>
        /// <param name="list">The list of clients</param>
        /// <param name="reli">How to send them</param>
        public void Send(IList<Message> messages, ICollection<Client> list, MessageProtocol reli)
        {
            if (!running) { throw new InvalidStateException("Cannot send on a stopped server", this); }
            foreach (Client c in list)
            {
                try
                {
                    c.Send(messages, reli);
                }
                catch (Exception e)
                {
                    ErrorClientHandlerMethod(e, SocketError.Fault, c, "exception when sending");
                }
            }
        }

        #endregion

        /// <summary>Represents a client using the server.</summary>
        public class Client : IDisposable
        {
            #region Variables and Properties

            /// <summary>Triggered when a data is received.</summary>
            public event MessageHandler MessageReceived;
            internal MessageHandler MessageReceivedDelegate;

            /// <summary>Triggered when an error occurs in this client.</summary>
            public event ErrorClientHandler ErrorEvent;
            internal ErrorClientHandler ErrorEventDelegate;

            /// <summary>Last exception encountered.</summary>
            public Exception LastError;

            /// <summary>
            /// The client's unique identifier; this should be globally unique
            /// </summary>
            protected Guid clientId;

            /// <summary>
            /// The server's unique identifier; this is not globally unique
            /// </summary>
            private int uniqueIdentity;

            private Server server;
            private Dictionary<MessageProtocol, ITransport> transports =
                new Dictionary<MessageProtocol, ITransport>();

            private bool dead;

            /// <summary>
            /// Is this client dead?
            /// </summary>
            public bool Dead
            {
                get { return dead; }
            }

            /// <summary>Average amount of latency between the server and this particular client.</summary>
            public float Delay
            {
                get
                {
                    float total = 0; int n = 0;
                    foreach (ITransport t in transports.Values)
                    {
                        float d = t.Delay;
                        if (d > 0) { total += d; n++; }
                    }
                    return n == 0 ? 0 : total / n;
                }
            }

            /// <summary>The unique id of this client</summary>
            public int UniqueIdentity
            {
                get { return uniqueIdentity; }
            }

            public Guid ClientIdentity
            {
                get { return clientId; }
            }

            #endregion


            #region Constructors and Destructors

            /// <summary>Creates a new Client to communicate with.</summary>
            /// <param name="s">The associated server instance.</param>
            /// <param name="id">The unique identity of this new Client.</param>
            public Client(Server s, Guid clientId, int id)
            {
                server = s;
                this.clientId = clientId;
                uniqueIdentity = id;
                dead = false;
            }

            #endregion

            #region Predicates

            /// <summary>Is this Client dead?  This function is intended for use as a predicate
            /// such as in <c>List.FindAll()</c>.</summary>
            /// <param name="c">The client to check.</param>
            /// <returns>True if the client <c>c</c> </c>is dead.</returns>
            internal static bool IsDead(Client c)
            {
                return c.Dead;
            }

            #endregion

            #region Lifecycle

            public void Dispose()
            {
                dead = true;
                MessageReceived -= MessageReceivedDelegate;
                ErrorEvent -= ErrorEventDelegate;
            }

            #endregion

            override public string ToString()
            {
                return "Server.Client(" + uniqueIdentity + ")";
            }


            internal void AddTransport(ITransport t)
            {
                DebugUtils.Write(this + ": added new transport: " + t);
                t.PacketReceivedEvent += new PacketReceivedHandler(PostNewlyReceivedPacket);
                transports[t.MessageProtocol] = t;
            }

            #region Send

            /// <summary>Send a byte array on the channel <c>id</c>.</summary>
            /// <param name="buffer">The byte array to send</param>
            /// <param name="id">The channel id to be sent on</param>
            /// <param name="reli">How to send it</param>
            public void Send(byte[] buffer, byte id, MessageProtocol reli)
            {
                List<Message> messages = new List<Message>(1);
                messages.Add(new BinaryMessage(id, buffer));
                Send(messages, reli);
            }

            /// <summary>Send a string on channel <c>id</c>.</summary>
            /// <param name="s">The string to send</param>
            /// <param name="id">The channel id to be sent on</param>
            /// <param name="reli">How to send it</param>
            public void Send(string s, byte id, MessageProtocol reli)
            {
                List<Message> messages = new List<Message>(1);
                messages.Add(new StringMessage(id, s));
                Send(messages, reli);
            }

            /// <summary>Sends an bject on channel <c>id</c>.</summary>
            /// <param name="o">The object to send</param>
            /// <param name="id">The channel id to be sent on</param>
            /// <param name="reli">How to send it</param>
            public void Send(object o, byte id, MessageProtocol reli)
            {
                List<Message> messages = new List<Message>(1);
                messages.Add(new ObjectMessage(id, o));
                Send(messages, reli);
            }

            /// <summary>Send SessionAction.</summary>
            /// <param name="clientId">Client who is doing the action.</param>
            /// <param name="e">SessionEvent data to send.</param>
            /// <param name="id">Channel to send on.</param>
            /// <param name="protocol">What protocol the data should use.</param>
            public void Send(int clientId, SessionAction e, byte id, MessageProtocol protocol)
            {
                List<Message> messages = new List<Message>(1);
                messages.Add(new SessionMessage(id, clientId, e));
                Send(messages, protocol);
            }


            /// <summary>Send byte array.</summary>
            /// <param name="buffer">Binary data to send.</param>
            /// <param name="id">Channel to send on.</param>
            public void Send(byte[] buffer, byte id)
            {
                Send(buffer, id, MessageProtocol.Tcp);
            }

            /// <summary>Send object.</summary>
            /// <param name="o">Object data to send.</param>
            /// <param name="id">Channel to send on.</param>
            public void Send(Object o, byte id)
            {
                Send(o, id, MessageProtocol.Tcp);
            }

            /// <summary>Send string.</summary>
            /// <param name="s">String data to send.</param>
            /// <param name="id">Channel to send on.</param>
            public void Send(String s, byte id)
            {
                Send(s, id, MessageProtocol.Tcp);
            }

            public void Send(Message message, MessageProtocol protocol)
            {
                List<Message> messages = new List<Message>(1);
                messages.Add(message);
                Send(messages, protocol);
            }

            /// <summary>Sends a raw byte array of any data type using these parameters.  
            /// This can be used to manipulate received messages without converting it to another 
            /// format, for example, a string or an object.  Using this method, received messages 
            /// can be repeated, compared, or passed along faster than otherwise.</summary>
            /// <param name="messages">The messages to go across.</param>
            /// <param name="protocol">What protocol the data should use.</param>
            public void Send(IList<Message> messages, MessageProtocol protocol)
            {
                lock (this)
                {
                    if (dead)
                    {
                        throw new InvalidStateException("cannot send on a disposed client!", this);
                    }
                    ITransport t = FindTransport(protocol);
                    Stream ms = t.GetPacketStream();
                    int packetStart = (int)ms.Position;
                    while (messages.Count > 0)
                    {
                        Message m = messages[0];
                        int packetEnd = (int)ms.Position;
                        server.Marshaller.Marshal(m, ms, t);
                        bool dontRemove = false;
                        if (ms.Position - packetStart > t.MaximumPacketSize) // uh oh, rewind and redo
                        {
                            ms.SetLength(packetEnd);
                            dontRemove = true;  // need to redo it
                            ms.Position = packetStart;
                            t.SendPacket(ms);
                            
                            ms = t.GetPacketStream();
                            packetStart = (int)ms.Position;
                        }
                        else { messages.RemoveAt(0); }
                    }
                    if (ms.Position - packetStart != 0)
                    {
                        ms.Position = packetStart;
                        t.SendPacket(ms);
                    }
                }
            }

            #endregion

            protected ITransport FindTransport(MessageProtocol protocol)
            {
                ITransport t;
                if (!transports.TryGetValue(protocol, out t))
                {
                    throw new NoMatchingTransport("Cannot find matching transport: " + protocol);
                }
                return t;
            }



            /// <summary>Handles a system data in that it takes the information and does something with it.</summary>
            /// <param name="data">The data we received.</param>
            /// <param name="id">What channel it came in on.</param>
            private void HandleSystemMessage(SystemMessage m, ITransport t)
            {
                switch((SystemMessageType)m.id) {
                case SystemMessageType.UniqueIDRequest:
                    //they want to know their own id
                    Send(new SystemMessage(SystemMessageType.UniqueIDRequest, 
                        BitConverter.GetBytes(UniqueIdentity)), t.MessageProtocol);
                    break;

                case SystemMessageType.PingResponse:
                    //record the difference; half of it is the latency between this client and the server
                    int newDelay = (System.Environment.TickCount - BitConverter.ToInt32(m.data, 0)) / 2;
                    t.Delay = newDelay;
                    break;

                case SystemMessageType.PingRequest:
                    SendMessage((ITransport)t, new SystemMessage(SystemMessageType.PingResponse, m.data));
                    break;
                }
            }

            /// <summary>Send a ping to a client to see if it responds.</summary>
            internal void Ping()
            {
                byte[] buffer = BitConverter.GetBytes(System.Environment.TickCount);
                foreach (ITransport t in transports.Values)
                {
                    SendMessage(t, new SystemMessage(SystemMessageType.PingRequest,
                        BitConverter.GetBytes(System.Environment.TickCount)));
                }
            }

            protected void SendMessage(ITransport transport, Message msg)
            {
                //pack main message into a buffer and send it right away
                Stream packet = transport.GetPacketStream();
                server.Marshaller.Marshal(msg, packet, transport);

                // and be sure to catch exceptions; log and remove transport if unable to be started
                // if(!transport.Active) { transport.Start(); }
                transport.SendPacket(packet);
            }

            #region Receive

            /// <summary>Go through one tick of this client.</summary>
            public void Update()
            {
                lock (this)
                {
                    if (dead) { return; }
                    foreach (ITransport t in transports.Values)
                    {
                        t.Update();
                    }
                }
            }


            private void PostNewlyReceivedPacket(byte[] buffer, int offset, int count, ITransport t)
            {
                Stream stream = new MemoryStream(buffer, offset, count, false);
                while (stream.Position < stream.Length)
                {
                    Message m = server.Marshaller.Unmarshal(stream, t);
                    //DebugUtils.DumpMessage("Server.Client.PostNewlyReceivedMessage", m);

                    if (m.type == MessageType.System)
                    {
                        //System messages are special!  Yay!
                        HandleSystemMessage((SystemMessage)m, t);
                    }
                    else
                    {
                        MessageReceived(m, this, t.MessageProtocol);
                    }
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
