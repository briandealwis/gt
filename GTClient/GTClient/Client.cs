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

    /// <summary>Occurs whenever this client is updated.</summary>
    public delegate void UpdateEventDelegate(HPTimer hpTimer);

    #endregion

    #region Type Stream Interfaces

    public interface IStream
    {
        /// <summary>Average latency between the client and this particluar server 
        /// (in milliseconds).</summary>
        float Delay { get; }

        /// <summary> Get the unique identity of the client for this server.  This will be
        /// different for each server, and thus could be different for each connexion. </summary>
        int UniqueIdentity { get; }

        /// <summary>Flush all pending messages on this stream.</summary>
        void Flush();

        /// <summary>Occurs whenever this client is updated.</summary>
        event UpdateEventDelegate UpdateEvent;

    }

    /// <summary>
    /// A generic item stream as exposed by the GT Client.
    /// </summary>
    /// <typeparam name="SI">The type of generic items supported by this stream.</typeparam>
    /// <typeparam name="RI">The type of received items, which is generally expected to be
    ///     the same as <c>SI</c>.  Some special streams, such as <c>ISessionStream</c>, return
    ///     more complex objects.</typeparam>
    public interface IGenericStream<SI,RI> : IStream
    {
        /// <summary>Send an item to the server</summary>
        /// <param name="item">The item</param>
        void Send(SI item);

        /// <summary>Send an item to the server</summary>
        /// <param name="item">The item</param>
        /// <param name="mdr">How to send it</param>
        void Send(SI item, MessageDeliveryRequirements mdr);

        /// <summary>Take an item off the queue of received messages.</summary>
        /// <param name="index">The message to be dequeued, with a higher number indicating a newer message.</param>
        /// <returns>The message, or null if there is no such message.</returns>
        RI DequeueMessage(int index);

        /// <summary>Return the number of waiting messages.</summary>
        /// <returns>The number of waiting messages; 0 indicates there are no waiting message.</returns>
        int Count { get; }

        /// <summary>Received messages from the server.</summary>
        IList<Message> Messages { get; }

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
        /// <summary> Occurs when this connexion receives a message. </summary>
        event StringNewMessage StringNewMessageEvent;
    }

    /// <summary>A connexion of objects.</summary>
    public interface IObjectStream : IGenericStream<object,object>
    {
        /// <summary> Occurs when this connexion receives a message. </summary>
        event ObjectNewMessage ObjectNewMessageEvent;
    }

    /// <summary>A connexion of byte arrays.</summary>
    public interface IBinaryStream : IGenericStream<byte[],byte[]>
    {
        /// <summary> Occurs when this connexion receives a message. </summary>
        event BinaryNewMessage BinaryNewMessageEvent;
    }

    #endregion

    #region Type Stream Implementations

    public abstract class AbstractBaseStream : IStream
    {
        protected byte id;
        protected internal ServerConnexion connexion;
        protected ChannelDeliveryRequirements deliveryOptions;

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
        public string Port { get { return connexion.Port; } }

        /// <summary>Flush all pending messages on this stream.</summary>
        public abstract void Flush();

        public ChannelDeliveryRequirements ChannelDeliveryOptions { get { return deliveryOptions; } }

        internal AbstractBaseStream(ServerConnexion stream, byte id, ChannelDeliveryRequirements cdr)
        {
            this.connexion = stream;
            this.id = id;
            this.deliveryOptions = cdr;
        }

        internal virtual void Update(HPTimer hpTimer)
        {
            if (UpdateEvent != null)
                UpdateEvent(hpTimer);
        }

        public override string ToString()
        {
            return GetType().Name + "[" + connexion + "]";
        }
    }

    public abstract class AbstractStream<SI,RI> : AbstractBaseStream, IGenericStream<SI,RI>
    {
        protected List<Message> messages;

        /// <summary>Received messages from the server.</summary>
        public IList<Message> Messages { get { return messages; } }

        /// <summary> This streak uses this connexion. </summary>
        /// <remarks>deprecated</remarks>
        public ServerConnexion Connection { get { return connexion; } }

        /// <summary>Create a stream object.</summary>
        /// <param name="stream">The connexion used to actually send the messages.</param>
        /// <param name="id">The message channel.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal AbstractStream(ServerConnexion stream, byte id, ChannelDeliveryRequirements cdr) 
            : base(stream, id, cdr)
        {
            messages = new List<Message>();
        }

        public virtual int Count { get { return messages.Count; } }

        public void Send(SI item)
        {
            Send(item, null);
        }

        public abstract void Send(SI item, MessageDeliveryRequirements mdr);

        public abstract RI DequeueMessage(int index);

        /// <summary>Flush all aggregated messages on this connexion</summary>
        public override void Flush()
        {
            connexion.FlushChannelMessages(this.id, deliveryOptions);
        }
    }

    /// <summary>A connexion of session events.</summary>
    internal class SessionStream : AbstractStream<SessionAction, SessionMessage>, ISessionStream
    {
        /// <summary> Occurs when this session receives a message. </summary>
        public event SessionNewMessage SessionNewMessageEvent;

        /// <summary>Create a SessionStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">The message channel id.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal SessionStream(ServerConnexion stream, byte id, ChannelDeliveryRequirements cdr) 
            : base(stream, id, cdr)
        {
        }

        /// <summary>Send a session action to the server.</summary>
        /// <param name="action">The action.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(SessionAction action, MessageDeliveryRequirements mdr)
        {
            connexion.Send(new SessionMessage(id, UniqueIdentity, action), mdr, deliveryOptions);
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
    internal class StringStream : AbstractStream<string, string>, IStringStream
    {
        /// <summary> Occurs when this connexion receives a message. </summary>
        public event StringNewMessage StringNewMessageEvent;

        /// <summary>Create a StringStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">The message channel id.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal StringStream(ServerConnexion stream, byte id, ChannelDeliveryRequirements cdr) 
            : base(stream, id, cdr)
        {
        }

        
        /// <summary>Send a string to the server, specifying how.</summary>
        /// <param name="b">The string to send.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(string s, MessageDeliveryRequirements mdr)
        {
            connexion.Send(new StringMessage(id, s), mdr, deliveryOptions);
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
    internal class ObjectStream : AbstractStream<object, object>, IObjectStream
    {
        /// <summary> Occurs when this connexion receives a message. </summary>
        public event ObjectNewMessage ObjectNewMessageEvent;

        /// <summary>Create an ObjectStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the objects.</param>
        /// <param name="id">The message channel claimed.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal ObjectStream(ServerConnexion stream, byte id, ChannelDeliveryRequirements cdr) 
            : base(stream, id, cdr)
        {
        }

        /// <summary>Send an object using the specified method.</summary>
        /// <param name="o">The object to send.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(object o, MessageDeliveryRequirements mdr)
        {
            connexion.Send(new ObjectMessage(id, o), mdr, deliveryOptions);
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
    internal class BinaryStream : AbstractStream<byte[],byte[]>, IBinaryStream
    {
        /// <summary> Occurs when this connexion receives a message. </summary>
        public event BinaryNewMessage BinaryNewMessageEvent;

        /// <summary>Creates a BinaryStream object.</summary>
        /// <param name="stream">The SuperStream object on which to actually send the objects.</param>
        /// <param name="id">The message channel to claim.</param>
        /// <param name="cdr">The channel delivery options.</param>
        internal BinaryStream(ServerConnexion stream, byte id, ChannelDeliveryRequirements cdr) 
            : base(stream, id, cdr)
        {
        }

        /// <summary>Send a byte array using the specified method.</summary>
        /// <param name="b">The byte array to send.</param>
        /// <param name="mdr">Message delivery options</param>
        override public void Send(byte[] b, MessageDeliveryRequirements mdr)
        {
            connexion.Send(new BinaryMessage(id, b), mdr, deliveryOptions);
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
    public class ServerConnexion : BaseConnexion, IStartable
    {
        private Client owner;
        private string address;
        private string port;

        /// <summary>The unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connexion.</summary>
        public int UniqueIdentity { get { return uniqueIdentity; } }

        /// <summary>Incoming messages from the server. As messages are read in from the
        /// different transports, they are added to this list.  These messages are then
        /// processed by Client.Update() to dispatch to their corresponding stream.
        /// We separate these two steps to isolate potential problems.</summary>
        protected Queue<Message> receivedMessages;

        private Dictionary<byte, Queue<KeyValuePair<Message, MessageDeliveryRequirements>>> messagePools;

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
        /// Return the globally unique identifier for this stream's client.
        /// </summary>
        public Guid Guid
        {
            get { return owner.Guid; }
        }

        #region Constructors and Destructors

        /// <summary>Create a new SuperStream to handle a connexion to a server.</summary>
        /// <param name="owner">The owning client.</param>
        /// <param name="address">Who to try to connect to.</param>
        /// <param name="port">Which port to connect to.</param>
        protected internal ServerConnexion(Client owner, string address, string port)
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
        virtual public void Start()
        {
            if (Active) { return; }

            messagePools = new Dictionary<byte, Queue<KeyValuePair<Message, MessageDeliveryRequirements>>>();
            receivedMessages = new Queue<Message>();

            transports = new List<ITransport>();

            // FIXME: should this be done on demand?
            foreach (IConnector conn in owner.Connectors)
            {
                // FIXME: and if there is an error...?
                ITransport t = conn.Connect(Address, Port, owner.Capabilities);
                if (t != null) { 
                    AddTransport(t); 
                }
                else
                {
                    Console.WriteLine("{0}: WARNING: Could not connect to {1}:{2} via {3}", 
                        this, Address, Port, conn);
                }
            }

            active = true;

            // FIXME: This is bogus and should be changed.
            // request our id right away
            foreach (ITransport t in transports)
            {
                SendMessage(t, new SystemMessage(SystemMessageType.UniqueIDRequest));
            }
        }

        virtual public void Stop()
        {
            active = false;
            if (transports != null)
            {
                foreach (ITransport t in transports)
                {
                    try { t.Dispose(); }
                    catch (Exception e)
                    {
                        NotifyError("Exception disposing transport", t, e);
                    }
                }
            }
            transports = null;
        }

        override public void Dispose()
        {
            Stop();
            base.Dispose();
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
        public override int MyUniqueIdentity
        {
            get { return UniqueIdentity; }
        }

        protected override void HandleTransportError(string explanation, ITransport transport, object context)
        {
            // call the super to dispose and drop the transport from our transport list
            base.HandleTransportError(explanation, transport, context); 
            if (owner == null) { return; }

            // find the connector responsible for having connected this transport and
            // try to reconnect.
            foreach (IConnector conn in owner.Connectors)
            {
                // FIXME: and if there is an error...?
                if(conn.Responsible(transport)) {
                    ITransport t = conn.Connect(Address, Port, owner.Capabilities);
                    if (t != null) { 
                        Console.WriteLine("{0} [{1}] Reconnected: {2}", DateTime.Now, this, t);
                        AddTransport(t); 
                    } else {
                        Console.WriteLine("{0} [{1}] Could not reconnect to {2}/{3}", DateTime.Now, this,
                            Address, Port);
                    }
                }
            }
        }
        protected void HandleIncomingMessage(Message m, IConnexion client, ITransport transport)
        {
            // Hmm, this lock may not be necessary -- the Dequeueing of messages should
            // occur in the same thread.
            //Console.WriteLine("{0}: posting incoming message {1}", this, msg);
            lock (receivedMessages) { receivedMessages.Enqueue(m); }
        }


        /// <summary>Adds the message to a list, waiting to be sent out.</summary>
        /// <param name="msg">The message to be aggregated</param>
        /// <param name="mdr">How it should be sent out (potentially null)</param>
        /// <param name="cdr">General delivery instructions for this message's channel.</param>
        private void Aggregate(Message msg, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Queue<KeyValuePair<Message,MessageDeliveryRequirements>> mp;
            if (!messagePools.TryGetValue(msg.Id, out mp))
            {
                mp = messagePools[msg.Id] = new Queue<KeyValuePair<Message,MessageDeliveryRequirements>>();
            }
            // FIXME: presumably there is some maximum size or waiting time
            // before aggregated messages are flushed?
            // FIXME: what about QoS.Freshness -- where only the latest should be sent?
            mp.Enqueue(new KeyValuePair<Message,MessageDeliveryRequirements>(msg, mdr));
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

                if (msg.MessageType == MessageType.System)
                {
                    SendMessage(FindTransport(mdr, cdr), msg);
                    return;
                }

                MessageAggregation aggr = mdr == null ? cdr.Aggregation : mdr.Aggregation;
                if (aggr == MessageAggregation.Aggregatable)
                {
                    //Wait to send this message, hopefully to pack it with later messages.
                    Aggregate(msg, mdr, cdr);
                    return;
                } else if (aggr == MessageAggregation.FlushAll)
                {
                    //Make sure ALL messages are sent, then send <c>msg</c>.
                    FlushAllMessages(msg, mdr, cdr);
                    return;
                }

                // if aggr == FlushChannel, then must make sure ALL other messages 
                // on this CHANNEL are sent, and then send <c>msg</c>.  Otherwise
                // is Immediate, meaning bundle <c>msg</c> first and then cram on whatever 
                // other messages are waiting.
                FlushChannelMessages(msg.Id, msg, mdr, cdr, aggr == MessageAggregation.FlushChannel);
            }
        }

        /// <summary>Send a message using these parameters.</summary>
        /// <param name="msgs">The messages to send.</param>
        /// <param name="mdr">Particular instructions for this message.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        override public void Send(IList<Message> msgs, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            foreach (Message m in msgs) { Send(m, mdr, cdr); }
        }

        internal void FlushChannelMessages(byte id, ChannelDeliveryRequirements cdr)
        {
            FlushChannelMessages(id, null, null, cdr, false);
        }

        /// <summary>Flushes outgoing messages on channel <c>id</c> channel only.
        /// <c>msg</c>, if not null, will be sent first if <c>putMsgFirst == true</c>,
        /// or sent last if <c>putMsgFirst == false</c>.</summary>
        protected void FlushChannelMessages(byte id, Message msg, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr, bool putMsgFirst)
        {
            Queue<KeyValuePair<Message,MessageDeliveryRequirements>> list;
            if (!messagePools.TryGetValue(id, out list) || list == null || list.Count == 0)
            {
                if (msg != null) { SendMessage(FindTransport(mdr, cdr), msg); }
                return;
            }
            if (!putMsgFirst && msg != null)
            {
                list.Enqueue(new KeyValuePair<Message, MessageDeliveryRequirements>(msg, mdr));
            }

            Dictionary<ITransport, Stream> inProgress = new Dictionary<ITransport, Stream>();
            do
            {
                KeyValuePair<Message, MessageDeliveryRequirements> current;
                if (putMsgFirst)
                {
                    current = new KeyValuePair<Message, MessageDeliveryRequirements>(msg, mdr);
                    putMsgFirst = false;
                }
                else { current = list.Dequeue(); }
                // FindTransport() will throw exception if not found
                ITransport transport = FindTransport(current.Value, cdr);
                Stream stream;
                if (!inProgress.TryGetValue(transport, out stream))
                {
                    stream = inProgress[transport] = transport.GetPacketStream();
                }
                StreamMessage(current.Key, transport, ref stream);
                inProgress[transport] = stream; // be sure to update inProgress
            } while (list.Count > 0);

            if (list.Count == 0) { messagePools.Remove(id); }

            // send everything in progress
            foreach (ITransport t in inProgress.Keys)
            {
                Stream stream = inProgress[t];
                if (stream != null) { t.SendPacket(stream); }
            }
        }

        /// <summary>Flushes all messages and then <c>msg</c>; <c>msg</c> may be null.</summary>
        internal void FlushAllMessages(Message msg, MessageDeliveryRequirements mdr, 
            ChannelDeliveryRequirements cdr)
        {
            if (messagePools.Count == 0)
            {
                SendMessage(FindTransport(mdr, cdr), msg);
                return;
            }

            Queue<KeyValuePair<Message, MessageDeliveryRequirements>> list;
            if (msg != null)
            {
                if (!messagePools.TryGetValue(msg.Id, out list))
                {
                    list = messagePools[msg.Id] = new Queue<KeyValuePair<Message, MessageDeliveryRequirements>>();
                }
                list.Enqueue(new KeyValuePair<Message, MessageDeliveryRequirements>(msg, mdr));
            }

            Dictionary<ITransport, Stream> inProgress = new Dictionary<ITransport, Stream>();
            while (messagePools.Count > 0)
            {
                // FIXME: This does round-robin.  Should use channel/message priorities.
                foreach (byte id in messagePools.Keys)
                {
                    list = messagePools[id];
                    KeyValuePair<Message, MessageDeliveryRequirements> current = list.Dequeue();
                    // FindTransport() will throw exception if not found
                    ITransport transport = FindTransport(current.Value, cdr);
                    Stream stream;
                    if (!inProgress.TryGetValue(transport, out stream))
                    {
                        stream = inProgress[transport] = transport.GetPacketStream();
                    }
                    StreamMessage(current.Key, transport, ref stream);
                    inProgress[transport] = stream; // be sure to update inProgress

                    if (list.Count == 0) { messagePools.Remove(id); }
                }
            }
            // send everything that was in progress
            foreach (ITransport t in inProgress.Keys)
            {
                Stream stream = inProgress[t];
                if (stream != null) { t.SendPacket(stream); }
            }
        }
    
        /// <summary>Deal with a system message in whatever way we need to.</summary>
        /// <param name="message">The incoming message.</param>
        /// <param name="transport">The transport from which the message
	    ///  came.</param>
        override protected void HandleSystemMessage(SystemMessage message, ITransport transport)
        {
            switch ((SystemMessageType)message.Id)
            {
            case SystemMessageType.UniqueIDRequest:
                uniqueIdentity = BitConverter.ToInt32(message.data, 0);
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
            return GetType().Name + "[" + UniqueIdentity + "]";
        }
    }

    public abstract class ClientConfiguration : BaseConfiguration
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
        internal Dictionary<byte, ObjectStream> objectStreams;
        internal Dictionary<byte, BinaryStream> binaryStreams;
        internal Dictionary<byte, StringStream> stringStreams;
        internal Dictionary<byte, SessionStream> sessionStreams;
        internal Dictionary<byte, AbstractStreamedTuple> oneTupleStreams;
        internal Dictionary<byte, AbstractStreamedTuple> twoTupleStreams;
        internal Dictionary<byte, AbstractStreamedTuple> threeTupleStreams;

        protected IList<ServerConnexion> connexions;
        protected ICollection<IConnector> connectors;
        protected IMarshaller marshaller;
        protected HPTimer timer;
        protected long lastPingTime = 0;
        protected bool started = false;

        /// <summary>Occurs when there are errors on the network.</summary>
        public event ErrorEventNotication ErrorEvent;

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

        public ClientConfiguration Configuration { get { return configuration; } }

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
            Sleep((int)configuration.TickInterval.TotalMilliseconds);
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
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="T_Z">The Type of the third value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="id">The channel id to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y, T_Z> GetStreamedTuple<T_X, T_Y, T_Z>(string address, string port, 
            byte id, int milliseconds, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
            where T_Z : IConvertible
        {
            StreamedTuple<T_X, T_Y, T_Z> tuple;
            if (threeTupleStreams.ContainsKey(id))
            {
                if (threeTupleStreams[id].Address.Equals(address) && threeTupleStreams[id].Port.Equals(port))
                {
                    return (StreamedTuple<T_X, T_Y, T_Z>)threeTupleStreams[id];
                }

                tuple = (StreamedTuple<T_X, T_Y, T_Z>)threeTupleStreams[id]; 
                tuple.connexion = GetConnexion(address, port);
                return (StreamedTuple<T_X, T_Y, T_Z>)threeTupleStreams[id];
            }

            StreamedTuple<T_X, T_Y, T_Z> bs = (StreamedTuple<T_X, T_Y, T_Z>)new StreamedTuple<T_X, T_Y, T_Z>(GetConnexion(address, port), 
                id, milliseconds, cdr);
            threeTupleStreams.Add(id, (AbstractStreamedTuple)bs);
            return bs;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <typeparam name="T_Z">The Type of the third value of the tuple</typeparam>
        /// <param name="connexion">The stream to use to send the tuple</param>
        /// <param name="id">The channel id to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y, T_Z> GetStreamedTuple<T_X, T_Y, T_Z>(ServerConnexion connexion, 
            byte id, int milliseconds, ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
            where T_Z : IConvertible
        {
            StreamedTuple<T_X, T_Y, T_Z> tuple;
            if (threeTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<T_X, T_Y, T_Z>) threeTupleStreams[id];
                if (tuple.connexion == connexion)
                {
                    return tuple;
                }

                tuple.connexion = connexion;
                return tuple;
            }

            tuple = new StreamedTuple<T_X, T_Y, T_Z>(connexion, id, milliseconds, cdr);
            threeTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="id">The channel id to use for this two-tuple (unique to two-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y> GetStreamedTuple<T_X, T_Y>(string address, string port, byte id, int milliseconds,
            ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
        {
            StreamedTuple<T_X, T_Y> tuple;
            if (twoTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<T_X, T_Y>) twoTupleStreams[id];
                if (tuple.Address.Equals(address) && tuple.Port.Equals(port))
                {
                    return tuple;
                }

                tuple.connexion = GetConnexion(address, port);
                return tuple;
            }

            tuple = new StreamedTuple<T_X, T_Y>(GetConnexion(address, port), id, milliseconds, cdr);
            twoTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="T_X">The Type of the first value of the tuple</typeparam>
        /// <typeparam name="T_Y">The Type of the second value of the tuple</typeparam>
        /// <param name="connexion">The stream to use to send the tuple</param>
        /// <param name="id">The channel id to use for this three-tuple (unique to three-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X, T_Y> GetStreamedTuple<T_X, T_Y>(ServerConnexion connexion, byte id, int milliseconds,
            ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
            where T_Y : IConvertible
        {
            StreamedTuple<T_X, T_Y> tuple;
            if (twoTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<T_X, T_Y>)twoTupleStreams[id];
                if (tuple.connexion == connexion)
                {
                    return tuple;
                }

                tuple.connexion = connexion;
                return tuple;
            }

            tuple = new StreamedTuple<T_X, T_Y>(connexion, id, milliseconds, cdr);
            threeTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }

        /// <summary>Get a streaming tuple that is automatically sent to the server every so often</summary>
        /// <typeparam name="T_X">The Type of the value of the tuple</typeparam>
        /// <param name="address">The address to connect to</param>
        /// <param name="port">The port to connect to</param>
        /// <param name="id">The channel id to use for this one-tuple (unique to one-tuples)</param>
        /// <param name="milliseconds">The interval in milliseconds</param>
        /// <returns>The streaming tuple</returns>
        public IStreamedTuple<T_X> GetStreamedTuple<T_X>(string address, string port, byte id, int milliseconds,
            ChannelDeliveryRequirements cdr)
            where T_X : IConvertible
        {
            StreamedTuple<T_X> tuple;
            if (oneTupleStreams.ContainsKey(id))
            {
                tuple = (StreamedTuple<T_X>)oneTupleStreams[id];
                if (tuple.Address.Equals(address) && tuple.Port.Equals(port))
                {
                    return tuple;
                }

                tuple.connexion = GetConnexion(address, port);
                return tuple;
            }

            tuple = new StreamedTuple<T_X>(GetConnexion(address, port), id, milliseconds, cdr);
            oneTupleStreams.Add(id, (AbstractStreamedTuple)tuple);
            return tuple;
        }


        /// <summary>Gets a connexion for managing the session to this server.</summary>
        /// <param name="address">The address to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="port">The port to connect to.  Changes old connexion if id already claimed.</param>
        /// <param name="id">The channel id to claim or retrieve.</param>
        /// <returns>The created or retrived SessionStream</returns>
        public ISessionStream GetSessionStream(string address, string port, byte id, ChannelDeliveryRequirements cdr)
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

            SessionStream bs = new SessionStream(GetConnexion(address, port), id, cdr);
            sessionStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connexion for managing the session to this server.</summary>
        /// <param name="connexion">The connexion to use for the connexion.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim or retrieve.</param>
        /// <returns>The created or retrived SessionStream</returns>
        public ISessionStream GetSessionStream(ServerConnexion connexion, byte id, ChannelDeliveryRequirements cdr)
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

            SessionStream bs = new SessionStream(connexion, id, cdr);
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
        public IStringStream GetStringStream(string address, string port, byte id, ChannelDeliveryRequirements cdr)
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

            StringStream bs = new StringStream(GetConnexion(address, port), id, cdr);
            stringStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connexion for transmitting strings.</summary>
        /// <param name="connexion">The connexion to use for the connexion.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim.</param>
        /// <returns>The created or retrived StringStream</returns>
        public IStringStream GetStringStream(ServerConnexion connexion, byte id, ChannelDeliveryRequirements cdr)
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

            StringStream bs = new StringStream(connexion, id, cdr);
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
        public IObjectStream GetObjectStream(string address, string port, byte id, ChannelDeliveryRequirements cdr)
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
            ObjectStream bs = new ObjectStream(GetConnexion(address, port), id, cdr);
            objectStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connexion for transmitting objects.</summary>
        /// <param name="connexion">The connexion to use for the connexion.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim for this ObjectStream, unique for all ObjectStreams.</param>
        /// <returns>The created or retrived ObjectStream</returns>
        public IObjectStream GetObjectStream(ServerConnexion connexion, byte id, ChannelDeliveryRequirements cdr)
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
            ObjectStream bs = new ObjectStream(connexion, id, cdr);
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
        public IBinaryStream GetBinaryStream(string address, string port, byte id, ChannelDeliveryRequirements cdr)
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
            BinaryStream bs = new BinaryStream(GetConnexion(address, port), id, cdr);
            binaryStreams.Add(id, bs);
            return bs;
        }

        /// <summary>Gets a connexion for transmitting byte arrays.</summary>
        /// <param name="connexion">The connexion to use for the connexion.  Changes the server of id if the id is already claimed.</param>
        /// <param name="id">The channel id to claim for this BinaryStream, unique for all BinaryStreams.</param>
        /// <returns>The created or retrived BinaryStream.</returns>
        public IBinaryStream GetBinaryStream(ServerConnexion connexion, byte id, ChannelDeliveryRequirements cdr)
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
            BinaryStream bs = new BinaryStream(connexion, id, cdr);
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
            mySC.ErrorEvents += mySS_ErrorEvent;
            mySC.Start();
            connexions.Add(mySC);
            return mySC;
        }

        private void mySS_ErrorEvent(IConnexion ss, string explanation, object context)
        {
            if (ErrorEvent != null)
            {
                ErrorEvent(ss, explanation, context);
            }
        }

        /// <summary>One tick of the network beat.  Thread-safe.</summary>
        public void Update()
        {
            DebugUtils.WriteLine(this + ": Update() started");
            lock (this)
            {
                if (!started) { Start(); }  // deprecated behaviour
                timer.Update();
                if (timer.TimeInMilliseconds - lastPingTime > configuration.PingInterval.TotalMilliseconds)
                {
                    lastPingTime = timer.TimeInMilliseconds;
                    foreach (ServerConnexion s in connexions) { s.Ping(); }
                }
                foreach (ServerConnexion s in connexions)
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
                                case MessageType.System: HandleSystemMessage(m); break;
                                case MessageType.Binary: binaryStreams[m.Id].QueueMessage(m); break;
                                case MessageType.Object: objectStreams[m.Id].QueueMessage(m); break;
                                case MessageType.Session: sessionStreams[m.Id].QueueMessage(m); break;
                                case MessageType.String: stringStreams[m.Id].QueueMessage(m); break;
                                case MessageType.Tuple1D: oneTupleStreams[m.Id].QueueMessage(m); break;
                                case MessageType.Tuple2D: twoTupleStreams[m.Id].QueueMessage(m); break;
                                case MessageType.Tuple3D: threeTupleStreams[m.Id].QueueMessage(m); break;
                                default:
                                    // THIS IS NOT AN ERROR!
                                    Console.WriteLine("Client: WARNING: received message of unknown type: {1}",
                                        m);
                                    break;
                                }
                            }
                            catch (KeyNotFoundException e)
                            {
                                // THIS IS NOT AN ERROR!
                                Console.WriteLine("Client: WARNING: received message for unmonitored channel: {0}",
                                    m);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Client: ERROR: Exception occurred in Client.Update() processing stream {0}: {1}", s, e);
                        if (ErrorEvent != null)
                            ErrorEvent(s, "Exception occurred when trying to queue message.", e);
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
            Console.WriteLine("Client: WARNING: Unknown System Message: {0}", m);
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

        internal ChannelDeliveryRequirements GetChannelDeliveryRequirements(Message m)
        {
            switch (m.MessageType)
            {
            case MessageType.Binary: return binaryStreams[m.Id].ChannelDeliveryOptions;
            case MessageType.Object: return objectStreams[m.Id].ChannelDeliveryOptions;
            case MessageType.Session: return sessionStreams[m.Id].ChannelDeliveryOptions;
            case MessageType.String: return stringStreams[m.Id].ChannelDeliveryOptions;
            case MessageType.Tuple1D: return oneTupleStreams[m.Id].ChannelDeliveryOptions;
            case MessageType.Tuple2D: return twoTupleStreams[m.Id].ChannelDeliveryOptions;
            case MessageType.Tuple3D: return threeTupleStreams[m.Id].ChannelDeliveryOptions;

            case MessageType.System:
            default:
                throw new InvalidDataException();
            }
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder(GetType().Name);
            b.Append("(ids:");
            foreach(ServerConnexion c in connexions) {
                b.Append(' ');
                b.Append(c.UniqueIdentity);
            }
            b.Append(")");
            return b.ToString();
        }
    }
}
