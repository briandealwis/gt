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
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;
using Common.Logging;
using GT.Millipede;
using GT.Utils;

namespace GT.Net
{

    #region Delegates

    /// <summary>Notification of outgoing messages</summary>
    /// <param name="msgs">The outgoing messages.</param>
    /// <param name="list">The destinations for the messages</param>
    /// <param name="mdr">How the message is to be sent</param>
    public delegate void MessagesSentNotification(IList<Message> msgs, ICollection<IConnexion> list, MessageDeliveryRequirements mdr);

    /// <summary>Handles when clients leave the server.</summary>
    /// <param name="list">The clients who've left.</param>
    public delegate void ClientsRemovedHandler(ICollection<IConnexion> list);

    /// <summary>Handles when clients join the server.</summary>
    /// <param name="list">The clients who've joined.</param>
    public delegate void ClientsJoinedHandler(ICollection<IConnexion> list);

    #endregion

    /// <summary>
    /// Describes the configuration details and defines policy decisions for
    /// a GT server.
    /// </summary>
    public abstract class ServerConfiguration : BaseConfiguration
    {
        /// <summary>
        /// Create the marsheller for the server instance.
        /// </summary>
        /// <returns>the marshaller</returns>
        abstract public IMarshaller CreateMarshaller();

        /// <summary>
        /// Create the appropriate transport acceptors.
        /// </summary>
        /// <returns>a collection of acceptors</returns>
        abstract public ICollection<IAcceptor> CreateAcceptors();

        /// <summary>
        /// Check an incoming connection; if the connection is deemed to be
        /// invalid, then return a descriptive reason.
        /// </summary>
        /// <param name="server">the server instance</param>
        /// <param name="transport">the transport in question</param>
        /// <param name="capabilities">the capabilities from the remote</param>
        /// <returns>null if valid, a reason if invalid</returns>
        public virtual string ValidateIncomingTransport(Server server, ITransport transport,
            IDictionary<string, string> capabilities)
        {
            if(!capabilities.ContainsKey(GTCapabilities.MARSHALLER_DESCRIPTORS))
            {
                return "no marshaller capabilities provided";
            }
            if(!server.Marshaller.IsCompatible(capabilities[GTCapabilities.MARSHALLER_DESCRIPTORS], transport))
            {
                return "incompatible marshaller";
            }
            return null;
        }

        /// <summary>
        /// Create a server instance as repreented by this configuration instance.
        /// </summary>
        /// <returns>the created server</returns>
        virtual public Server BuildServer()
        {
            return new Server(this);
        }

        /// <summary>
        /// Create an connexion representing the client.
        /// </summary>
        /// <param name="owner">the associated server instance</param>
        /// <param name="clientGuid">the client's GUID</param>
        /// <param name="clientIdentity">the server-unique identity for the client</param>
        /// <returns>the client connexion</returns>
        virtual public IConnexion CreateClientConnexion(Server owner, 
            Guid clientGuid, int clientIdentity)
        {
            return new ConnexionToClient(owner, clientGuid, clientIdentity);
        }

        /// <summary>
        /// Return the default channel requirements for channels without specified
        /// channel requirements.
        /// </summary>
        /// <returns>the channel requirements</returns>
        virtual public ChannelDeliveryRequirements DefaultChannelRequirements()
        {
            return ChannelDeliveryRequirements.LeastStrict;
        }
    }

    /// <summary>
    /// A sample server configuration.  <strong>This class definition may change 
    /// in dramatic  ways in future releases.</strong>  This configuration should 
    /// serve only as an example, and applications should make their own server 
    /// configurations by copying this instance.  
    /// </summary>
    public class DefaultServerConfiguration : ServerConfiguration
    {
        /// <summary>
        /// The default configuration uses this port.
        /// </summary>
        protected int port = 9999;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public DefaultServerConfiguration() { }

        /// <summary>
        /// Construct the configuration instance using the provided port.
        /// </summary>
        /// <param name="port">the IP port for the server to use</param>
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
        public DefaultServerConfiguration(int port, TimeSpan pingInterval)
            : this(port)
        {
            Debug.Assert(pingInterval.TotalMilliseconds > 0);
            PingInterval = pingInterval;
        }

        /// <summary>
        /// Create the marshaller for the server instance.
        /// </summary>
        /// <returns>the marshaller</returns>
        public override IMarshaller CreateMarshaller()
        {
            return new LargeObjectMarshaller(new DotNetSerializingMarshaller());
        }

        /// <summary>
        /// Create the appropriate transport acceptors.
        /// </summary>
        /// <returns>a collection of acceptors</returns>
        public override ICollection<IAcceptor> CreateAcceptors()
        {
            ICollection<IAcceptor> acceptors = new List<IAcceptor>();
            acceptors.Add(new TcpAcceptor(IPAddress.Any, port));
            acceptors.Add(new UdpAcceptor(IPAddress.Any, port));
            // optionally use Millipede on the connectors, dependent on
            // GTMILLIPEDE environment variable
            return MillipedeAcceptor.Wrap(acceptors, MillipedeRecorder.Singleton);
        }

    }

    /// <summary>Represents traditional server.</summary>
    public class Server : Communicator
    {
        #region Variables and Properties

        private static readonly Random random = new Random();

        private bool running = false;
        private int serverIdentity;

        /// <summary>
        /// A factory-like object responsible for providing the server's runtime
        /// configuration.
        /// </summary>
        private readonly ServerConfiguration configuration;

        private ICollection<IAcceptor> acceptors;
        private IMarshaller marshaller;
        private readonly Dictionary<byte, ChannelDeliveryRequirements> channelRequirements
            = new Dictionary<byte,ChannelDeliveryRequirements>();

        private int lastPingTime = 0;

        /// <summary>
        /// All of the client identities that this server knows about.  
        /// </summary>
        private readonly Dictionary<int, IConnexion> clientIDs =
            new Dictionary<int, IConnexion>();
        private readonly ICollection<IConnexion> newlyAddedClients =
            new List<IConnexion>();

        /// <summary>
        /// Return the set of active clients to which this server is talking.
        /// </summary>
        public ICollection<IConnexion> Clients 
        { 
            get { return Connexions; } 
        }

	    /// <summary>
	    /// Return the associated marshaller
	    /// </summary>
        public override IMarshaller Marshaller { get { return marshaller; } }

	    /// <summary>
	    /// Return the server configuration object.
	    /// </summary>
        public ServerConfiguration Configuration { get { return configuration; } }

	    /// <summary>
	    /// Return this server's unique identity.
	    /// </summary>
        public int ServerIdentity { get { return serverIdentity; } }

        public override TimeSpan TickInterval
        {
            get { return configuration.TickInterval; }
        }

        public override TimeSpan PingInterval
        {
            get { return configuration.PingInterval; }
        }

        #endregion

        #region Events

        /// <summary>Invoked each time a message is sent.</summary>
        public event MessagesSentNotification MessagesSent;

        /// <summary>Invoked each time a client disconnects.</summary>
        public event ClientsRemovedHandler ClientsRemoved;

        /// <summary>Invoked each time a client connects.</summary>
        public event ClientsJoinedHandler ClientsJoined;

        /// <summary>Invoked each time a message is received.</summary>
        public event MessageHandler MessageReceived;

        /// <summary>Invoked each time a session message is received.</summary>
        public event MessageHandler SessionMessageReceived;

        /// <summary>Invoked each time a string message is received.</summary>
        public event MessageHandler StringMessageReceived;

        /// <summary>Invoked each time a object message is received.</summary>
        public event MessageHandler ObjectMessageReceived;

        /// <summary>Invoked each time a binary mesage is received.</summary>
        public event MessageHandler BinaryMessageReceived;

        #endregion


        /// <summary>Creates a new Server object.</summary>
        /// <param name="port">The port to listen on.</param>
        public Server(int port)
            : this(new DefaultServerConfiguration(port))
        {
        }

        /// <summary>Creates a new Server object.</summary>
        /// <param name="port">The port to listen on.</param>
        /// <param name="interval">The update interval at which to check 
        /// for new connections or new messages.</param>
        public Server(int port, TimeSpan interval)
            : this(new DefaultServerConfiguration(port, interval))
        {
        }

        /// <summary>Creates a new Server object.</summary>
        /// <param name="sc">The server configuration object.</param>
        public Server(ServerConfiguration sc)
        {
            log = LogManager.GetLogger(GetType());
            configuration = sc;
            serverIdentity = GenerateIdentity();
        }


        #region Vital Server Mechanics

        /// <summary>
        /// Create a descriptive string representation 
        /// </summary>
        /// <returns>a descriptive string representation</returns>
        override public string ToString()
        {
            return this.GetType().Name + "(" + (Clients == null ? 0 : Clients.Count) + " clients)";
        }

        /// <summary>
        /// Run a cycle to process any pending events for the connexions or
        /// other related objects for this instance.  This method is <strong>not</strong> 
        /// re-entrant and should not be called from GT callbacks.
        /// <strong>deprecated behaviour:</strong> the server is started if not active.
        /// </summary>
        public override void Update()
        {
            //log.Trace("Server.Update(): started");

            lock (this)
            {
                if (!Active)
                {
                    Start();
                }

                newlyAddedClients.Clear();
                UpdateAcceptors();
                if (newlyAddedClients.Count > 0 && ClientsJoined != null)
                {
                    ClientsJoined(newlyAddedClients);
                }

                //ping, if needed
                if (Environment.TickCount - lastPingTime >= configuration.PingInterval.Ticks)
                {
                    // DebugUtils.WriteLine("Server.Update(): pinging clients");
                    lastPingTime = System.Environment.TickCount;
                    log.Debug("Pinging");
                    foreach (IConnexion c in clientIDs.Values)
                    {
                        if (c.Active) { c.Ping(); }
                    }
                }

                // DebugUtils.WriteLine("Server.Update(): Clients.Update()");
                //update all clients, reading from the network
                foreach (IConnexion c in clientIDs.Values)
                {
                    try
                    {
                        if (c.Active) { c.Update(); }
                    }
                    catch (ConnexionClosedException e)
                    {
                        Debug.Assert(e.SourceComponent == c);
                        c.Dispose();
                    }
                }

                //remove dead clients (includes disposed and clients with no transports)
                RemoveDeadConnexions();
            }
            //log.Trace("Server.Update(): finished");

            //if anyone is listening, tell them we're done one cycle
            NotifyTick();
        }

        private void UpdateAcceptors()
        {
            List<IAcceptor> toRemove = null;
            foreach (IAcceptor acc in acceptors)
            {
                if (!acc.Active)
                {
                    if(toRemove == null) { toRemove = new List<IAcceptor>(); }
                    toRemove.Add(acc);
                    continue;
                }
                // DebugUtils.WriteLine("Server.Update(): checking acceptor " + acc);
                try { acc.Update(); }
                catch (TransportError e)
                {
                    try {
                        log.Warn(String.Format("Exception from acceptor {0}", acc), e);
                        acc.Stop(); acc.Start();
                    }
                    catch (TransportError)
                    {
                        log.Warn(String.Format("Unable to restart acceptor {0}; removing", acc), e);
                        if (toRemove == null) { toRemove = new List<IAcceptor>(); }
                        toRemove.Add(acc);
                    }
                }
            }
            if (toRemove == null) { return; }
            foreach (IAcceptor acc in toRemove) { acceptors.Remove(acc); }
        }

        protected override void AddConnexion(IConnexion cnx)
        {
            base.AddConnexion(cnx);
            newlyAddedClients.Add(cnx); // used for ClientsJoined event
            clientIDs.Add(cnx.Identity, cnx);
        }

        protected override void RemovedConnexion(IConnexion cnx)
        {
            clientIDs.Remove(cnx.Identity);
            if (ClientsRemoved != null)
            {
                ClientsRemoved(new SingleItem<IConnexion>(cnx));
            }
            base.RemovedConnexion(cnx);
        }

        protected virtual IConnexion CreateNewConnexion(Guid clientGuid)
        {
            IConnexion cnx = configuration.CreateClientConnexion(this, clientGuid, GenerateIdentity());
            cnx.MessageReceived += ReceivedClientMessage;
            cnx.ErrorEvents += NotifyError;
            AddConnexion(cnx);
            return cnx;
        }
        
        protected virtual void NewTransport(ITransport t, IDictionary<string, string> capabilities)
        {
            Guid clientGuid;
            try
            {
                clientGuid = new Guid(capabilities[GTCapabilities.CLIENT_GUID]);
            }
            catch (Exception e)
            {
                log.Warn(String.Format("Exception occurred when decoding client's GUID: {0}",
                    capabilities[GTCapabilities.CLIENT_GUID]), e);
                t.Dispose();
                return;
            }

            // FIXME: Hmmm, how do we check the GTCapabilities.MARSHALLER_DESCRIPTORS?

            IConnexion c = GetConnexionForClientGuid(clientGuid);
            if (c == null)
            {
                //if (log.IsInfoEnabled)
                //{
                //    log.Info(String.Format("{0}: new client {1} via {2}", this, clientGuid, t));
                //}
                c = CreateNewConnexion(clientGuid);
            }
            else
            {
                //if (log.IsInfoEnabled)
                //{
                //    log.Info(String.Format("{0}: for client {1} via {2}", this, clientGuid, t));
                //}
            }
            t = Configuration.ConfigureTransport(t);
            c.AddTransport(t);
        }

        /// <summary>Returns the client matching the provided GUID.</summary>
        /// <param name="clientGuid">The remote client GUID.</param>
        /// <returns>The client with that GUID.  If no match, then return null.</returns>
        virtual protected BaseConnexion GetConnexionForClientGuid(Guid clientGuid)
        {
            foreach (BaseConnexion c in Clients)
            {
                if (c.ClientGuid.Equals(clientGuid)) { return c; }
            }
            return null;
        }

        public override void Start()
        {
            if (Active) { return; }
            acceptors = configuration.CreateAcceptors();
            foreach (IAcceptor acc in acceptors)
            {
                acc.NewTransportAccepted += NewTransport;
                acc.ValidateTransport += ValidateIncomingTransport;
                acc.Start();
            }
            marshaller = configuration.CreateMarshaller();
            base.Start();
            running = true;
            log.Trace(this + ": started");
        }

        protected virtual void ValidateIncomingTransport(object sender, ValidateTransportArgs e)
        {
            string rejection = configuration.ValidateIncomingTransport(this, e.Transport, e.Capabilities);
            if (rejection != null)
            {
                e.Reject(rejection);
            }
        }

        public override void Stop()
        {
            lock (this)
            {
                // we were told to die.  die gracefully.
                if (!Active) { return; }
                running = false;
                log.Trace(this + ": stopped");

                StopListeningThread();

                Stop(acceptors);
                Dispose(acceptors);
                acceptors = null;

                clientIDs.Clear();
                base.Stop();
            }
        }

        public override void Dispose()
        {
            running = false;
            log.Trace(this + ": disposed");

            StopListeningThread();

            Dispose(acceptors);
            acceptors = null;
            base.Dispose();

        }

        public override bool Active
        {
            get { return running; }
        }

        /// <summary>
        /// Return the instance's acceptors
        /// </summary>
        public ICollection<IAcceptor> Acceptors
        {
            get { return acceptors; }
        }

        /// <summary>Generates a identity number that clients (and this server) 
        /// can use to identify each other.  These numbers are unique across this
        /// server, but not necessarily between different servers.</summary>
        /// <returns>The server-unique identity number</returns>
        virtual protected int GenerateIdentity()
        {
            int clientId = 0;
            DateTime timeStamp = DateTime.Now;
            do
            {
                clientId = (timeStamp.Hour * 100 + timeStamp.Minute) * 100 + timeStamp.Second;
                clientId = clientId * 1000 + random.Next(0, 1000);
                // keep going until we create something never previously seen
            } while (clientId == serverIdentity || clientIDs.ContainsKey(clientId));
            return clientId;
        }

        #endregion

        #region Sending

        /// <summary>Sends a byte array on <see cref="channelId"/> to many clients in an efficient manner.</summary>
        /// <param name="buffer">The byte array to send</param>
        /// <param name="channelId">The channel to be sent on</param>
        /// <param name="list">The list of clients; if null then all clients</param>
        /// <param name="mdr">How to send it (can be null)</param>
        virtual public void Send(byte[] buffer, byte channelId, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            Send(new SingleItem<Message>(new BinaryMessage(channelId, buffer)),
		list, mdr);
        }

        /// <summary>Sends a string on <see cref="channelId"/> to many clients in an efficient manner.</summary>
        /// <param name="s">The string to send</param>
        /// <param name="channelId">The channel to be sent on</param>
        /// <param name="list">The list of clients; if null then all clients</param>
        /// <param name="mdr">How to send it (can be null)</param>
        virtual public void Send(string s, byte channelId, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            Send(new SingleItem<Message>(new StringMessage(channelId, s)), list, mdr);
        }

        /// <summary>Sends an object on <see cref="channelId"/> to many clients in an efficient manner.</summary>
        /// <param name="o">The object to send</param>
        /// <param name="channelId">The channel to be sent on</param>
        /// <param name="list">The list of clients; if null then all clients</param>
        /// <param name="mdr">How to send it (can be null)</param>
        virtual public void Send(object o, byte channelId, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            Send(new SingleItem<Message>(new ObjectMessage(channelId, o)), list, mdr);
        }

        /// <summary>Send a message to many clients in an efficient manner.</summary>
        /// <param name="message">The message to send</param>
        /// <param name="list">The list of clients; if null then all clients</param>
        /// <param name="mdr">How to send it (can be null)</param>
        virtual public void Send(Message message, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            Send(new SingleItem<Message>(message), list, mdr);
        }

        /// <summary>Sends a collection of messages in an efficient way to a list of clients.</summary>
        /// <param name="messages">The list of messages to send</param>
        /// <param name="list">The list of clients; if null then all clients</param>
        /// <param name="mdr">How to send it (can be null)</param>
        virtual public void Send(IList<Message> messages, ICollection<IConnexion> list, MessageDeliveryRequirements mdr)
        {
            InvalidStateException.Assert(Active, "Cannot send on a stopped server", this);
            if (list == null)
            {
                list = Connexions;
            }
            foreach (IConnexion c in list)
            {
                if (!c.Active) { continue; }
                //Console.WriteLine("{0}: sending to {1}", this, c);
                try
                {
                    c.Send(messages, mdr, GetChannelDeliveryRequirements(messages[0].ChannelId));
                }
                catch (GTException e)
                {
                    NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.MessagesCannotBeSent,
                        "Exception when sending messages", e));
                }
            }
            if (MessagesSent != null) { MessagesSent(messages, list, mdr); }
        }

        /// <summary>
        /// Return the delivery requirements for a channel; if the channel has not hd
        /// a set of delivery requirements configured, then return the default set.
        /// </summary>
        /// <param name="channelId">the channel</param>
        /// <returns>the delivery requirements configured for the channel</returns>
        virtual public ChannelDeliveryRequirements GetChannelDeliveryRequirements(byte channelId)
        {
            ChannelDeliveryRequirements cdr;
            if (channelRequirements.TryGetValue(channelId, out cdr)) { return cdr; }
            return configuration.DefaultChannelRequirements();
        }

        /// <summary>
        /// Set the delivery requirements for a particular channel.
        /// </summary>
        /// <param name="channelId">the channel</param>
        /// <param name="cdr">the delivery requirements to be configured; null to remove</param>
        virtual public void SetChannelDeliveryRequirements(byte channelId, ChannelDeliveryRequirements cdr)
        {
            if (cdr == null)
            {
                channelRequirements.Remove(channelId);
            }
            else
            {
                channelRequirements[channelId] = cdr;
            }
        }

        #endregion

        /// <summary>Handle a message that was received by a client.</summary>
        /// <param name="m">The message.</param>
        /// <param name="client">Which client sent it.</param>
        /// <param name="t">How the message was sent</param>
        virtual protected void ReceivedClientMessage(Message m, IConnexion client, ITransport t)
        {
            if (log.IsTraceEnabled)
            {
                log.Trace("Received from " + client + ": " + m);
            }
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

    /// <summary>Represents a logical connexion to a client, suitable for use of the server.</summary>
    public class ConnexionToClient : BaseConnexion
    {
        #region Variables and Properties

        /// <summary>
        /// The client's globally unique identifier (vs <see cref="IConnexion.Identity"/>,
        /// which is only unique to the clients connected to <see cref="owner"/>).
        /// </summary>
        protected Guid clientGuid;

        protected Server owner;

        override public Guid ClientGuid
        {
            get { return clientGuid; }
        }

        /// <summary>
        /// The server has a unique identity for itself.
        /// </summary>
        public override int SendingIdentity
        {
            get { return owner.ServerIdentity; }
        }

        public override IMarshaller Marshaller
        {
            get { return owner.Marshaller; }
        }

        #endregion

        #region Constructors and Destructors

        /// <summary>Creates a new ClientConnexion to communicate with.</summary>
        /// <param name="s">The associated server instance.</param>
        /// <param name="clientGuid">A globally unique identifier for the associated client.</param>
        /// <param name="clientIdentity">The server-unique identity of this new client's connexion.</param>
        public ConnexionToClient(Server s, Guid clientGuid, int clientIdentity)
        {
            owner = s;
            this.clientGuid = clientGuid;
            identity = clientIdentity;
            active = true;
        }

        #endregion

        override public int Compare(ITransport a, ITransport b)
        {
            return owner.Configuration.Compare(a,b);
        }

        protected override IPacketScheduler CreatePacketScheduler()
        {
            return new ImmediatePacketScheduler(this);
        }

        public override void AddTransport(ITransport t)
        {
            base.AddTransport(t);
            // Send their identity right away
            Send(new SystemIdentityResponseMessage(Identity),
                new SpecificTransportRequirement(t), null);
        }

        /// <summary>Send notice of some session action.</summary>
        /// <param name="clientId">The subject of the action.</param>
        /// <param name="e">The session action.</param>
        /// <param name="channelId">Channel on which to send the notice.</param>
        /// <param name="mdr">How to send the session message (can be null)</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public void Send(int clientId, SessionAction e, byte channelId, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            Send(new SessionMessage(channelId, clientId, e), mdr, cdr);
        }

        /// <summary>Handles a system message in that it takes the information and does something with it.</summary>
	    /// <param name="message">The message received.</param>
	    /// <param name="transport">The transport the message was received on.</param>
        override protected void HandleSystemMessage(SystemMessage message, ITransport transport)
        {
            switch (message.Descriptor)
            {
            case SystemMessageType.IdentityRequest:
                //they want to know their own id?  They should have received it already...
                // (see above in AddTransport())
                Send(new SystemIdentityResponseMessage(Identity),
                    new SpecificTransportRequirement(transport), null);
                break;

            default:
                base.HandleSystemMessage(message, transport);
                break;
            }
        }

    }

}
