using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace GTClient
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

    /// <summary>Possible message types for Messages to have.</summary>
    public enum MessageType
    {
        /// <summary>This message is a byte array</summary>
        Binary = 1,
        /// <summary>This message is an object</summary>
        Object = 2,
        /// <summary>This message is a string</summary>
        String = 3,
        /// <summary>This message is for the system, and special</summary>
        System = 4,
        /// <summary>This message refers to a session</summary>
        Session = 5,
        /// <summary>This message refers to a streaming 1-tuple</summary>
        Tuple1D = 6,
        /// <summary>This message refers to a streaming 2-tuple</summary>
        Tuple2D = 7,
        /// <summary>This message refers to a streaming 3-tuple</summary>
        Tuple3D = 8
    }

    /// <summary>Possible ways messages can be sent.</summary>
    public enum MessageProtocol
    {
        /// <summary>This message will be sent via TCP, and is very reliable.</summary>
        Tcp = 1,
        /// <summary>This message will be sent via UDP, and is not reliable at all.</summary>
        Udp = 2
    }

    /// <summary>Should this message be aggregated?</summary>
    public enum MessageAggregation
    {
        /// <summary>This message will be saved, and sent dependant on the specified message ordering</summary>
        Yes,
        /// <summary>This message will be sent immediately</summary>
        No
    }

    /// <summary>Which messages should be sent before this one?</summary>
    public enum MessageOrder
    {
        /// <summary>This message will flush all other saved-to-be-aggregated messages out beforehand</summary>
        All,
        /// <summary>This message will flush all other saved-to-be-aggregated messages on this channel out beforehand</summary>
        AllChannel,
        /// <summary>This message will be sent immediately, without worrying about any saved-to-be-aggregated messages</summary>
        None
    }

    /// <summary>Should receiving clients keep old messages?</summary>
    public enum MessageTimeliness
    {
        /// <summary>Throw away old messages</summary>
        RealTime,
        /// <summary>Keep old messages</summary>
        NonRealTime
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
    public delegate void ErrorEventHandler(Exception e, SocketError se, ServerStream ss, string explanation);
    /// <summary>Occurs whenever this client is updated.</summary>
    public delegate void UpdateEventDelegate(HPTimer hpTimer);

    #endregion


    #region Helper Classes

    /// <summary>A message from a session about id</summary>
    public class SessionMessage
    {
        /// <summary>What id did.</summary>
        public SessionAction Action;
        /// <summary>Who is doing it.</summary>
        public int ClientId;

        /// <summary>Create a new SessionMessage</summary>
        /// <param name="clientId">What id did.</param>
        /// <param name="e">Who is doing it.</param>
        internal SessionMessage(int clientId, SessionAction e)
        {
            this.ClientId = clientId;
            this.Action = e;
        }
    }

    /// <summary>Represents a message from the server.</summary>
    public class MessageIn
    {
        /// <summary>The channel that this message is on.</summary>
        public byte id;

        /// <summary>The type of message.</summary>
        public MessageType type;

        /// <summary>The data of the message.</summary>
        public byte[] data;

        /// <summary>The server that sent the message.</summary>
        public ServerStream ss;

        /// <summary>
        /// Creates a new message.
        /// </summary>
        /// <param name="id">The channel that this message is on.</param>
        /// <param name="type">The type of message.</param>
        /// <param name="data">The data of the message.</param>
        /// <param name="ss">The server that sent the message.</param>
        public MessageIn(byte id, MessageType type, byte[] data, ServerStream ss)
        {
            this.id = id;
            this.type = type;
            this.data = data;
            this.ss = ss;
        }
    }

    /// <summary>Represents a message going to the server.</summary>
    internal class MessageOut
    {
        /// <summary>The channel that this message is on.</summary>
        public byte id;

        /// <summary>The type of message.</summary>
        public MessageType type;

        /// <summary>The data of the message.</summary>
        public byte[] data;

        /// <summary>The ordering of the message.</summary>
        public MessageOrder ordering;

        /// <summary>
        /// Creates a new message.
        /// </summary>
        /// <param name="id">The channel that this message is on.</param>
        /// <param name="type">The type of message.</param>
        /// <param name="data">The data of the message.</param>
        /// <param name="ordering">The ordering of the message.</param>
        public MessageOut(byte id, MessageType type, byte[] data, MessageOrder ordering)
        {
            this.id = id;
            this.type = type;
            this.data = data;
            this.ordering = ordering;
        }
    }

    #endregion


    #region Type Stream Interfaces

    /// <summary>A connection of session events.</summary>
    public interface ISessionStream
    {
        /// <summary>Send a session action to the server</summary>
        /// <param name="action">The action</param>
        void Send(SessionAction action);

        /// <summary>Send a session action to the server</summary>
        /// <param name="action">The action</param>
        /// <param name="reli">How to send it</param>
        void Send(SessionAction action, MessageProtocol reli);

        /// <summary>Send a session action to the server</summary>
        /// <param name="action">The action</param>
        /// <param name="reli">How to send it</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages</param>
        void Send(SessionAction action, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering);

        /// <summary>Take a SessionMessage off the queue of received messages.</summary>
        /// <param name="index">Which one to take off, the higher number being newer messages.</param>
        /// <returns>The message.</returns>
        SessionMessage DequeueMessage(int index);

        /// <summary>Received messages from the server.</summary>
        List<MessageIn> Messages { get; }

        /// <summary>Average latency between the client and this particluar server.</summary>
        float Delay { get; }

        /// <summary> Occurs when this connection receives a message. </summary>
        event SessionNewMessage SessionNewMessageEvent;

        /// <summary> Get the unique identity of the client for this server.  This will be
        /// different for each server, and thus could be different for each connection. </summary>
        int UniqueIdentity { get; }

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol);

        /// <summary>Occurs whenever this client is updated.</summary>
        event UpdateEventDelegate UpdateEvent;
    }

    /// <summary>A connection of strings.</summary>
    public interface IStringStream
    {
        /// <summary>Send a string to the server</summary>
        /// <param name="s">The string</param>
        void Send(string s);

        /// <summary>Send a string to the server, specifying how.</summary>
        /// <param name="s">The string to send.</param>
        /// <param name="reli">What method to send it by.</param>
        void Send(string s, MessageProtocol reli);

        /// <summary>Send a string to the server, specifying how.</summary>
        /// <param name="s">The string to send.</param>
        /// <param name="reli">What method to send it by.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        void Send(string s, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering);

        /// <summary>Take a String off the queue of received messages.</summary>
        /// <param name="index">Which message to take, with higher numbers being newer messages.</param>
        /// <returns>The message.</returns>
        string DequeueMessage(int index);

        /// <summary>Received messages from the server.</summary>
        List<MessageIn> Messages { get; }

        /// <summary>Average latency between the client and this particluar server.</summary>
        float Delay { get; }

        /// <summary> Occurs when this connection receives a message. </summary>
        event StringNewMessage StringNewMessageEvent;

        /// <summary>Get the unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connection.</summary>
        int UniqueIdentity { get; }

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol);

        /// <summary>Occurs whenever this client is updated.</summary>
        event UpdateEventDelegate UpdateEvent;
    }

    /// <summary>A connection of objects.</summary>
    public interface IObjectStream
    {
        /// <summary>Send an object.</summary>
        /// <param name="o">The object to send.</param>
        void Send(object o);

        /// <summary>Send an object using the specified method.</summary>
        /// <param name="o">The object to send.</param>
        /// <param name="reli">How to send the object.</param>
        void Send(object o, MessageProtocol reli);

        /// <summary>Send an object using the specified method.</summary>
        /// <param name="o">The object to send.</param>
        /// <param name="reli">How to send the object.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        void Send(object o, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering);

        /// <summary>Dequeues an object from the message list.</summary>
        /// <param name="index">Which to dequeue, where a higher number means a newer message.</param>
        /// <returns>The object that was there.</returns>
        object DequeueMessage(int index);

        /// <summary>Received messages from the server.</summary>
        List<MessageIn> Messages { get; }

        /// <summary>Average latency between the client and this particluar server.</summary>
        float Delay { get; }

        /// <summary> Occurs when this connection receives a message. </summary>
        event ObjectNewMessage ObjectNewMessageEvent;

        /// <summary>Get the unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connection.
        /// </summary>
        int UniqueIdentity { get; }

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol);

        /// <summary>Occurs whenever this client is updated.</summary>
        event UpdateEventDelegate UpdateEvent;
    }

    /// <summary>A connection of byte arrays.</summary>
    public interface IBinaryStream
    {
        /// <summary>Send a byte array</summary>
        /// <param name="b">The byte array to send</param>
        void Send(byte[] b);

        /// <summary>Send a byte array in the specified way</summary>
        /// <param name="b">The byte array to send</param>
        /// <param name="reli">The way to send it</param>
        void Send(byte[] b, MessageProtocol reli);

        /// <summary>Send a byte array in the specified way</summary>
        /// <param name="b">The byte array to send</param>
        /// <param name="reli">The way to send it</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages</param>
        void Send(byte[] b, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering);

        /// <summary>Takes a message from the message list</summary>
        /// <param name="index">The message to take, where a higher number means a newer message</param>
        /// <returns>The byte array of the message</returns>
        byte[] DequeueMessage(int index);

        /// <summary>Received messages from the server.</summary>
        List<MessageIn> Messages { get; }

        /// <summary>Average latency between the client and this particluar server.</summary>
        float Delay { get; }

        /// <summary> Occurs when this connection receives a message. </summary>
        event BinaryNewMessage BinaryNewMessageEvent;

        /// <summary>Get the unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connection.
        /// </summary>
        int UniqueIdentity { get; }

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol);

        /// <summary>Occurs whenever this client is updated.</summary>
        event UpdateEventDelegate UpdateEvent;
    }

    #endregion


    #region Type Stream Implementations

    /// <summary>A connection of session events.</summary>
    public class SessionStream : ISessionStream
    {
        private List<MessageIn> messages;
        private byte id;
        internal ServerStream connection;

        /// <summary> Occurs when client is updated. </summary>
        public event UpdateEventDelegate UpdateEvent;

        /// <summary> This SessionStream's ID. </summary>
        public byte ID { get { return id; } }

        /// <summary> This SessionStream uses this ServerStream. </summary>
        public ServerStream Connection { get { return connection; } }

        /// <summary> Occurs when this session receives a message. </summary>
        public event SessionNewMessage SessionNewMessageEvent;

        /// <summary>Average latency between the client and this particluar server.</summary>
        public float Delay { get { return connection.Delay; } }

        /// <summary>Received messages from the server.</summary>
        public List<MessageIn> Messages { get { return messages; } }

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

        /// <summary>Create a SessionStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">What channel this will be.</param>
        internal SessionStream(ServerStream stream, byte id)
        {
            messages = new List<MessageIn>();
            this.connection = stream;
            this.id = id;
        }

        /// <summary>Send a session action to the server.</summary>
        /// <param name="action">The action.</param>
        public void Send(SessionAction action)
        {
            Send(action, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
        }

        /// <summary>Send a session action to the server.</summary>
        /// <param name="action">The action.</param>
        /// <param name="reli">How to send it.</param>
        public void Send(SessionAction action, MessageProtocol reli)
        {
            Send(action, reli, MessageAggregation.No, MessageOrder.None);
        }

        /// <summary>Send a session action to the server.</summary>
        /// <param name="action">The action.</param>
        /// <param name="reli">How to send it.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        public void Send(SessionAction action, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            byte[] buffer = new byte[1];
            buffer[0] = (byte) action;
            this.connection.Send(buffer, id, MessageType.Session, reli, aggr, ordering);
        }

        /// <summary>Take a SessionMessage off the queue of received messages.</summary>
        /// <param name="index">Which one to take off, the higher number being newer messages.</param>
        /// <returns>The message.</returns>
        public SessionMessage DequeueMessage(int index)
        {
            try
            {
                byte[] b;
                if (index >= messages.Count)
                    return null;

                lock (messages)
                {
                    b = messages[index].data;
                    messages.RemoveAt(index);
                }

                int id = BitConverter.ToInt32(b, 0);

                return new SessionMessage(id, (SessionAction)b[4]);
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
        internal void QueueMessage(MessageIn message)
        {
            messages.Add(message);
            if(SessionNewMessageEvent != null)
                SessionNewMessageEvent(this);
        }

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        public void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol)
        {
            connection.FlushOutgoingMessages(this.id, protocol);
        }

        internal void Update(HPTimer hpTimer)
        {
            if (UpdateEvent != null)
                UpdateEvent(hpTimer);
        }
    }

    /// <summary>A connection of strings.</summary>
    public class StringStream : IStringStream
    {
        private List<MessageIn> messages;
        private byte id;
        internal ServerStream connection;

        /// <summary> Occurs when client is updated. </summary>
        public event UpdateEventDelegate UpdateEvent;

        /// <summary> This StringStream's ID. </summary>
        public byte ID { get { return id; } }

        /// <summary> This StringStream uses this ServerStream. </summary>
        public ServerStream Connection { get { return connection; } }

        /// <summary> Occurs when this connection receives a message. </summary>
        public event StringNewMessage StringNewMessageEvent;

        /// <summary>Average latency between the client and this particluar server.</summary>
        public float Delay { get { return connection.Delay; } }

        /// <summary>Received messages from the server.</summary>
        public List<MessageIn> Messages { get { return messages; } }

        /// <summary>Get the unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connection.</summary>
        public int UniqueIdentity
        {
            get { return connection.UniqueIdentity; }
        }

        /// <summary>Get the connection's destination address</summary>
        public string Address
        {
            get { return connection.Address; }
        }

        /// <summary>Get the connection's destination port</summary>
        public string Port
        {
            get { return connection.Port; }
        }

        /// <summary>Create a StringStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the messages.</param>
        /// <param name="id">What channel this will be.</param>
        internal StringStream(ServerStream stream, byte id)
        {
            messages = new List<MessageIn>();
            this.connection = stream;
            this.id = id;
        }

        /// <summary>Send a string to the server.</summary>
        /// <param name="b">The string.</param>
        public void Send(string b)
        {
            Send(b, MessageProtocol.Tcp);
        }

        /// <summary>Send a string to the server, specifying how.</summary>
        /// <param name="b">The string to send.</param>
        /// <param name="reli">What method to send it by.</param>
        public void Send(string b, MessageProtocol reli)
        {
            Send(b, reli, MessageAggregation.No, MessageOrder.None);
        }

        /// <summary>Send a string to the server, specifying how.</summary>
        /// <param name="b">The string to send.</param>
        /// <param name="reli">What method to send it by.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        public void Send(string b, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            byte[] buffer = System.Text.ASCIIEncoding.ASCII.GetBytes(b);
            this.connection.Send(buffer, id, MessageType.String, reli, aggr, ordering);
        }

        /// <summary>Take a String off the queue of received messages.</summary>
        /// <param name="index">Which message to take, with higher numbers being newer messages.</param>
        /// <returns>The message.</returns>
        public string DequeueMessage(int index)
        {
            try
            {
                string s;
                if (index >= messages.Count)
                    return null;

                lock (messages)
                {
                    s = System.Text.ASCIIEncoding.ASCII.GetString(messages[index].data);
                    messages.RemoveAt(index);
                }
                return s;
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
        internal void QueueMessage(MessageIn message)
        {
            messages.Add(message);
            if (StringNewMessageEvent != null)
                StringNewMessageEvent(this);
        }

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        public void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol)
        {
            connection.FlushOutgoingMessages(this.id, protocol);
        }

        internal void Update(HPTimer hpTimer)
        {
            if (UpdateEvent != null)
                UpdateEvent(hpTimer);
        }
    }

    /// <summary>A connection of Objects.</summary>
    public class ObjectStream : IObjectStream
    {
        private List<MessageIn> messages;
        private static BinaryFormatter formatter = new BinaryFormatter();
        private byte id;
        internal ServerStream connection;

        /// <summary> Occurs when client is updated. </summary>
        public event UpdateEventDelegate UpdateEvent;

        /// <summary> This StringStream's ID. </summary>
        public byte ID { get { return id; } }

        /// <summary> This StringStream uses this ServerStream.</summary>
        public ServerStream Connection { get { return connection; } }

        /// <summary> Occurs when this connection receives a message. </summary>
        public event ObjectNewMessage ObjectNewMessageEvent;

        /// <summary>Average latency between the client and this particluar server.</summary>
        public float Delay { get { return connection.Delay; } }

        /// <summary>Received messages from the server.</summary>
        public List<MessageIn> Messages
        {
            get
            {
                return messages;
            }
        }

        /// <summary>Get the unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connection.
        /// </summary>
        public int UniqueIdentity
        {
            get { return connection.UniqueIdentity; }
        }

        /// <summary>Get the connection's destination address</summary>
        public string Address
        {
            get { return connection.Address; }
        }

        /// <summary>Get the connection's destination port</summary>
        public string Port
        {
            get { return connection.Port; }
        }

        /// <summary>Create an ObjectStream object.</summary>
        /// <param name="stream">The SuperStream to use to actually send the objects.</param>
        /// <param name="id">The channel we will claim.</param>
        internal ObjectStream(ServerStream stream, byte id)
        {
            messages = new List<MessageIn>();
            this.connection = stream;
            this.id = id;
        }

        /// <summary>Send an object.</summary>
        /// <param name="o">The object to send.</param>
        public void Send(object o)
        {
            Send(o, MessageProtocol.Tcp);
        }

        /// <summary>Send an object using the specified method.</summary>
        /// <param name="o">The object to send.</param>
        /// <param name="reli">How to send the object.</param>
        public void Send(object o, MessageProtocol reli)
        {
            Send(o, reli, MessageAggregation.No, MessageOrder.None);
        }

        /// <summary>Send an object using the specified method.</summary>
        /// <param name="o">The object to send.</param>
        /// <param name="reli">How to send the object.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        public void Send(object o, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, o);
            byte[] buffer = new byte[ms.Position];
            ms.Position = 0;
            ms.Read(buffer, 0, buffer.Length);
            this.connection.Send(buffer, id, MessageType.Object, reli, aggr, ordering);
        }

        /// <summary>Dequeues an object from the message list.</summary>
        /// <param name="index">Which to dequeue, where a higher number means a newer message.</param>
        /// <returns>The object that was there.</returns>
        public object DequeueMessage(int index)
        {
            try
            {
                if (index >= messages.Count)
                    return null;

                MemoryStream ms = new MemoryStream();
                lock (messages)
                {
                    ms.Write(messages[index].data, 0, messages[index].data.Length);
                    messages.RemoveAt(index);
                }
                ms.Position = 0;
                return formatter.Deserialize(ms);
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
        internal void QueueMessage(MessageIn message)
        {
            messages.Add(message);
            if (ObjectNewMessageEvent != null)
                ObjectNewMessageEvent(this);
        }

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        public void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol)
        {
            connection.FlushOutgoingMessages(this.id, protocol);
        }

        internal void Update(HPTimer hpTimer)
        {
            if (UpdateEvent != null)
                UpdateEvent(hpTimer);
        }
    }

    /// <summary>A connection of byte arrays.</summary>
    public class BinaryStream : IBinaryStream
    {
        private List<MessageIn> messages;
        private byte id;
        internal ServerStream connection;

        /// <summary> Occurs when client is updated. </summary>
        public event UpdateEventDelegate UpdateEvent;

        /// <summary> This StringStream's ID. </summary>
        public byte ID { get { return id; } }

        /// <summary> This StringStream uses this ServerStream.</summary>
        public ServerStream Connection { get { return connection; } }

        /// <summary> Occurs when this connection receives a message. </summary>
        public event BinaryNewMessage BinaryNewMessageEvent;

        /// <summary>Average latency between the client and this particluar server.</summary>
        public float Delay { get { return connection.Delay; } }

        /// <summary>Received messages from the server.</summary>
        public List<MessageIn> Messages
        {
            get
            {
                return messages;
            }
        }

        /// <summary>Get the unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connection.
        /// </summary>
        public int UniqueIdentity
        {
            get { return connection.UniqueIdentity; }
        }

        /// <summary>Get the connection's destination address</summary>
        public string Address
        {
            get { return connection.Address; }
        }

        /// <summary>Get the connection's destination port</summary>
        public string Port
        {
            get { return connection.Port; }
        }

        /// <summary>Creates a BinaryStream object.</summary>
        /// <param name="stream">The SuperStream object on which to actually send the objects.</param>
        /// <param name="id">The channel to claim.</param>
        internal BinaryStream(ServerStream stream, byte id)
        {
            messages = new List<MessageIn>();
            this.connection = stream;
            this.id = id;
        }

        /// <summary>Send a byte array.</summary>
        /// <param name="b">The byte array to send.</param>
        public void Send(byte[] b)
        {
            this.connection.Send(b, id, MessageType.Binary, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
        }

        /// <summary>Send a byte array in the specified way.</summary>
        /// <param name="b">The byte array to send.</param>
        /// <param name="reli">The way to send it.</param>
        public void Send(byte[] b, MessageProtocol reli)
        {
            this.connection.Send(b, id, MessageType.Binary, reli, MessageAggregation.No, MessageOrder.None);
        }

        /// <summary>Send a byte array in the specified way.</summary>
        /// <param name="b">The byte array to send.</param>
        /// <param name="reli">The way to send it.</param>
        /// <param name="aggr">Should the message be saved to be packed with others?</param>
        /// <param name="ordering">In what order should this message be sent in relation to other aggregated messages.</param>
        public void Send(byte[] b, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
        {
            this.connection.Send(b, id, MessageType.Binary, reli, aggr, ordering);
        }

        /// <summary>Takes a message from the message list.</summary>
        /// <param name="index">The message to take, where a higher number means a newer message.</param>
        /// <returns>The byte array of the message.</returns>
        public byte[] DequeueMessage(int index)
        {
            try
            {
                byte[] b;
                if (index >= messages.Count)
                    return null;

                lock (messages)
                {
                    b = messages[index].data;
                    messages.RemoveAt(index);
                }
                return b;
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
        internal void QueueMessage(MessageIn message)
        {
            messages.Add(message);
            if (BinaryNewMessageEvent != null)
                    BinaryNewMessageEvent(this);
        }

        /// <summary>Flush all aggregated messages on this connection</summary>
        /// <param name="protocol"></param>
        public void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol)
        {
            connection.FlushOutgoingMessages(this.id, protocol);
        }

        internal void Update(HPTimer hpTimer)
        {
            if (UpdateEvent != null)
                UpdateEvent(hpTimer);
        }
    }

    #endregion


    #region Tuple Implementations

    /// <summary>
    /// Represents a tuple that automatically sends itself, and receives sends from others
    /// </summary>
    public abstract class StreamedTuple 
    {
        /// <summary>
        /// The address that this tuple sends to
        /// </summary>
        abstract public string Address { get; }

        /// <summary>
        /// The port that this tuple sends to
        /// </summary>
        abstract public string Port { get; }

        /// <summary>
        /// The network connection of this tuple
        /// </summary>
        abstract public ServerStream Connection { get; }

        abstract internal void Update(HPTimer hpTimer);

        abstract internal void QueueMessage(MessageIn m);
    }

    #endregion


    /// <summary>Controls the sending of messages to a particular server.</summary>
    public class ServerStream
    {
        private const int bufferSize = 512;
        private string address, port;
        private TcpClient tcpClient;
        private MemoryStream tcpIn;
        private int tcpInBytesLeft;
        private int tcpInMessageType;
        private byte tcpInID;
        private UdpClient udpClient;
        private IPEndPoint endPoint;
        private bool dead;
        internal double NextPingTime = 0;

        /// <summary>The last error encountered</summary>
        public Exception LastError = null;
        /// <summary>Occurs when there is an error.</summary>
        public event ErrorEventHandler ErrorEvent;
        internal ErrorEventHandler ErrorEventDelegate;

        //used in various places to build up buffers of messages.  having it ready is fast.
        private MemoryStream finalBuffer = new MemoryStream();

        private List<MessageOut> MessageOutFreePool = new List<MessageOut>();
        private List<MessageOut> MessageOutAwayPoolTCP = new List<MessageOut>();
        private List<MessageOut> MessageOutAwayPoolUDP = new List<MessageOut>();

        //If bits can't be written to the network now, try later
        //We use this so that we don't have to block on the writing to the network
        private List<byte[]> tcpOut = new List<byte[]>();
        private List<byte[]> udpOut = new List<byte[]>();

        /// <summary>The average amount of latency between this client and the server.</summary>
        public float Delay = 20f;

        /// <summary>The unique identity of the client for this server.
        /// This will be different for each server, and thus could be different for each connection.</summary>
        public int UniqueIdentity;

        /// <summary>Messages from the Server.</summary>
        internal List<MessageIn> messages;

        /// <summary>What is the address of the server?  Thread-safe.</summary>
        public string Address
        {
            get
            {
                return address;
            }
        }

        /// <summary>What is the TCP port of the server.  Thread-safe.</summary>
        public string Port
        {
            get
            {
                return port;
            }
        }

        /// <summary>Is this connection dead?
        /// If set to true, then this connection is killed.
        /// If set to false while this connection is dead, then this connection will be reestablished.</summary>
        public bool Dead
        {
            get
            {
                return dead;
            }
            set
            {
                if (value)
                {
                    //kill the connection as best we can
                    lock (this)
                    {
                        dead = true;

                        try
                        {
                            tcpClient.Client.LingerState.Enabled = false;
                            tcpClient.Client.ExclusiveAddressUse = false;
                            tcpClient.Client.Close();
                        }
                        catch (Exception) { }

                        try
                        {
                            udpClient.Client.LingerState.Enabled = false;
                            udpClient.Client.ExclusiveAddressUse = false;
                            udpClient.Client.Close();
                        }
                        catch (Exception) { }
                    }
                }
                else if(dead)
                {
                    //we are dead, but they want us to live again.  Reconnect!
                    Reconnect();
                }
            }
        }

        /// <summary>Reset the superstream and reconnect to the server (blocks)</summary>
        private void Reconnect()
        {
            lock (this)
            {
                dead = true;

                tcpIn = new MemoryStream();
                messages = new List<MessageIn>();

                IPHostEntry he = Dns.Resolve(address);
                IPAddress[] addr = he.AddressList;

                //try to connect to the address
                Exception error = null;
                for (int i = 0; i < addr.Length; i++)
                {
                    try
                    {
                        endPoint = new IPEndPoint(addr[0], Int32.Parse(port));
                        tcpClient = new TcpClient();
                        tcpClient.NoDelay = true;
                        tcpClient.ReceiveTimeout = 1;
                        tcpClient.SendTimeout = 1;
                        tcpClient.Connect(endPoint);
                        tcpClient.Client.Blocking = false;
                        error = null;
                        break;
                    }
                    catch (Exception e)
                    {
                        error = new Exception("There was a problem connecting to the server you specified. " +
                        "The address or port you provided may be improper, the receiving server may be down, " +
                        "full, or unavailable, or your system's host file may be corrupted. " +
                        "See inner exception for details.", e);
                    }
                }

                if (error != null)
                    throw error;

                Console.WriteLine("Address resolved and contacted.  Now connected to " + endPoint.ToString());

                //prepare udp
                udpClient = new UdpClient();
                udpClient.DontFragment = true;
                udpClient.Client.Blocking = false;
                udpClient.Client.SendTimeout = 1;
                udpClient.Client.ReceiveTimeout = 1;
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                //we are now good to go
                dead = false;
            }

            //request our id right away
            byte[] b = new byte[1];
            b[0] = 1;
            Send(b, (byte)SystemMessageType.UniqueIDRequest, MessageType.System, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
        }

        /// <summary>Send TCP message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        private void SendTcpMessage(byte[] buffer)
        {
            if (TryAndFlushOldTcpBytesOut())
            {
                tcpOut.Add(buffer);
                return;
            }

            SocketError error = SocketError.Success; //added
            try
            {
                tcpClient.Client.Send(buffer, 0, buffer.Length, SocketFlags.None, out error);

                switch (error)
                {
                    case SocketError.Success:
                        return;
                    case SocketError.WouldBlock:
                        tcpOut.Add(buffer);
                        LastError = null;
                        if (ErrorEvent != null)
                            ErrorEvent(null, error, this, "The TCP write buffer is full now, but the data will be saved and " +
                                "sent soon.  Send less data to reduce perceived latency.");
                        return;
                    default:
                        tcpOut.Add(buffer);
                        LastError = null;
                        if (ErrorEvent != null)
                            ErrorEvent(null, error, this, "Failed to Send TCP Message.");
                        dead = true;
                        return;
                }
            }
            catch (Exception e)
            {
                //something awful happened
                tcpOut.Add(buffer);
                LastError = e;
                if (ErrorEvent != null)
                    ErrorEvent(e, SocketError.NoRecovery, this, "Failed to Send to TCP.  See Exception for details.");
                dead = true;
            }
        }

        /// <summary>Send UDP message to server.</summary>
        /// <param name="buffer">The message to send.</param>
        private void SendUdpMessage(byte[] buffer)
        {
            if (udpClient.Client.Connected)
            {
                //if there is old stuff to send yet, try the old stuff first
                //before sending the new stuff
                if (TryAndFlushOldUdpBytesOut())
                {
                    udpOut.Add(buffer);
                    return;
                }
                //if it's all clear, send the new stuff
                
                SocketError error = SocketError.Success; //added
                try
                {
                    udpClient.Client.Send(buffer, 0, buffer.Length, SocketFlags.None, out error);
                    switch (error)
                    {
                        case SocketError.Success:
                            return;
                        case SocketError.WouldBlock:
                            tcpOut.Add(buffer);
                            LastError = null;
                            if (ErrorEvent != null)
                                ErrorEvent(null, error, this, "The UDP write buffer is full now, but the data will be saved and " +
                                    "sent soon.  Send less data to reduce perceived latency.");
                            return;
                        default:
                            tcpOut.Add(buffer);
                            LastError = null;
                            if (ErrorEvent != null)
                                ErrorEvent(null, error, this, "Failed to Send UDP Message.");
                            return;
                    }
                }
                catch (Exception e)
                {
                    //something awful happened
                    udpOut.Add(buffer);
                    LastError = e;
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.Fault, this, "Failed to Send UDP Message (" + buffer.Length +
                            " bytes) because of an exception: " + buffer.ToString());
                }
                
            }
            else
            {
                //save this data to be sent later
                udpOut.Add(buffer);
                //request the server for the port to send to
                byte[] data = BitConverter.GetBytes((short)(((IPEndPoint)udpClient.Client.LocalEndPoint).Port));
                try
                {
                    Send(data, (byte)SystemMessageType.UDPPortRequest, MessageType.System, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
                }
                catch (Exception e)
                {
                    //don't save this or die, but still throw an error
                    LastError = e;
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.Fault, this, "Failed to Request UDP port from server.");
                }
            }
        }

        /// <summary>Adds the message to a list, waiting to be sent out.</summary>
        /// <param name="data">The data that will wait</param>
        /// <param name="id">The channel it will be sent on</param>
        /// <param name="type">The type of data this is</param>
        /// <param name="ordering">The ordering of how it will be sent</param>
        /// <param name="protocol">How it should be sent out</param>
        private void Aggregate(byte[] data, byte id, MessageType type, 
            MessageProtocol protocol, MessageOrder ordering)
        {
            MessageOut message;
            if (MessageOutFreePool.Count < 1)
                message = new MessageOut(id, type, data, ordering);
            else
            {
                message = MessageOutFreePool[0];
                MessageOutFreePool.Remove(message);
                message.data = data;
                message.id = id;
                message.type = type;
                message.ordering = ordering;
            }


            if (protocol == MessageProtocol.Tcp)
                MessageOutAwayPoolTCP.Add(message);
            else
                MessageOutAwayPoolUDP.Add(message);
        }

        /// <summary>Send a message using these parameters.</summary>
        /// <param name="data">The data to send.</param>
        /// <param name="id">What channel to send it on.</param>
        /// <param name="type">What type of data it is.</param>
        /// <param name="protocol">How to send it.</param>
        /// <param name="aggr">Should this data be aggregated?</param>
        /// <param name="ordering">What data should be sent before this?</param>
        internal void Send(byte[] data, byte id, MessageType type, MessageProtocol protocol, 
            MessageAggregation aggr, MessageOrder ordering)
        {
            lock (this)
            {
                if (dead)
                    return;

                if (aggr == MessageAggregation.Yes)
                {
                    //Wait to send this data, hopefully to pack it with later data.
                    Aggregate(data, id, type, protocol, ordering);
                    return;
                }

                if (ordering == MessageOrder.All)
                {
                    //Make sure ALL messages on the CLIENT are sent,
                    //then send this one.
                    Aggregate(data, id, type, protocol, ordering);
                    FlushOutgoingMessages(protocol);
                    return;
                }
                else if (ordering == MessageOrder.AllChannel)
                {
                    //Make sure ALL other messages on this CHANNEL are sent,
                    //then send this one.
                    Aggregate(data, id, type, protocol, ordering);
                    FlushOutgoingMessages(id, protocol);
                    return;
                }

                //pack main message into a buffer and send it right away
                byte[] buffer = new byte[data.Length + 8];
                buffer[0] = id;
                buffer[1] = (byte)type;
                BitConverter.GetBytes(data.Length).CopyTo(buffer, 4);
                data.CopyTo(buffer, 8);

                //Actually send the data.  Note that after this point, ordering and 
                //aggregation are locked in / already decided.
                //If you're here, then you're not aggregating and you're not concerned 
                //about ordering.
                if (protocol == MessageProtocol.Tcp)
                    SendTcpMessage(buffer);
                else
                    SendUdpMessage(buffer);
            }
        }

        /// <summary>Flushes outgoing tcp or udp messages on this channel only</summary>
        internal void FlushOutgoingMessages(byte id, MessageProtocol protocol)
        {
            const int maxSize = 512;
            MessageOut message;
            List<MessageOut> list;
            List<MessageOut> chosenMessages = new List<MessageOut>(8);
            byte[] buffer;

            //get the correct list
            if(protocol == MessageProtocol.Tcp)
                list = (List<MessageOut>)this.MessageOutAwayPoolTCP;
            else
                list = (List<MessageOut>)this.MessageOutAwayPoolUDP;

            if (list.Count == 0)
                return;

            lock (finalBuffer)
            {
                while (true)
                {
                    //Find first message in this channel
                    if ((message = FindFirstChannelMessageInList(id, list)) == null)
                        break;

                    //if packing a packet, and the packet is full
                    if (protocol == MessageProtocol.Udp && (finalBuffer.Position + message.data.Length + 8 > maxSize))
                        break;

                    //remove this message
                    list.Remove(message);

                    //pack the message into the buffer
                    buffer = new byte[message.data.Length + 8];
                    buffer[0] = (byte)message.id;
                    buffer[1] = (byte)message.type;
                    BitConverter.GetBytes(message.data.Length).CopyTo(buffer, 4);
                    message.data.CopyTo(buffer, 8);

                    finalBuffer.Write(buffer, 0, buffer.Length);

                    MessageOutFreePool.Add(message);
                }

                buffer = new byte[finalBuffer.Position];
                finalBuffer.Position = 0;
                finalBuffer.Read(buffer, 0, buffer.Length);
                finalBuffer.Position = 0;
            }

            //Actually send the data.  Note that after this point, ordering and 
            //aggregation are locked in / already decided.
            if (protocol == MessageProtocol.Tcp)
                SendTcpMessage(buffer);
            if (protocol == MessageProtocol.Udp)
                SendUdpMessage(buffer);

            //if more messages to flush on this channel, repeat above process again
            if ((message = FindFirstChannelMessageInList(id, list)) != null)
                FlushOutgoingMessages(id, protocol);
        }

        /// <summary>Flushes outgoing tcp or udp messages</summary>
        private void FlushOutgoingMessages(MessageProtocol protocol)
        {
            const int maxSize = 512;
            MessageOut message;
            List<MessageOut> list;
            
            byte[] buffer;

            //get the correct list
            if(protocol == MessageProtocol.Tcp)
                list = (List<MessageOut>)this.MessageOutAwayPoolTCP;
            else
                list = (List<MessageOut>)this.MessageOutAwayPoolUDP;

            //finalBuffer is a memory connection that we keep around so we don't have to recreate it each time
            lock (finalBuffer)
            {
                while (true)
                {
                    //if there are no more messages in the list
                    if ((message = FindFirstMessageInList(list)) == null)
                        break;

                    //if we're packing a udp packet with messages, and it's full
                    if (protocol == MessageProtocol.Udp && (finalBuffer.Position + message.data.Length + 8 < maxSize))
                        break;

                    //remove this message from the list of messages to be packed
                    list.Remove(message);

                    //pack the message into the buffer
                    buffer = new byte[message.data.Length + 8];
                    buffer[0] = (byte)message.id;
                    buffer[1] = (byte)message.type;
                    BitConverter.GetBytes(message.data.Length).CopyTo(buffer, 4);
                    message.data.CopyTo(buffer, 8);

                    finalBuffer.Write(buffer, 0, buffer.Length);

                    MessageOutFreePool.Add(message);
                }

                //convert the memory connection into an array, and reset it back to position zero.
                //TODO: check the size of the memory connection to make sure it has not become too large.
                buffer = new byte[finalBuffer.Position];
                finalBuffer.Position = 0;
                finalBuffer.Read(buffer, 0, buffer.Length);
                finalBuffer.Position = 0;
            }

            //Actually send the data.  Note that after this point, ordering and 
            //aggregation are locked in / already decided.
            if (protocol == MessageProtocol.Tcp)
                SendTcpMessage(buffer);
            if (protocol == MessageProtocol.Udp)
                SendUdpMessage(buffer);

            //if there is more data to flush, do it
            if (list.Count > 0)
                FlushOutgoingMessages(protocol);
        }

        /// <summary>Return the first message of a certain channel in a list</summary>
        /// <param name="id">The channel id</param>
        /// <param name="list">The list</param>
        /// <returns>The first message of this channel</returns>
        private MessageOut FindFirstChannelMessageInList(byte id, List<MessageOut> list)
        {
            foreach (MessageOut message in list)
            {
                if (message.id == id)
                    return message;
            }
            return null;
        }

        /// <summary>Return the first message in a list of messages</summary>
        /// <param name="list">The list</param>
        /// <returns>The first message</returns>
        private MessageOut FindFirstMessageInList(List<MessageOut> list)
        {
            if (list.Count > 0)
                return list[0];
            return null;
        }

        /// <summary> Flushes out old messages that couldn't be sent because of exceptions</summary>
        /// <returns>True if there are bytes that still have to be sent out</returns>
        private bool TryAndFlushOldTcpBytesOut()
        {
            byte[] b;
            SocketError error = SocketError.Success;

            try
            {
                while (tcpOut.Count > 0)
                {
                    b = tcpOut[0];
                    tcpClient.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

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

        /// <summary> Flushes out old messages that couldn't be sent because of exceptions</summary>
        /// <returns>True if there are bytes that still have to be sent out</returns>
        private bool TryAndFlushOldUdpBytesOut()
        {
            byte[] b;
            SocketError error = SocketError.Success;

            try
            {
                while (udpOut.Count > 0 && udpClient.Client.Connected)
                {
                    b = udpOut[0];
                    udpClient.Client.Send(b, 0, b.Length, SocketFlags.None, out error);

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

        /// <summary>A single tick of the SuperStream.</summary>
        internal void Update()
        {
            lock (this)
            {
                if (!tcpClient.Connected || dead)
                {
                    dead = true;
                    return;
                }

                TryAndFlushOldTcpBytesOut();
                TryAndFlushOldUdpBytesOut();

                while (tcpClient.Available > 0)
                {
                    try
                    {
                        this.UpdateFromNetworkTcp();
                    }
                    catch (Exception e)
                    {
                        dead = true;
                        LastError = e;
                        if (ErrorEvent != null)
                            ErrorEvent(e, SocketError.NoRecovery, this, "Updating from TCP failed because of an exception.");
                    }
                }

                this.UpdateFromNetworkUdp();
            }
        }

        /// <summary>Get data from the UDP connection and interpret them.</summary>
        private void UpdateFromNetworkUdp()
        {
            byte[] buffer, data;
            byte id, type;
            int length, cursor;
            IPEndPoint ep = null;

            try
            {
                //while there are more packets to read
                while (udpClient.Client.Available > 8)
                {
                    buffer = udpClient.Receive(ref ep);
                    cursor = 0;

                    //while there are more messages in this packet
                    while (cursor < buffer.Length)
                    {
                        id = buffer[cursor + 0];
                        type = buffer[cursor + 1];
                        length = BitConverter.ToInt32(buffer, cursor + 4);
                        data = new byte[length];
                        Array.Copy(buffer, 8, data, 0, length);

                        if (type == (byte)MessageType.System)
                            HandleSystemMessage(id, data);
                        else
                            messages.Add(new MessageIn(id, (MessageType)type, data, this));

                        cursor += length + 8;
                    }
                }
            }
            catch (SocketException e)
            {
                if (e.ErrorCode != 10035)
                {
                    LastError = e;
                    if (ErrorEvent != null)
                        ErrorEvent(e, SocketError.NoRecovery, this, "Updating from UDP failed because of a socket exception.");
                }
            }
            catch (Exception e)
            {
                LastError = e;
                if (ErrorEvent != null)
                    ErrorEvent(e, SocketError.Fault, this, "UDP Data Interpretation Error.  There must have been a mistake in a message header. " +
                        "Data has been lost.");
                //Don't die on a mistake interpreting the data, because we can always 
                //try to interpret more data starting from a new set of packets. 
                //However, the data we were currently working on is lost.
                return;
            }
        }

        /// <summary>Get one message from the tcp connection and interpret it.</summary>
        private void UpdateFromNetworkTcp()
        {
            byte[] buffer;
            int size;
            SocketError error;

            if (tcpInMessageType < 1)
            {
                //restart the counters to listen for a new message.
                if (tcpClient.Available < 8)
                    return;

                buffer = new byte[8];
                size = tcpClient.Client.Receive(buffer, 0, 8, SocketFlags.None, out error);
                switch (error)
                {
                    case SocketError.Success:
                        break;
                    default:
                        LastError = null;
                        if (ErrorEvent != null)
                            ErrorEvent(null, error, this, "TCP Read Header Error.  See Exception for details.");
                        dead = true;
                        return;
                }
                byte id = buffer[0];
                byte type = buffer[1];
                int length = BitConverter.ToInt32(buffer, 4);

                this.tcpInBytesLeft = length;
                this.tcpInMessageType = type;
                this.tcpInID = id;
                tcpIn.Position = 0;
            }

            buffer = new byte[bufferSize];
            int amountToRead = Math.Min(tcpInBytesLeft, bufferSize);

            size = tcpClient.Client.Receive(buffer, 0, amountToRead, SocketFlags.None, out error);
            switch (error)
            {
                case SocketError.Success:
                    break;
                default:
                    LastError = null;
                    if (ErrorEvent != null)
                        ErrorEvent(null, error, this, "TCP Read Data Error.  See Exception for details.");
                    dead = true;
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

                if(tcpInMessageType == (byte)MessageType.System)
                    HandleSystemMessage(tcpInID, buffer);
                else
                    messages.Add(new MessageIn(tcpInID, (MessageType)tcpInMessageType, buffer, this));

                tcpInMessageType = 0;
            }
        }

        /// <summary>Deal with a system message in whatever way we need to.</summary>
        /// <param name="id">The channel it came alone.</param>
        /// <param name="buffer">The data that came along with it.</param>
        private void HandleSystemMessage(byte id, byte[] buffer)
        {
            if (id == (byte)SystemMessageType.UniqueIDRequest)
            {
                UniqueIdentity = BitConverter.ToInt32(buffer, 0);
            }
            else if (id == (byte)SystemMessageType.UDPPortRequest)
            {
                //they want to receive udp messages on this port
                short port = BitConverter.ToInt16(buffer, 0);
                IPEndPoint remoteUdp = new IPEndPoint(((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address, port);
                udpClient.Connect(remoteUdp);

                //they want the udp port.  Send it.
                BitConverter.GetBytes(((short)((IPEndPoint)udpClient.Client.LocalEndPoint).Port)).CopyTo(buffer, 0);
                Send(buffer, (byte)SystemMessageType.UDPPortResponse, MessageType.System, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
            }
            else if (id == (byte)SystemMessageType.UDPPortResponse)
            {
                short port = BitConverter.ToInt16(buffer, 0);
                IPEndPoint remoteUdp = new IPEndPoint(((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address, port);
                udpClient.Connect(remoteUdp);
            }
            else if (id == (byte)SystemMessageType.ServerPingAndMeasure)
            {
                this.Send(buffer, id, MessageType.System, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);
            }
            else if (id == (byte)SystemMessageType.ClientPingAndMeasure)
            {
                //record the difference; half of it is the latency between this client and the server
                int newDelay = (System.Environment.TickCount - BitConverter.ToInt32(buffer, 0)) / 2;
                this.Delay = this.Delay * 0.95f + newDelay * 0.05f;
            }
        }

        /// <summary>Create a new SuperStream to handle a connection to a server. (blocks)</summary>
        /// <param name="address">Who to try to connect to.</param>
        /// <param name="port">Which port to connect to.</param>
        internal ServerStream(string address, string port)
        {
            lock (this)
            {
                dead = true;

                tcpIn = new MemoryStream();
                messages = new List<MessageIn>();

                this.address = address;
                this.port = port;

                IPHostEntry he = Dns.Resolve(address);
                IPAddress[] addr = he.AddressList;

                //try to connect to the address
                Exception error = null;
                for(int i = 0; i < addr.Length; i++)
                {
                    try
                    {
                        endPoint = new IPEndPoint(addr[0], Int32.Parse(port));
                        tcpClient = new TcpClient();
                        tcpClient.NoDelay = true;
                        tcpClient.ReceiveTimeout = 1;
                        tcpClient.SendTimeout = 1;
                        tcpClient.Connect(endPoint);
                        tcpClient.Client.Blocking = false;
                        error = null;
                        break;
                    }
                    catch (Exception e) 
                    { 
                        error = new Exception("There was a problem connecting to the server you specified. " +
                        "The address or port you provided may be improper, the receiving server may be down, " + 
                        "full, or unavailable, or your system's host file may be corrupted. " + 
                        "See inner exception for details.", e); 
                    }
                }

                if (error != null)
                    throw error;

                Console.WriteLine("Address resolved and contacted.  Now connected to " + endPoint.ToString());

                //prepare udp
                udpClient = new UdpClient();
                udpClient.DontFragment = true;
                udpClient.Client.Blocking = false;
                udpClient.Client.SendTimeout = 1;
                udpClient.Client.ReceiveTimeout = 1;
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                //we are now good to go
                dead = false;
            }

            //request our id right away
            byte[] b = new byte[1];
            b[0] = 1;
            Send(b, (byte)SystemMessageType.UniqueIDRequest, MessageType.System, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.None);

        }

        /// <summary>Ping the server to determine delay, as well as act as a keep-alive.</summary>
        internal void Ping()
        {
            byte[] buffer = BitConverter.GetBytes(System.Environment.TickCount);
            Send(buffer, (byte)SystemMessageType.ClientPingAndMeasure, MessageType.System, MessageProtocol.Tcp, 
                MessageAggregation.No, MessageOrder.None);
        }
    }

    /// <summary>Represents a client that can connect to multiple servers.</summary>
    public class Client
    {
        private Dictionary<byte, ObjectStream> objectStreams;
        private Dictionary<byte, BinaryStream> binaryStreams;
        private Dictionary<byte, StringStream> stringStreams;
        private Dictionary<byte, SessionStream> sessionStreams;
        private Dictionary<byte, StreamedTuple> oneTupleStreams;
        private Dictionary<byte, StreamedTuple> twoTupleStreams;
        private Dictionary<byte, StreamedTuple> threeTupleStreams;
        private List<ServerStream> superStreams;
        private HPTimer timer;

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
            oneTupleStreams = new Dictionary<byte, StreamedTuple>();
            twoTupleStreams = new Dictionary<byte, StreamedTuple>();
            threeTupleStreams = new Dictionary<byte, StreamedTuple>();
            superStreams = new List<ServerStream>();
            timer = new HPTimer();
            timer.Start();
            timer.Update();
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
            threeTupleStreams.Add(id, (StreamedTuple)bs);
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
            threeTupleStreams.Add(id, (StreamedTuple)tuple);
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
            twoTupleStreams.Add(id, (StreamedTuple)tuple);
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
            threeTupleStreams.Add(id, (StreamedTuple)tuple);
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
            oneTupleStreams.Add(id, (StreamedTuple)tuple);
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
        /// <returns>The created or retrived connection itself.</returns>
        public ServerStream GetSuperStream(string address, string port)
        {
            foreach (ServerStream s in superStreams)
            {
                if (s.Address.Equals(address) && s.Port.Equals(port))
                {
                    return s;
                }
            }
            ServerStream mySS = new ServerStream(address, port);
            mySS.ErrorEventDelegate = new ErrorEventHandler(mySS_ErrorEvent);
            mySS.ErrorEvent += mySS.ErrorEventDelegate;
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
            timer.Update();
            lock (superStreams)
            {
                foreach (ServerStream s in superStreams)
                {
                    if (s.NextPingTime < timer.TimeInMilliseconds)
                    {
                        s.NextPingTime = timer.TimeInMilliseconds + PingInterval;
                        s.Ping();
                    }

                    s.Update();
                    lock (s.messages)
                    {
                        foreach (MessageIn m in s.messages)
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
                                        if (ErrorEvent != null)
                                            ErrorEvent(null, SocketError.Fault, s, "Received " + m.type + "message for connection " + m.id +
                                                ", but that type does not exist.");
                                        break;
                                }
                            }
                            catch (KeyNotFoundException e)
                            {
                                if (ErrorEvent != null)
                                    ErrorEvent(e, SocketError.Fault, s, "Received " + m.type + "message for connection " + m.id +
                                        ", but that id does not exist for that type.");
                            }
                            catch (Exception e)
                            {
                                if (ErrorEvent != null)
                                    ErrorEvent(e, SocketError.Fault, s, "Exception occurred when trying to queue message.");
                            }
                        }
                        s.messages.Clear();
                    }
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
            foreach (StreamedTuple s in oneTupleStreams.Values)
                s.Update(timer);
            foreach (StreamedTuple s in twoTupleStreams.Values)
                s.Update(timer);
            foreach (StreamedTuple s in threeTupleStreams.Values)
                s.Update(timer);
        }

        /// <summary>This is a placeholder for more possible system message handling.</summary>
        /// <param name="m">The message we're handling.</param>
        private void HandleSystemMessage(MessageIn m)
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

            try
            {
                //check for new data
                while (true)
                {
                    timer.Update();
                    oldTime = timer.TimeInMilliseconds;


                    this.Update();


                    timer.Update();
                    newTime = timer.TimeInMilliseconds;
                    int sleepCount = (int)(this.ListeningInterval - (newTime - oldTime));
                    if (sleepCount > 15)
                    {
                        Thread.Sleep(sleepCount);
                    }
                    else
                        Thread.Sleep(0);
                }
            }
            catch (ThreadAbortException) //we were told to die.  die gracefully.
            {
                //kill the connection
                lock (this)
                {
                    KillAll();
                }
            } 
        }

        private void KillAll()
        {
            for (int i = 0; i < superStreams.Count; i++)
            {
                superStreams[i].Dead = true;
            }
        }
    }
}
