using System.Collections.Generic;
using GT.Utils;
using System;
using System.IO;
using System.Diagnostics;

namespace GT.Net {

    public abstract class BaseConfiguration : IComparer<ITransport> {

        /// <summary>
        /// Default transport orderer: orders by reliability, then sequencing, then delay.
        /// </summary>
        /// <param name="x">first transport</param>
        /// <param name="y">second transport</param>
        /// <returns>-1 if x < y, 0 if they're equivalent, and 1 if x > y</returns>
        public virtual int Compare(ITransport x, ITransport y)
        {
            if (x.Reliability < y.Reliability) { return -1; }
            if (x.Reliability > y.Reliability) { return 1; }
            if (x.Ordering < y.Ordering) { return -1; }
            if (x.Ordering > y.Ordering) { return 1; }
            if (x.Delay < y.Delay) { return -1; }
            if (x.Delay > y.Delay) { return 1; }
            return 0;
        }

    }

    /// <summary>
    /// A code describing the summary.
    /// </summary>
    public enum SummaryErrorCode {
        UserException,
        RemoteUnavailable,
        MessagesCannotBeSent,
        InvalidIncomingMessage,
        TransportBacklogged,
    }

    public struct ErrorSummary
    {
        public ErrorSummary(Severity sev, SummaryErrorCode sec, string msg, Exception ctxt)
        {
            Severity = sev;
            ErrorCode = sec;
            Message = msg;
            Context = ctxt;
        }

        public Severity Severity;
        public SummaryErrorCode ErrorCode;
        public string Message;
        public Exception Context;

        public override string ToString()
        {
            return String.Format("{0}[{1}]: {2}: {3}", Severity, ErrorCode,
                Message, Context);
        }
    }

    /// <summary>Notification of an error event on a connexion.</summary>
    /// <param name="summary">A summary of the error event.</param>
    public delegate void ErrorEventNotication(ErrorSummary summary);

    /// <summary>Handles a Message event, when a new message arrives</summary>
    /// <param name="m">The incoming message.</param>
    /// <param name="client">Who sent the message</param>
    /// <param name="transport">How the message was sent</param>
    public delegate void MessageHandler(Message m, IConnexion client, ITransport transport);


    /// <summary>
    /// Connexions represent a communication connection between a client and server.
    /// Using a connexion, a client can send a message or messages to a server, and
    /// vice-versa.
    /// </summary>
    public interface IConnexion : IDisposable
    {
        float Delay { get; }

        bool Active { get; }

        /// <summary>The server-unique identity of this client</summary>
        int UniqueIdentity { get; }

        /// <summary>
        /// Notification of fatal errors occurring on the connexion.
        /// </summary>
        event ErrorEventNotication ErrorEvents;

        // public StatisticalMoments DelayStatistics { get; }

        IMarshaller Marshaller { get; }

        /// <summary>
        /// The list of currently-connected transports.  Transports are ordered as
        /// determined by this connexion's owner (see <c>BaseConfguration</c>).
        /// </summary>
        IList<ITransport> Transports { get; }

        /// <summary>
        /// Close this connexion, while telling the other side.
        /// </summary>
        void ShutDown();

        /// <summary>
        /// Close this connection immediately.  See <c>ShutDown()</c> for a kinder
        /// variant that notifies the other side.
        /// </summary>
        void Dispose();

        /// <summary>Triggered when a message is received.</summary>
        event MessageHandler MessageReceived;

        /// <summary>Send a message using these parameters.  At least one of <c>mdr</c> and
        /// <c>cdr</c> are expected to be specified (i.e., be non-null).</summary>
        /// <param name="msg">The message to send.</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(Message msg, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        /// <summary>Send a set of messages using these parameters.  
        /// At least one of <c>mdr</c> and <c>cdr</c> are expected to be specified 
        /// (i.e., be non-null).</summary>
        /// <param name="msg">The message to send.</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(IList<Message> msgs, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        /// <summary>Send a byte array on the channel <c>id</c>.
        /// At least one of <c>mdr</c> and <c>cdr</c> are expected to be specified 
        /// (i.e., be non-null).</summary>
        /// <param name="buffer">The byte array to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(byte[] buffer, byte id, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        /// <summary>Send a string on channel <c>id</c>.
        /// At least one of <c>mdr</c> and <c>cdr</c> are expected to be specified 
        /// (i.e., be non-null).</summary>
        /// <param name="s">The string to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(string s, byte id, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        /// <summary>Sends an bject on channel <c>id</c>.
        /// At least one of <c>mdr</c> and <c>cdr</c> are expected to be specified 
        /// (i.e., be non-null).</summary>
        /// <param name="o">The object to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(object o, byte id, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);
    }

    public abstract class BaseConnexion : IConnexion, IComparer<ITransport>
    {
        protected bool active = false;
        protected List<ITransport> transports = new List<ITransport>();

        /// <summary>
        /// Notification of fatal errors occurring on the connexion.
        /// </summary>
        public event ErrorEventNotication ErrorEvents;

        /// <summary>
        /// The server's unique identifier for this connexion; this identifier is only 
        /// unique within the server's client group and is not globally unique.
        /// </summary>
        protected int uniqueIdentity;

        abstract public IMarshaller Marshaller { get; }

        /// <summary>Triggered when a message is received.</summary>
        public event MessageHandler MessageReceived;

        public IList<ITransport> Transports { get { return transports; } }

        /// <summary>
        /// Return the unique identity as represented by *this instance*.
        /// For a client's server-connexion, this will be the server's id for
        /// this client.  For a server's client-connexion, this will be the
        /// server's id for the client represented by this connexion.
        /// See <c>MyUniqueIdentity</c> for the local instance's identity.
        /// </summary>
        public int UniqueIdentity
        {
            get { return uniqueIdentity; }
        }

        /// <summary>
        /// Return the unique identity for this instance's owner.  For clients,
        /// this is the server's id for this client.  For servers, this is the
        /// unique identity for this server.  This id may be different from
        /// UniqueIdentity.
        /// </summary>
        public abstract int MyUniqueIdentity { get; }

        /// <summary>Average latency on this connexion.</summary>
        public float Delay
        {
            get
            {
                float total = 0; int n = 0;
                foreach (ITransport t in transports)
                {
                    float d = t.Delay;
                    if (d > 0) { total += d; n++; }
                }
                return n == 0 ? 0 : total / n;
            }
        }

        /// <summary>
        /// Is this client dead?
        /// </summary>
        public bool Active
        {
            get { return active; }
        }

        public virtual void ShutDown()
        {
            active = false;
            if (transports != null)
            {
                foreach (ITransport t in transports)
                {
                    if (t.Active)
                    {
                        SendMessage(t, new SystemMessage(SystemMessageType.ConnexionClosing));
                    }
                }
            }
            Dispose();
        }

        public virtual void Dispose()
        {
            active = false;
            if (transports != null)
            {
                foreach (ITransport t in transports) { t.Dispose(); }
            }
        }

        /// <summary>Occurs when there is an error.</summary>
        protected internal void NotifyError(ErrorSummary summary)
        {
            // FIXME: This should be logging
            Console.WriteLine(summary.ToString());
            if (ErrorEvents != null)
            {
                try { ErrorEvents(summary); }
                catch (Exception e)
                {
                    Console.WriteLine("WARNING: Application ErrorEvents event handler threw an exception: {0}", e);
                }
            }
        }

        /// <summary>Ping the other side to determine delay, as well as act as a keep-alive.</summary>
        public void Ping()
        {
            // need to create new list as the ping may lead to a send error,
            // resulting in the transport being removed from underneath us.
            foreach (ITransport t in new List<ITransport>(transports))
            {
                try
                {
                    Send(new SystemMessage(SystemMessageType.PingRequest,
                            BitConverter.GetBytes(System.Environment.TickCount)),
                        new SpecificTransportRequirement(t), null);
                }
                catch (GTException e)
                {
                    NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.RemoteUnavailable,
                        "Could not ping remote using transport " + t, e));
                }
            }
        }

        /// <summary>A single tick of the connexion.</summary>
        public void Update()
        {
            lock (this)
            {
                if (!Active) { return; }
                // must track transports to be removed separately to avoid concurrent
                // modification problems.  Create list lazily to minimize creating garbage.
                IDictionary<ITransport,GTException> toRemove = null;
                foreach (ITransport t in transports)
                {
                    if (!t.Active)
                    {
                        if (toRemove == null) { toRemove = new Dictionary<ITransport,GTException>(); }
                        toRemove[t] = null; 
                        continue;
                    }
                    // Note: we only catch our GT transport exceptions and leave all others
                    // to be percolated upward -- we should avoid swallowing exceptions
                    // FIXME: we really should provide the user some notification
                    try { t.Update(); }
                    catch (TransportError e)
                    {
                        // FIXME: Log the error
                        Console.WriteLine("{0} {1} WARNING: Transport error [{2}]: {3}", 
                            DateTime.Now, this, t, e);
                        if (toRemove == null) { toRemove = new Dictionary<ITransport,GTException>(); }
                        toRemove[t] = e;
                    }
                }
                if (toRemove == null) { return; }
                foreach (ITransport t in toRemove.Keys)
                {
                    HandleTransportDisconnect(t);
                    if(toRemove[t] != null) {
                        NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.RemoteUnavailable,
                            "Transport failed", toRemove[t]));
                    }
                }
            }
        }

        public virtual void AddTransport(ITransport t)
        {
            DebugUtils.Write("{0}: added new transport: {1}", this, t);
            t.PacketReceivedEvent += HandleNewPacket;
            transports.Add(t);
            transports.Sort(this);
        }

        public virtual bool RemoveTransport(ITransport t) 
        {
            DebugUtils.Write("{0}: removing transport: {1}", this, t);
            t.PacketReceivedEvent -= HandleNewPacket;
            t.Dispose();
            return transports.Remove(t);
        }

        abstract public int Compare(ITransport a, ITransport b);

        protected virtual ITransport HandleTransportDisconnect(ITransport transport)
        {
            Debug.Assert(transport != null, "we shouldn't receive a null transport!");
            RemoveTransport(transport);
            // We can't unconditionally reconnect: consider where a server goes down,
            // and we have unreliable transports that provide no information.
            //if ((transport = AttemptReconnect(transport)) != null)
            //{
            //    AddTransport(transport);
            //    return transport;
            //}
            return null;    // we don't find a replacement
        }

        /// <summary>
        /// A transport has been disconnected.  Provide an opportunity to reconnect.
        /// The implementation should *not* call AddTransport().
        /// </summary>
        /// <param name="transport">the disconnected transport</param>
        /// <returns>the replacement transport if successful, null otherwise.</returns>
        virtual protected ITransport AttemptReconnect(ITransport transport) 
        {
            return null;
        }


        virtual protected void HandleSystemMessage(SystemMessage message, ITransport transport)
        {
            switch ((SystemMessageType)message.Id)
            {
            case SystemMessageType.PingRequest:
                Send(new SystemMessage(SystemMessageType.PingResponse, message.data),
                        new SpecificTransportRequirement(transport), null);
                break;

            case SystemMessageType.PingResponse:
                //record the difference; half of it is the latency between this client and the server
                int newDelay = (System.Environment.TickCount - BitConverter.ToInt32(message.data, 0)) / 2;
                // NB: transport.Delay set may (and probably will) scale this value
                transport.Delay = newDelay;
                break;

            case SystemMessageType.ConnexionClosing:
                throw new ConnexionClosedException(this);

            case SystemMessageType.UnknownConnexion:
                throw new TransportError(SystemMessageType.UnknownConnexion,
                    "Remote has no record of the connexion using this transport.", message);

            case SystemMessageType.IncompatibleVersion:
                // Is this the right exception?
                throw new CannotConnectException("Remote does not speak a compatible protocol");

            default:
                Debug.WriteLine("connexion.HandleSystemMessage(): Unknown message type: " +
                    (SystemMessageType)message.Id);
                break;
            }
        }


        virtual protected void HandleNewPacket(byte[] buffer, int offset, int count, ITransport transport)
        {
            Stream stream = new MemoryStream(buffer, offset, count, false);
            while (stream.Position < stream.Length)
            {
                Message m = Marshaller.Unmarshal(stream, transport);
                //DebugUtils.DumpMessage("ClientConnexionConnexion.PostNewlyReceivedMessage", m);

                if (m.MessageType == MessageType.System)
                {
                    //System messages are special!  Yay!
                    HandleSystemMessage((SystemMessage)m, transport);
                }
                else
                {
                    if (MessageReceived == null)
                    {
                        Console.WriteLine("{0}: WARNING: no MessageReceived listener!", this);
                    }
                    else { MessageReceived(m, this, transport); }
                }
            }
        }


        protected virtual ITransport FindTransport(MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            ITransport t = null;
            if (mdr != null) { t = mdr.SelectTransport(transports); }
            if (t != null) { return t; }
            if (t == null && cdr != null) { t = cdr.SelectTransport(transports); }
            if (t != null) { return t; }
            throw new NoMatchingTransport(this, mdr, cdr);
        }

        #region Sending

        /// <summary>Send a byte array on the channel <c>id</c>.</summary>
        /// <param name="buffer">The byte array to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public void Send(byte[] buffer, byte id, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new BinaryMessage(id, buffer), mdr, cdr);
        }

        /// <summary>Send a string on channel <c>id</c>.</summary>
        /// <param name="s">The string to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public void Send(string s, byte id, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new StringMessage(id, s), mdr, cdr);
        }

        /// <summary>Sends an bject on channel <c>id</c>.</summary>
        /// <param name="o">The object to send</param>
        /// <param name="id">The channel id to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public void Send(object o, byte id, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new ObjectMessage(id, o), mdr, cdr);
        }

        /// <summary>Send a message.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public virtual void Send(Message message, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            IList<Message> messages = new List<Message>(1);
            messages.Add(message);
            Send(messages, mdr, cdr);
        }

        /// <summary>Send a set of messages.</summary>
        /// <param name="messages">The messages to send.</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        abstract public void Send(IList<Message> messages, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr);

        /// <summary>
        /// Short-circuit operation to send a message with no fuss, no muss, and no waiting.
        /// This should be used very sparingly.
        /// </summary>
        /// <param name="transport">Where to send it</param>
        /// <param name="msg">What to send</param>
        protected void SendMessage(ITransport transport, Message msg)
        {
            //pack main message into a buffer and send it right away
            Stream packet = transport.GetPacketStream();
            Marshaller.Marshal(MyUniqueIdentity, msg, packet, transport);
            try { SendPacket(transport, packet); }
            catch (TransportError e) { throw new CannotSendMessagesError(this, e, msg); }
        }

        protected void SendMessages(ITransport transport, IList<Message> messages)
        {
            //Console.WriteLine("{0}: Sending {1} messages to {2}", this, messages.Count, transport);
            Stream ms = transport.GetPacketStream();
            int packetStart = (int)ms.Position;
            int index = 0;
            while (index < messages.Count)
            {
                Message m = messages[index];
                int packetEnd = (int)ms.Position;
                Marshaller.Marshal(MyUniqueIdentity, m, ms, transport);
                if (ms.Position - packetStart > transport.MaximumPacketSize) // uh oh, rewind and redo
                {
                    ms.SetLength(packetEnd);
                    ms.Position = packetStart;
                    try { SendPacket(transport, ms); }
                    catch (TransportError e)
                    {
                        throw new CannotSendMessagesError(this, e, messages);
                    }

                    ms = transport.GetPacketStream();
                    packetStart = (int)ms.Position;
                }
                else { index++; }
            }
            if (ms.Position - packetStart != 0)
            {
                ms.Position = packetStart;
                try { SendPacket(transport, ms); }
                catch (TransportError e)
                {
                    throw new CannotSendMessagesError(this, e, messages);
                }
            }
        }

        protected void SendPacket(ITransport transport, Stream stream)
        {
            try { transport.SendPacket(stream); }
            catch (TransportBackloggedWarning e)
            {
                NotifyError(new ErrorSummary(e.Severity, SummaryErrorCode.TransportBacklogged,
                    "Transport is backlogged: there are too many messages being sent", e));
                // rethrow the error if it's not for information purposes
                if (e.Severity != Severity.Information) { throw e; }
            }
            catch (TransportError e)
            {
                HandleTransportDisconnect(transport);
                throw e;
            }
        }

        #endregion

        override public string ToString()
        {
            return GetType().Name + "(" + uniqueIdentity + ")";
        }

        /// <summary>
        /// Filter the list of provided connexions to only include those that are usable.
        /// A usable connexion is active and has transports available to send and receive messages.
        /// </summary>
        /// <typeparam name="T">an IConnexion implementation</typeparam>
        /// <param name="connexions">the provided connexions</param>
        /// <returns>the usable subset of <c>connexions</c></returns>
        public static ICollection<IConnexion> SelectUsable<T>(ICollection<T> connexions)
            where T : IConnexion
        {
            List<IConnexion> usable = new List<IConnexion>(connexions.Count);
            foreach (T connexion in connexions)
            {
                if (connexion.Active && connexion.Transports.Count > 0) { usable.Add(connexion); }
            }
            return usable;
        }

        /// <summary>
        /// Downcast the list of provided objects to a common superclass/interface.
        /// </summary>
        /// <typeparam name="S">the superclass</typeparam>
        /// <typeparam name="T">the current list type</typeparam>
        /// <param name="original">the original collection</param>
        /// <returns>the downcast collection</returns>
        public static ICollection<S> Downcast<S,T>(ICollection<T> original)
            where T : S
        {
            List<S> downcast = new List<S>(original.Count);
            foreach (T element in original)
            {
                downcast.Add(element);
            }
            return downcast;
        }

    }
}
