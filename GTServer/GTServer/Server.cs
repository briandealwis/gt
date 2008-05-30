using System;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using GT;
using System.Net.Sockets;
using GT.Utils;

namespace GT.Net
{

    #region Delegates

    /// <summary>Handles a tick event, which is one loop of the server</summary>
    public delegate void TickHandler();

    /// <summary>Handles a SessionMessage event, when a SessionMessage arrives.</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the message.</param>
    /// <param name="transport">How the message was sent</param>
    public delegate void SessionMessageHandler(Message m, IConnexion client, ITransport transport);

    /// <summary>Handles a StringMessage event, when a StringMessage arrives.</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the message.</param>
    /// <param name="transport">How the message was sent</param>
    public delegate void StringMessageHandler(Message m, IConnexion client, ITransport transport);

    /// <summary>Handles a ObjectMessage event, when a ObjectMessage arrives.</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the message.</param>
    /// <param name="transport">How the message was sent</param>
    public delegate void ObjectMessageHandler(Message m, IConnexion client, ITransport transport);

    /// <summary>Handles a BinaryMessage event, when a BinaryMessage arrives.</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the message.</param>
    /// <param name="transport">How the message was sent</param>
    public delegate void BinaryMessageHandler(Message m, IConnexion client, ITransport transport);

    /// <summary>Handles when clients leave the server.</summary>
    /// <param name="list">The clients who've left.</param>
    public delegate void ClientsRemovedHandler(ICollection<IConnexion> list);

    /// <summary>Handles when clients join the server.</summary>
    /// <param name="list">The clients who've joined.</param>
    public delegate void ClientsJoinedHandler(ICollection<IConnexion> list);

    /// <summary>Handles when there is an internal error that the application should know about.</summary>
    /// <param name="c">The client where the exception occurred</param>
    /// <param name="explanation">An explanation of the error encountered</param>
    /// <param name="context">A contextual object (e.g., an exception, a network error object)</param>
    public delegate void ErrorClientHandler(IConnexion c, string explanation, object context);


    #endregion

    public abstract class ServerConfiguration : BaseConfiguration
    {
        abstract public IMarshaller CreateMarshaller();
        abstract public ICollection<IAcceptor> CreateAcceptors();

        /// <summary>
        /// The time between pings to clients.
        /// </summary>
        abstract public TimeSpan PingInterval { get; set; }

        /// <summary>
        /// The time between server ticks.
        /// </summary>
        abstract public TimeSpan TickInterval { get; set; }

        abstract public Server BuildServer();

        virtual public ClientConnexion CreateClientConnexion(Server owner, 
            Guid clientId, int uniqueId)
        {
            return new ClientConnexion(owner, clientId, uniqueId);
        }

        virtual public ChannelDeliveryRequirements DefaultChannelRequirements()
        {
            return ChannelDeliveryRequirements.LeastStrict;
        }
    }

    public class DefaultServerConfiguration : ServerConfiguration
    {
        protected TimeSpan pingInterval = TimeSpan.FromMilliseconds(10000);
        protected TimeSpan tickInterval = TimeSpan.FromMilliseconds(10);
        protected int port = 9999;

        public DefaultServerConfiguration() { }

        public DefaultServerConfiguration(int port)
        {
            Debug.Assert(port > 0);
            this.port = port;
        }

        /// <summary>
        /// Create new configuration with given port and ping interval
        /// </summary>
        /// <param name="port">the port to be used for IP-based transports</param>
        /// <param name="pingInterval">the client-ping frequency (milliseconds to wait between pings)</param>
        public DefaultServerConfiguration(int port, int pingInterval)
            : this(port)
        {
            Debug.Assert(pingInterval > 0);
            this.pingInterval = TimeSpan.FromMilliseconds(pingInterval);
        }

        public override Server BuildServer()
        {
            return new Server(this);
        }


        public override IMarshaller CreateMarshaller()
        {
            return new DotNetSerializingMarshaller();
        }

        public override ICollection<IAcceptor> CreateAcceptors()
        {
            ICollection<IAcceptor> acceptors = new List<IAcceptor>();
            acceptors.Add(new TcpAcceptor(IPAddress.Any, port));
            acceptors.Add(new UdpAcceptor(IPAddress.Any, port));
            return acceptors;
        }

        override public TimeSpan PingInterval
        {
            get { return pingInterval; }
            set { pingInterval = value; }
        }

        override public TimeSpan TickInterval
        {
            get { return tickInterval; }
            set { tickInterval = value; }
        }
    }

    /// <summary>Represents traditional server.</summary>
    public class Server : IDisposable
    {
        #region Variables and Properties

        private static Random random = new Random();

        private bool running = false;
        private int uniqueIdentity;

        /// <summary>
        /// A factory-like object responsible for providing the server's runtime
        /// configuration.
        /// </summary>
        private ServerConfiguration configuration;

        private ICollection<IAcceptor> acceptors;
        private IMarshaller marshaller;
        private Dictionary<byte, ChannelDeliveryRequirements> channelRequirements
            = new Dictionary<byte,ChannelDeliveryRequirements>();

        private int lastPingTime = 0;

        /// <summary>All of the clientIDs that this server knows about.  
        /// Hide this so that users cannot cause mischief.  I accept that this list may 
        /// not be accurate because the users have direct access to the clientList.</summary>
        private Dictionary<int, ClientConnexion> clientIDs = new Dictionary<int, ClientConnexion>();
        private ICollection<ClientConnexion> newlyAddedClients = new List<ClientConnexion>();

        public ICollection<IConnexion> Clients { 
            get { return BaseConnexion.Downcast<IConnexion,ClientConnexion>(clientIDs.Values); } 
        }

        public IMarshaller Marshaller { get { return marshaller; } }
        public ServerConfiguration Configuration { get { return configuration; } }
        public int UniqueIdentity { get { return uniqueIdentity; } }

        #endregion

        #region Events

        /// <summary>Invoked each cycle of the server.</summary>
        public event TickHandler Tick;

        /// <summary>Invoked each time a message is received.</summary>
        public event MessageHandler MessageReceived;

        /// <summary>Invoked each time a session message is received.</summary>
        public event SessionMessageHandler SessionMessageReceived;

        /// <summary>Invoked each time a client disconnects.</summary>
        public event ClientsRemovedHandler ClientsRemoved;

        /// <summary>Invoked each time a client connects.</summary>
        public event ClientsJoinedHandler ClientsJoined;

        /// <summary>Invoked each time an error occurs.</summary>
        public event ErrorClientHandler ErrorEvent;

        #endregion



        /// <summary>Invoked each time a string message is received.</summary>
        public event StringMessageHandler StringMessageReceived;

        /// <summary>Invoked each time a object message is received.</summary>
        public event ObjectMessageHandler ObjectMessageReceived;

        /// <summary>Invoked each time a binary mesage is received.</summary>
        public event BinaryMessageHandler BinaryMessageReceived;

        /// <summary>Creates a new Server object.</summary>
        /// <param name="port">The port to listen on.</param>
        public Server(int port)
            : this(new DefaultServerConfiguration(port))
        {
        }

        /// <summary>Creates a new Server object.</summary>
        /// <param name="port">The port to listen on.</param>
        /// <param name="interval">The interval in milliseconds at which to check 
        /// for new connections or new messages.</param>
        public Server(int port, int interval)
            : this(new DefaultServerConfiguration(port, interval))
        {
        }

        /// <summary>Creates a new Server object.</summary>
        /// <param name="sc">The server configuration object.</param>
        public Server(ServerConfiguration sc)
        {
            configuration = sc;
            uniqueIdentity = GenerateUniqueIdentity();
        }


        #region Vital Server Mechanics
        /// <summary>Starts a new thread that listens for new clients or
        /// new messages.  Abort the returned thread at any time
        /// to stop listening.
        /// <param name="interval">The interval in milliseconds at which to check 
        /// for new connections or new message.</param> </summary>
        public Thread StartSeparateListeningThread(int interval)
        {
            configuration.TickInterval = TimeSpan.FromMilliseconds(interval);

            Thread t = new Thread(new ThreadStart(StartListening));
            t.Name = "Server Thread[" + this.ToString() + "]";
            t.IsBackground = true;
            t.Start();
            return t;
        }

        private void ErrorClientHandlerMethod(IConnexion client, string ex, object context)
        {
            Console.WriteLine("{0} ({1}): ERROR: {2}: {3}", this, client, ex, context);
            if (ErrorEvent != null) { ErrorEvent(client, ex, context); }
        }

        /// <summary>
        /// Create a descriptive string representation 
        /// </summary>
        /// <returns>a descriptive string representation</returns>
        override public string ToString()
        {
            return this.GetType().Name + "(" + Clients.Count + " clients)";
        }

        /// <summary>Process a single tick of the server.
        /// <strong>deprecated:</strong> the server is started if not active.
        /// </summary>
        public void Update()
        {
            DebugUtils.WriteLine(">>>> Server.Update() started");

            if (!Active) { Start(); }

            newlyAddedClients.Clear();
            UpdateAcceptors();
            if (newlyAddedClients.Count > 0 && ClientsJoined != null)
            {
                ClientsJoined(BaseConnexion.Downcast<IConnexion,ClientConnexion>(newlyAddedClients));
            }

            //ping, if needed
            if (System.Environment.TickCount - lastPingTime >= configuration.PingInterval.Ticks)
            {
                DebugUtils.WriteLine("Server.Update(): pinging clients");
                lastPingTime = System.Environment.TickCount;
                foreach (ClientConnexion c in clientIDs.Values) { c.Ping(); }
            }

            DebugUtils.WriteLine("Server.Update(): Clients.Update()");
            //update all clients, reading from the network
            foreach (ClientConnexion c in clientIDs.Values) { c.Update(); }

            //if anyone is listening, tell them we're done one cycle
            if (Tick != null) { Tick(); }

            //remove dead clients
            List<ClientConnexion> listD = FindAll(clientIDs.Values, ClientConnexion.IsDead);
            if (listD.Count > 0)
            {
                DebugUtils.WriteLine("Server.Update(): removing dead clients");
                foreach (ClientConnexion c in listD)
                {
                    clientIDs.Remove(c.UniqueIdentity);
                    c.Dispose();  //make sure it's gone
                }
                if (ClientsRemoved != null)
                {
                    ClientsRemoved(BaseConnexion.Downcast<IConnexion,ClientConnexion>(listD));
                }
            }

            DebugUtils.WriteLine("<<<< Server.Update() finished");
        }

        private void UpdateAcceptors()
        {
            List<IAcceptor> toRemove = new List<IAcceptor>();
            foreach (IAcceptor acc in acceptors)
            {
                if (!acc.Active) { toRemove.Add(acc); continue; }
                DebugUtils.WriteLine("Server.Update(): checking acceptor " + acc);
                try { acc.Update(); }
                catch (FatalTransportError e)
                {
                    Console.WriteLine("{0} {1} Error updating acceptor {2}: {3}",
                        DateTime.Now, this, acc, e);
                    toRemove.Add(acc);
                }
            }
            if (toRemove.Count == 0) { return; }
            DebugUtils.WriteLine("Trying to restart error-raising acceptors");
            foreach (IAcceptor acc in toRemove)
            {
                try { acc.Stop(); acc.Start(); }
                catch (Exception e)
                {
                    Console.WriteLine("{0} {1} ERROR: could not restart acceptor {1}",
                        DateTime.Now, this, acc);
                    acceptors.Remove(acc);
                }
            }
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

        protected void NewClient(ITransport t, Dictionary<string, string> capabilities)
        {
            Guid clientId;
            try
            {
                clientId = new Guid(capabilities[GTCapabilities.CLIENT_ID]);
            }
            catch (Exception e)
            {
                Console.WriteLine("{0}: EXCEPTION when decoding client's GUID: {1}",
                    DateTime.Now, e);
                t.Dispose();
                return;
            }
            ClientConnexion c = GetClientForClientIdentity(clientId);
            if (c == null)
            {
                Console.WriteLine("{0}: new client {1} via {2}", this, clientId, t);
                c = CreateNewClient(clientId);
                newlyAddedClients.Add(c);
            }
            else
            {
                Console.WriteLine("{0}: for client {1} via {2}", this, clientId, t);
            }
            c.AddTransport(t);
        }

        /// <summary>Returns the client matching that unique identity number.</summary>
        /// <param name="uniqueIdentity">The unique identity of this client</param>
        /// <returns>The client with that unique identity.  If the number doesn't 
        /// match a client, then it returns null.</returns>
        protected ClientConnexion GetClientForClientIdentity(Guid id)
        {
            foreach (ClientConnexion c in Clients)
            {
                if (c.ClientIdentity.Equals(id)) { return c; }
            }
            return null;
        }

        protected ClientConnexion CreateNewClient(Guid clientId)
        {
            ClientConnexion client = configuration.CreateClientConnexion(this, clientId, GenerateUniqueIdentity());
            client.MessageReceived += ReceivedClientMessage;
            client.ErrorEvent += ErrorClientHandlerMethod;

            clientIDs.Add(client.UniqueIdentity, client);
            return client;
        }

        /// <summary>Starts a new thread that listens for new clients or
        /// new messages on the current thread.</summary>
        public void StartListening()
        {
            int oldTickCount;
            int newTickCount;

            Start();

            //check this server for new connections or new messages forevermore
            while (running)
            {
                try
                {
                    // tick count is in milliseconds
                    oldTickCount = System.Environment.TickCount;

                    Update();

                    newTickCount = System.Environment.TickCount;
                    int sleepCount = Math.Max(0,
                        configuration.TickInterval.TotalMilliseconds - (newTickCount - oldTickCount));

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
                    Console.WriteLine("{0}: EXCEPTION: in listening loop: {1}", this, e);
                    if (ErrorEvent != null)
                        ErrorEvent(null, "An error occurred in the server.", e);
                }
            }
        }

        public void Sleep()
        {
            Sleep((int)configuration.TickInterval.TotalMilliseconds);
        }

        public void Sleep(int milliseconds)
        {
            Trace.TraceInformation("{0}: sleeping for {1}ms", this, milliseconds);

            // FIXME: This should be more clever and use Socket.Select()
            Thread.Sleep(milliseconds);
        }

        public void Start()
        {
            if (Active) { return; }
            acceptors = configuration.CreateAcceptors();
            foreach (IAcceptor acc in acceptors)
            {
                acc.NewClientEvent += new NewClientHandler(NewClient);
                acc.Start();
            }
            marshaller = configuration.CreateMarshaller();
            running = true;
        }

        public void Stop()
        {
            // we were told to die.  die gracefully.
            running = false;
            if (acceptors != null)
            {
                foreach (IAcceptor acc in acceptors)
                {
                    acc.Stop(); // FIXME: trap exceptions?
                }
            }
            KillAll();
        }

        public void Dispose()
        {
            Stop();
            if (acceptors != null)
            {
                foreach (IAcceptor acc in acceptors)
                {
                    try { acc.Dispose(); }
                    catch (Exception e)
                    {
                        ErrorEvent(null, 
                            "Acceptor " + acc + " threw exception on dispose", e);
                    }
                }
                acceptors = null;
            }
        }

        public bool Active
        {
            get { return running; }
        }

        private void KillAll()
        {
            if (clientIDs == null) { return; }
            foreach (ClientConnexion c in Clients)
            {
                try
                {
                    c.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0} EXCEPTION: while disposing client: {1}",
                        DateTime.Now, e);
                }
            }
            clientIDs.Clear();
        }

        /// <summary>Generates a unique identity number that clients can use to identify each other.</summary>
        /// <returns>The unique identity number</returns>
        protected int GenerateUniqueIdentity()
        {
            int clientId = 0;
            DateTime timeStamp = DateTime.Now;
            do
            {
                clientId = (timeStamp.Hour * 100 + timeStamp.Minute) * 100 + timeStamp.Second;
                clientId = clientId * 1000 + random.Next(0, 1000);
                // keep going until we create something never previously seen
            } while (clientId == uniqueIdentity || clientIDs.ContainsKey(clientId));
            return clientId;
        }

        #endregion

        #region Sending

        /// <summary>Sends a byte array on channel <c>id</c> to many clients in an efficient manner.</summary>
        /// <param name="buffer">The byte array to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="mdr">How to send it (can be null)</param>
        public void Send(byte[] buffer, byte id, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            List<Message> messages = new List<Message>(1);
            messages.Add(new BinaryMessage(id, buffer));
            Send(messages, list, mdr);
        }

        /// <summary>Sends a string on channel <c>id</c> to many clients in an efficient manner.</summary>
        /// <param name="s">The byte array to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="mdr">How to send it (can be null)</param>
        public void Send(string s, byte id, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            List<Message> messages = new List<Message>(1);
            messages.Add(new StringMessage(id, s));
            Send(messages, list, mdr);
        }

        /// <summary>Sends an object on channel <c>id</c> to many clients in an efficient manner.</summary>
        /// <param name="o">The bject to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="mdr">How to send it (can be null)</param>
        public void Send(object o, byte id, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            List<Message> messages = new List<Message>(1);
            messages.Add(new ObjectMessage(id, o));
            Send(messages, list, mdr);
        }

        /// <summary>Send a message to many clients in an efficient manner.</summary>
        /// <param name="message">The message to send</param>
        /// <param name="list">The list of clients to send it to</param>
        /// <param name="mdr">How to send it (can be null)</param>
        public void Send(Message message, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            List<Message> messages = new List<Message>(1);
            messages.Add(message);
            Send(messages, list, mdr);
        }

        /// <summary>Sends a collection of messages in an efficient way to a list of clients.</summary>
        /// <param name="messages">The list of messages</param>
        /// <param name="list">The list of clients</param>
        /// <param name="mdr">How to send it (can be null)</param>
        public void Send(IList<Message> messages, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            if (!running) { throw new InvalidStateException("Cannot send on a stopped server", this); }
            foreach (IConnexion c in list)
            {
                //Console.WriteLine("{0}: sending to {1}", this, c);
                try
                {
                    c.Send(messages, mdr, GetChannelDeliveryRequirements(messages[0].Id));
                }
                catch (Exception e)
                {
                    ErrorClientHandlerMethod(c, "EXCEPTION: when sending", e);
                }
            }
        }

        public ChannelDeliveryRequirements GetChannelDeliveryRequirements(byte id)
        {
            ChannelDeliveryRequirements cdr;
            if (channelRequirements.TryGetValue(id, out cdr)) { return cdr; }
            return configuration.DefaultChannelRequirements();
        }

        public void SetChannelDeliveryRequirements(byte id, ChannelDeliveryRequirements cdr)
        {
            channelRequirements[id] = cdr;
        }

        #endregion

        /// <summary>Handle a message that was received by a client.</summary>
        /// <param name="m">The message.</param>
        /// <param name="client">Which client sent it.</param>
        /// <param name="t">How the message was sent</param>
        virtual public void ReceivedClientMessage(Message m, IConnexion client, ITransport t)
        {
            DebugUtils.DumpMessage(this + ": MessageReceived from " + client, m);
            //send to this
            if (MessageReceived != null) { MessageReceived(m, client, t); }

            //sort to the correct type
            switch (m.MessageType)
            {
            case MessageType.Binary:
                if (BinaryMessageReceived != null) BinaryMessageReceived(m, client, t); break;
            case MessageType.Object:
                if (ObjectMessageReceived != null) ObjectMessageReceived(m, client, t); break;
            case MessageType.Session:
                if (SessionMessageReceived != null) SessionMessageReceived(m, client, t); break;
            case MessageType.String:
                if (StringMessageReceived != null) StringMessageReceived(m, client, t); break;
            default:
                break;
            }
        }
    }

    /// <summary>Represents a client using the server.</summary>
    public class ClientConnexion : BaseConnexion
    {
        #region Variables and Properties

        /// <summary>Triggered when an error occurs in this client.</summary>
        public event ErrorClientHandler ErrorEvent;

        /// <summary>
        /// The client's unique identifier; this should be globally unique
        /// </summary>
        protected Guid clientId;

        private Server server;

        public Guid ClientIdentity
        {
            get { return clientId; }
        }

        /// <summary>
        /// The server has a unique identity for itself.
        /// </summary>
        public override int MyUniqueIdentity
        {
            get { return server.UniqueIdentity; }
        }

        public override IMarshaller Marshaller
        {
            get { return server.Marshaller; }
        }

        #endregion

        #region Constructors and Destructors

        /// <summary>Creates a new ClientConnexion to communicate with.</summary>
        /// <param name="s">The associated server instance.</param>
        /// <param name="id">The unique identity of this new ClientConnexion.</param>
        public ClientConnexion(Server s, Guid clientId, int id)
        {
            server = s;
            this.clientId = clientId;
            uniqueIdentity = id;
            active = true;
        }

        #endregion

        #region Predicates

        /// <summary>Is this ClientConnexion dead?  This function is intended for use as a predicate
        /// such as in <c>List.FindAll()</c>.</summary>
        /// <param name="c">The client to check.</param>
        /// <returns>True if the client <c>c</c> </c>is dead.</returns>
        internal static bool IsDead(ClientConnexion c)
        {
            return !c.Active;
        }

        #endregion

        override public int Compare(ITransport a, ITransport b)
        {
            return server.Configuration.Compare(a,b);
        }

        public override void AddTransport(ITransport t)
        {
            base.AddTransport(t);
            // Send their unique ID right away
            SendMessage(t, new SystemMessage(SystemMessageType.UniqueIDRequest,
                BitConverter.GetBytes(UniqueIdentity)));
        }

        /// <summary>Send SessionAction.</summary>
        /// <param name="clientId">ClientConnexion who is doing the action.</param>
        /// <param name="e">Session action to send.</param>
        /// <param name="id">Channel to send on.</param>
        /// <param name="mdr">How to send it (can be null)</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public void Send(int clientId, SessionAction e, byte id, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            Send(new SessionMessage(id, clientId, e), mdr, cdr);
        }

        public override void Send(IList<Message> messages, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            if (!Active)
            {
                throw new InvalidStateException("cannot send on a disposed client!", this);
            }
            ITransport t = FindTransport(mdr, cdr);
            SendMessages(t, messages);

        }

        /// <summary>Handles a system message in that it takes the information and does something with it.</summary>
        /// <param name="m">The message received.</param>
        /// <param name="id">What channel it came in on.</param>
        override protected void HandleSystemMessage(SystemMessage message, ITransport transport)
        {
            switch ((SystemMessageType)message.Id)
            {
            case SystemMessageType.UniqueIDRequest:
                //they want to know their own id?  They should have received it already...
                // (see above in AddTransport())
                SendMessage(transport, new SystemMessage(SystemMessageType.UniqueIDRequest,
                    BitConverter.GetBytes(UniqueIdentity)));
                break;

            default:
                base.HandleSystemMessage(message, transport);
                break;
            }
        }

    }

}
