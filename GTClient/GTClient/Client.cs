using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using GT.Millipede;
using GT.Utils;

namespace GT.Net
{
    #region Client-side Channel Representations

    #region Typed Channel Interfaces

    /// <summary>
    /// An abstracted channel interface; not intended to be used directly, but
    /// represents a base functionality of all channels.  All channel
    /// implementations are required to also implement <see cref="IUpdatableChannel"/>;
    /// implementors may wish to use <see cref="AbstractChannel"/>.
    /// 
    /// Note: channels now implement <see cref="IDisposable"/>: when finished with 
    /// a channel, please  be sure to call its <see cref="IChannel.Dispose"/> method.
    /// </summary>
    /// <seealso cref="IUpdatableChannel"/>
    /// <seealso cref="AbstractChannel"/>
    public interface IChannel : IDisposable
    {
        /// <summary>
        /// Triggered whenever the owning <see cref="Client"/> is updated.
        /// </summary>
        event Action<HPTimer> UpdateEvent;

        /// <summary>
        /// Return true if this instance is active
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// Return this instance's associated channelId.
        /// </summary>
        byte ChannelId { get; }

        /// <summary>
        /// Return the underlying <see cref="IConnexion.Identity"/>, a
        /// server-unique identity for this client amongst the server's clients.
        /// </summary>
        int Identity { get; }

        /// <summary>Flush all pending messages on this instance.</summary>
        void Flush();

        /// <summary>
        /// Return this instance's associated connexion.
        /// </summary>
        IConnexion Connexion { get; }
        
        /// <summary>
        /// Average latency between the client and this particluar server 
        /// (in milliseconds).
        /// </summary>
        float Delay { get; }
    }

    /// <summary>
    /// A channel carrying content items, as compared to streaming channels that
    /// carry content updates.
    /// </summary>
    /// <typeparam name="SI">The type of generic items supported by this channel.</typeparam>
    /// <typeparam name="RI">The type of received items, which is generally expected to be
    ///     the same as <c>SI</c>.  Some special channels, such as <see cref="ISessionChannel"/>,
    ///     return more complex objects.</typeparam>
    /// <typeparam name="ST">The type of this channel.</typeparam>
    public interface IContentChannel<SI,RI,ST> : IChannel
        where RI : class
        where ST : IChannel
    {
        /// <summary>Send an item to the server</summary>
        /// <param name="item">The item</param>
        void Send(SI item);

        /// <summary>Send an item to the server</summary>
        /// <param name="item">The item</param>
        /// <param name="mdr">How to send it</param>
        void Send(SI item, MessageDeliveryRequirements mdr);

        /// <summary>
        /// Remove and return the content of the message found at the specified index
        /// of the queue of received messages.</summary>
        /// <param name="count">The message to be dequeued, with a higher number indicating a newer message.</param>
        /// <returns>The message content, or null if the content could not be obtained.</returns>
        RI DequeueMessage(int count);

        /// <summary>Return the number of waiting messages.</summary>
        /// <returns>The number of waiting messages; 0 indicates there are no waiting message.</returns>
        int Count { get; }

        /// <summary>Received messages from the server.</summary>
        IList<Message> Messages { get; }

        /// <summary>
        /// Triggered when this channel has new messages; there may be multiple messages.
        /// </summary>
        event Action<ST> MessagesReceived;
    }

    /// <summary>
    /// A specialized interface that channel implementations must implement
    /// to be used within the <see cref="Client"/> framework.
    /// </summary>
    public interface IUpdatableChannel : IChannel
    {
        /// <summary>
        /// Notify that all current messages have been enqueued.
        /// </summary>
        /// <param name="timer">a timer recording the duration since last enqueued</param>
        void Update(HPTimer timer);
    }

    /// <summary>A content-bearing channel carrying session events.</summary>
    public interface ISessionChannel : IContentChannel<SessionAction,SessionMessage,ISessionChannel>
    {
    }

    /// <summary>A content-bearing channel carrying strings.</summary>
    public interface IStringChannel : IContentChannel<string,string,IStringChannel>
    {
    }

    /// <summary>A content-bearing channel carrying objects.</summary>
    public interface IObjectChannel : IContentChannel<object,object,IObjectChannel>
    {
    }

    /// <summary>A content-bearing channel carrying byte arrays.</summary>
    public interface IBinaryChannel : IContentChannel<byte[],byte[],IBinaryChannel>
    {
    }

    #endregion

    /// <summary>
    /// A very base level implementation
    /// </summary>
    public abstract class AbstractChannel : IChannel, IUpdatableChannel
    {
        protected byte channelId;
        protected IConnexion connexion;
        protected ChannelDeliveryRequirements deliveryOptions;

        public event Action<HPTimer> UpdateEvent;

        /// <summary>
        /// Return true if this instance is active
        /// </summary>
        public bool Active { get { return connexion != null && connexion.Active; } }

        /// <summary> This instance's channelId. </summary>
        public byte ChannelId { get { return channelId; } }

        /// <summary>Average latency between the client and this particluar server.</summary>
        public float Delay { get { return connexion.Delay; } }

        /// <summary> Get the server-unique identity of the client.</summary>
        /// <seealso cref="IConnexion.Identity"/>
        public int Identity { get { return connexion == null ? 0 : connexion.Identity; } }

        /// <summary> Get the connexion's destination address </summary>
        public string Address
        {
            get
            {
                if(connexion is IAddressableConnexion)
                {
                    return ((IAddressableConnexion)connexion).Address;
                }
                return null;
            }
        }

        /// <summary>Get the connexion's destination port</summary>
        public string Port
        {
            get
            {
                if (connexion is IAddressableConnexion)
                {
                    return ((IAddressableConnexion)connexion).Port;
                }
                return null;
            }
        }

        /// <summary>Flush all pending messages on this channel.</summary>
        public virtual void Flush()
        {
            connexion.FlushChannel(this.channelId);
        }

        /// <summary>
        /// Return this instance's connexion.
        /// </summary>
        public IConnexion Connexion 
        { 
            get { return connexion; }
            internal set { connexion = value; }
        }

        /// <summary>
        /// Return the default channel delivery requirements to be used
        /// when delivery requirements have not been specified.
        /// </summary>
        public ChannelDeliveryRequirements ChannelDeliveryOptions 
        { 
            get { return deliveryOptions; }
            internal set { deliveryOptions = value; }
        }

        protected AbstractChannel(IConnexion cnx, byte channelId, ChannelDeliveryRequirements cdr)
        {
            connexion = cnx;
            cnx.MessageReceived += _cnx_MessageReceived;
            this.channelId = channelId;
            deliveryOptions = cdr;
        }

        /// <remarks>
        /// We can't register for fine-grained events, so we have to do the processing 
        /// ourselves to ensure the message is actually intended for this channel
        /// </remarks>
        protected abstract void _cnx_MessageReceived(Message m, IConnexion client, ITransport transport);

        public virtual void Update(HPTimer hpTimer)
        {
            if (connexion != null && UpdateEvent != null) { UpdateEvent(hpTimer); }
        }

        public virtual void Dispose()
        {
            if(connexion != null)
            {
                connexion.MessageReceived -= _cnx_MessageReceived;
            }
            connexion = null;
        }

        public override string ToString()
        {
            return GetType().Name + "[" + connexion + "]";
        }
    }

    /// <summary>
    /// The base implementation for content-bearing channels.
    /// We differentiate between <typeparamref name="SI"/> and <typeparamref name="RI"/>
    /// as some channels, particularly the session channel, send and return 
    /// different types of items.
    /// </summary>
    /// <typeparam name="SI">the type of items carried</typeparam>
    /// <typeparam name="RI">the type of returned items</typeparam>
    /// <typeparam name="ST">the actual top-level type of this instance</typeparam>
    public abstract class AbstractContentChannel<SI, RI, ST> 
            : AbstractChannel, IContentChannel<SI, RI, ST>
        where ST: IChannel
        where RI: class
    {
        public event Action<ST> MessagesReceived;
        protected IList<Message> messages = new List<Message>();
        protected bool newMessagesReceived = false;

        /// <summary>Received messages from the server.</summary>
        public IList<Message> Messages { get { return messages; } }

        /// <summary>Create a channel object.</summary>
        /// <param name="cnx">The connexion used to actually send the messages.</param>
        /// <param name="channelId">The channelId.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal AbstractContentChannel(IConnexion cnx, byte channelId,
                ChannelDeliveryRequirements cdr) 
            : base(cnx, channelId, cdr)
        {
        }

        public virtual int Count { get { return messages.Count; } }

        public void Send(SI item)
        {
            Send(item, null);
        }

        public abstract void Send(SI item, MessageDeliveryRequirements mdr);

        virtual public RI DequeueMessage(int count)
        {
            try
            {
                Message m;
                while (count < messages.Count)
                {
                    lock(messages)
                    {
                        m = messages[count];
                        messages.RemoveAt(count);
                    }
                    RI result;
                    if(GetMessageContents(m, out result))
                    {
                        return result;
                    }
                }
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>
        /// Extract the appropriate typed object from the provided message.
        /// </summary>
        /// <param name="m">the message</param>
        /// <param name="contents">the appropriately-typed object content from <s
        /// <returns>true if the contents was successfully extracted</returns>
        abstract protected bool GetMessageContents(Message m, out RI contents);

        /// <summary>Obtain a properly typed variant of this channel.</summary>
        protected abstract ST CastedChannel { get; }

        /// <summary>Queue a message in the list, triggering events</summary>
        /// <param name="message">The message to be queued.</param>
        /// <param name="client">The connexion receiving the message</param>
        /// <param name="transport">The actual transport from which the message was received</param>
        protected override void _cnx_MessageReceived(Message message, IConnexion client, ITransport transport)
        {
            if (!Active) { return; }
            /// We can't register for fine-grained events, so we have to do the processing 
            /// ourselves to ensure the message is actually intended for this channel
            if (message.ChannelId != ChannelId) { return; }
            RI content;
            if (!GetMessageContents(message, out content)) { return; }
            messages.Add(message);
            newMessagesReceived = true;
        }

        public override void Update(HPTimer hpTimer)
        {
            if (!Active) { return; }
            base.Update(hpTimer);
            if (newMessagesReceived && MessagesReceived != null)
            {
                MessagesReceived(CastedChannel);
            }
            newMessagesReceived = false;
        }

        public override void Dispose()
        {
            base.Dispose();
            messages.Clear();
        }
    }

    /// <summary>A connexion of session events.</summary>
    internal class SessionChannel : AbstractContentChannel<SessionAction, SessionMessage, ISessionChannel>, ISessionChannel
    {
        /// <summary>Create a SessionChannel object.</summary>
        /// <param name="cnx">The connexion to use to actually send the messages.</param>
        /// <param name="channelId">The message channel.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal SessionChannel(IConnexion cnx, byte channelId, ChannelDeliveryRequirements cdr) 
            : base(cnx, channelId, cdr)
        {
        }

        /// <summary>Send a session action to the server.</summary>
        /// <param name="action">The action.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(SessionAction action, MessageDeliveryRequirements mdr)
        {
            InvalidStateException.Assert(Active, "channel is not active", this);
            connexion.Send(new SessionMessage(channelId, Identity, action), mdr, deliveryOptions);
        }

        override protected bool GetMessageContents(Message m, out SessionMessage contents)
        {
            if(m is SessionMessage)
            {
                contents = (SessionMessage)m;
                return true;
            }
            contents = null;
            return false;
        }

        protected override ISessionChannel CastedChannel { get { return this; } }
    }

    /// <summary>A connexion of strings.</summary>
    internal class StringChannel : AbstractContentChannel<string, string, IStringChannel>, IStringChannel
    {
        /// <summary>Create a StringChannel object.</summary>
        /// <param name="cnx">The connexion to use to actually send the messages.</param>
        /// <param name="channelId">The message channel.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal StringChannel(IConnexion cnx, byte channelId, ChannelDeliveryRequirements cdr) 
            : base(cnx, channelId, cdr)
        {
        }

        
        /// <summary>Send a string to the server, specifying how.</summary>
        /// <param name="s">The string to send.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(string s, MessageDeliveryRequirements mdr)
        {
            InvalidStateException.Assert(Active, "channel is not active", this);
            connexion.Send(new StringMessage(channelId, s), mdr, deliveryOptions);
        }

        override protected bool GetMessageContents(Message m, out string contents)
        {
            if (m is StringMessage)
            {
                contents = ((StringMessage)m).Text;
                return true;
            }
            contents = null;
            return false;
        }

        protected override IStringChannel CastedChannel { get { return this; } }
    }

    /// <summary>A connexion of Objects.</summary>
    internal class ObjectChannel : AbstractContentChannel<object, object, IObjectChannel>, IObjectChannel
    {
        /// <summary>Create an ObjectChannel object.</summary>
        /// <param name="cnx">The connexion to use to actually send the objects.</param>
        /// <param name="channelId">The message channel claimed.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal ObjectChannel(IConnexion cnx, byte channelId, ChannelDeliveryRequirements cdr) 
            : base(cnx, channelId, cdr)
        {
        }

        /// <summary>Send an object using the specified method.</summary>
        /// <param name="o">The object to send.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(object o, MessageDeliveryRequirements mdr)
        {
            InvalidStateException.Assert(Active, "channel is not active", this);
            connexion.Send(new ObjectMessage(channelId, o), mdr, deliveryOptions);
        }

        override protected bool GetMessageContents(Message m, out object contents)
        {
            if(m is ObjectMessage)
            {
                contents = ((ObjectMessage)m).Object;
                return true;
            }
            contents = null;
            return false;
        }

        protected override IObjectChannel CastedChannel { get { return this; } }
    }

    /// <summary>A connexion of byte arrays.</summary>
    internal class BinaryChannel : AbstractContentChannel<byte[],byte[], IBinaryChannel>, IBinaryChannel
    {
        /// <summary>Creates a BinaryChannel object.</summary>
        /// <param name="cnx">The connexion object on which to actually send the objects.</param>
        /// <param name="channelId">The message channel to claim.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal BinaryChannel(IConnexion cnx, byte channelId, ChannelDeliveryRequirements cdr) 
            : base(cnx, channelId, cdr)
        {
        }

        /// <summary>Send a byte array using the specified method.</summary>
        /// <param name="b">The byte array to send.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(byte[] b, MessageDeliveryRequirements mdr)
        {
            InvalidStateException.Assert(Active, "channel is not active", this);
            connexion.Send(new BinaryMessage(channelId, b), mdr, deliveryOptions);
        }

        override protected bool GetMessageContents(Message m, out byte[] contents)
        {
            if (m is BinaryMessage)
            {
                contents = ((BinaryMessage)m).Bytes;
                return true;
            }
            contents = null;
            return false;
        }
        
        protected override IBinaryChannel CastedChannel { get { return this; } }
    }

    #endregion

    /// <summary>
    /// An interface for those connexions that have an addressable remote sendpoint.
    /// </summary>
    public interface IAddressableConnexion : IConnexion
    {
        /// <summary>
        /// Return the address component used in creating this connexion
        /// </summary>
        string Address { get; }
        
        /// <summary>
        /// Return the port component used in creating this connexion
        /// </summary>
        string Port { get; }
    }

    /// <summary>Controls the sending of messages to a particular server.</summary>
    public class ConnexionToServer : BaseConnexion, IAddressableConnexion, IStartable
    {
        private Client owner;
        private string address;
        private string port;

        /// <summary>
        /// Return the marshaller configured for this connexion's client.
        /// </summary>
        override public IMarshaller Marshaller
        {
            get { return owner.Marshaller; }
        }

        override public int Compare(ITransport a, ITransport b)
        {
            return owner.Configuration.Compare(a,b);
        }

        /// <summary>
        /// Return the globally unique identifier for the client
        /// represented by this connexion.
        /// </summary>
        override public Guid ClientGuid
        {
            get { return owner.Guid; }
        }

        #region Constructors and Destructors

        /// <summary>Create a new connexion to a server.</summary>
        /// <param name="owner">The owning client.</param>
        /// <param name="address">Who to try to connect to.</param>
        /// <param name="port">Which port to connect to.</param>
        protected internal ConnexionToServer(Client owner, string address, string port)
        {
            active = false;
            this.owner = owner;
            this.address = address;
            this.port = port;
        }

        protected override IPacketScheduler CreatePacketScheduler()
        {
            return new RoundRobinPacketScheduler(this);
        }

        /// <summary>
        /// Start this instance.
        /// </summary>
        /// <exception cref="CannotConnectException">thrown if we cannot
        /// connect to the specified server.</exception>
        virtual public void Start()
        {
            if (Active) { return; }

            if (owner.Connectors.Count == 0)
            {
                NotifyError(new ErrorSummary(Severity.Error, SummaryErrorCode.RemoteUnavailable,
                    "There are no connectors configured", null));
                throw new CannotConnectException("There are no connectors configured!");
            }
            transports = new List<ITransport>();

            foreach (IConnector conn in owner.Connectors)
            {
                // What should happen when we have a transport that can't interpret
                // the Address/Port?  E.g., what if we have an SMTP transport?
                try {
                    ITransport t = conn.Connect(Address, Port, owner.Capabilities);
                    t = owner.Configuration.ConfigureTransport(t);
                    AddTransport(t);
                }
                catch(CannotConnectException e)
                {
                    NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.RemoteUnavailable,
                        String.Format("Could not connect to {0}:{1} via {2}", Address, Port, conn), e));
                }
            }
            if (transports.Count == 0)
            {
                NotifyError(new ErrorSummary(Severity.Error, SummaryErrorCode.RemoteUnavailable,
                    String.Format("Unable to establish any connections to {0}:{1}", Address, Port), null));
                throw new CannotConnectException("could not connect to any transports");
            }
            // otherwise...
            active = true;
        }

        virtual public void Stop()
        {
            ShutDown();
            transports = null;
            scheduler.Reset();
        }

        #endregion

        /// <summary>
        /// Return the address component used in creating this connexion
        /// </summary>
        public virtual string Address
        {
            get { return address; }
        }

        /// <summary>
        /// Return the port component used in creating this connexion
        /// </summary>
        public virtual string Port
        {
            get { return port; }
        }

        /// <summary>
        /// Our unique identifier is the identifier bestowed upon us by the server.
        /// </summary>
        public override int SendingIdentity
        {
            get { return Identity; }
        }

        protected override ITransport AttemptReconnect(ITransport transport)
        {
            // find the connector responsible for having connected this transport and
            // try to reconnect.
            if (owner == null) { return null; }
            foreach (IConnector conn in owner.Connectors)
            {
                if(conn.Responsible(transport)) {
                    try {
                        ITransport t = conn.Connect(Address, Port, owner.Capabilities);
                        Debug.Assert(t != null, "IConnector.Connect() shouldn't return null: " + conn);
                        log.Info(String.Format("Reconnected to: {0}", t));
                        AddTransport(t);
                        return t;
                    } catch(CannotConnectException e) {
                        log.Warn(String.Format("Could not reconnect to {0}/{1}", Address, Port), e);
                    }
                }
            }
            log.Warn(String.Format("Unable to reconnect to {0}/{1}: no connectors found", 
                Address, Port));
            return null;
        }

        /// <summary>Deal with a system message in whatever way we need to.</summary>
        /// <param name="message">The incoming message.</param>
        /// <param name="transport">The transport from which the message
        ///  came.</param>
        override protected void HandleSystemMessage(SystemMessage message, ITransport transport)
        {
            switch (message.Descriptor)
            {
            case SystemMessageType.IdentityResponse:
                identity = ((SystemIdentityResponseMessage)message).Identity;
                break;

            default:
                base.HandleSystemMessage(message, transport);
                return;
            }
        }

        public override string ToString()
        {
            return GetType().Name + "[" + Identity + "]";
        }
    }

    public abstract class ClientConfiguration : BaseConfiguration
    {
        /// <summary>
        /// Create the marsheller for the server instance.
        /// </summary>
        /// <returns>the marshaller</returns>
        abstract public IMarshaller CreateMarshaller();

        /// <summary>
        /// Create the appropriate transport connectors.
        /// </summary>
        /// <returns>a collection of connectors</returns>
        abstract public ICollection<IConnector> CreateConnectors();
        
        /// <summary>
        /// Create a client instance as repreented by this configuration instance.
        /// </summary>
        /// <returns>the created client</returns>
        virtual public Client BuildClient()
        {
            return new Client(this);
        }

        /// <summary>
        /// Create an connexion representing a server.
        /// </summary>
        /// <param name="owner">the associated client instance</param>
        /// <param name="address">the server's address component</param>
        /// <param name="port">the server's port component</param>
        /// <returns>the server connexion</returns>
        virtual public IConnexion CreateServerConnexion(Client owner,
            string address, string port)
        {
            return new ConnexionToServer(owner, address, port);
        }
    }

    /// <summary>
    /// A sample client configuration.  <strong>This class definition may change 
    /// in dramatic  ways in future releases.</strong>  This configuration should 
    /// serve only as an example, and applications should make their own client 
    /// configurations by copying this instance.  
    /// </summary>
    public class DefaultClientConfiguration : ClientConfiguration
    {
        protected int port = 9999;

        public override IMarshaller CreateMarshaller()
        {
            return new DotNetSerializingMarshaller();
        }

        public override ICollection<IConnector> CreateConnectors()
        {
            ICollection<IConnector> connectors = new List<IConnector>();
            connectors.Add(new TcpConnector());
            connectors.Add(new UdpConnector());
            // optionally use Millipede on the connectors, dependent on
            // GTMILLIPEDE environment variable
            return MillipedeConnector.Wrap(connectors, MillipedeRecorder.Singleton);
        }
    }

    /// <summary>Represents a client that can connect to multiple servers.</summary>
    public class Client : Communicator
    {
        protected ClientConfiguration configuration;

        protected Guid guid = Guid.NewGuid();

        /// <summary>
        /// The currently opened channels.
        /// </summary>
        protected readonly ICollection<IChannel> channels =
             new WeakCollection<IChannel>();

        protected ICollection<IConnector> connectors;
        protected IMarshaller marshaller;
        protected HPTimer timer;
        protected long lastPingTime = 0;
        protected bool started = false;

        /// <summary>Creates a Client object using the default configuration.</summary>
        public Client()
            : this(new DefaultClientConfiguration())
        {
        }

        public Client(ClientConfiguration cc)
        {
            configuration = cc;
            timer = new HPTimer();
        }

        /// <summary>
        /// Return the marshaller configured for this client.
        /// </summary>
        public override IMarshaller Marshaller
        {
            get { return marshaller; }
        }

        /// <summary>
        /// Return the configuration guiding this instance.  This
        /// configuration acts as both a factory, responsible for 
        /// building the objects used by a client, as well as providing
        /// policy guidance.
        /// </summary>
        public ClientConfiguration Configuration { get { return configuration; } }

        /// <summary>
        /// Return a dictionary describing the capabilities and requirements 
        /// of this instance.  Used during handshaking when establishing new transports.
        /// </summary>
        public virtual IDictionary<string, string> Capabilities
        {
            get
            {
                Dictionary<string, string> caps = new Dictionary<string, string>();
                caps[GTCapabilities.CLIENT_GUID] = 
                    Guid.ToString("N");  // "N" is the most compact form
                StringBuilder sb = new StringBuilder();
                foreach(string d in Marshaller.Descriptors) {
                    sb.Append(d);
                    sb.Append(' ');
                }
                caps[GTCapabilities.MARSHALLER_DESCRIPTORS] = sb.ToString().Trim();
                return caps;
            }
        }

        /// <summary>
        /// Return the configured connector; these are responsible for establishing
        /// new connections (<see cref="ITransport"/>) to servers.
        /// </summary>
        public ICollection<IConnector> Connectors
        {
            get { return connectors; }
        }

        /// <summary>
        /// Return this client's globally unique identifier (GUID).
        /// </summary>
        public Guid Guid { get { return guid; } }

        /// <summary>
        /// Start the instance.  Starting an instance may throw an exception on error.
        /// </summary>
        public override void Start()
        {
            lock (this)
            {
                if(Active) { return; }

                marshaller = configuration.CreateMarshaller();
                timer.Start();
                timer.Update();
                connectors = configuration.CreateConnectors();
                foreach (IConnector conn in connectors)
                {
                    conn.Start();
                }
                started = true;
            }
        }

        /// <summary>
        /// Stop the instance.  Instances can be stopped multiple times.
        /// Stopping an instance may throw an exception on error.
        /// </summary>
        public override void Stop()
        {
            lock (this)
            {
                if (!Active) { return; }
                started = false;

                StopListeningThread();

                Stop(connectors);
                connectors = null;
                Dispose(channels);
                channels.Clear();
                base.Stop();
                // timer.Stop();?
            }
        }

        /// <summary>
        /// Dispose of any system resources that may be held onto by this
        /// instance.  Instances 
        /// </summary>
        public override void Dispose()
        {
            lock (this)
            {
                started = false;
                StopListeningThread();
                Dispose(connectors);
                connectors = null;
                Dispose(channels);
                channels.Clear();
                base.Dispose();
                timer = null;
            }
        }

        /// <summary>
        /// Return true if the instance has been started (<see cref="Start"/>)
        /// and neither stopped nor disposed (<see cref="Stop"/> and 
        /// <see cref="Dispose"/>).
        /// </summary>
        public override bool Active
        {
            get { return started; }
        }

        protected override TimeSpan TickInterval
        {
            get { return configuration.TickInterval; }
        }

        #region Channels

        protected void RecordChannel(byte channelId, IChannel cnx)
        {
            channels.Add(cnx);
        }

        /// <summary>
        /// Get a streaming tuple: changes to a streaming tuples are automatically sent to the 
        /// server periodically.
        /// </summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="T_Z">The Type of the third value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="channelId">The channel to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="updateInterval">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        /// <exception cref="CannotConnectException">thrown if connexion could not be established</exception>
        public IStreamedTuple<T_X, T_Y, T_Z> OpenStreamedTuple<T_X, T_Y, T_Z>(string address, string port, 
            byte channelId, TimeSpan updateInterval, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
            where T_Z : IConvertible
        {
            StreamedTuple<T_X, T_Y, T_Z> tuple = 
                new StreamedTuple<T_X, T_Y, T_Z>(GetConnexion(address, port), 
                    channelId, updateInterval, cdr);
            RecordChannel(channelId, tuple);
            return tuple;
        }

        /// <summary>
        /// Get a streaming tuple that is automatically sent to the server periodically.
        /// It is the caller's responsibility to ensure <see cref="connexion"/> 
        /// is still active.
        /// </summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="T_Z">The Type of the third value of the tuple</typeparam>
        /// <param name="connexion">The stream to use to send the tuple</param>
        /// <param name="channelId">The channel to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="updateInterval">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y, T_Z> OpenStreamedTuple<T_X, T_Y, T_Z>(IConnexion connexion, 
            byte channelId, TimeSpan updateInterval, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
            where T_Z : IConvertible
        {
            StreamedTuple<T_X, T_Y, T_Z> tuple = 
                new StreamedTuple<T_X, T_Y, T_Z>(connexion, channelId, updateInterval, cdr);
            RecordChannel(channelId, tuple);
            return tuple;
        }

        /// <summary>
        /// Get a streaming tuple that is automatically sent to the server periodically.
        /// </summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="channelId">The channel to use for this two-tuple (unique to two-tuples)</param>
        /// <param name="updateInterval">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        /// <exception cref="CannotConnectException">thrown if connexion could not be established</exception>
        public IStreamedTuple<T_X, T_Y> OpenStreamedTuple<T_X, T_Y>(string address, string port, 
            byte channelId, TimeSpan updateInterval, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
        {
            StreamedTuple<T_X, T_Y> tuple = 
                new StreamedTuple<T_X, T_Y>(GetConnexion(address, port), 
                    channelId, updateInterval, cdr);
            RecordChannel(channelId, tuple);
            return tuple;
        }

        /// <summary>
        /// Get a streaming tuple that is automatically sent to the server periodically.
        /// It is the caller's responsibility to ensure <see cref="connexion"/> 
        /// is still active.
        /// </summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <param name="connexion">The stream to use to send the tuple</param>
        /// <param name="channelId">The channel to use for this two-tuple (unique to two-tuples)</param>
        /// <param name="updateDelay">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y> OpenStreamedTuple<T_X, T_Y>(IConnexion connexion, byte channelId,
            TimeSpan updateDelay, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
        {
            StreamedTuple<T_X, T_Y> tuple = 
                new StreamedTuple<T_X, T_Y>(connexion, channelId, updateDelay, cdr);
            RecordChannel(channelId, tuple);
            return tuple;
        }

        /// <summary>
        /// Get a streaming tuple that is automatically sent to the server periodically.
        /// </summary>
        /// <typeparam name="T_X">The Type of the value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="channelId">The channel to use for this one-tuple (unique to one-tuples)</param>
        /// <param name="updateDelay">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        /// <exception cref="CannotConnectException">thrown if connexion could not be established</exception>
        public IStreamedTuple<T_X> OpenStreamedTuple<T_X>(string address, string port,
            byte channelId, TimeSpan updateDelay, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
        {
            StreamedTuple<T_X> tuple = 
                new StreamedTuple<T_X>(GetConnexion(address, port), 
                    channelId, updateDelay, cdr);
            RecordChannel(channelId, tuple);
            return tuple;
        }

        /// <summary>
        /// Get a streaming tuple that is automatically sent to the server periodically. 
        /// It is the caller's responsibility to ensure <see cref="connexion"/> 
        /// is still active.
        /// </summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <param name="connexion">The connexion to use to send the tuple</param>
        /// <param name="channelId">The channel to use for this one-tuple (unique to one-tuples)</param>
        /// <param name="updateDelay">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X> OpenStreamedTuple<T_X>(IConnexion connexion,
            byte channelId, TimeSpan updateDelay, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
        {
            StreamedTuple<T_X> tuple = 
                new StreamedTuple<T_X>(connexion, channelId, updateDelay, cdr);
            RecordChannel(channelId, tuple);
            return tuple;
        }


        /// <summary>Opens a channel for managing the session to this server.</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="channelId">The channel to claim or retrieve.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrieved SessionChannel</returns>
        /// <exception cref="CannotConnectException">thrown if connexion could not be established</exception>
        public ISessionChannel OpenSessionChannel(string address, string port,
            byte channelId, ChannelDeliveryRequirements cdr)
        {
            SessionChannel s = new SessionChannel(GetConnexion(address, port),
                channelId, cdr);
            RecordChannel(channelId, s);
            return s;
        }

        /// <summary>
        /// Opens a channel for managing the session to this server.  It is
        /// the caller's responsibility to ensure <see cref="connexion"/> is still active.
        /// </summary>
        /// <param name="connexion">The connexion to use for the connexion.</param>
        /// <param name="channelId">The channel to claim or retrieve.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrieved SessionChannel</returns>
        public ISessionChannel OpenSessionChannel(IConnexion connexion,
            byte channelId, ChannelDeliveryRequirements cdr)
        {
            SessionChannel ss = new SessionChannel(connexion, channelId, cdr);
            RecordChannel(channelId, ss);
            return ss;
        }

        /// <summary>Opens a channel for transmitting strings.</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="channelId">The channel to claim.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrieved IStringChannel</returns>
        /// <exception cref="CannotConnectException">thrown if connexion could not be established</exception>
        public IStringChannel OpenStringChannel(string address, string port,
            byte channelId, ChannelDeliveryRequirements cdr)
        {
            StringChannel ss = new StringChannel(GetConnexion(address, port),
                channelId, cdr);
            RecordChannel(channelId, ss);
            return ss;
        }

        /// <summary>
        /// Opens a channel for transmitting strings.  It is
        /// the caller's responsibility to ensure <see cref="connexion"/> is still active.
        /// </summary>
        /// <param name="connexion">The connexion to use for the channel</param>
        /// <param name="channelId">The channel to claim</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrieved IStringChannel</returns>
        public IStringChannel OpenStringChannel(IConnexion connexion,
            byte channelId, ChannelDeliveryRequirements cdr)
        {
            StringChannel ss = new StringChannel(connexion, channelId, cdr);
            RecordChannel(channelId, ss);
            return ss;
        }

        /// <summary>Opens a channel for transmitting objects.</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="channelId">The channel.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrieved ObjectChannel</returns>
        /// <exception cref="CannotConnectException">thrown if connexion could not be established</exception>
        public IObjectChannel OpenObjectChannel(string address, string port,
            byte channelId, ChannelDeliveryRequirements cdr)
        {
            ObjectChannel os = new ObjectChannel(GetConnexion(address, port),
                channelId, cdr);
            RecordChannel(channelId, os);
            return os;
        }

        /// <summary>Opens a channel for transmitting objects.  It is
        /// the caller's responsibility to ensure <see cref="connexion"/> is still active.</summary>
        /// <param name="connexion">The connexion to use for the channel.</param>
        /// <param name="channelId">The channelId.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrieved ObjectChannel</returns>
        public IObjectChannel OpenObjectChannel(IConnexion connexion, byte channelId,
            ChannelDeliveryRequirements cdr)
        {
            ObjectChannel os = new ObjectChannel(connexion, channelId, cdr);
            RecordChannel(channelId, os);
            return os;
        }

        /// <summary>Gets a connexion for transmitting byte arrays.</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="channelId">The channel.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrieved BinaryChannel.</returns>
        /// <exception cref="CannotConnectException">thrown if connexion could not be established</exception>
        public IBinaryChannel OpenBinaryChannel(string address, string port,
            byte channelId, ChannelDeliveryRequirements cdr)
        {
            BinaryChannel bs = new BinaryChannel(GetConnexion(address, port),
                channelId, cdr);
            RecordChannel(channelId, bs);
            return bs;
        }

        /// <summary>Gets a connexion for transmitting byte arrays.  It is
        /// the caller's responsibility to ensure <see cref="connexion"/> is still active.</summary>
        /// <param name="connexion">The connexion to use for the connexion.</param>
        /// <param name="channelId">The channel</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrieved BinaryChannel.</returns>
        public IBinaryChannel OpenBinaryChannel(IConnexion connexion,
            byte channelId, ChannelDeliveryRequirements cdr)
        {
            BinaryChannel bs = new BinaryChannel(connexion, channelId, cdr);
            RecordChannel(channelId, bs);
            return bs;
        }

        #endregion

        /// <summary>Gets a server connexion; if no such connexion exists establish one.</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <returns>The created or retrieved connexion itself.</returns>
        /// <exception cref="CannotConnectException">thrown if the
        ///     remote could not be contacted.</exception>
        virtual protected IConnexion GetConnexion(string address, string port)
        {
            InvalidStateException.Assert(Active, "Cannot connect from a stopped client", this);

            foreach (IAddressableConnexion s in connexions)
            {
                if (s.Active && s.Address.Equals(address) && s.Port.Equals(port))
                {
                    return s;
                }
            }
            IConnexion mySC = configuration.CreateServerConnexion(this, address, port);
            mySC.ErrorEvents += NotifyError;
            if(mySC is IStartable)
            {
                ((IStartable)mySC).Start();
            }
            AddConnexion(mySC);
            return mySC;
        }

        /// <summary>
        /// Run a cycle to process any pending events for the connexions or
        /// other related objects for this instance.  This method is <strong>not</strong> 
        /// re-entrant and should not be called from GT callbacks.
        /// </summary>
        public override void Update()
        {
            if (!Active) { return; }
            log.Trace("Client.Update(): Starting");
            lock (this)
            {
                timer.Update();
                if (timer.TimeInMilliseconds - lastPingTime
                    > configuration.PingInterval.TotalMilliseconds)
                {
                    log.Debug("Pinging");
                    lastPingTime = timer.TimeInMilliseconds;
                    foreach (IAddressableConnexion s in connexions)
                    {
                        s.Ping();
                    }
                }

                foreach (IAddressableConnexion s in connexions)
                {
                    if (!s.Active) { continue; }
                    try
                    {
                        s.Update();
                    }
                    catch (ConnexionClosedException) { s.Dispose(); }
                    catch (GTException e)
                    {
                        string message = String.Format("GT Exception occurred in Client.Update() while processing connexion {0}", s);
                        log.Info(message, e);
                        NotifyError(new ErrorSummary(e.Severity,
                            SummaryErrorCode.RemoteUnavailable,
                            message, e));
                    }
                }

                // let each channel have a chance to update itself
                foreach (IChannel mq in channels)
                {
                    if (mq is IUpdatableChannel)
                    {
                        ((IUpdatableChannel)mq).Update(timer);
                    }
                }

                // Remove any dead connexions
                RemoveDeadConnexions();
            }
            log.Trace("Client.Update(): Finished");
            NotifyTick();
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder(GetType().Name);
            b.Append("(ids:");
            foreach (IAddressableConnexion c in connexions)
            {
                b.Append(' ');
                b.Append(c.Identity);
            }
            b.Append(")");
            return b.ToString();
        }
    }
}
