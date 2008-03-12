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
using GT.Net;
using GT.Utils;

namespace GT.Net
{

    #region Delegates

    /// <summary>Handles a Message event, when a message arrives.</summary>
    /// <param name="stream">The stream that has the new message.</param>
    public delegate void SessionNewMessage(ISessionStream stream);
    /// <summary>Handles a Message event, when a message arrives.</summary>
    /// /// <param name="stream">The stream that has the new message.</param>
    public delegate void StringNewMessage(IStringStream stream);
    /// <summary>Handles a Message event, when a message arrives.</summary>
    /// /// <param name="stream">The stream that has the new message.</param>
    public delegate void ObjectNewMessage(IObjectStream stream);
    /// <summary>Handles a Message event, when a message arrives.</summary>
    /// /// <param name="stream">The stream that has the new message.</param>
    public delegate void BinaryNewMessage(IBinaryStream stream);

    /// <summary>Handles a Error event, when an error occurs on the network.</summary>
    /// <param name="e">The exception.  May be null.</param>
    /// <param name="se">The networking error type.</param>
    /// <param name="ss">The server stream in which it occurred.  May be null if exception not in ServerConnexion.</param>
    /// <param name="explanation">A string describing the problem.</param>
    /// <remarks>deprecated: use ErrorEvents instead</remarks>
    public delegate void ErrorEventHandler(Exception e, SocketError se, ServerConnexion ss, string explanation);

    /// <summary>Occurs whenever this client is updated.</summary>
    public delegate void UpdateEventDelegate(HPTimer hpTimer);

    #endregion

    #region Type Stream Interfaces

    public interface IStream
    {
        /// <summary>Average latency between the client and this particluar server.</summary>
        float Delay { get; }

        /// <summary> Get the unique identity of the client for this server.  This will be
        /// different for each server, and thus could be different for each connexion. </summary>
        int UniqueIdentity { get; }

        /// <summary>Occurs whenever this client is updated.</summary>
        event UpdateEventDelegate UpdateEvent;

    }
    public interface IGenericStream<T,M> : IStream
    {
        /// <summary>Send an item to the server</summary>
        /// <param name="item">The item</param>
        void Send(T item);

        /// <summary>Send an item to the server</summary>
        /// <param name="item">The item</param>
        /// <param name="reli">How to send it</param>
        void Send(T item, MessageProtocol reli);

        /// <summary>Send an item to the server</summary>
        /// <param name="item">The item</param>
        /// <param name="reli">How to send it</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages</param>
        void Send(T item, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering);

        /// <summary>Take an item off the queue of received messages.</summary>
        /// <param name="index">The message to take off, with a higher number indicating a newer message.</param>
        /// <returns>The message.</returns>
        M DequeueMessage(int index);

        /// <summary>Received messages from the server.</summary>
        List<Message> Messages { get; }

        /// <summary>Flush all aggregated messages on this connexion</summary>
        /// <param name="protocol"></param>
        void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol);

        // FIXME: How can the new message event be brought in?
        ///// <summary> Occurs when this connexion receives a message. </summary>
        //// event SessionNewMessage SessionNewMessageEvent;
    }

    /// <summary>A connexion of session events.</summary>
    public interface ISessionStream : IGenericStream<SessionAction,SessionMessage>
    {
        /// <summary> Occurs when this connexion receives a message. </summary>
        event SessionNewMessage SessionNewMessageEvent;
    }

    /// <summary>A connexion of strings.</summary>
    public interface IStringStream : IGenericStream<string,string>
    {
        /// <summary>Take a String off the queue of received messages.</summary>
        /// <param name="index">Which message to take, with higher numbers being newer messages.</param>
        /// <returns>The message.</returns>
        string DequeueMessage(int index);

        /// <summary> Occurs when this connexion receives a message. </summary>
        event StringNewMessage StringNewMessageEvent;
    }

    /// <summary>A connexion of objects.</summary>
    public interface IObjectStream : IGenericStream<object,object>
    {
        /// <summary>Dequeues an object from the message list.</summary>
        /// <param name="index">Which to dequeue, where a higher number means a newer message.</param>
        /// <returns>The object that was there.</returns>
        object DequeueMessage(int index);

        /// <summary> Occurs when this connexion receives a message. </summary>
        event ObjectNewMessage ObjectNewMessageEvent;
    }

    /// <summary>A connexion of byte arrays.</summary>
    public interface IBinaryStream : IGenericStream<byte[],byte[]>
    {
        /// <summary>Takes a message from the message list</summary>
        /// <param name="index">The message to take, where a higher number means a newer message</param>
        /// <returns>The byte array of the message</returns>
        byte[] DequeueMessage(int index);

        /// <summary> Occurs when this connexion receives a message. </summary>
        event BinaryNewMessage BinaryNewMessageEvent;
    }

    #endregion


    #region Type Stream Implementations

    public abstract class AbstractBaseStream : IStream
    {
        protected byte id;
        protected internal ServerConnexion connexion;

        /// <summary> Occurs when client is updated. </summary>
        public event UpdateEventDelegate UpdateEvent;

        /// <summary> This SessionStream's ID. </summary>
        public byte ID { get { return id; } }

        /// <summary>Average latency between the client and this particluar server.</summary>
        public float Delay { get { return connexion.Delay; } }

        /// <summary> Get the unique identity of the client for this server.  This will be
        /// different for each server, and thus could be different for each connexion. </summary>
        public int UniqueIdentity { get { return connexion.UniqueIdentity; } }

        /// <summary> Get the connexion's destination address </summary>
        public string Address { get { return connexion.Address; } }

        /// <summary>Get the connexion's destination port</summary>
        public string Port
        {
            get { return connexion.Port; }
        }

        internal AbstractBaseStream(ServerConnexion stream, byte id)
        {
            this.connexion = stream;
            this.id = id;
        }

        internal virtual void Update(HPTimer hpTimer)
        {
            if (UpdateEvent != null)
                UpdateEvent(hpTimer);
        }
    }

    public abstract class AbstractStream<T,M> : AbstractBaseStream, IGenericStream<T,M>
    {
        protected List<Message> messages;

        /// <summary>Received messages from the server.</summary>
        public List<Message> Messages { get { return messages; } }

        /// <summary> This SessionStream uses this ServerConnexion. </summary>
        /// <remarks>deprecated</remarks>
        public ServerConnexion Connection { get { return connexion; } }

        /// <summary>Create a stream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">The message channel.</param>
        internal AbstractStream(ServerConnexion stream, byte id) : base(stream, id)
        {
            messages = new List<Message>();
        }

        public void Send(T item)
        {
            Send(item, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
        }

        public void Send(T item, MessageProtocol reli)
        {
            Send(item, reli, MessageAggregation.No, MessageOrder.None);
        }

        public abstract void Send(T action, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering);

        public abstract M DequeueMessage(int index);

        /// <summary>Flush all aggregated messages on this connexion</summary>
        /// <param name="protocol"></param>
        public virtual void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol)
        {
            connexion.FlushOutgoingMessages(this.id, protocol);
        }
    }

    /// <summary>A connexion of session events.</summary>
    public class SessionStream : AbstractStream<SessionAction,SessionMessage>, ISessionStream
    {
        /// <summary> Occurs when this session receives a message. </summary>
        public event SessionNewMessage SessionNewMessageEvent;

        /// <summary>Create a SessionStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">The message channel id.</param>
        internal SessionStream(ServerConnexion stream, byte id) : base(stream, id)
        {
        }

        /// <summary>Send a session action to the server.</summary>
        /// <param name="action">The action.</param>
        /// <param name="reli">How to send it.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        override public void Send(SessionAction action, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            connexion.Send(new SessionMessage(id, UniqueIdentity, action), reli, aggr, ordering);
        }

        /// <summary>Take a SessionMessage off the queue of received messages.</summary>
        /// <param name="index">Which one to take off, the higher number being newer messages.</param>
        /// <returns>The message.</returns>
        override public SessionMessage DequeueMessage(int index)
        {
            try
            {
                SessionMessage m;
                if (index >= messages.Count)
                    return null;

                lock (messages)
                {
                    m = ((SessionMessage)messages[index]);
                    messages.RemoveAt(index);
                }

                return m;
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

        /// <summary>Queue a message in the list, triggering events</summary>
        /// <param name="message">The message to be queued.</param>
        internal void QueueMessage(Message message)
        {
            messages.Add(message);
            if(SessionNewMessageEvent != null)
                SessionNewMessageEvent(this);
        }

    }

    /// <summary>A connexion of strings.</summary>
    public class StringStream : AbstractStream<string,string>, IStringStream
    {
        /// <summary> Occurs when this connexion receives a message. </summary>
        public event StringNewMessage StringNewMessageEvent;

        /// <summary>Create a StringStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">The message channel id.</param>
        internal StringStream(ServerConnexion stream, byte id) : base(stream, id)
        {
        }

        
        /// <summary>Send a string to the server, specifying how.</summary>
        /// <param name="b">The string to send.</param>
        /// <param name="reli">What method to send it by.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        override public void Send(string b, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            connexion.Send(new StringMessage(id, b), reli, aggr, ordering);
        }

        /// <summary>Take a String off the queue of received messages.</summary>
        /// <param name="index">Which message to take, with higher numbers being newer messages.</param>
        /// <returns>The message.</returns>
        override public string DequeueMessage(int index)
        {
            try
            {
                StringMessage m;
                if (index >= messages.Count) { return null; }

                lock (messages)
                {
                    m = (StringMessage)messages[index];
                    messages.RemoveAt(index);
                }
                return m.Text;
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

        /// <summary>Queue a message in the list, triggering events</summary>
        /// <param name="message">The message to be queued.</param>
        internal void QueueMessage(Message message)
        {
            messages.Add(message);
            if (StringNewMessageEvent != null)
                StringNewMessageEvent(this);
        }
    }

    /// <summary>A connexion of Objects.</summary>
    public class ObjectStream : AbstractStream<object,object>, IObjectStream
    {
        private static BinaryFormatter formatter = new BinaryFormatter();

        /// <summary> Occurs when this connexion receives a message. </summary>
        public event ObjectNewMessage ObjectNewMessageEvent;

        /// <summary>Create an ObjectStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the objects.</param>
        /// <param name="id">The message channel claimed.</param>
        internal ObjectStream(ServerConnexion stream, byte id) : base(stream,id)
        {
        }

        /// <summary>Send an object using the specified method.</summary>
        /// <param name="o">The object to send.</param>
        /// <param name="reli">How to send the object.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        override public void Send(object o, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            connexion.Send(new ObjectMessage(id, o), reli, aggr, ordering);
        }

        /// <summary>Dequeues an object from the message list.</summary>
        /// <param name="index">Which to dequeue, where a higher number means a newer message.</param>
        /// <returns>The object that was there.</returns>
        override public object DequeueMessage(int index)
        {
            try
            {
                ObjectMessage m;
                if (index >= messages.Count)
                    return null;

                lock (messages)
                {
                    m = (ObjectMessage)messages[index];
                    messages.RemoveAt(index);
                }
                return m.Object;
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

        /// <summary>Queue a message in the list, triggering events</summary>
        /// <param name="message">The message to be queued.</param>
        internal void QueueMessage(Message message)
        {
            messages.Add(message);
            if (ObjectNewMessageEvent != null)
                ObjectNewMessageEvent(this);
        }
    }

    /// <summary>A connexion of byte arrays.</summary>
    public class BinaryStream : AbstractStream<byte[],byte[]>, IBinaryStream
    {
        /// <summary> Occurs when this connexion receives a message. </summary>
        public event BinaryNewMessage BinaryNewMessageEvent;

        /// <summary>Creates a BinaryStream object.</summary>
        /// <param name="stream">The SuperStream object on which to actually send the objects.</param>
        /// <param name="id">The message channel to claim.</param>
        internal BinaryStream(ServerConnexion stream, byte id) : base(stream, id)
        {
        }

        override public void Send(byte[] b, MessageProtocol reli, MessageAggregation aggr, 
            MessageOrder ordering)
        {
            connexion.Send(new BinaryMessage(id, b), reli, aggr, ordering);
        }

        /// <summary>Takes a message from the message list.</summary>
        /// <param name="index">The message to take, where a higher number means a newer message.</param>
        /// <returns>The byte array of the message.</returns>
        override public byte[] DequeueMessage(int index)
        {
            try
            {
                BinaryMessage m;
                if (index >= messages.Count)
                    return null;

                lock (messages)
                {
                    m = (BinaryMessage)messages[index];
                    messages.RemoveAt(index);
                }
                return m.Bytes;
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

        /// <summary>Queue a message in the list, triggering events</summary>
        /// <param name="message">The message to be queued.</param>
        internal void QueueMessage(Message message)
        {
            messages.Add(message);
            if (BinaryNewMessageEvent != null)
                    BinaryNewMessageEvent(this);
        }
    }

    #endregion

    /// <summary>Controls the sending of messages to a particular server.</summary>
    public class ServerConnexion : IStartable
    {
        private Client owner;
        private bool started;
        private string address;
        private string port;

        internal double nextPingTime;

        /// <summary>The unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connexion.</summary>
        public int UniqueIdentity;

        /// <summary>Incoming messages from the server. As messages are read in from the
        /// different transports, they are added to this list.  The different types of 
        /// streams process this list to select messages corresponding to their particular 
        /// type.</summary>
        internal List<Message> messages;

        /// <summary>Occurs when there is an error.</summary>
        public event ErrorEventHandler ErrorEvent;

        private Dictionary<MessageProtocol, ITransport> transports = 
            new Dictionary<MessageProtocol,ITransport>();
        private Dictionary<MessageProtocol, List<Message>> messagePools;

        /// <summary>Create a new SuperStream to handle a connexion to a server.</summary>
        /// <param name="owner">The owning client.</param>
        /// <param name="address">Who to try to connect to.</param>
        /// <param name="port">Which port to connect to.</param>
        internal ServerConnexion(Client owner, string address, string port)
        {
            started = false;
            this.owner = owner;
            this.address = address;
            this.port = port;
        }

        /// <summary>
        /// Return the marshaller configured for this stream's client.
        /// </summary>
        public IMarshaller Marshaller
        {
            get { return owner.Marshaller; }
        }

        /// <summary>
        /// Return the globally unique identifier for this stream's client.
        /// </summary>
        public Guid Guid
        {
            get { return owner.Guid; }
        }

        /// <summary>
        /// Start this instance.
        /// </summary>
        public void Start()
        {
            if (Active) { return; }
            nextPingTime = 0;

            messagePools = new Dictionary<MessageProtocol, List<Message>>();
            messages = new List<Message>();

            transports = new Dictionary<MessageProtocol, ITransport>();

            // FIXME: should this be done on demand?
            foreach (IConnector conn in owner.Connectors)
            {
                // FIXME: and if there is an error...?
                ITransport t = conn.Connect(Address, Port, owner.Capabilities);
                if (t != null) { AddTransport(t); }
            }

            started = true;

            // FIXME: This is bogus and should be changed.
            // request our id right away
            Send(new SystemMessage(SystemMessageType.UniqueIDRequest), MessageProtocol.Tcp, 
                MessageAggregation.No, MessageOrder.None);
        }

        public void Stop()
        {
            started = false;
            if (transports != null)
            {
                foreach (ITransport t in transports.Values) { t.Dispose(); }
            }
            transports = null;
        }

        public void Dispose()
        {
            Stop();
        }

        public void AddTransport(ITransport t)
        {
            // transports[t.Name] = t;
            transports[t.MessageProtocol] = t;  // FIXME: this is to be replaced!
            t.PacketReceivedEvent += new PacketReceivedHandler(HandleNewPacket);
            t.TransportErrorEvent += new TransportErrorHandler(HandleTransportError);
            // t.ErrorEvent += new ErrorEventHandler();
        }

        public virtual string Address
        {
            get { return address; }
        }

        public virtual string Port
        {
            get { return port; }
        }

        public virtual bool Active
        {
            get { return started; }
        }

        /// <summary>Average latency between the client and this particluar server.</summary>
        public float Delay {
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

        protected void HandleNewPacket(byte[] buffer, int offset, int count, ITransport transport)
        {
            Message msg = owner.Marshaller.Unmarshal(new MemoryStream(buffer, offset, count, false), transport);
            if (msg.MessageType == MessageType.System)
            {
                HandleSystemMessage((SystemMessage)msg, transport);
            }
            else
            {
                messages.Add(msg);
            }
        }

        protected void HandleTransportError(string explanation, ITransport transport, object context)
        {
            // FIXME: find the associated connector and re-connect
            if (transport != null)
            {
                transport.Dispose();
                transports.Remove(transport.MessageProtocol);
            }
        }
        /// <summary>Occurs when there is an error.</summary>
        protected internal void NotifyError(object generator, Exception e, SocketError se, string explanation)
        {
            // FIXME: This should be logging
            Console.WriteLine("Error[" + generator + "]: " + explanation + " (" + e + ", " + se + ")");
            if (ErrorEvent != null)
            {
                ErrorEvent(e, se, this, explanation);
            }
        }



        /// <summary>Adds the message to a list, waiting to be sent out.</summary>
        /// <param name="msg">The message to be aggregated</param>
        /// <param name="ordering">The ordering of how it will be sent</param>
        /// <param name="protocol">How it should be sent out</param>
        private void Aggregate(Message msg, 
            MessageProtocol protocol, MessageOrder ordering)
        {
            List<Message> mp;
            messagePools.TryGetValue(protocol, out mp);
            if(mp == null) {
                mp = messagePools[protocol] = new List<Message>();
            }
            // FIXME: presumably there is some maximum size or waiting time
            // before aggregated messages are flushed?
            // FIXME: what about QoS.Freshness -- where only the latest should be sent?
            mp.Add(msg);
        }

        /// <summary>Send a message using these parameters.</summary>
        /// <param name="msg">The message to send.</param>
        /// <param name="protocol">How to send it.</param>
        /// <param name="aggr">Should this message be aggregated?</param>
        /// <param name="ordering">Should message sent before this be sent?</param>
        public void Send(Message msg, MessageProtocol protocol, 
            MessageAggregation aggr, MessageOrder ordering)
        {
            lock (this)
            {
                if (!Active) { 
                    throw new InvalidStateException("Cannot send on a stopped client", this); 
                }

                if (aggr == MessageAggregation.Yes)
                {
                    //Wait to send this Bytes, hopefully to pack it with later Bytes.
                    Aggregate(msg, protocol, ordering);
                    return;
                }

                if (ordering == MessageOrder.All)
                {
                    //Make sure ALL messages on the CLIENT are sent,
                    //then send this one.
                    Aggregate(msg, protocol, ordering);
                    FlushOutgoingMessages(protocol);
                    return;
                }
                else if (ordering == MessageOrder.AllChannel)
                {
                    //Make sure ALL other messages on this CHANNEL are sent,
                    //then send this one.
                    Aggregate(msg, protocol, ordering);
                    FlushOutgoingMessages(msg.Id, protocol);
                    return;
                }

                //Actually send the Bytes.  Note that after this point, ordering and 
                //aggregation are locked in / already decided.
                //If you're here, then you're not aggregating and you're not concerned 
                //about ordering.
                SendMessage(transports[protocol], msg);
            }
        }

        protected void SendMessage(ITransport transport, Message msg)
        {
            //pack main message into a buffer and send it right away
            Stream packet = transport.GetPacketStream();
            owner.Marshaller.Marshal(msg, packet, transport);

            // and be sure to catch exceptions; log and remove transport if unable to be started
            // if(!transport.Active) { transport.Start(); }
            transport.SendPacket(packet);
        }

        /// <summary>Flushes outgoing messages on this channel only</summary>
        internal void FlushOutgoingMessages(byte id, MessageProtocol protocol)
        {
            ITransport t = transports[protocol];  // will throw exception if not found
            Debug.Assert(t.MaximumPacketSize > 0);

            List<Message> list;
            messagePools.TryGetValue(protocol, out list);
            if (list == null || list.Count == 0) { return; }
            FlushOutgoingMessages(t, 
                new PredicateListProcessor<Message>(list, 
                    new Predicate<Message>(new SpecificChannelMessageProcessor(id).Matches)));
        }

        /// <summary>Flushes outgoing messages</summary>
        private void FlushOutgoingMessages(MessageProtocol protocol)
        {
            List<Message> list;
            byte[] buffer;

            ITransport t = transports[protocol];
            Debug.Assert(t.MaximumPacketSize > 0);

            messagePools.TryGetValue(protocol, out list);
            if (list == null || list.Count == 0) { return; }
            FlushOutgoingMessages(t, new SequentialListProcessor<Message>(list));
        }

        private void FlushOutgoingMessages(ITransport transport, IProcessingQueue<Message> elements)
        {
            while (!elements.Empty)
            {
                Message message;
                Stream packet = transport.GetPacketStream();
                //if there are no more messages in the list
                while((message = elements.Current) != null) 
                {
                    long previousLength = packet.Length;
                    try
                    {
                        owner.Marshaller.Marshal(message, packet, transport);
                    }
                    catch (ArgumentException) { }

                    //if packing a packet, and the packet is full
                    if (packet.Length >= transport.MaximumPacketSize)
                    {
                        // we're too big: go back to previous length, send this, and cause
                        // the last message to be remarshalled
                        packet.SetLength(previousLength);
                        break;
                    }
                    // successfully marshalled: remove the message and find the next
                    elements.Remove();
                }
                if(packet.Length == 0) { break; }

                //Actually send the Bytes.  Note that after this point, ordering and 
                //aggregation are locked in / already decided.
                transport.SendPacket(packet);
            }
        }
    
        /// <summary>A single tick of the SuperStream.</summary>
        internal void Update()
        {
            lock (this)
            {
                if (!Active) { return; }
                foreach (ITransport t in transports.Values)
                {
                    t.Update();
                }
            }
        }

        /// <summary>Deal with a system message in whatever way we need to.</summary>
        /// <param name="message">The incoming message.</param>
        /// <param name="transport">The transport from which the message
	///  came.</param>
        internal void HandleSystemMessage(SystemMessage message, ITransport transport)
        {
            switch ((SystemMessageType)message.Id)
            {
            case SystemMessageType.UniqueIDRequest:
                UniqueIdentity = BitConverter.ToInt32(message.data, 0);
                break;

            case SystemMessageType.PingRequest:
                SendMessage(transport, new SystemMessage(SystemMessageType.PingResponse, message.data));
                break;

            case SystemMessageType.PingResponse:
                //record the difference; half of it is the latency between this client and the server
                int newDelay = (System.Environment.TickCount - BitConverter.ToInt32(message.data, 0)) / 2;
                // NB: transport.Delay set may (and probably will) scale this value
                transport.Delay = newDelay;
                break;

            default:
                Debug.WriteLine("connexion.HandleSystemMessage(): Unknown message type: " +
                    (SystemMessageType)message.Id);
                break;
            }
        }

        
        /// <summary>Ping the server to determine delay, as well as act as a keep-alive.</summary>
        internal void Ping()
        {
            foreach (ITransport t in transports.Values)
            {
                SendMessage(t, new SystemMessage(SystemMessageType.PingRequest,
                    BitConverter.GetBytes(System.Environment.TickCount)));
            }
        }
    }

    internal class SpecificChannelMessageProcessor {
        protected byte soughtChannel;

        internal SpecificChannelMessageProcessor(byte channel)
        {
            this.soughtChannel = channel;
        }

        internal bool Matches(Message element)
        {
            return element.Id == soughtChannel;
        }
    }

    public abstract class ClientConfiguration
    {
        abstract public IMarshaller CreateMarshaller();
        abstract public ICollection<IConnector> CreateConnectors();

        /// <summary>
        /// The time between pings to servers.
        /// </summary>
        abstract public TimeSpan PingInterval { get; set; }

        /// <summary>
        /// The time between client ticks.
        /// </summary>
        abstract public TimeSpan TickInterval { get; set; }

        abstract public Client BuildClient();

        virtual public ServerConnexion CreateServerConnexion(Client owner,
            string address, string port)
        {
            return new ServerConnexion(owner, address, port);
        }
    }

    public class DefaultClientConfiguration : ClientConfiguration
    {
        protected TimeSpan pingInterval = TimeSpan.FromMilliseconds(10000);
        protected TimeSpan tickInterval = TimeSpan.FromMilliseconds(10);
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
            return connectors;
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

        public override Client BuildClient()
        {
            return new Client(this);
        }
    }

    /// <summary>Represents a client that can connect to multiple servers.</summary>
    public class Client : IStartable
    {
        private ClientConfiguration configuration;

        private Guid guid = Guid.NewGuid();
        private Dictionary<byte, ObjectStream> objectStreams;
        private Dictionary<byte, BinaryStream> binaryStreams;
        private Dictionary<byte, StringStream> stringStreams;
        private Dictionary<byte, SessionStream> sessionStreams;
        private Dictionary<byte, AbstractStreamedTuple> oneTupleStreams;
        private Dictionary<byte, AbstractStreamedTuple> twoTupleStreams;
        private Dictionary<byte, AbstractStreamedTuple> threeTupleStreams;

        private IList<ServerConnexion> connexions;
        private ICollection<IConnector> connectors;
        private IMarshaller marshaller;
        private HPTimer timer;
        private bool started = false;

        /// <summary>Occurs when there are errors on the network.</summary>
        public event ErrorEventHandler ErrorEvent;

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
        public IMarshaller Marshaller
        {
            get { return marshaller; }
        }

        public Dictionary<string, string> Capabilities
        {
            get
            {
                Dictionary<string, string> caps = new Dictionary<string, string>();
                caps[GTCapabilities.CLIENT_ID] = 
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

        public ICollection<IConnector> Connectors
        {
            get { return connectors;  }
        }

        /// <summary>
        /// Return globally unique identifier for this client.
        /// </summary>
        public Guid Guid {
            get { return guid; }
        }

        public void Start()
        {
            lock (this)
            {
                if(Active) { return; }
                marshaller = configuration.CreateMarshaller();
                connexions = new List<ServerConnexion>();
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

        public void Stop()
        {
            lock (this)
            {
                if (!Active) { return; }
                started = false;
                foreach (IConnector conn in connectors)
                {
                    conn.Stop();
                }
                foreach(ServerConnexion s in connexions)
                {
                    s.Stop();
                }
                // timer.Stop();
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                Stop();
                if (connexions != null)
                {
                    foreach(ServerConnexion sc in connexions)
                    {
                        sc.Dispose();
                    }
                }
                connexions = null;
                timer = null;
            }
        }

        public bool Active
        {
            get { return started; }
        }

        /// <summary>
        /// Sleep for the tick-time from the configuration
        /// </summary>
        public void Sleep()
        {
            Sleep(configuration.TickInterval.Milliseconds);
        }

        /// <summary>
        /// Sleep for the specified amount of time, overruling the tick-time from the
        /// configuration
        /// </summary>
        /// <param name="milliseconds"></param>
        public void Sleep(int milliseconds)
        {
            // FIXME: this should do something smarter
            // Socket.Select(listenList, null, null, 1000);
            Thread.Sleep(milliseconds);
        }

        #region Streams

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="B">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="C">The Type of the third value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="id">The channel id to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public StreamedTuple<A, B, C> GetStreamedTuple<A, B, C>(string address, string port, byte id, int milliseconds)
            where A : IConvertible
            where B : IConvertible
            where C : IConvertible
        {
            StreamedTuple<A, B, C> tuple;
            if (threeTupleStreams.ContainsKey(id))
            {
                if (threeTupleStreams[id].Address.Equals(address) && threeTupleStreams[id].Port.Equals(port))
                {
                    return (StreamedTuple<A, B, C>)threeTupleStreams[id];
                }

                tuple = (StreamedTuple<A, B, C>)threeTupleStreams[id]; 
                tuple.connexion = GetConnexion(address, port);
                return (StreamedTuple<A, B, C>)threeTupleStreams[id];
            }

            StreamedTuple<A, B, C> bs = (StreamedTuple<A, B, C>)new StreamedTuple<A, B, C>(GetConnexion(address, port), id, milliseconds);
            threeTupleStreams.Add(id, (AbstractStreamedTuple)bs);
            return bs;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="B">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="C">The Type of the third value of the tuple</typeparam>
        /// <param name="connexion">The stream to use to send the tuple</param>
        /// <param name="id">The channel id to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public StreamedTuple<A, B, C> GetStreamedTuple<A, B, C>(ServerConnexion connexion, byte id, int milliseconds)
            where A : IConvertible
            where B : IConvertible
            where C : IConvertible
        {
            StreamedTuple<A, B, C> tuple;
            if (threeTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<A, B, C>) threeTupleStreams[id];
                if (tuple.connexion == connexion)
                {
                    return tuple;
                }

                tuple.connexion = connexion;
                return tuple;
            }

            tuple = new StreamedTuple<A, B, C>(connexion, id, milliseconds);
            threeTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="B">The Type of the second value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="id">The channel id to use for this two-tuple (unique to two-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public StreamedTuple<A, B> GetStreamedTuple<A, B>(string address, string port, byte id, int milliseconds)
            where A : IConvertible
            where B : IConvertible
        {
            StreamedTuple<A, B> tuple;
            if (twoTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<A, B>) twoTupleStreams[id];
                if (tuple.Address.Equals(address) && tuple.Port.Equals(port))
                {
                    return tuple;
                }

                tuple.connexion = GetConnexion(address, port);
                return tuple;
            }

            tuple = new StreamedTuple<A, B>(GetConnexion(address, port), id, milliseconds);
            twoTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="B">The Type of the second value of the tuple</typeparam>
        /// <param name="connexion">The stream to use to send the tuple</param>
        /// <param name="id">The channel id to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public StreamedTuple<A, B> GetStreamedTuple<A, B>(ServerConnexion connexion, byte id, int milliseconds)
            where A : IConvertible
            where B : IConvertible
        {
            StreamedTuple<A, B> tuple;
            if (twoTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<A, B>)twoTupleStreams[id];
                if (tuple.connexion == connexion)
                {
                    return tuple;
                }

                tuple.connexion = connexion;
                return tuple;
            }

            tuple = new StreamedTuple<A, B>(connexion, id, milliseconds);
            threeTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="id">The channel id to use for this one-tuple (unique to one-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public StreamedTuple<A> GetStreamedTuple<A>(string address, string port, byte id, int milliseconds)
            where A : IConvertible
        {
            StreamedTuple<A> tuple;
            if (oneTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<A>)oneTupleStreams[id];
                if (tuple.Address.Equals(address) && tuple.Port.Equals(port))
                {
                    return tuple;
                }

                tuple.connexion = GetConnexion(address, port);
                return tuple;
            }

            tuple = new StreamedTuple<A>(GetConnexion(address, port), id, milliseconds);
            oneTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }


        /// <summary>Gets a connexion for managing the session to this server.</summary>
        /// <param name="address">The address to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="port">The port to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="id">The channel id to claim or retrieve.</param>
        /// <returns>The created or retrived SessionStream</returns>
        public ISessionStream GetSessionStream(string address, string port, byte id)
        {
            if (sessionStreams.ContainsKey(id))
            {
                if (sessionStreams[id].Address.Equals(address) && sessionStreams[id].Port.Equals(port))
                {
                    return sessionStreams[id];
                }

                sessionStreams[id].connexion = GetConnexion(address, port);
                return sessionStreams[id];
            }

            SessionStream bs = new SessionStream(GetConnexion(address, port), id);
            sessionStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connexion for managing the session to this server.</summary>
        /// <param name="connexion">The connexion to use for the connexion.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim or retrieve.</param>
        /// <returns>The created or retrived SessionStream</returns>
        public ISessionStream GetSessionStream(ServerConnexion connexion, byte id)
        {
            if (sessionStreams.ContainsKey(id))
            {
                if (sessionStreams[id].connexion == connexion)
                {
                    return sessionStreams[id];
                }

                sessionStreams[id].connexion = connexion;
                return sessionStreams[id];
            }

            SessionStream bs = new SessionStream(connexion, id);
            sessionStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets an already created SessionStream</summary>
        /// <param name="id">The channel id of the SessionStream unique to SessionStreams.</param>
        /// <returns>The found SessionStream</returns>
        public ISessionStream GetSessionStream(byte id)
        {
            return sessionStreams[id];
        }

        /// <summary>Gets a connexion for transmitting strings.</summary>
        /// <param name="address">The address to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="port">The port to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="id">The channel id to claim.</param>
        /// <returns>The created or retrived StringStream</returns>
        public IStringStream GetStringStream(string address, string port, byte id)
        {
            if (stringStreams.ContainsKey(id))
            {
                if (stringStreams[id].Address.Equals(address) && stringStreams[id].Port.Equals(port))
                {
                    return stringStreams[id];
                }

                stringStreams[id].connexion = GetConnexion(address, port);
                return stringStreams[id];
            }

            StringStream bs = new StringStream(GetConnexion(address, port), id);
            stringStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connexion for transmitting strings.</summary>
        /// <param name="connexion">The connexion to use for the connexion.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim.</param>
        /// <returns>The created or retrived StringStream</returns>
        public IStringStream GetStringStream(ServerConnexion connexion, byte id)
        {
            if (stringStreams.ContainsKey(id))
            {
                if (stringStreams[id].connexion == connexion)
                {
                    return stringStreams[id];
                }

                stringStreams[id].connexion = connexion;
                return stringStreams[id];
            }

            StringStream bs = new StringStream(connexion, id);
            stringStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets an already created StringStream</summary>
        /// <param name="id">The channel id of the StringStream unique to StringStreams.</param>
        /// <returns>The found StringStream</returns>
        public IStringStream GetStringStream(byte id)
        {
            return stringStreams[id];
        }

        /// <summary>Gets a connexion for transmitting objects.</summary>
        /// <param name="address">The address to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="port">The port to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="id">The channel id to claim for this ObjectStream, unique for all ObjectStreams.</param>
        /// <returns>The created or retrived ObjectStream</returns>
        public IObjectStream GetObjectStream(string address, string port, byte id)
        {
            if (objectStreams.ContainsKey(id))
            {
                if (objectStreams[id].Address.Equals(address) && objectStreams[id].Port.Equals(port))
                {
                    return objectStreams[id];
                }
                objectStreams[id].connexion = GetConnexion(address, port);
                return objectStreams[id];
            }
            ObjectStream bs = new ObjectStream(GetConnexion(address, port), id);
            objectStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connexion for transmitting objects.</summary>
        /// <param name="connexion">The connexion to use for the connexion.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim for this ObjectStream, unique for all ObjectStreams.</param>
        /// <returns>The created or retrived ObjectStream</returns>
        public IObjectStream GetObjectStream(ServerConnexion connexion, byte id)
        {
            if (objectStreams.ContainsKey(id))
            {
                if (objectStreams[id].connexion == connexion)
                {
                    return objectStreams[id];
                }
                objectStreams[id].connexion = connexion;
                return objectStreams[id];
            }
            ObjectStream bs = new ObjectStream(connexion, id);
            objectStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Get an already created ObjectStream</summary>
        /// <param name="id">The channel id of the ObjectStream unique, to ObjectStreams.</param>
        /// <returns>The found ObjectStream.</returns>
        public IObjectStream GetObjectStream(byte id)
        {
            return objectStreams[id];
        }

        /// <summary>Gets a connexion for transmitting byte arrays.</summary>
        /// <param name="address">The address to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="port">The port to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="id">The channel id to claim for this BinaryStream, unique for all BinaryStreams.</param>
        /// <returns>The created or retrived BinaryStream.</returns>
        public IBinaryStream GetBinaryStream(string address, string port, byte id)
        {
            if (binaryStreams.ContainsKey(id))
            {
                BinaryStream s = binaryStreams[id];
                if (s.Address.Equals(address) && s.Port.Equals(port))
                {
                    return binaryStreams[id];
                }
                binaryStreams[id].connexion = GetConnexion(address, port);
                return binaryStreams[id];
            }
            BinaryStream bs = new BinaryStream(GetConnexion(address, port), id);
            binaryStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connexion for transmitting byte arrays.</summary>
        /// <param name="connexion">The connexion to use for the connexion.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim for this BinaryStream, unique for all BinaryStreams.</param>
        /// <returns>The created or retrived BinaryStream.</returns>
        public IBinaryStream GetBinaryStream(ServerConnexion connexion, byte id)
        {
            if (binaryStreams.ContainsKey(id))
            {
                if (binaryStreams[id].connexion == connexion)
                {
                    return binaryStreams[id];
                }
                binaryStreams[id].connexion = connexion;
                return binaryStreams[id];
            }
            BinaryStream bs = new BinaryStream(connexion, id);
            binaryStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Get an already created BinaryStream</summary>
        /// <param name="id">The channel id of the BinaryStream, unique to BinaryStreams.</param>
        /// <returns>A BinaryStream</returns>
        public IBinaryStream GetBinaryStream(byte id)
        {
                return binaryStreams[id];
        }


        #endregion

        /// <summary>Gets a server connexion; if no such connexion exists establish one.</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <returns>The created or retrieved connexion itself.</returns>
        protected ServerConnexion GetConnexion(string address, string port)
        {
            foreach (ServerConnexion s in connexions)
            {
                if (s.Address.Equals(address) && s.Port.Equals(port))
                {
                    return s;
                }
            }
            ServerConnexion mySC = configuration.CreateServerConnexion(this, address, port);
            mySC.ErrorEvent += new ErrorEventHandler(mySS_ErrorEvent);
            mySC.Start();
            connexions.Add(mySC);
            return mySC;
        }

        private void mySS_ErrorEvent(Exception e, SocketError se, ServerConnexion ss, string explanation)
        {
            if (ErrorEvent != null)
                ErrorEvent(e, se, ss, explanation);
        }

        /// <summary>One tick of the network beat.  Thread-safe.</summary>
        public void Update()
        {
            DebugUtils.WriteLine(this + ": Update() started");
            lock (this)
            {
                if (!started) { Start(); }
                timer.Update();
                foreach (ServerConnexion s in connexions)
                {
                    try
                    {
                        if (s.nextPingTime < timer.TimeInMilliseconds)
                        {
                            s.nextPingTime = timer.TimeInMilliseconds + configuration.PingInterval.Milliseconds;
                            s.Ping();
                        }

                        s.Update();
                        lock (s.messages)
                        {
                            foreach (Message m in s.messages)
                            {
                                try
                                {
                                    switch (m.MessageType)
                                    {
                                    case MessageType.Binary: binaryStreams[m.Id].QueueMessage(m); break;
                                    case MessageType.Object: objectStreams[m.Id].QueueMessage(m); break;
                                    case MessageType.Session: sessionStreams[m.Id].QueueMessage(m); break;
                                    case MessageType.String: stringStreams[m.Id].QueueMessage(m); break;
                                    case MessageType.System: HandleSystemMessage(m); break;
                                    case MessageType.Tuple1D: oneTupleStreams[m.Id].QueueMessage(m); break;
                                    case MessageType.Tuple2D: twoTupleStreams[m.Id].QueueMessage(m); break;
                                    case MessageType.Tuple3D: threeTupleStreams[m.Id].QueueMessage(m); break;
                                    default:
                                        Console.WriteLine("Client: WARNING: received message (id={0}) with unknown type: {1}",
                                            m.Id, m.MessageType);
                                        if (ErrorEvent != null)
                                            ErrorEvent(null, SocketError.Fault, s, "Received " + m.MessageType + "message for connection " + m.Id +
                                                ", but that type does not exist.");
                                        break;
                                    }
                                }
                                catch (KeyNotFoundException e)
                                {
                                    Console.WriteLine("Client: WARNING: received message with unmonitored id (type={0}): id={1}",
                                        m.MessageType, m.Id);
                                    if (ErrorEvent != null)
                                        ErrorEvent(e, SocketError.Fault, s, "Received " + m.MessageType + "message for connection " + m.Id +
                                            ", but that id does not exist for that type.");
                                }
                            }
                            s.messages.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Client: ERROR: Exception occurred in Client.Update() processing stream {0}: {1}", s, e);
                        if (ErrorEvent != null)
                            ErrorEvent(e, SocketError.Fault, s, "Exception occurred when trying to queue message.");
                    }
                }


                //let each stream update itself
                foreach (SessionStream s in sessionStreams.Values)
                    s.Update(timer);
                foreach (StringStream s in stringStreams.Values)
                    s.Update(timer);
                foreach (ObjectStream s in objectStreams.Values)
                    s.Update(timer);
                foreach (BinaryStream s in binaryStreams.Values)
                    s.Update(timer);
                foreach (AbstractStreamedTuple s in oneTupleStreams.Values)
                    s.Update(timer);
                foreach (AbstractStreamedTuple s in twoTupleStreams.Values)
                    s.Update(timer);
                foreach (AbstractStreamedTuple s in threeTupleStreams.Values)
                    s.Update(timer);
            }
            DebugUtils.WriteLine(this + ": Update() finished");
        }

        /// <summary>This is a placeholder for more possible system message handling.</summary>
        /// <param name="m">The message we're handling.</param>
        private void HandleSystemMessage(Message m)
        {
            //this should definitely not happen!  No good code leads to this point.  This should be only a placeholder.
            Console.WriteLine("Client handled System Message.");
        }

        /// <summary>Starts a new thread that listens for new clients or new Bytes.
        /// Abort returned thread at any time to stop listening.</summary>
        public Thread StartListeningOnSeperateThread(int interval)
        {
            configuration.TickInterval = TimeSpan.FromMilliseconds(interval);

            Thread t = new Thread(new ThreadStart(StartListening));
            t.Name = "Listening Thread";
            t.IsBackground = true;
            t.Start();
            return t;
        }

        /// <summary>Enter an infinite loop, which will listen for incoming Bytes.
        /// Use this to dedicate the current thread of execution to listening.
        /// If there are any exceptions, you should can catch them.
        /// Aborting this thread of execution will cause this thread to die
        /// gracefully, and is recommended.
        /// </summary>
        public void StartListening()
        {
            double oldTime;
            double newTime;
            Start();
            try
            {
                //check for new Bytes
                while (started)
                {
                    timer.Update();
                    oldTime = timer.TimeInMilliseconds;

                    // lock(this) {
                    this.Update();
                    // }

                    timer.Update();
                    newTime = timer.TimeInMilliseconds;
                    int sleepCount = (int)(configuration.TickInterval.Milliseconds - (newTime - oldTime));
                    Sleep(sleepCount);
                }
            }
            catch (ThreadAbortException) //we were told to die.  die gracefully.
            {
                //kill the connexion
                Stop();
            } 
        }
    }
}
