using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using GT;
using System.Diagnostics;

namespace GT
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
    /// <param name="ss">The server stream in which it occurred.  May be null if exception not in ServerStream.</param>
    /// <param name="explanation">A string describing the problem.</param>
    /// <remarks>deprecated: use ErrorEvents instead</remarks>
    public delegate void ErrorEventHandler(Exception e, SocketError se, ServerStream ss, string explanation);
    // FIXME: is this really the right way to notify of exceptions?
    // And what if they are non-socket-errors?
    public delegate void ErrorEvent(/*Severity sev, */ 
        IServerSurrogate ss, string explanation, Exception e);

    /// <summary>Occurs whenever this client is updated.</summary>
    public delegate void UpdateEventDelegate(HPTimer hpTimer);

    #endregion

    #region Type Stream Interfaces

    public interface IStream
    {
        /// <summary>Average latency between the client and this particluar server.</summary>
        float Delay { get; }

        /// <summary> Get the unique identity of the client for this server.  This will be
        /// different for each server, and thus could be different for each connection. </summary>
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

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol);

        // FIXME: How can the new message event be brought in?
        ///// <summary> Occurs when this connection receives a message. </summary>
        //// event SessionNewMessage SessionNewMessageEvent;
    }

    /// <summary>A connection of session events.</summary>
    public interface ISessionStream : IGenericStream<SessionAction,SessionMessage>
    {
        /// <summary> Occurs when this connection receives a message. </summary>
        event SessionNewMessage SessionNewMessageEvent;
    }

    /// <summary>A connection of strings.</summary>
    public interface IStringStream : IGenericStream<string,string>
    {
        /// <summary>Take a String off the queue of received messages.</summary>
        /// <param name="index">Which message to take, with higher numbers being newer messages.</param>
        /// <returns>The message.</returns>
        string DequeueMessage(int index);

        /// <summary> Occurs when this connection receives a message. </summary>
        event StringNewMessage StringNewMessageEvent;
    }

    /// <summary>A connection of objects.</summary>
    public interface IObjectStream : IGenericStream<object,object>
    {
        /// <summary>Dequeues an object from the message list.</summary>
        /// <param name="index">Which to dequeue, where a higher number means a newer message.</param>
        /// <returns>The object that was there.</returns>
        object DequeueMessage(int index);

        /// <summary> Occurs when this connection receives a message. </summary>
        event ObjectNewMessage ObjectNewMessageEvent;
    }

    /// <summary>A connection of byte arrays.</summary>
    public interface IBinaryStream : IGenericStream<byte[],byte[]>
    {
        /// <summary>Takes a message from the message list</summary>
        /// <param name="index">The message to take, where a higher number means a newer message</param>
        /// <returns>The byte array of the message</returns>
        byte[] DequeueMessage(int index);

        /// <summary> Occurs when this connection receives a message. </summary>
        event BinaryNewMessage BinaryNewMessageEvent;
    }

    #endregion


    #region Type Stream Implementations

    public abstract class AbstractBaseStream : IStream
    {
        protected byte id;
        protected internal ServerStream connection;

        /// <summary> Occurs when client is updated. </summary>
        public event UpdateEventDelegate UpdateEvent;

        /// <summary> This SessionStream's ID. </summary>
        public byte ID { get { return id; } }

        /// <summary>Average latency between the client and this particluar server.</summary>
        public float Delay { get { return connection.Delay; } }

        /// <summary> Get the unique identity of the client for this server.  This will be
        /// different for each server, and thus could be different for each connection. </summary>
        public int UniqueIdentity { get { return connection.UniqueIdentity; } }

        /// <summary> Get the connection's destination address </summary>
        public string Address { get { return connection.Address; } }

        /// <summary>Get the connection's destination port</summary>
        public string Port
        {
            get { return connection.Port; }
        }

        internal AbstractBaseStream(ServerStream stream, byte id)
        {
            this.connection = stream;
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

        /// <summary> This SessionStream uses this ServerStream. </summary>
        /// <remarks>deprecated</remarks>
        public ServerStream Connection { get { return connection; } }

        /// <summary>Create a stream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">What channel this will be.</param>
        internal AbstractStream(ServerStream stream, byte id) : base(stream, id)
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

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        public virtual void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol)
        {
            connection.FlushOutgoingMessages(this.id, protocol);
        }
    }

    /// <summary>A connection of session events.</summary>
    public class SessionStream : AbstractStream<SessionAction,SessionMessage>, ISessionStream
    {
        /// <summary> Occurs when this session receives a message. </summary>
        public event SessionNewMessage SessionNewMessageEvent;

        /// <summary>Create a SessionStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">What channel this will be.</param>
        internal SessionStream(ServerStream stream, byte id) : base(stream, id)
        {
        }

        /// <summary>Send a session action to the server.</summary>
        /// <param name="action">The action.</param>
        /// <param name="reli">How to send it.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        override public void Send(SessionAction action, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            this.connection.Send(new SessionMessage(id, UniqueIdentity, action), reli, aggr, ordering);
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

    /// <summary>A connection of strings.</summary>
    public class StringStream : AbstractStream<string,string>, IStringStream
    {
        /// <summary> Occurs when this connection receives a message. </summary>
        public event StringNewMessage StringNewMessageEvent;

        /// <summary>Create a StringStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">What channel this will be.</param>
        internal StringStream(ServerStream stream, byte id) : base(stream, id)
        {
        }

        
        /// <summary>Send a string to the server, specifying how.</summary>
        /// <param name="b">The string to send.</param>
        /// <param name="reli">What method to send it by.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        override public void Send(string b, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            this.connection.Send(new StringMessage(id, b), reli, aggr, ordering);
        }

        /// <summary>Take a String off the queue of received messages.</summary>
        /// <param name="index">Which message to take, with higher numbers being newer messages.</param>
        /// <returns>The message.</returns>
        override public string DequeueMessage(int index)
        {
            try
            {
                StringMessage m;
                if (index >= messages.Count)
                    return null;

                lock (messages)
                {
                    m = (StringMessage)messages[index];
                    messages.RemoveAt(index);
                }
                return m.text;
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

    /// <summary>A connection of Objects.</summary>
    public class ObjectStream : AbstractStream<object,object>, IObjectStream
    {
        private static BinaryFormatter formatter = new BinaryFormatter();

        /// <summary> Occurs when this connection receives a message. </summary>
        public event ObjectNewMessage ObjectNewMessageEvent;

        /// <summary>Create an ObjectStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the objects.</param>
        /// <param name="id">The channel we will claim.</param>
        internal ObjectStream(ServerStream stream, byte id) : base(stream,id)
        {
        }

        /// <summary>Send an object using the specified method.</summary>
        /// <param name="o">The object to send.</param>
        /// <param name="reli">How to send the object.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        override public void Send(object o, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            this.connection.Send(new ObjectMessage(id, o), reli, aggr, ordering);
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
                return m.obj;
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

    /// <summary>A connection of byte arrays.</summary>
    public class BinaryStream : AbstractStream<byte[],byte[]>, IBinaryStream
    {
        /// <summary> Occurs when this connection receives a message. </summary>
        public event BinaryNewMessage BinaryNewMessageEvent;

        /// <summary>Creates a BinaryStream object.</summary>
        /// <param name="stream">The SuperStream object on which to actually send the objects.</param>
        /// <param name="id">The channel to claim.</param>
        internal BinaryStream(ServerStream stream, byte id) : base(stream, id)
        {
        }

        override public void Send(byte[] b, MessageProtocol reli, MessageAggregation aggr, 
            MessageOrder ordering)
        {
            this.connection.Send(new BinaryMessage(id, b), reli, aggr, ordering);
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
                return m.data;
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
    public class ServerStream : IServerSurrogate
    {
        private Client owner;
        private bool started;
        private string address;
        private string port;

        internal double nextPingTime;

        /// <summary>The unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connection.</summary>
        public int UniqueIdentity;

        /// <summary>Incoming messages from the server. As messages are read in from the
        /// different transports, they are added to this list.  The different types of 
        /// streams process this list to select messages corresponding to their particular 
        /// type.</summary>
        internal List<Message> messages;

        /// <summary>Occurs when there is an error.</summary>
        public event ErrorEventHandler ErrorEvent;
        internal ErrorEventHandler ErrorEventDelegate;

        private Dictionary<MessageProtocol, ITransport> transports = 
            new Dictionary<MessageProtocol,ITransport>();
        private Dictionary<MessageProtocol, List<Message>> messagePools;

        /// <summary>Create a new SuperStream to handle a connection to a server. (blocks)</summary>
        /// <param name="address">Who to try to connect to.</param>
        /// <param name="port">Which port to connect to.</param>
        internal ServerStream(Client owner, string address, string port)
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

            foreach (IConnector conn in owner.Connectors)
            {
                // FIXME: and if there is an error...?
                ITransport t = conn.Connect(Address, Port, owner.Capabilities);
                if (t != null) { AddTransport(t); }
            }

            started = true;

            // FIXME: This is bogus and should be changed.
            //request our id right away
            Send(new SystemMessage(SystemMessageType.UniqueIDRequest), MessageProtocol.Tcp, 
                MessageAggregation.No, MessageOrder.None);
        }

        public void Stop()
        {
            started = false;
            foreach (ITransport t in transports.Values)
            {
                t.Dispose();
            }
            transports = null;
        }

        public void AddTransport(ITransport t)
        {
            // transports[t.Name] = t;
            transports[t.MessageProtocol] = t;  // FIXME: this is to be replaced!
            t.PacketReceivedEvent += new PacketReceivedHandler(MessageReceived);
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

        protected void MessageReceived(byte[] buffer, int offset, int count, ITransport transport)
        {
            Message msg = owner.Marshaller.Unmarshal(new MemoryStream(buffer, offset, count, false), transport);
            if (msg.type == MessageType.System)
            {
                HandleSystemMessage((SystemMessage)msg, transport);
            }
            else
            {
                messages.Add(msg);
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
        /// <param name="data">The data that will wait</param>
        /// <param name="id">The channel it will be sent on</param>
        /// <param name="type">The type of data this is</param>
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
        /// <param name="data">The data to send.</param>
        /// <param name="id">What channel to send it on.</param>
        /// <param name="type">What type of data it is.</param>
        /// <param name="protocol">How to send it.</param>
        /// <param name="aggr">Should this data be aggregated?</param>
        /// <param name="ordering">What data should be sent before this?</param>
        internal void Send(Message msg, MessageProtocol protocol, 
            MessageAggregation aggr, MessageOrder ordering)
        {
            lock (this)
            {
                if (!Active) { 
                    throw new InvalidStateException("Cannot send on a stopped client", this); 
                }

                if (aggr == MessageAggregation.Yes)
                {
                    //Wait to send this data, hopefully to pack it with later data.
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
                    FlushOutgoingMessages(msg.id, protocol);
                    return;
                }

                //Actually send the data.  Note that after this point, ordering and 
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

                //Actually send the data.  Note that after this point, ordering and 
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
        /// <param name="id">The channel it came alone.</param>
        /// <param name="buffer">The data that came along with it.</param>
        internal void HandleSystemMessage(SystemMessage message, ITransport transport)
        {
            switch ((SystemMessageType)message.id)
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
                Debug.WriteLine("ServerStream.HandleSystemMessage(): Unknown message type: " +
                    (SystemMessageType)message.id);
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
            return element.id == soughtChannel;
        }
    }

    /// <summary>Represents a client that can connect to multiple servers.</summary>
    public class Client : IStartable
    {
        private Guid guid = Guid.NewGuid();
        private Dictionary<byte, ObjectStream> objectStreams;
        private Dictionary<byte, BinaryStream> binaryStreams;
        private Dictionary<byte, StringStream> stringStreams;
        private Dictionary<byte, SessionStream> sessionStreams;
        private Dictionary<byte, AbstractStreamedTuple> oneTupleStreams;
        private Dictionary<byte, AbstractStreamedTuple> twoTupleStreams;
        private Dictionary<byte, AbstractStreamedTuple> threeTupleStreams;
        private List<ServerStream> superStreams;
        private List<IConnector> connectors = new List<IConnector>();
        private HPTimer timer;
        private bool started = false;

        private IMarshaller marshaller = new DotNetSerializingMarshaller();

        /// <summary>Occurs when there are errors on the network.</summary>
        public event ErrorEventHandler ErrorEvent;

        /// <summary>How often to ping the servers.  These are keep-alives.</summary>
        public double PingInterval = 10000;

        /// <summary>How often to check the network for new data.
        /// Only applies if StartListeningOnSeperateThread() or StartListening() methods 
        /// are used.  Can be changed at runtime.  Unit is milliseconds.</summary>
        public double ListeningInterval = 0;

        /// <summary>Creates a Client object.</summary>
        public Client()
        {
            objectStreams = new Dictionary<byte, ObjectStream>();
            binaryStreams = new Dictionary<byte, BinaryStream>();
            stringStreams = new Dictionary<byte, StringStream>();
            sessionStreams = new Dictionary<byte, SessionStream>();
            oneTupleStreams = new Dictionary<byte, AbstractStreamedTuple>();
            twoTupleStreams = new Dictionary<byte, AbstractStreamedTuple>();
            threeTupleStreams = new Dictionary<byte, AbstractStreamedTuple>();
            superStreams = new List<ServerStream>();
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
                caps[GTConstants.CAPABILITIES_CLIENT_ID] = 
                    Guid.ToString("N");  // "N" is the most compact form
                StringBuilder sb = new StringBuilder();
                foreach(string d in Marshaller.Descriptors) {
                    sb.Append(d);
                    sb.Append(' ');
                }
                caps[GTConstants.CAPABILITIES_MARSHALLER_DESCRIPTORS] = sb.ToString().Trim();
                return caps;
            }
        }

        public ICollection<IConnector> Connectors
        {
            get {
                if (connectors.Count == 0)
                {
                    connectors.Add(new TcpConnector());
                    connectors.Add(new UdpConnector());
                }
                return connectors; 
            }
        }

        /// <summary>
        /// Return globally unique identifier for this client.
        /// </summary>
        public Guid Guid {
            get { return guid; }
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="B">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="C">The Type of the third value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="id">The id to use for this three-tuple (unique to three-tuples)</param>
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
                tuple.connection = GetSuperStream(address, port);
                return (StreamedTuple<A, B, C>)threeTupleStreams[id];
            }

            StreamedTuple<A, B, C> bs = (StreamedTuple<A, B, C>)new StreamedTuple<A, B, C>(GetSuperStream(address, port), id, milliseconds);
            threeTupleStreams.Add(id, (AbstractStreamedTuple)bs);
            return bs;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="B">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="C">The Type of the third value of the tuple</typeparam>
        /// <param name="serverStream">The stream to use to send the tuple</param>
        /// <param name="id">The id to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public StreamedTuple<A, B, C> GetStreamedTuple<A, B, C>(ServerStream serverStream, byte id, int milliseconds)
            where A : IConvertible
            where B : IConvertible
            where C : IConvertible
        {
            StreamedTuple<A, B, C> tuple;
            if (threeTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<A, B, C>) threeTupleStreams[id];
                if (tuple.connection == serverStream)
                {
                    return tuple;
                }

                tuple.connection = serverStream;
                return tuple;
            }

            tuple = new StreamedTuple<A, B, C>(serverStream, id, milliseconds);
            threeTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="B">The Type of the second value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="id">The id to use for this two-tuple (unique to two-tuples)</param>
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

                tuple.connection = GetSuperStream(address, port);
                return tuple;
            }

            tuple = new StreamedTuple<A, B>(GetSuperStream(address, port), id, milliseconds);
            twoTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="B">The Type of the second value of the tuple</typeparam>
        /// <param name="serverStream">The stream to use to send the tuple</param>
        /// <param name="id">The id to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public StreamedTuple<A, B> GetStreamedTuple<A, B>(ServerStream serverStream, byte id, int milliseconds)
            where A : IConvertible
            where B : IConvertible
        {
            StreamedTuple<A, B> tuple;
            if (twoTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<A, B>)twoTupleStreams[id];
                if (tuple.connection == serverStream)
                {
                    return tuple;
                }

                tuple.connection = serverStream;
                return tuple;
            }

            tuple = new StreamedTuple<A, B>(serverStream, id, milliseconds);
            threeTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="A">The Type of the value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="id">The id to use for this one-tuple (unique to one-tuples)</param>
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

                tuple.connection = GetSuperStream(address, port);
                return tuple;
            }

            tuple = new StreamedTuple<A>(GetSuperStream(address, port), id, milliseconds);
            oneTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }


        /// <summary>Gets a connection for managing the session to this server.</summary>
        /// <param name="address">The address to connect to.  Changes old connection if id already claimed.</param>
        /// <param name="port">The port to connect to.  Changes old connection if id already claimed.</param>
        /// <param name="id">The channel id to claim or retrieve.</param>
        /// <returns>The created or retrived SessionStream</returns>
        public SessionStream GetSessionStream(string address, string port, byte id)
        {
            if (sessionStreams.ContainsKey(id))
            {
                if (sessionStreams[id].Address.Equals(address) && sessionStreams[id].Port.Equals(port))
                {
                    return sessionStreams[id];
                }

                sessionStreams[id].connection = GetSuperStream(address, port);
                return sessionStreams[id];
            }

            SessionStream bs = new SessionStream(GetSuperStream(address, port), id);
            sessionStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connection for managing the session to this server.</summary>
        /// <param name="serverStream">The serverStream to use for the connection.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim or retrieve.</param>
        /// <returns>The created or retrived SessionStream</returns>
        public SessionStream GetSessionStream(ServerStream serverStream, byte id)
        {
            if (sessionStreams.ContainsKey(id))
            {
                if (sessionStreams[id].connection == serverStream)
                {
                    return sessionStreams[id];
                }

                sessionStreams[id].connection = serverStream;
                return sessionStreams[id];
            }

            SessionStream bs = new SessionStream(serverStream, id);
            sessionStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets an already created SessionStream</summary>
        /// <param name="id">The channel id of the SessionStream unique to SessionStreams.</param>
        /// <returns>The found SessionStream</returns>
        public SessionStream GetSessionStream(byte id)
        {
            return sessionStreams[id];
        }

        /// <summary>Gets a Superstream, and if not already created, makes a new one for that destination.</summary>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The port to connect to.</param>
        /// <returns>The created or retrieved connection itself.</returns>
        public ServerStream GetSuperStream(string address, string port)
        {
            foreach (ServerStream s in superStreams)
            {
                if (s.Address.Equals(address) && s.Port.Equals(port))
                {
                    return s;
                }
            }
            ServerStream mySS = new ServerStream(this, address, port);
            mySS.ErrorEventDelegate = new ErrorEventHandler(mySS_ErrorEvent);
            mySS.ErrorEvent += mySS.ErrorEventDelegate;
            mySS.Start();
            superStreams.Add(mySS);
            return mySS;
        }

        private void mySS_ErrorEvent(Exception e, SocketError se, ServerStream ss, string explanation)
        {
            if (ErrorEvent != null)
                ErrorEvent(e, se, ss, explanation);
        }

        /// <summary>Gets a connection for transmitting strings.</summary>
        /// <param name="address">The address to connect to.  Changes old connection if id already claimed.</param>
        /// <param name="port">The port to connect to.  Changes old connection if id already claimed.</param>
        /// <param name="id">The channel id to claim.</param>
        /// <returns>The created or retrived StringStream</returns>
        public StringStream GetStringStream(string address, string port, byte id)
        {
            if (stringStreams.ContainsKey(id))
            {
                if (stringStreams[id].Address.Equals(address) && stringStreams[id].Port.Equals(port))
                {
                    return stringStreams[id];
                }

                stringStreams[id].connection = GetSuperStream(address, port);
                return stringStreams[id];
            }

            StringStream bs = new StringStream(GetSuperStream(address, port), id);
            stringStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connection for transmitting strings.</summary>
        /// <param name="serverStream">The serverStream to use for the connection.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim.</param>
        /// <returns>The created or retrived StringStream</returns>
        public StringStream GetStringStream(ServerStream serverStream, byte id)
        {
            if (stringStreams.ContainsKey(id))
            {
                if (stringStreams[id].connection == serverStream)
                {
                    return stringStreams[id];
                }

                stringStreams[id].connection = serverStream;
                return stringStreams[id];
            }

            StringStream bs = new StringStream(serverStream, id);
            stringStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets an already created StringStream</summary>
        /// <param name="id">The channel id of the StringStream unique to StringStreams.</param>
        /// <returns>The found StringStream</returns>
        public StringStream GetStringStream(byte id)
        {
            return stringStreams[id];
        }

        /// <summary>Gets a connection for transmitting objects.</summary>
        /// <param name="address">The address to connect to.  Changes old connection if id already claimed.</param>
        /// <param name="port">The port to connect to.  Changes old connection if id already claimed.</param>
        /// <param name="id">The channel id to claim for this ObjectStream, unique for all ObjectStreams.</param>
        /// <returns>The created or retrived ObjectStream</returns>
        public ObjectStream GetObjectStream(string address, string port, byte id)
        {
            if (objectStreams.ContainsKey(id))
            {
                if (objectStreams[id].Address.Equals(address) && objectStreams[id].Port.Equals(port))
                {
                    return objectStreams[id];
                }
                objectStreams[id].connection = GetSuperStream(address, port);
                return objectStreams[id];
            }
            ObjectStream bs = new ObjectStream(GetSuperStream(address, port), id);
            objectStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connection for transmitting objects.</summary>
        /// <param name="serverStream">The serverStream to use for the connection.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim for this ObjectStream, unique for all ObjectStreams.</param>
        /// <returns>The created or retrived ObjectStream</returns>
        public ObjectStream GetObjectStream(ServerStream serverStream, byte id)
        {
            if (objectStreams.ContainsKey(id))
            {
                if (objectStreams[id].connection == serverStream)
                {
                    return objectStreams[id];
                }
                objectStreams[id].connection = serverStream;
                return objectStreams[id];
            }
            ObjectStream bs = new ObjectStream(serverStream, id);
            objectStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Get an already created ObjectStream</summary>
        /// <param name="id">The channel id of the ObjectStream unique, to ObjectStreams.</param>
        /// <returns>The found ObjectStream.</returns>
        public ObjectStream GetObjectStream(byte id)
        {
            return objectStreams[id];
        }

        /// <summary>Gets a connection for transmitting byte arrays.</summary>
        /// <param name="address">The address to connect to.  Changes old connection if id already claimed.</param>
        /// <param name="port">The port to connect to.  Changes old connection if id already claimed.</param>
        /// <param name="id">The channel id to claim for this BinaryStream, unique for all BinaryStreams.</param>
        /// <returns>The created or retrived BinaryStream.</returns>
        public BinaryStream GetBinaryStream(string address, string port, byte id)
        {
            if (binaryStreams.ContainsKey(id))
            {
                BinaryStream s = binaryStreams[id];
                if (s.Address.Equals(address) && s.Port.Equals(port))
                {
                    return binaryStreams[id];
                }
                binaryStreams[id].connection = GetSuperStream(address, port);
                return binaryStreams[id];
            }
            BinaryStream bs = new BinaryStream(GetSuperStream(address, port), id);
            binaryStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connection for transmitting byte arrays.</summary>
        /// <param name="serverStream">The serverStream to use for the connection.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim for this BinaryStream, unique for all BinaryStreams.</param>
        /// <returns>The created or retrived BinaryStream.</returns>
        public BinaryStream GetBinaryStream(ServerStream serverStream, byte id)
        {
            if (binaryStreams.ContainsKey(id))
            {
                if (binaryStreams[id].connection == serverStream)
                {
                    return binaryStreams[id];
                }
                binaryStreams[id].connection = serverStream;
                return binaryStreams[id];
            }
            BinaryStream bs = new BinaryStream(serverStream, id);
            binaryStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Get an already created BinaryStream</summary>
        /// <param name="id">The channel id of the BinaryStream, unique to BinaryStreams.</param>
        /// <returns>A BinaryStream</returns>
        public BinaryStream GetBinaryStream(byte id)
        {
                return binaryStreams[id];
        }

        /// <summary>One tick of the network beat.  Thread-safe.</summary>
        public void Update()
        {
            DebugUtils.WriteLine(this + ": Update() started");
            lock (this)
            {
                if (!started) { Start(); }
                timer.Update();
                foreach (ServerStream s in superStreams)
                {
                    try
                    {
                        if (s.nextPingTime < timer.TimeInMilliseconds)
                        {
                            s.nextPingTime = timer.TimeInMilliseconds + PingInterval;
                            s.Ping();
                        }

                        s.Update();
                        lock (s.messages)
                        {
                            foreach (Message m in s.messages)
                            {
                                try
                                {
                                    switch (m.type)
                                    {
                                    case MessageType.Binary: binaryStreams[m.id].QueueMessage(m); break;
                                    case MessageType.Object: objectStreams[m.id].QueueMessage(m); break;
                                    case MessageType.Session: sessionStreams[m.id].QueueMessage(m); break;
                                    case MessageType.String: stringStreams[m.id].QueueMessage(m); break;
                                    case MessageType.System: HandleSystemMessage(m); break;
                                    case MessageType.Tuple1D: oneTupleStreams[m.id].QueueMessage(m); break;
                                    case MessageType.Tuple2D: twoTupleStreams[m.id].QueueMessage(m); break;
                                    case MessageType.Tuple3D: threeTupleStreams[m.id].QueueMessage(m); break;
                                    default:
                                        Console.WriteLine("Client: WARNING: received message (id={0}) with unknown type: {1}",
                                            m.id, m.type);
                                        if (ErrorEvent != null)
                                            ErrorEvent(null, SocketError.Fault, s, "Received " + m.type + "message for connection " + m.id +
                                                ", but that type does not exist.");
                                        break;
                                    }
                                }
                                catch (KeyNotFoundException e)
                                {
                                    Console.WriteLine("Client: WARNING: received message with unmonitored id (type={0}): id={1}",
                                        m.type, m.id);
                                    if (ErrorEvent != null)
                                        ErrorEvent(e, SocketError.Fault, s, "Received " + m.type + "message for connection " + m.id +
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

        /// <summary>Starts a new thread that listens for new clients or new data.
        /// Abort returned thread at any time to stop listening.</summary>
        public Thread StartListeningOnSeperateThread(int interval)
        {
            Thread t = new Thread(new ThreadStart(StartListening));
            t.Name = "Listening Thread";
            t.IsBackground = true;
            t.Start();
            return t;
        }

        /// <summary>Enter an infinite loop, which will listen for incoming data.
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
                //check for new data
                while (started)
                {
                    timer.Update();
                    oldTime = timer.TimeInMilliseconds;

                    // lock(this) {
                    this.Update();
                    // }

                    timer.Update();
                    newTime = timer.TimeInMilliseconds;
                    int sleepCount = (int)(this.ListeningInterval - (newTime - oldTime));
                    Sleep(sleepCount);
                }
            }
            catch (ThreadAbortException) //we were told to die.  die gracefully.
            {
                //kill the connection
                Stop();
            } 
        }

        public void Start()
        {
            lock (this)
            {
                if (started) { return; }
                timer.Start();
                timer.Update();
                if (connectors.Count == 0)
                {
                    connectors.Add(new TcpConnector());
                    connectors.Add(new UdpConnector());
                }
                foreach (IConnector conn in connectors)
                {
                    conn.Start();
                }

                for (int i = 0; i < superStreams.Count; i++)
                {
                    superStreams[i].Start();
                }
                started = true;
            }
        }

        public void Stop()
        {
            lock (this)
            {
                if (!started) { return; }
                started = false;
                foreach (IConnector conn in connectors)
                {
                    conn.Start();
                }
                foreach(ServerStream s in superStreams)
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
                if (superStreams != null)
                {
                    //for (int i = 0; i < superStreams.Count; i++)
                    //{
                    //    superStreams[i].Dispose();
                    //}
                }
                superStreams = null;
                timer = null;
            }
        }

        public bool Active
        {
            get { return started; }
        }

        public void Sleep(TimeSpan span)
        {
            Sleep(span.Milliseconds);
        }

        public void Sleep(int milliseconds)
        {
            // FIXME: this should do something smarter
            // Socket.Select(listenList, null, null, 1000);
            Thread.Sleep(milliseconds);
        }
    }
}
