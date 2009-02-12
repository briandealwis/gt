using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using GT.Millipede;
using GT.Net;
using GT.Utils;

namespace GT.Net
{

    #region Delegates

    /// <summary>Occurs whenever a client is updated.</summary>
    public delegate void UpdateEventDelegate(HPTimer hpTimer);

    #endregion

    #region Streams

    #region Type Stream Interfaces

    public interface IStream
    {
        /// <summary>Occurs whenever this client is updated.</summary>
        event UpdateEventDelegate UpdateEvent;

        /// <summary>
        /// Return this stream's associated channel.
        /// </summary>
        byte Channel { get; }

        /// <summary>Return the underlying <see cref="IConnexion.Identity"/>, a
        /// server-unique identity for this client amongst the server's clients.</summary>
        int Identity { get; }

        /// <summary>Flush all pending messages on this stream.</summary>
        void Flush();

        /// <summary>
        /// Return this stream's associated connexion.
        /// </summary>
        IConnexion Connexion { get; }
        
        /// <summary>
        /// Average latency between the client and this particluar server 
        /// (in milliseconds).
        /// </summary>
        float Delay { get; }
    }

    /// <summary>
    /// A generic item stream as exposed by the GT Client.
    /// </summary>
    /// <typeparam name="SI">The type of generic items supported by this stream.</typeparam>
    /// <typeparam name="RI">The type of received items, which is generally expected to be
    ///     the same as <c>SI</c>.  Some special streams, such as <c>ISessionStream</c>, return
    ///     more complex objects.</typeparam>
    /// <typeparam name="ST">The type of this stream.</typeparam>
    public interface IGenericStream<SI,RI,ST> : IStream
        where RI : class
        where ST : IStream
    {
        /// <summary>Send an item to the server</summary>
        /// <param name="item">The item</param>
        void Send(SI item);

        /// <summary>Send an item to the server</summary>
        /// <param name="item">The item</param>
        /// <param name="mdr">How to send it</param>
        void Send(SI item, MessageDeliveryRequirements mdr);

        /// <summary>Take an item off the queue of received messages and returns its content
        /// object.</summary>
        /// <param name="index">The message to be dequeued, with a higher number indicating a newer message.</param>
        /// <returns>The message content, or null if the content could not be obtained.</returns>
        RI DequeueMessage(int index);

        /// <summary>Return the number of waiting messages.</summary>
        /// <returns>The number of waiting messages; 0 indicates there are no waiting message.</returns>
        int Count { get; }

        /// <summary>Received messages from the server.</summary>
        IList<Message> Messages { get; }

        /// <summary> Triggered when the underlying connexion has new messages. </summary>
        event Action<ST> MessagesReceived;
    }

    /// <summary>A connexion of session events.</summary>
    public interface ISessionStream : IGenericStream<SessionAction,SessionMessage,ISessionStream>
    {
    }

    /// <summary>A connexion of strings.</summary>
    public interface IStringStream : IGenericStream<string,string,IStringStream>
    {
    }

    /// <summary>A connexion of objects.</summary>
    public interface IObjectStream : IGenericStream<object,object,IObjectStream>
    {
    }

    /// <summary>A connexion of byte arrays.</summary>
    public interface IBinaryStream : IGenericStream<byte[],byte[],IBinaryStream>
    {
    }

    #endregion

    public abstract class AbstractBaseStream : IStream
    {
        protected byte channel;
        protected ConnexionToServer connexion;
        protected ChannelDeliveryRequirements deliveryOptions;

        /// <summary> Occurs when client is updated. </summary>
        public event UpdateEventDelegate UpdateEvent;

        /// <summary> This stream's channel. </summary>
        public byte Channel { get { return channel; } }

        /// <summary>Average latency between the client and this particluar server.</summary>
        public float Delay { get { return connexion.Delay; } }

        /// <summary> Get the server-unique identity of the client.</summary>
        /// <seealso cref="IConnexion.Identity"/>
        public int Identity { get { return connexion.Identity; } }

        /// <summary> Get the connexion's destination address </summary>
        public string Address { get { return connexion.Address; } }

        /// <summary>Get the connexion's destination port</summary>
        public string Port { get { return connexion.Port; } }

        /// <summary>Flush all pending messages on this stream.</summary>
        public abstract void Flush();

        /// <summary>
        /// Return this stream's connexion.
        /// </summary>
        public IConnexion Connexion 
        { 
            get { return connexion; }
            internal set { connexion = (ConnexionToServer)value; }
        }

        public ChannelDeliveryRequirements ChannelDeliveryOptions { 
            get { return deliveryOptions; }
            internal set { deliveryOptions = value; }
        }

        internal AbstractBaseStream(ConnexionToServer cnx, byte channel, ChannelDeliveryRequirements cdr)
        {
            connexion = cnx;
            this.channel = channel;
            deliveryOptions = cdr;
        }

        internal virtual void Update(HPTimer hpTimer)
        {
            if (UpdateEvent != null)
            {
                UpdateEvent(hpTimer);
            }
        }

        public override string ToString()
        {
            return GetType().Name + "[" + connexion + "]";
        }
    }

    /// <summary>
    /// The base implementation for the client stream abstraction.
    /// We differentiate between <typeparamref name="SI"/> and <typeparamref name="RI"/>
    /// as some streams, particularly the session stream, send and return 
    /// different types of items.
    /// </summary>
    /// <typeparam name="SI">the type of stream items</typeparam>
    /// <typeparam name="RI">the type of returned items</typeparam>
    /// <typeparam name="ST">the type of this stream interface</typeparam>
    public abstract class AbstractStream<SI, RI, ST> : AbstractBaseStream, IGenericStream<SI, RI, ST>
        where ST: IStream
        where RI: class
    {
        public event Action<ST> MessagesReceived;
        protected List<Message> messages;

        /// <summary>Received messages from the server.</summary>
        public IList<Message> Messages { get { return messages; } }

        /// <summary> This stream uses this connexion. </summary>
        /// <remarks>deprecated</remarks>
        public ConnexionToServer Connection { get { return connexion; } }

        /// <summary>Create a stream object.</summary>
        /// <param name="stream">The connexion used to actually send the messages.</param>
        /// <param name="channel">The message channel.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal AbstractStream(ConnexionToServer stream, byte channel, ChannelDeliveryRequirements cdr) 
            : base(stream, channel, cdr)
        {
            messages = new List<Message>();
        }

        /// <summary>
        /// See <see cref="IGenericStream{SI,RI,ST}.Count"/>
        /// </summary>
        public virtual int Count { get { return messages.Count; } }

        /// <summary>
        /// See <see cref="IGenericStream{SI,RI,ST}.Send(SI)"/>
        /// </summary>
        /// <param name="item">the item to send</param>
        public void Send(SI item)
        {
            Send(item, null);
        }

        /// <summary>
        /// See <see cref="IGenericStream{SI,RI,ST}.Send(SI,MessageDeliveryRequirements)"/>
        /// </summary>
        /// <param name="item">the item to send</param>
        /// <param name="mdr">the delivery requirements for the message, overriding the
        /// channel's delivery requirements</param>
        public abstract void Send(SI item, MessageDeliveryRequirements mdr);

        /// <summary>
        /// See <see cref="IGenericStream{SI,RI,ST}.DequeueMessage"/>
        /// </summary>
        /// <param name="index">the message to dequeue (FIFO order)</param>
        virtual public RI DequeueMessage(int index)
        {
            try
            {
                if (index >= messages.Count)
                    return null;

                Message m;
                lock (messages)
                {
                    m = messages[index];
                    messages.RemoveAt(index);
                }
                return GetMessageContents(m);
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
        /// <returns>the appropriately-typed object content from <see cref="m"/></returns>
        abstract public RI GetMessageContents(Message m);

        /// <summary>Flush all aggregated messages on this connexion</summary>
        public override void Flush()
        {
            connexion.FlushChannelMessages(this.channel, deliveryOptions);
        }

        protected abstract ST CastedStream { get; }

        /// <summary>Queue a message in the list, triggering events</summary>
        /// <param name="message">The message to be queued.</param>
        internal void QueueMessage(Message message)
        {
            messages.Add(message);
            if (MessagesReceived != null)
                MessagesReceived(CastedStream);
        }


    }

    /// <summary>A connexion of session events.</summary>
    internal class SessionStream : AbstractStream<SessionAction, SessionMessage, ISessionStream>, ISessionStream
    {
        /// <summary>Create a SessionStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="channel">The message channel.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal SessionStream(ConnexionToServer stream, byte channel, ChannelDeliveryRequirements cdr) 
            : base(stream, channel, cdr)
        {
        }

        /// <summary>Send a session action to the server.</summary>
        /// <param name="action">The action.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(SessionAction action, MessageDeliveryRequirements mdr)
        {
            connexion.Send(new SessionMessage(channel, Identity, action), mdr, deliveryOptions);
        }

        override public SessionMessage GetMessageContents(Message m)
        {
            return (SessionMessage)m;
        }

        protected override ISessionStream CastedStream { get { return this; } }
    }

    /// <summary>A connexion of strings.</summary>
    internal class StringStream : AbstractStream<string, string, IStringStream>, IStringStream
    {
        /// <summary>Create a StringStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="channel">The message channel.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal StringStream(ConnexionToServer stream, byte channel, ChannelDeliveryRequirements cdr) 
            : base(stream, channel, cdr)
        {
        }

        
        /// <summary>Send a string to the server, specifying how.</summary>
        /// <param name="s">The string to send.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(string s, MessageDeliveryRequirements mdr)
        {
            connexion.Send(new StringMessage(channel, s), mdr, deliveryOptions);
        }

        override public string GetMessageContents(Message m)
        {
            return ((StringMessage)m).Text;
        }

        protected override IStringStream CastedStream { get { return this; } }
    }

    /// <summary>A connexion of Objects.</summary>
    internal class ObjectStream : AbstractStream<object, object, IObjectStream>, IObjectStream
    {
        /// <summary>Create an ObjectStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the objects.</param>
        /// <param name="channel">The message channel claimed.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal ObjectStream(ConnexionToServer stream, byte channel, ChannelDeliveryRequirements cdr) 
            : base(stream, channel, cdr)
        {
        }

        /// <summary>Send an object using the specified method.</summary>
        /// <param name="o">The object to send.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(object o, MessageDeliveryRequirements mdr)
        {
            connexion.Send(new ObjectMessage(channel, o), mdr, deliveryOptions);
        }

        override public object GetMessageContents(Message m)
        {
            return ((ObjectMessage)m).Object;
        }

        protected override IObjectStream CastedStream { get { return this; } }
    }

    /// <summary>A connexion of byte arrays.</summary>
    internal class BinaryStream : AbstractStream<byte[],byte[], IBinaryStream>, IBinaryStream
    {
        /// <summary>Creates a BinaryStream object.</summary>
        /// <param name="stream">The SuperStream object on which to actually send the objects.</param>
        /// <param name="channel">The message channel to claim.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal BinaryStream(ConnexionToServer stream, byte channel, ChannelDeliveryRequirements cdr) 
            : base(stream, channel, cdr)
        {
        }

        /// <summary>Send a byte array using the specified method.</summary>
        /// <param name="b">The byte array to send.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(byte[] b, MessageDeliveryRequirements mdr)
        {
            connexion.Send(new BinaryMessage(channel, b), mdr, deliveryOptions);
        }

        override public byte[] GetMessageContents(Message m)
        {
            return ((BinaryMessage)m).Bytes;
        }
        
        protected override IBinaryStream CastedStream { get { return this; } }
    }

    #endregion

    /// <summary>Controls the sending of messages to a particular server.</summary>
    public class ConnexionToServer : BaseConnexion, IStartable
    {
        private Client owner;
        private string address;
        private string port;

        /// <summary>Incoming messages from the server. As messages are read in from the
        /// different transports, they are added to this list.  These messages are then
        /// processed by Client.Update() to dispatch to their corresponding stream.
        /// We separate these two steps to isolate potential problems.</summary>
        protected Queue<Message> receivedMessages;

        private Dictionary<byte, Queue<PendingMessage>> messageQueues;

        /// <summary>
        /// Return the marshaller configured for this stream's client.
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

        /// <summary>Create a new SuperStream to handle a connexion to a server.</summary>
        /// <param name="owner">The owning client.</param>
        /// <param name="address">Who to try to connect to.</param>
        /// <param name="port">Which port to connect to.</param>
        protected internal ConnexionToServer(Client owner, string address, string port)
        {
            active = false;
            this.owner = owner;
            this.address = address;
            this.port = port;
            this.MessageReceived += HandleIncomingMessage;
        }

        /// <summary>
        /// Start this instance.
        /// </summary>
        /// <exception cref="CannotConnectException">thrown if we cannot
        /// connect to the specified server.</exception>
        virtual public void Start()
        {
            if (Active) { return; }

            messageQueues = new Dictionary<byte, Queue<PendingMessage>>();
            receivedMessages = new Queue<Message>();

            transports = new List<ITransport>();

            // FIXME: should this be done on demand?
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
                throw new CannotConnectException("could not connect to any transports");
            }
            // otherwise...
            active = true;
        }

        virtual public void Stop()
        {
            ShutDown();
            transports = null;
        }

        #endregion

        public virtual string Address
        {
            get { return address; }
        }

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

        protected void HandleIncomingMessage(Message m, IConnexion client, ITransport transport)
        {
            // Hmm, this lock may not be necessary -- the Dequeueing of messages should
            // occur in the same thread.
            //Console.WriteLine("{0}: posting incoming message {1}", this, msg);
            lock (receivedMessages) 
            { 
                receivedMessages.Enqueue(m); 
            }
        }


        /// <summary>Adds the message to a list, waiting to be sent out.</summary>
        /// <param name="msg">The message to be aggregated</param>
        /// <param name="mdr">How it should be sent out (potentially null)</param>
        /// <param name="cdr">General delivery instructions for this message's channel.</param>
        private void Aggregate(Message msg, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Queue<PendingMessage> mp;
            if (!messageQueues.TryGetValue(msg.Channel, out mp))
            {
                mp = messageQueues[msg.Channel] = new Queue<PendingMessage>();
            }
            else if (cdr != null && cdr.Freshness == Freshness.IncludeLatestOnly)
            {
                mp.Clear();
            }
            mp.Enqueue(new PendingMessage(msg, mdr, cdr));
        }

        /// <summary>Send a message using these parameters.</summary>
        /// <param name="msg">The message to send.</param>
        /// <param name="mdr">Particular instructions for this message.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        override public void Send(Message msg, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            lock (this)
            {
                if (!Active) {
                    throw new InvalidStateException("Cannot send on a stopped client", this); 
                }

                try
                {
                    MessageAggregation aggr = mdr == null ? cdr.Aggregation : mdr.Aggregation;
                    if (aggr == MessageAggregation.Aggregatable)
                    {
                        // Wait to send this message, hopefully to pack it with later messages.
                        Aggregate(msg, mdr, cdr);
                        return;
                    }

                    if (messageQueues == null || messageQueues.Count == 0)
                    {
                        // Short circuit since there are no other messages waiting
                        SendMessage(FindTransport(mdr, cdr), msg);
                        return;
                    }

                    switch (aggr)
                    {
                    case MessageAggregation.Aggregatable:
                        // already handled
                        log.Error("MessageAggregation.Aggregatable should have alread been handled");
                        return;

                    case MessageAggregation.FlushChannel:
                        // make sure ALL other messages on this CHANNEL are sent, and then send <c>msg</c>.
                        FlushMessages(new ProcessorChain<PendingMessage>(
                            new SameChannelProcessor(msg.Channel, messageQueues),
                            new SingleElementProcessor<PendingMessage>(new PendingMessage(msg, mdr, cdr))));
                        return;

                    case MessageAggregation.FlushAll:
                        // make sure ALL messages are sent, then send <c>msg</c>.
                        // FIXME: channels should be prioritized?  So shouldn't be round robin.
                        FlushMessages(new ProcessorChain<PendingMessage>(
                            new RoundRobinProcessor<byte, PendingMessage>(messageQueues),
                            new SingleElementProcessor<PendingMessage>(new PendingMessage(msg, mdr, cdr))));
                        return;

                    case MessageAggregation.Immediate:
                        // bundle <c>msg</c> first and then cram on whatever other messages are waiting.
                        // FIXME: channels should be prioritized?  So shouldn't be round robin.
                        FlushMessages(new ProcessorChain<PendingMessage>(
                            new SingleElementProcessor<PendingMessage>(new PendingMessage(msg, mdr, cdr)),
                            new RoundRobinProcessor<byte, PendingMessage>(messageQueues)));
                        return;

                    default:
                        throw new ArgumentException("Unhandled aggregation type: " + aggr);
                    }
                }
                catch (GTException e)
                {
                    NotifyError(new ErrorSummary(e.Severity, SummaryErrorCode.MessagesCannotBeSent,
                        e.Message, e));
                }
            }
        }

        /// <summary>Send a message using these parameters.</summary>
        /// <param name="msgs">The messages to send.</param>
        /// <param name="mdr">Particular instructions for this message.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        override public void Send(IList<Message> msgs, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            // GTExceptions caught by Send()
            foreach (Message m in msgs) { Send(m, mdr, cdr); }
        }

        internal void FlushChannelMessages(byte channel, ChannelDeliveryRequirements cdr)
        {
            // must be locked as is called by AbstractStream implementations
            lock(this)
            {
                try
                {
                    FlushMessages(new SameChannelProcessor(channel, messageQueues));
                }
                catch (CannotSendMessagesError e)
                {
                    NotifyError(new ErrorSummary(Severity.Warning,
                        SummaryErrorCode.MessagesCannotBeSent,
                        String.Format("Unable to flush channel {0}", channel), e));
                }
            }
        }

        protected void FlushMessages(IProcessingQueue<PendingMessage> queue)
        {
            // FIXME: this method is too long
            Dictionary<ITransport, Stream> inProgress = new Dictionary<ITransport, Stream>();
            Dictionary<ITransport, IList<PendingMessage>> dequeuedMessages = 
                new Dictionary<ITransport, IList<PendingMessage>>();
            PendingMessage pm;
            CannotSendMessagesError csme = new CannotSendMessagesError(this);

            while ((pm = queue.Current) != null)
            {
                ITransport transport;
                try
                {
                    transport = FindTransport(pm.MDR, pm.CDR);
                }
                catch (NoMatchingTransport e)
                {
                    csme.Add(e, pm);
                    queue.Remove();
                    continue;
                }
                Stream stream;
                IList<PendingMessage> pending;
                if (!inProgress.TryGetValue(transport, out stream) || stream == null)
                {
                    stream = inProgress[transport] = transport.GetPacketStream();
                }
                if (!dequeuedMessages.TryGetValue(transport, out pending) || pending == null)
                {
                    pending = dequeuedMessages[transport] = new List<PendingMessage>();
                }

                /// Attempt to marshal pm onto the transport stream.  Should the stream 
                /// exceed the maximum packet size as defined by <c>t</c>, back off the 
                /// message, send the stream contents, and obtain a new stream.
                long previousLength = stream.Length;
                try
                {
                    Marshaller.Marshal(SendingIdentity, pm.Message, stream, transport);
                }
                catch (MarshallingException e)
                {
                    csme.Add(e, pm.Message);
                    queue.Remove();
                    stream.SetLength(previousLength);
                    continue;
                }
                if (stream.Length < transport.MaximumPacketSize)
                {
                    queue.Remove(); // remove current message
                    pending.Add(pm);
                }
                else
                {
                    // resulting packet is too big: go back to previous length, send what we had
                    stream.SetLength(previousLength);
                    try { 
                        SendPacket(transport, stream);
                        NotifyMessagesSent(pending, transport);
                    }
                    catch (TransportError e)
                    {
                        // requeue these messages to try them again on a different transport
                        // FIXME: some of the messages might have actually been sent!
                        csme.AddAll(e, pending);
                        pending.Clear();
                        HandleTransportDisconnect(transport);
                        continue;
                    }
                    catch (TransportBackloggedWarning e)
                    {
                        // The packet is still outstanding; just warn the user 
                        NotifyError(new ErrorSummary(Severity.Information,
                            SummaryErrorCode.TransportBacklogged,
                            "Transport backlogged: " + transport, e));
                    }
                    inProgress[transport] = stream = transport.GetPacketStream();
                    pending.Clear();
                }
            }

            // send everything in progress
            foreach (ITransport t in inProgress.Keys)
            {
                Stream stream = inProgress[t];
                try { 
                    t.SendPacket(stream);
                    if (dequeuedMessages.ContainsKey(t) && dequeuedMessages[t].Count > 0)
                    {
                        NotifyMessagesSent(dequeuedMessages[t], t);
                    }
                }
                catch (TransportError e)
                {
                    csme.AddAll(e, dequeuedMessages[t]);
                    HandleTransportDisconnect(t);
                }
                catch (TransportBackloggedWarning e)
                {
                    // The packet is still outstanding; just warn the user 
                    NotifyError(new ErrorSummary(Severity.Information,
                        SummaryErrorCode.TransportBacklogged,
                        "Transport backlogged: " + t, e));
                }
            }
            // No point re-queuing the messages since there's no available transport
            csme.ThrowIfApplicable();
        }

        /// <summary>Deal with a system message in whatever way we need to.</summary>
        /// <param name="message">The incoming message.</param>
        /// <param name="transport">The transport from which the message
        ///  came.</param>
        override protected void HandleSystemMessage(SystemMessage message, ITransport transport)
        {
            switch (message.Descriptor)
            {
            case SystemMessageType.IdentityRequest:
                identity = BitConverter.ToInt32(message.data, 0);
                break;

            default:
                base.HandleSystemMessage(message, transport);
                return;
            }
        }

        internal Message DequeueMessage()
        {
            lock (receivedMessages)
            {
                if (receivedMessages.Count == 0) { return null; }
                return receivedMessages.Dequeue();
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
        virtual public ConnexionToServer CreateServerConnexion(Client owner,
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
        private ClientConfiguration configuration;

        private Guid guid = Guid.NewGuid();
        internal Dictionary<byte, ObjectStream> objectStreams;
        internal Dictionary<byte, BinaryStream> binaryStreams;
        internal Dictionary<byte, StringStream> stringStreams;
        internal Dictionary<byte, SessionStream> sessionStreams;
        internal Dictionary<byte, AbstractStreamedTuple> oneTupleStreams;
        internal Dictionary<byte, AbstractStreamedTuple> twoTupleStreams;
        internal Dictionary<byte, AbstractStreamedTuple> threeTupleStreams;

        protected ICollection<IConnector> connectors;
        protected IMarshaller marshaller;
        protected HPTimer timer;
        protected long lastPingTime = 0;
        protected bool started = false;

        // Keep track of the channels that have had warnings of a missing event listener:
        // it's annoying to have hundreds of warnings scroll by!
        protected IDictionary<byte, byte> previouslyWarnedChannels;
        protected IDictionary<MessageType, MessageType> previouslyWarnedMessageTypes;

        /// <summary>Creates a Client object.  
        /// <strong>deprecated:</strong> The client is started</summary>
        public Client()
            : this(new DefaultClientConfiguration())
        {
            Start();    // this behaviour is deprecated
        }

        public Client(ClientConfiguration cc)
        {
            configuration = cc;
            objectStreams = new Dictionary<byte, ObjectStream>();
            binaryStreams = new Dictionary<byte, BinaryStream>();
            stringStreams = new Dictionary<byte, StringStream>();
            sessionStreams = new Dictionary<byte, SessionStream>();
            oneTupleStreams = new Dictionary<byte, AbstractStreamedTuple>();
            twoTupleStreams = new Dictionary<byte, AbstractStreamedTuple>();
            threeTupleStreams = new Dictionary<byte, AbstractStreamedTuple>();
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
                previouslyWarnedChannels = new Dictionary<byte, byte>();
                previouslyWarnedMessageTypes = new Dictionary<MessageType, MessageType>();

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

        #region Streams

        /// <summary>Get a streaming tuple: changes to a streaming tuples are automatically sent to the 
        /// server every <see cref="updateInterval"/> milliseconds.
        /// Note: This call rebinds any existing 3-element tuple stream on this channel to the 
        /// provided connexion!</summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="T_Z">The Type of the third value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="channel">The channel to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="updateInterval">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y, T_Z> GetStreamedTuple<T_X, T_Y, T_Z>(string address, string port, 
            byte channel, TimeSpan updateInterval, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
            where T_Z : IConvertible
        {
            StreamedTuple<T_X, T_Y, T_Z> tuple;
            if (threeTupleStreams.ContainsKey(channel) 
                && threeTupleStreams[channel] is StreamedTuple<T_X, T_Y, T_Z>)
            {
                tuple = (StreamedTuple<T_X, T_Y, T_Z>)threeTupleStreams[channel];
                if (tuple.Address.Equals(address) && tuple.Port.Equals(port)
                    && tuple.Connexion.Active)
                {
                    return tuple;
                }

                tuple.Connexion = GetConnexion(address, port);
                return tuple;
            }

            tuple = new StreamedTuple<T_X, T_Y, T_Z>(GetConnexion(address, port), 
                channel, updateInterval, cdr);
            threeTupleStreams.Add(channel, tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every 
        /// so often. It is the caller's responsibility to ensure <see cref="connexion"/> 
        /// is still active.</summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="T_Z">The Type of the third value of the tuple</typeparam>
        /// <param name="connexion">The stream to use to send the tuple</param>
        /// <param name="channel">The channel to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="updateInterval">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y, T_Z> GetStreamedTuple<T_X, T_Y, T_Z>(IConnexion connexion, 
            byte channel, TimeSpan updateInterval, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
            where T_Z : IConvertible
        {
            StreamedTuple<T_X, T_Y, T_Z> tuple;
            if (threeTupleStreams.ContainsKey(channel) 
                && threeTupleStreams[channel] is StreamedTuple<T_X, T_Y, T_Z>)
            {
                tuple = (StreamedTuple<T_X, T_Y, T_Z>) threeTupleStreams[channel];
                if (tuple.Connexion == connexion)
                {
                    return tuple;
                }

                tuple.Connexion = connexion;
                return tuple;
            }

            tuple = new StreamedTuple<T_X, T_Y, T_Z>(connexion as ConnexionToServer, channel, updateInterval, cdr);
            threeTupleStreams.Add(channel, tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="channel">The channel to use for this two-tuple (unique to two-tuples)</param>
        /// <param name="updateInterval">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y> GetStreamedTuple<T_X, T_Y>(string address, string port, 
            byte channel, TimeSpan updateInterval, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
        {
            StreamedTuple<T_X, T_Y> tuple;
            if (twoTupleStreams.ContainsKey(channel) 
                && twoTupleStreams[channel] is StreamedTuple<T_X, T_Y>)
            {
                tuple = (StreamedTuple<T_X, T_Y>) twoTupleStreams[channel];
                if (tuple.Address.Equals(address) && tuple.Port.Equals(port)
                    && tuple.Connexion.Active)
                {
                    return tuple;
                }

                tuple.Connexion = GetConnexion(address, port);
                return tuple;
            }

            tuple = new StreamedTuple<T_X, T_Y>(GetConnexion(address, port), channel, updateInterval, cdr);
            twoTupleStreams.Add(channel, tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every 
        /// so often. It is the caller's responsibility to ensure <see cref="connexion"/> 
        /// is still active.</summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <param name="connexion">The stream to use to send the tuple</param>
        /// <param name="channel">The channel to use for this two-tuple (unique to two-tuples)</param>
        /// <param name="updateDelay">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y> GetStreamedTuple<T_X, T_Y>(IConnexion connexion, byte channel,
            TimeSpan updateDelay, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
        {
            StreamedTuple<T_X, T_Y> tuple;
            if (twoTupleStreams.ContainsKey(channel)
                && twoTupleStreams[channel] is StreamedTuple<T_X, T_Y>)
            {
                tuple = (StreamedTuple<T_X, T_Y>)twoTupleStreams[channel];
                if (tuple.Connexion == connexion)
                {
                    return tuple;
                }

                tuple.Connexion = connexion;
                return tuple;
            }

            tuple = new StreamedTuple<T_X, T_Y>(connexion as ConnexionToServer, channel, updateDelay, cdr);
            twoTupleStreams.Add(channel, tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often.</summary>
        /// <typeparam name="T_X">The Type of the value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="channel">The channel to use for this one-tuple (unique to one-tuples)</param>
        /// <param name="updateDelay">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X> GetStreamedTuple<T_X>(string address, string port, byte channel, 
            TimeSpan updateDelay, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
        {
            StreamedTuple<T_X> tuple;
            if (oneTupleStreams.ContainsKey(channel) && oneTupleStreams[channel] is StreamedTuple<T_X>)
            {
                tuple = (StreamedTuple<T_X>)oneTupleStreams[channel];
                if (tuple.Address.Equals(address) && tuple.Port.Equals(port)
                    && tuple.Connexion.Active)
                {
                    return tuple;
                }

                tuple.Connexion = GetConnexion(address, port);
                return tuple;
            }

            tuple = new StreamedTuple<T_X>(GetConnexion(address, port), channel, updateDelay, cdr);
            oneTupleStreams.Add(channel, tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every 
        /// so often. It is the caller's responsibility to ensure <see cref="connexion"/> 
        /// is still active.</summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <param name="connexion">The stream to use to send the tuple</param>
        /// <param name="channel">The channel to use for this one-tuple (unique to one-tuples)</param>
        /// <param name="updateDelay">The interval between updates</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X> GetStreamedTuple<T_X>(IConnexion connexion, byte channel, 
            TimeSpan updateDelay, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
        {
            StreamedTuple<T_X> tuple;
            if (oneTupleStreams.ContainsKey(channel)
                && oneTupleStreams[channel] is StreamedTuple<T_X>)
            {
                tuple = (StreamedTuple<T_X>)oneTupleStreams[channel];
                if (tuple.Connexion == connexion)
                {
                    return tuple;
                }

                tuple.Connexion = connexion;
                return tuple;
            }

            tuple = new StreamedTuple<T_X>(connexion as ConnexionToServer, channel, updateDelay, cdr);
            oneTupleStreams.Add(channel, tuple);
            return tuple;
        }


        /// <summary>Gets a connexion for managing the session to this server.</summary>
        /// Note: This call rebinds any existing string stream on this channel to the 
        /// provided connexion!</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="channel">The channel to claim or retrieve.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrived SessionStream</returns>
        public ISessionStream GetSessionStream(string address, string port, byte channel, ChannelDeliveryRequirements cdr)
        {
            SessionStream s;
            if (sessionStreams.TryGetValue(channel, out s))
            {
                if (!s.Address.Equals(address) || !s.Port.Equals(port) || !s.Connexion.Active)
                {
                    s.Connexion = GetConnexion(address, port);
                } else
                {
                    s.ChannelDeliveryOptions = cdr;
                }
                return s;
            }

            s = new SessionStream(GetConnexion(address, port), channel, cdr);
            sessionStreams.Add(channel, s);
            return s;
        }

        /// <summary>Gets a connexion for managing the session to this server.  It is
        /// the caller's responsibility to ensure <see cref="connexion"/> is still active.
        /// Note: This call rebinds any existing session stream on this channel to the 
        /// provided connexion!</summary>
        /// <param name="connexion">The connexion to use for the connexion.</param>
        /// <param name="channel">The channel to claim or retrieve.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrived SessionStream</returns>
        public ISessionStream GetSessionStream(IConnexion connexion, byte channel, ChannelDeliveryRequirements cdr)
        {
            SessionStream ss;
            if (sessionStreams.ContainsKey(channel))
            {
                ss = sessionStreams[channel];
                if (ss.Connexion == connexion)
                {
                    return ss;
                }

                ss.Connexion = connexion;
                return ss;
            }

            ss = new SessionStream(connexion as ConnexionToServer, channel, cdr);
            sessionStreams.Add(channel, ss);
            return ss;
        }

        /// <summary>Gets an already created SessionStream</summary>
        /// <param name="channel">The channel for the stream.</param>
        /// <returns>The found SessionStream</returns>
        public ISessionStream GetSessionStream(byte channel)
        {
            return sessionStreams[channel];
        }

        /// <summary>Obtain a stream on a channel for transmitting strings.
        /// Note: This call rebinds any existing string stream on this channel to the 
        /// provided connexion!</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="channel">The channel to claim.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrived StringStream</returns>
        public IStringStream GetStringStream(string address, string port, byte channel, ChannelDeliveryRequirements cdr)
        {
            StringStream ss;
            if (stringStreams.ContainsKey(channel))
            {
                ss = stringStreams[channel];
                if (ss.Address.Equals(address) && ss.Port.Equals(port) && ss.Connexion.Active)
                {
                    return ss;
                }

                ss.Connexion = GetConnexion(address, port);
                return ss;
            }

            ss = new StringStream(GetConnexion(address, port), channel, cdr);
            stringStreams.Add(channel, ss);
            return ss;
        }

        /// <summary>Gets a connexion for transmitting strings.  It is
        /// the caller's responsibility to ensure <see cref="connexion"/> is still active.
        /// Note: This call rebinds any existing string stream on this channel to the 
        /// provided connexion!</summary>
        /// <param name="connexion">The connexion to use for the connexion.</param>
        /// <param name="channel">The channel to claim.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrived StringStream</returns>
        public IStringStream GetStringStream(IConnexion connexion, byte channel, ChannelDeliveryRequirements cdr)
        {
            StringStream ss;
            if (stringStreams.ContainsKey(channel))
            {
                ss = stringStreams[channel];
                if (ss.Connexion == connexion)
                {
                    return ss;
                }

                ss.Connexion = connexion;
                return ss;
            }

            ss = new StringStream(connexion as ConnexionToServer, channel, cdr);
            stringStreams.Add(channel, ss);
            return ss;
        }

        /// <summary>Gets an already created StringStream</summary>
        /// <param name="channel">The channel of the stream.</param>
        /// <returns>The found StringStream</returns>
        public IStringStream GetStringStream(byte channel)
        {
            return stringStreams[channel];
        }

        /// <summary>Gets a connexion for transmitting objects.
        /// Note: This call rebinds any existing string stream on this channel to the 
        /// provided connexion!</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="channel">The channel for this stream.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrived ObjectStream</returns>
        public IObjectStream GetObjectStream(string address, string port, byte channel, ChannelDeliveryRequirements cdr)
        {
            ObjectStream os;
            if (objectStreams.ContainsKey(channel))
            {
                os = objectStreams[channel];
                if (os.Address.Equals(address) && os.Port.Equals(port) && os.Connexion.Active)
                {
                    return os;
                }
                os.Connexion = GetConnexion(address, port);
                os.ChannelDeliveryOptions = cdr;
                return os;
            }
            os = new ObjectStream(GetConnexion(address, port), channel, cdr);
            objectStreams.Add(channel, os);
            return os;
        }

        /// <summary>Gets a connexion for transmitting objects.  It is
        /// the caller's responsibility to ensure <see cref="connexion"/> is still active.</summary>
        /// <param name="connexion">The connexion to use for the stream.</param>
        /// <param name="channel">The channel to claim for this stream.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrived ObjectStream</returns>
        public IObjectStream GetObjectStream(IConnexion connexion, byte channel, ChannelDeliveryRequirements cdr)
        {
            ObjectStream os;
            if (objectStreams.ContainsKey(channel))
            {
                os = objectStreams[channel];
                if (os.Connexion == connexion)
                {
                    return os;
                }
                os.Connexion = connexion;
                return os;
            }
            os = new ObjectStream(connexion as ConnexionToServer, channel, cdr);
            objectStreams.Add(channel, os);
            return os;
        }

        /// <summary>Get an already created ObjectStream</summary>
        /// <param name="channel">The channel of the stream.</param>
        /// <returns>The found ObjectStream.</returns>
        public IObjectStream GetObjectStream(byte channel)
        {
            return objectStreams[channel];
        }

        /// <summary>Gets a connexion for transmitting byte arrays.</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="channel">The channel for this stream.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrived BinaryStream.</returns>
        public IBinaryStream GetBinaryStream(string address, string port, byte channel, ChannelDeliveryRequirements cdr)
        {
            BinaryStream bs;
            if (binaryStreams.ContainsKey(channel))
            {
                bs = binaryStreams[channel];
                if (bs.Address.Equals(address) && bs.Port.Equals(port) && bs.Connexion.Active)
                {
                    return bs;
                }
                bs.Connexion = GetConnexion(address, port);
                return bs;
            }
            bs = new BinaryStream(GetConnexion(address, port), channel, cdr);
            binaryStreams.Add(channel, bs);
            return bs;
        }

        /// <summary>Gets a connexion for transmitting byte arrays.  It is
        /// the caller's responsibility to ensure <see cref="connexion"/> is still active.</summary>
        /// <param name="connexion">The connexion to use for the connexion.</param>
        /// <param name="channel">The channel for this stream.</param>
        /// <param name="cdr">The delivery requirements for this channel</param>
        /// <returns>The created or retrived BinaryStream.</returns>
        public IBinaryStream GetBinaryStream(IConnexion connexion, byte channel, ChannelDeliveryRequirements cdr)
        {
            BinaryStream bs;
            if (binaryStreams.ContainsKey(channel))
            {
                bs = binaryStreams[channel];
                if (bs.Connexion == connexion)
                {
                    return bs;
                }
                bs.Connexion = connexion;
                return bs;
            }
            bs = new BinaryStream(connexion as ConnexionToServer, channel, cdr);
            binaryStreams.Add(channel, bs);
            return bs;
        }

        /// <summary>Get an already created BinaryStream</summary>
        /// <param name="channel">The channel of the stream.</param>
        /// <returns>A BinaryStream</returns>
        public IBinaryStream GetBinaryStream(byte channel)
        {
                return binaryStreams[channel];
        }


        #endregion

        /// <summary>Gets a server connexion; if no such connexion exists establish one.</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <returns>The created or retrieved connexion itself.</returns>
        /// <exception cref="CannotConnectException">thrown if the
        ///     remote could not be contacted.</exception>
        virtual protected ConnexionToServer GetConnexion(string address, string port)
        {
            foreach (ConnexionToServer s in connexions)
            {
                if (s.Address.Equals(address) && s.Port.Equals(port) && s.Active)
                {
                    return s;
                }
            }
            ConnexionToServer mySC = configuration.CreateServerConnexion(this, address, port);
            mySC.ErrorEvents += NotifyError;
            mySC.Start();
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
            log.Trace("Client.Update(): Starting");
            lock (this)
            {
                if (!started)
                {
                    Start();
                } // deprecated behaviour
                timer.Update();
                if (timer.TimeInMilliseconds - lastPingTime
                    > configuration.PingInterval.TotalMilliseconds)
                {
                    lastPingTime = timer.TimeInMilliseconds;
                    foreach (ConnexionToServer s in connexions)
                    {
                        s.Ping();
                    }
                }
                foreach (ConnexionToServer s in connexions)
                {
                    try
                    {
                        s.Update();
                        Message m;
                        while ((m = s.DequeueMessage()) != null)
                        {
                            try
                            {
                                switch (m.MessageType)
                                {
                                case MessageType.System:
                                    HandleSystemMessage(m);
                                    break;
                                case MessageType.Binary:
                                    binaryStreams[m.Channel].QueueMessage(m);
                                    break;
                                case MessageType.Object:
                                    objectStreams[m.Channel].QueueMessage(m);
                                    break;
                                case MessageType.Session:
                                    sessionStreams[m.Channel].QueueMessage(m);
                                    break;
                                case MessageType.String:
                                    stringStreams[m.Channel].QueueMessage(m);
                                    break;
                                case MessageType.Tuple1D:
                                    oneTupleStreams[m.Channel].QueueMessage(m);
                                    break;
                                case MessageType.Tuple2D:
                                    twoTupleStreams[m.Channel].QueueMessage(m);
                                    break;
                                case MessageType.Tuple3D:
                                    threeTupleStreams[m.Channel].QueueMessage(m);
                                    break;
                                default:
                                    /// FIXME: THIS IS NOT AN ERROR!  But how do we support
                                    /// new message types?
                                    if (!previouslyWarnedMessageTypes.ContainsKey(m.MessageType))
                                    {
                                        log.Warn(String.Format("received message of unknown type: {0}", m));
                                        previouslyWarnedMessageTypes[m.MessageType] = m.MessageType;
                                    }
                                    break;
                                }
                            }
                            catch (KeyNotFoundException)
                            {
                                // THIS IS NOT AN ERROR!  It's just that nobody's listening here.
                                if (!previouslyWarnedChannels.ContainsKey(m.Channel))
                                {
                                    log.Warn(String.Format("received message for unmonitored channel: {0}", m));
                                    previouslyWarnedChannels[m.Channel] = m.Channel;
                                }
                            }
                        }
                    }
                    catch (ConnexionClosedException) { s.Dispose(); }
                    catch (GTException e)
                    {
                        string message = String.Format("GT Exception occurred in Client.Update() while processing stream {0}", s);
                        log.Info(message, e);
                        NotifyError(new ErrorSummary(e.Severity,
                            SummaryErrorCode.RemoteUnavailable,
                            message, e));
                    }
                }

                //let each stream update itself
                foreach (SessionStream s in sessionStreams.Values) s.Update(timer);
                foreach (StringStream s in stringStreams.Values) s.Update(timer);
                foreach (ObjectStream s in objectStreams.Values) s.Update(timer);
                foreach (BinaryStream s in binaryStreams.Values) s.Update(timer);
                foreach (AbstractStreamedTuple s in oneTupleStreams.Values) s.Update(timer);
                foreach (AbstractStreamedTuple s in twoTupleStreams.Values) s.Update(timer);
                foreach (AbstractStreamedTuple s in threeTupleStreams.Values) s.Update(timer);

                // Remove dead connexions
                RemoveDeadConnexions();
            }
            log.Trace("Client.Update(): Finished");
            NotifyTick();
        }

        /// <summary>This is a placeholder for more possible system message handling.</summary>
        /// <param name="m">The message we're handling.</param>
        private void HandleSystemMessage(Message m)
        {
            //this should definitely not happen!  No good code leads to this point.  This should be only a placeholder.
            log.Warn(String.Format("Unknown System Message: {0}", m));
        }


        internal ChannelDeliveryRequirements GetChannelDeliveryRequirements(Message m)
        {
            switch (m.MessageType)
            {
            case MessageType.Binary: return binaryStreams[m.Channel].ChannelDeliveryOptions;
            case MessageType.Object: return objectStreams[m.Channel].ChannelDeliveryOptions;
            case MessageType.Session: return sessionStreams[m.Channel].ChannelDeliveryOptions;
            case MessageType.String: return stringStreams[m.Channel].ChannelDeliveryOptions;
            case MessageType.Tuple1D: return oneTupleStreams[m.Channel].ChannelDeliveryOptions;
            case MessageType.Tuple2D: return twoTupleStreams[m.Channel].ChannelDeliveryOptions;
            case MessageType.Tuple3D: return threeTupleStreams[m.Channel].ChannelDeliveryOptions;

            case MessageType.System:
            default:
                throw new InvalidDataException();
            }
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder(GetType().Name);
            b.Append("(ids:");
            foreach(ConnexionToServer c in connexions) {
                b.Append(' ');
                b.Append(c.Identity);
            }
            b.Append(")");
            return b.ToString();
        }
    }

    /// <summary>Selected messages for a particular channel only.</summary>
    internal class SameChannelProcessor : IProcessingQueue<PendingMessage>
    {
        // invariant: queue == null || queue.Count > 0
        protected byte channel;
        protected IDictionary<byte, Queue<PendingMessage>> pendingMessages;
        protected Queue<PendingMessage> queue;

        internal SameChannelProcessor(byte channel, IDictionary<byte, Queue<PendingMessage>> pm)
        {
            this.channel = channel;
            pendingMessages = pm;

            if (!pendingMessages.TryGetValue(this.channel, out queue)) { queue = null; }
            else if (queue.Count == 0)
            {
                queue = null;
                pendingMessages.Remove(this.channel);
            }
        }

        public PendingMessage Current
        {
            get {
                if (queue == null) { return null; }
                return queue.Peek();
            }
        }

        public void Remove()
        {
            if (queue == null) { return; }
            queue.Dequeue();
            if (queue.Count == 0)
            {
                queue = null;
                pendingMessages.Remove(channel);
            }
        }

        public bool Empty
        {
            get {
                if (queue == null) { return true; }
                return queue.Count > 0;
            }
        }

    }

}
