using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace GTServer
{

    #region Enumerations

    /// <summary>Internal message types for SystemMessages to have.</summary>
    internal enum SystemMessageType
    {
        UniqueIDRequest = 1,
        UDPPortRequest = 2,
        UDPPortResponse = 3,
        ServerPingAndMeasure = 4,
        ClientPingAndMeasure = 5
    }

    /// <summary>The type of message</summary>
    public enum MessageType
    {
        /// <summary>A byte array is sent as the payload of a message.</summary>
        Binary = 1,
        /// <summary>An object is sent as the payload of a message.</summary>
        Object = 2,
        /// <summary>A string is sent as the payload of a message.</summary>
        String = 3,
        /// <summary>This is used by the system to communicate internally.</summary>
        System = 4,
        /// <summary>A session action is sent as the payload of a message.</summary>
        Session = 5,
        /// <summary>This message refers to a streaming 1-tuple</summary>
        Tuple1D = 6,
        /// <summary>This message refers to a streaming 2-tuple</summary>
        Tuple2D = 7,
        /// <summary>This message refers to a streaming 3-tuple</summary>
        Tuple3D = 8
    }

    /// <summary>What protocol should we use for this message.</summary>
    public enum MessageProtocol
    {
        /// <summary>Completely reliable, but slow.</summary>
        Tcp = 1,
        /// <summary>Completely unreliable, but fast.</summary>
        Udp = 2
    }

    /// <summary>Session action performed.  We can add a lot more to this list.</summary>
    public enum SessionAction
    {
        /// <summary>This client is joining this session.</summary>
        Joined = 1,
        /// <summary>This client is part of this session.</summary>
        Lives = 2,
        /// <summary>This client is inactive.</summary>
        Inactive = 3,
        /// <summary>This client is leaving this session.</summary>
        Left = 4
    }

    #endregion

    #region Delegates

    /// <summary>Handles a tick event, which is one loop of the server</summary>
    public delegate void TickHandler();

    /// <summary>Handles a Message event, when a message arrives</summary>
    /// <param name="id">The id of the channel the message was sent along</param>
    /// <param name="type">The type of message sent</param>
    /// <param name="data">The data sent</param>
    /// <param name="client">Who sent the message</param>
    /// <param name="protocol">How the message was sent</param>
    public delegate void MessageHandler(byte id, MessageType type, byte[] data, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a SessionMessage event, when a SessionMessage arrives.</summary>
    /// <param name="e">The action performed.</param>
    /// <param name="id">The id of the channel the message was sent along.</param>
    /// <param name="client">Who sent the message.</param>
    /// <param name="protocol">How the message was sent</param>
    public delegate void SessionMessageHandler(SessionAction e, byte id, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a StringMessage event, when a StringMessage arrives.</summary>
    /// <param name="s">The string sent.</param>
    /// <param name="id">The id of the channel the message was sent along.</param>
    /// <param name="client">Who sent the message.</param>
    /// <param name="protocol">How the message was sent</param>
    public delegate void StringMessageHandler(byte[] s, byte id, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a ObjectMessage event, when a ObjectMessage arrives.</summary>
    /// <param name="o">The object sent.</param>
    /// <param name="id">The id of the channel the message was sent along.</param>
    /// <param name="client">Who sent the message.</param>
    /// <param name="protocol">How the message was sent</param>
    public delegate void ObjectMessageHandler(byte[] o, byte id, Server.Client client, MessageProtocol protocol);

    /// <summary>Handles a BinaryMessage event, when a BinaryMessage arrives.</summary>
    /// <param name="b">The byte array sent.</param>
    /// <param name="id">The id of the channel the message was sent along.</param>
    /// <param name="client">Who sent the message.</param>
    /// <param name="protocol">How the message was sent</param>
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

    #region Helper Classes

    /// <summary>An outbound message</summary>
    public class MessageOut
    {
        /// <summary>The channel that this message is on.</summary>
        public byte id;

        /// <summary>The type of message.</summary>
        public MessageType type;

        /// <summary>The data of the message.</summary>
        public byte[] data;

        /// <summary>Creates a new outbound message.</summary>
        /// <param name="id">The channel that this message is on.</param>
        /// <param name="type">The type of message.</param>
        /// <param name="data">The data of the message.</param>
        public MessageOut(byte id, MessageType type, byte[] data)
        {
            this.id = id;
            this.type = type;
            this.data = data;
        }

        /// <summary>Creates a new outbound message.</summary>
        /// <param name="id">The channel that this message is on.</param>
        /// <param name="s">TThe string to send.</param>
        public MessageOut(byte id, string s)
        {
            this.data = Server.StringToBytes(s);
            this.id = id;
            this.type = MessageType.String;
        }

        /// <summary>Creates a new outbound message.</summary>
        /// <param name="id">The channel that this message is on.</param>
        /// <param name="client">The client doing the action.</param>
        /// <param name="action">The action being done.</param>
        public MessageOut(byte id, Server.Client client, SessionAction action)
        {
            this.data = new byte[5];
            BitConverter.GetBytes(client.UniqueIdentity).CopyTo(this.data, 0);
            this.data[4] = (byte)action;
            this.id = id;
            this.type = MessageType.Session;
        }
    }

    /// <summary>Represents a 1-tuple.</summary>
    /// <typeparam name="T">The type of the tuple parameter T.</typeparam>
    public class RemoteTuple<T>
    {
        /// <summary>The value of this tuple.</summary>
        protected T x;
        /// <summary>A value of this tuple.</summary>
        public T X { get { return x; } set { x = value; } }
        /// <summary>Constructor.</summary>
        public RemoteTuple() { }
        /// <summary>Constructor.</summary>
        public RemoteTuple(T x)
        {
            this.x = x;
        }
    }

    /// <summary>Represents a 1-tuple.</summary>
    /// <typeparam name="T">The type of the tuple parameter T.</typeparam>
    /// <typeparam name="K">The type of the tuple parameter T.</typeparam>
    public class RemoteTuple<T, K>
    {
        /// <summary>A value of this tuple.</summary>
        protected T x;
        /// <summary>A value of this tuple.</summary>
        public T X { get { return x; } set { x = value; } }
        /// <summary>A value of this tuple.</summary>
        protected K y;
        /// <summary>A value of this tuple.</summary>
        public K Y { get { return y; } set { y = value; } }
        /// <summary>Constructor.</summary>
        public RemoteTuple() { }
        /// <summary>Constructor.</summary>
        public RemoteTuple(T x, K y)
        {
            this.x = x;
            this.y = y;
        }
    }

    /// <summary>Represents a 1-tuple.</summary>
    /// <typeparam name="T">The type of the tuple parameter T.</typeparam>
    /// <typeparam name="K">The type of the tuple parameter K.</typeparam>
    /// <typeparam name="J">The type of the tuple parameter J.</typeparam>
    public class RemoteTuple<T, K, J> 
    {
        /// <summary>A value of this tuple.</summary>
        protected T x;
        /// <summary>A value of this tuple.</summary>
        public T X { get { return x; } set { x = value; } }
        /// <summary>A value of this tuple.</summary>
        protected K y;
        /// <summary>A value of this tuple.</summary>
        public K Y { get { return y; } set { y = value; } }
        /// <summary>A value of this tuple.</summary>
        protected J z;
        /// <summary>A value of this tuple.</summary>
        public J Z { get { return z; } set { z = value; } }
        /// <summary>Constructor.</summary>
        public RemoteTuple() { }
        /// <summary>Constructor.</summary>
        public RemoteTuple(T x, K y, J z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    #endregion

    /// <summary>Represents a server.</summary>
    public class Server
    {
        #region Variables and Properties

        private static Random random = new Random();
        private static BinaryFormatter formatter = new BinaryFormatter();
        TcpListener bouncer;
        private int lastPingTime = 0;
        private int port;

        /// <summary>Last exception encountered.</summary>
        public Exception LastError = null;

        /// <summary>All of the clients that this server knows about.</summary>
        public List<Client> ClientList = new List<Client>();

        /// <summary>All of the clientIDs that this server knows about.  
        /// Hide this so that users cannot cause mischief.  I accept that this list may 
        /// not be accurate because the users have direct access to the ClientList.</summary>
        private Dictionary<int, Client> clientIDs = new Dictionary<int, Client>();

        /// <summary>Time in milliseconds between keep-alive pings.</summary>
        public int PingInterval = 10000;

        /// <summary>Time in milliseconds between server ticks.</summary>
        public int Interval = 10;

        #endregion

        #region Events

        /// <summary>Invoked each cycle of the server.</summary>
        public event TickHandler Tick;

        /// <summary>Invoked each time a message is received.</summary>
        public event MessageHandler MessageReceived;

        /// <summary>Invoked each time a session message is received.</summary>
        public event SessionMessageHandler SessionMessageReceived;

        /// <summary>Invoked each time a string message is received.</summary>
        public event StringMessageHandler StringMessageReceived;

        /// <summary>Invoked each time a object message is received.</summary>
        public event ObjectMessageHandler ObjectMessageReceived;

        /// <summary>Invoked each time a binary message is received.</summary>
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
            bouncer = new TcpListener(IPAddress.Any, port);
            bouncer.ExclusiveAddressUse = true;
            bouncer.Start(1);
        }

        /// <summary>Creates a new Server object.</summary>
        /// <param name="port">The port to listen on.</param>
        /// <param name="interval">The interval at which to check for new connections or new data.</param>
        public Server(int port, int interval)
        {
            this.port = port;
            this.Interval = interval;
            bouncer = new TcpListener(IPAddress.Any, port);
            bouncer.ExclusiveAddressUse = true;
            bouncer.Start(1);
        }

        /// <summary>Starts a new thread that listens for new clients or new data.
        /// Abort returned thread at any time to stop listening.</summary>
        public Thread StartSeparateListeningThread(int interval)
        {
            this.Interval = interval;

            Thread t = new Thread(new ThreadStart(StartListening));
            t.Name = "Listening Thread";
            t.IsBackground = true;
            t.Start();
            return t;
        }

        private void ErrorClientHandlerMethod(Exception e, SocketError se, Client client, string ex)
        {
            if (ErrorEvent != null)
                ErrorEvent(e, se, client, ex);
        }

        /// <summary>One tick of the server, manually
        /// Use if not using Start
        /// </summary>
        public void Update()
        {

            if (bouncer == null)
                RestartBouncer();
            else
            {
                //add new clients
                List<Client> listA = new List<Client>();
                TcpClient connection;
                while (bouncer.Pending())
                {
                    //let them join us
                    try
                    {
                        connection = bouncer.AcceptTcpClient();
                    }
                    catch (Exception e)
                    {
                        LastError = e;
                        if (ErrorEvent != null)
                            ErrorEvent(e, SocketError.Fault, null, "An error occurred when trying to accept new client.");
                        bouncer = null;
                        break;
                    }

                    //set them up with a session
                    Client client = new Client(connection, GenerateUniqueIdentity());
                    ClientList.Add(client);
                    client.MessageReceivedDelegate = new MessageHandler(client_MessageReceived);
                    client.MessageReceived += client.MessageReceivedDelegate;
                    client.ErrorEventDelegate = new ErrorClientHandler(ErrorClientHandlerMethod);
                    client.ErrorEvent += client.ErrorEventDelegate;
                    Console.WriteLine("Client created: " + client.UniqueIdentity);
                    clientIDs.Add(client.UniqueIdentity, client);
                    listA.Add(client);
                }
                if (listA.Count > 0 && ClientsJoined != null)
                    ClientsJoined(listA);

            }

            //ping, if needed
            if (lastPingTime + PingInterval < System.Environment.TickCount)
            {
                lastPingTime = System.Environment.TickCount;
                foreach (Client c in ClientList)
                    c.Ping();
            }

            //update all clients, reading from the network
            foreach (Client c in ClientList)
            {
                c.Update();
            }

            //if anyone is listening, tell them we're done one cycle
            if (Tick != null)
                Tick();

            //remove dead clients
            List<Client> listD = ClientList.FindAll(Client.isDead);
            if (listD.Count > 0)
            {
                foreach (Client c in listD)
                {
                    ClientList.Remove(c);
                    c.MessageReceived -= c.MessageReceivedDelegate;
                    c.ErrorEvent -= c.ErrorEventDelegate;
                    clientIDs.Remove(c.UniqueIdentity);
                    c.Dead = true;  //make sure it's gone
                }
                if (ClientsRemoved != null)
                    ClientsRemoved(listD);
            }
        }

        /// <summary>Returns the client matching that unique identity number.</summary>
        /// <param name="uniqueIdentity">The unique identity of this client</param>
        /// <returns>The client with that unique identity.  If the number doesn't 
        /// match a client, then it returns null.</returns>
        public Client GetClientFromUniqueIdentity(int uniqueIdentity)
        {
            if (clientIDs.ContainsKey(uniqueIdentity))
                return clientIDs[uniqueIdentity];
            return null;
        }

        /// <summary>Starts a new thread that listens for new clients or new data on the current thread.</summary>
        public void StartListening()
        {
            int oldTickCount;
            int newTickCount;

            //start up the listener
            RestartBouncer();

            try
            {
                //never die!
                while (true)
                {
                    try
                    {
                        //check this server for new connections or new data forevermore
                        while (true)
                        {
                            oldTickCount = System.Environment.TickCount;


                            Update();


                            newTickCount = System.Environment.TickCount;
                            int sleepCount = Math.Max(this.Interval - (newTickCount - oldTickCount), 0);
                            Thread.Sleep(sleepCount);
                        }
                    }
                    catch (ThreadAbortException t ) { throw t; }
                    catch (Exception e)
                    {
                        LastError = e;
                        if(ErrorEvent != null)
                            ErrorEvent(e, SocketError.Fault, null, "An error occurred in the server.");
                    }
                }
            }
            catch (ThreadAbortException) 
            {
                try
                {
                    KillAll();
                }
                catch (Exception) { }
                //we were told to die.  die gracefully.
            }
        }

        /// <summary>Handle a message that was received by a client.</summary>
        /// <param name="id">Channel of the message.</param>
        /// <param name="type">Type of Message sent.</param>
        /// <param name="data">The data of the Message.</param>
        /// <param name="client">Which client sent it.</param>
        /// <param name="protocol">How the message was sent</param>
        void client_MessageReceived(byte id, MessageType type, byte[] data, Client client, MessageProtocol protocol)
        {
            //sort to the correct message type
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
            for (int i = 0; i < ClientList.Count; i++)
                ClientList[i].Dead = true;
        }

        private void RestartBouncer()
        {
            try
            {
                bouncer.Server.LingerState.Enabled = false;
                bouncer.Server.Close();
            }
            catch (ThreadAbortException t) { throw t; }
            catch (Exception) { }

            bouncer = null;
            try
            {
                bouncer = new TcpListener(IPAddress.Any, this.port);
                bouncer.ExclusiveAddressUse = true;
                bouncer.Start(1);
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

        /// <summary>Generates a unique identity number that clients can use to identify each other.</summary>
        /// <returns>The unique identity number</returns>
        public int GenerateUniqueIdentity()
        {
            int number = random.Next(Int32.MinValue, Int32.MaxValue);

            while(clientIDs.ContainsKey(number))
                number = random.Next(Int32.MinValue, Int32.MaxValue);

            return number;
        }

        #endregion

        #region List Sending

        /// <summary>Sends a byte array as a message on the id binary channel to many clients in an efficient way.</summary>
        /// <param name="buffer">The byte array to send</param>
        /// <param name="id">The id of the channel to send it on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="reli">How to send it</param>
        public void Send(byte[] buffer, byte id, List<Client> list, MessageProtocol reli)
        {
            byte[] data = Client.FromMessageToBytes(buffer, id);
            SendToList(data, list, reli);
        }

        /// <summary>Sends a string as a message on the id binary channel to many clients in an efficient way.</summary>
        /// <param name="s">The byte array to send</param>
        /// <param name="id">The id of the channel to send it on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="reli">How to send it</param>
        public void Send(string s, byte id, List<Client> list, MessageProtocol reli)
        {
            byte[] data = Client.FromMessageToBytes(s, id);
            SendToList(data, list, reli);
        }

        /// <summary>Sends a byte array as a message on the id binary channel to many clients in an efficient way.</summary>
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
        public void Send(List<MessageOut> messagesOut, List<Client> list, MessageProtocol reli)
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
            #region Variables and Properties

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
                    return ((IPEndPoint)connection.Client.RemoteEndPoint).Address.ToString();
                }
            }
            /// <summary>Theremote computer's Port, or null if not set.</summary>
            public string RemotePort
            {
                get
                {
                    return ((IPEndPoint)connection.Client.RemoteEndPoint).Port.ToString();
                }
            }
            /// <summary>The unique id of this client</summary>
            public int UniqueIdentity
            {
                get { return uniqueIdentity; }
            }

            /// <summary>All of the received Binary Messages that we have kept.</summary>
            public List<byte[]> BinaryMessages;
            /// <summary>All of the received Object Messages that we have kept.</summary>
            public List<byte[]> ObjectMessages;
            /// <summary>All of the received String Messages that we have kept.</summary>
            public List<byte[]> StringMessages;

            /// <summary>Triggered when a message is received.</summary>
            public event MessageHandler MessageReceived;
            internal MessageHandler MessageReceivedDelegate;

            /// <summary>Triggered when an error occurs in this client.</summary>
            public event ErrorClientHandler ErrorEvent;
            internal ErrorClientHandler ErrorEventDelegate;

            /// <summary>Last exception encountered.</summary>
            public Exception LastError;

            #region Private Variables and Properties

            private const int bufferSize = 512;
            private TcpClient connection;
            private UdpClient udpRoute;
            private MemoryStream tcpIn;
            private MemoryStream udpIn;
            private List<byte[]> tcpOut;
            private List<byte[]> udpOut;
            private int tcpInBytesLeft;
            private byte tcpInMessageType;
            private byte tcpInID;
            private bool dead;
            private short udpPort = 0;
            private int uniqueIdentity;
            private float delay = 20;

            #endregion

            #endregion


            #region Constructors and Destructors

            /// <summary>Creates a new Client to communicate with.</summary>
            /// <param name="connection">The connection to communicate over.</param>
            /// <param name="id">The unique identity of this new Client.</param>
            public Client(TcpClient connection, int id)
            {
                lock (this)
                {
                    uniqueIdentity = id;
                    tcpIn = new MemoryStream();
                    udpIn = new MemoryStream();
                    tcpOut = new List<byte[]>();
                    udpOut = new List<byte[]>();
                    BinaryMessages = new List<byte[]>();
                    ObjectMessages = new List<byte[]>();
                    StringMessages = new List<byte[]>();
                    connection.NoDelay = true;
                    connection.SendTimeout = 1;
                    connection.ReceiveTimeout = 1;
                    connection.Client.Blocking = false;
                    this.connection = connection;

                    //reserve a udp port
                    udpRoute = new UdpClient();
                    udpRoute.Client.Blocking = false;
                    udpRoute.DontFragment = true;
                    udpRoute.Client.SendTimeout = 1;
                    udpRoute.Client.ReceiveTimeout = 1;
                    udpRoute.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    udpPort = (short)((IPEndPoint)udpRoute.Client.LocalEndPoint).Port;

                    dead = false;
                }
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
                lock (c) 
                {
                    c.dead = true;

                    try
                    {
                        c.connection.Client.LingerState.Enabled = false;
                        c.connection.Client.Close();
                    }
                    catch (Exception) { }

                    try
                    {
                        c.udpRoute.Client.LingerState.Enabled = false;
                        c.udpRoute.Client.Close();
                    }
                    catch (Exception) { }
                }
            }

            #region Internal

            /// <summary>Add the id and the message type to the front of a raw binary message</summary>
            /// <param name="data"></param>
            /// <param name="id"></param>
            /// <param name="type"></param>
            /// <returns></returns>
            internal static byte[] FromMessageToBytes(byte[] data, byte id, MessageType type)
            {
                byte[] buffer = new byte[data.Length + 8];
                buffer[0] = id;
                buffer[1] = (byte)type;
                BitConverter.GetBytes(data.Length).CopyTo(buffer, 4);
                data.CopyTo(buffer, 8);
                return buffer;
            }

            /// <summary>Give bytes to be sent a message header and return as bytes</summary>
            /// <param name="data"></param>
            /// <param name="id"></param>
            /// <returns></returns>
            internal static byte[] FromMessageToBytes(byte[] data, byte id)
            {
                return FromMessageToBytes(data, id, MessageType.Binary);
            }

            /// <summary>Give a string to be sent a message header and return as bytes</summary>
            /// <param name="s"></param>
            /// <param name="id"></param>
            /// <returns></returns>
            internal static byte[] FromMessageToBytes(string s, byte id)
            {
                byte[] buffer = System.Text.ASCIIEncoding.ASCII.GetBytes(s);
                return FromMessageToBytes(buffer, id, MessageType.String);
            }

            /// <summary>Give an object to be sent a message header and return as bytes</summary>
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
            /// The bytes include the 2 byte message headers.</summary>
            /// <param name="messagesOut"></param>
            /// <returns></returns>
            internal static byte[] FromMessageToBytes(List<MessageOut> messagesOut)
            {
                int sizeMax = 0;
                int size = 0;

                foreach (MessageOut message in messagesOut)
                    sizeMax += message.data.Length + 8;

                byte[] buffer = new byte[sizeMax];
                byte[] data;
                foreach (MessageOut message in messagesOut)
                {
                    data = FromMessageToBytes(message.data, message.id, message.type);
                    data.CopyTo(buffer, size);
                    size += data.Length;
                }

                return buffer;
            }

            #endregion

            #endregion


            #region Send

            /// <summary>Send SessionAction.</summary>
            /// <param name="clientId">Client who is doing the action.</param>
            /// <param name="e">SessionEvent data to send.</param>
            /// <param name="id">Channel to send on.</param>
            /// <param name="protocol">What protocol the message should use.</param>
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
            /// <param name="protocol">What protocol the message should use.</param>
            public void Send(byte[] buffer, byte id, MessageProtocol protocol)
            {
                this.Send(buffer, id, MessageType.Binary, protocol);
            }

            /// <summary>Send object.</summary>
            /// <param name="o">Object data to send.</param>
            /// <param name="id">Channel to send on.</param>
            /// <param name="protocol">What protocol the message should use.</param>
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
            /// <param name="protocol">What protocol the message should use.</param>
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

            /// <summary>Sends a raw byte array of any message type using these parameters.  
            /// This can be used to manipulate received messages without converting it to another 
            /// format, for example, a string or an object.  Using this method, received messages 
            /// can be repeated, compared, or passed along faster than otherwise.</summary>
            /// <param name="data">Raw bytes to be sent.</param>
            /// <param name="id">The channel to send on.</param>
            /// <param name="type">The raw message type.</param>
            /// <param name="protocol">What protocol the message should use.</param>
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

            /// <summary>Sends a message via UDP.
            /// We don't care if it doesn't get through.</summary>
            /// <param name="buffer">Raw stuff to send.</param>
            internal void SendUdpMessage(byte[] buffer)
            {
                if (this.dead) return;

                if (udpRoute.Client.Connected)
                {

                    SocketError error = SocketError.Success;
                    if (TryAndFlushOldUdpBytesOut())
                    {
                        udpOut.Add(buffer);
                        return;
                    }

                    try
                    {
                        udpRoute.Client.Send(buffer, 0, buffer.Length, SocketFlags.None, out error);
                        switch (error)
                        {
                            case SocketError.Success:
                                return;
                            case SocketError.WouldBlock:
                                udpOut.Add(buffer);
                                if (ErrorEvent != null)
                                    ErrorEvent(null, error, this, "The TCP write buffer is full now, but the data will be saved and " +
                                        "sent soon.  Send less data to reduce perceived latency.");
                                return;
                            default:
                                udpOut.Add(buffer);
                                if (ErrorEvent != null)
                                    ErrorEvent(null, error, this, "Error occurred while trying to send TCP to client.");
                                return;
                        }
                    }
                    catch (Exception e)
                    {
                        udpOut.Add(buffer);
                        LastError = e;
                        if (ErrorEvent != null)
                            ErrorEvent(e, SocketError.NoRecovery, this, "Exception occurred while trying to send TCP to client.");
                    }
                }
                else
                {
                    //save this data to be sent later
                    udpOut.Add(buffer);
                    //request the client for the port to send to
                    byte[] data = BitConverter.GetBytes((short)(((IPEndPoint)udpRoute.Client.LocalEndPoint).Port));
                    try
                    {
                        Send(data, (byte)SystemMessageType.UDPPortRequest, MessageType.System, MessageProtocol.Tcp);
                    }
                    catch (Exception e)
                    {
                        //don't save this or die, but still throw an error
                        LastError = e;
                        if (ErrorEvent != null)
                            ErrorEvent(e, SocketError.Fault, this, "Failed to Request UDP port from client.");
                    }
                }
            }

            /// <summary>Sends a message via TCP.
            /// We DO care if it doesn't get through; we throw an exception.
            /// </summary>
            /// <param name="buffer">Raw stuff to send.</param>
            internal void SendTcpMessage(byte[] buffer)
            {
                if (this.dead) return;

                if (TryAndFlushOldTcpBytesOut())
                {
                    tcpOut.Add(buffer);
                    return;
                }

                SocketError error = SocketError.Success;
                try
                {
                    connection.Client.Send(buffer, 0, buffer.Length, SocketFlags.None, out error);
                    switch (error)
                    {
                        case SocketError.Success:
                            return;
                        case SocketError.WouldBlock:
                            tcpOut.Add(buffer);
                            if (ErrorEvent != null)
                                ErrorEvent(null, error, this, "The TCP write buffer is full now, but the data will be saved and " +
                                    "sent soon.  Send less data to reduce perceived latency.");
                            return;
                        default:
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
                        udpRoute.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

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
                        connection.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

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

            /// <summary>Handles a system message in that it takes the information and does something with it.</summary>
            /// <param name="data">The message we received.</param>
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
                    IPEndPoint remoteUdp = new IPEndPoint(((IPEndPoint)connection.Client.RemoteEndPoint).Address, port);
                    udpRoute.Connect(remoteUdp);

                    //they want the udp port.  Send it.
                    BitConverter.GetBytes(UdpPort).CopyTo(buffer, 0);
                    Send(buffer, (byte)SystemMessageType.UDPPortResponse, MessageType.System, MessageProtocol.Tcp);
                }
                else if (id == (byte)SystemMessageType.UDPPortResponse)
                {
                    //they want to receive udp messages on this port
                    short port = BitConverter.ToInt16(data, 0);
                    IPEndPoint remoteUdp = new IPEndPoint(((IPEndPoint)connection.Client.RemoteEndPoint).Address, port);
                    udpRoute.Connect(remoteUdp);
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
                    //if something went wrong at a lower level, die.
                    if (!connection.Connected || dead)
                    {
                        dead = true;
                        return;
                    }

                    //grab data if we can
                    while (connection.Available > 0)
                    {
                        try
                        {
                            this.UpdateFromNetworkTcp();
                        }
                        catch(SocketException e)
                        {
                            dead = true;
                            LastError = e;
                            if (ErrorEvent != null)
                                ErrorEvent(e, SocketError.NoRecovery, this, "Updating from TCP connection failed because of a socket exception.");
                        }
                        catch (Exception e)
                        {
                            LastError = e;
                            if (ErrorEvent != null)
                                ErrorEvent(e, SocketError.NoRecovery, this, "Exception occured (not socket exception).");
                        }
                    }

                    this.UpdateFromNetworkUdp();
                }
            }

            /// <summary>Gets available data from UDP.</summary>
            private void UpdateFromNetworkUdp()
            {
                byte[] buffer, data;
                int length, cursor;
                byte id, type;
                IPEndPoint ep = null;


                try
                {
                    //while there are more packets to read
                    while (udpRoute.Client.Available > 8)
                    {
                        //get a packet
                        buffer = udpRoute.Receive(ref ep);
                        cursor = 0;

                        //while there are more messages in this packet
                        while(cursor < buffer.Length)
                        {
                            id = buffer[cursor];
                            type = buffer[cursor + 1];
                            length = BitConverter.ToInt32(buffer, cursor + 4);
                            data = new byte[length];
                            Array.Copy(buffer, 8, data, 0, length);

                            MessageType typeForm = (MessageType)type;
                            if (typeForm == MessageType.Session) //session messages are special!  Weee!
                                data = ConvertIncomingSessionMessageToNormalForm(data);

                            if (typeForm == MessageType.System)//system messages are special!  Weee!
                                HandleSystemMessage(data, id);
                            else
                                MessageReceived(id, typeForm, data, this, MessageProtocol.Udp);

                            cursor += length + 8;
                        }
                    }
                }
                catch (SocketException e)
                {
                    //Exception 10035 means that a non-blocking port has nothing to read, which is okay.
                    if (e.ErrorCode != 10035)
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

            /// <summary>Gets one message from the tcp and interprets it.</summary>
            private void UpdateFromNetworkTcp()
            {
                byte[] buffer;
                int size;
                SocketError error = SocketError.Success;

                if (tcpInMessageType < 1)
                {
                    //restart the counters to listen for a new message.
                    if (connection.Available < 8)
                        return;

                    buffer = new byte[8];
                    size = connection.Client.Receive(buffer, 0, 8, SocketFlags.None, out error);
                    switch (error)
                    {
                        case SocketError.Success:
                            break;
                        default:
                            dead = true;
                            if (ErrorEvent != null)
                                ErrorEvent(null, SocketError.Fault, this, "Error reading TCP data header.");
                            return;
                    }
                    byte id = buffer[0];
                    byte type = buffer[1];
                    int length = BitConverter.ToInt32(buffer, 4);

                    this.tcpInBytesLeft = length;
                    this.tcpInMessageType = type;
                    this.tcpInID = id;
                }

                
                int amountToRead = Math.Min(tcpInBytesLeft, bufferSize);
                buffer = new byte[amountToRead];

                size = connection.Client.Receive(buffer, 0, amountToRead, SocketFlags.None, out error);
                switch (error)
                {
                    case SocketError.Success:
                        break;
                    default:
                        dead = true;
                        if (ErrorEvent != null)
                            ErrorEvent(null, SocketError.Fault, this, "Error reading TCP data.");
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

                    MessageType typeForm = (MessageType)tcpInMessageType;
                    if (typeForm == MessageType.Session) //session messages are special!  Weee!
                        buffer = ConvertIncomingSessionMessageToNormalForm(buffer);

                    if (typeForm == MessageType.System)//System messages are special!  Yay!
                        HandleSystemMessage(buffer, tcpInID);
                    else
                        MessageReceived(tcpInID, typeForm, buffer, this, MessageProtocol.Tcp);

                    tcpInMessageType = 0;
                    tcpIn.Position = 0;
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
