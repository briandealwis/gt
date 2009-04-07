using System.Collections.Generic;
using System.Text;
using System.Threading;
using Common.Logging;
using GT.Utils;
using System;
using System.Diagnostics;

/// <summary>
/// Networking-related parts of GT.
/// </summary>
namespace GT.Net 
{
    /// <summary>
    /// Delegate specification for notification of some change in the lifecycle
    /// of <c>conn</c>.
    /// </summary>
    /// <param name="c">the associated communicator instance</param>
    /// <param name="conn">the actual connexion</param>
    public delegate void ConnexionLifecycleNotification(Communicator c, IConnexion conn);

    /// <summary>
    /// A base-level class encompassing the commonalities between GT Client
    /// and GT Server instances.
    /// </summary>
    public abstract class Communicator : IStartable
    {
        protected ILog log;

        protected IList<IConnexion> connexions = new List<IConnexion>();
        protected Thread listeningThread;

        #region Events

        /// <summary>Occurs when there are errors on the network.</summary>
        public event ErrorEventNotication ErrorEvent;

        public event ConnexionLifecycleNotification ConnexionAdded;
        public event ConnexionLifecycleNotification ConnexionRemoved;

        /// <summary>Invoked each cycle of the server.</summary>
        public event Action<Communicator> Tick;

        #endregion

        /// <summary>
        /// Return the list of current connexions.  This list may include 
        /// inactive or now-dead connexions; it is the caller's responsibility 
        /// to check the status of the connexion before use.  This method may
        /// return the live list used by this instance; the caller should be
        /// aware that any GT actions may cause this list to be changed from
        /// underneath the caller.
        /// </summary>
        public virtual ICollection<IConnexion> Connexions
        {
            get { return connexions; }
        }

        /// <summary>
        /// Return the marshaller configured for this client.
        /// </summary>
        public abstract IMarshaller Marshaller { get; }

        /// <summary>
        /// Returns the interval to wait between calls <see cref="Update"/>
        /// in <see cref="StartListening"/>.
        /// </summary>
        protected abstract TimeSpan TickInterval { get; }

        /// <summary>
        /// Return true if the instance has been started (<see cref="Start"/>)
        /// and neither stopped nor disposed (<see cref="Stop"/> and 
        /// <see cref="Dispose"/>).
        /// </summary>
        public abstract bool Active { get; }

        public Communicator()
        {
            log = LogManager.GetLogger(GetType());
        }

        /// <summary>
        /// Start the instance.  Starting an instance may throw an exception on error.
        /// </summary>
        public virtual void Start()
        {
            /*do nothing*/
        }

        /// <summary>
        /// Stop the instance.  Instances can be stopped multiple times.
        /// Stopping an instance may throw an exception on error.
        /// </summary>
        public virtual void Stop()
        {
            // Should we call ConnexionRemoved on stop?
            foreach(IConnexion cnx in connexions)
            {
                try { cnx.ShutDown(); }
                catch (Exception e)
                {
                    log.Info("exception thrown when shutting down " + cnx, e);
                }
            }
            connexions.Clear();
        }

        /// <summary>
        /// Dispose of any system resources that may be held onto by this
        /// instance.  There should never be an exception thrown.
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(connexions);
        }

        /// <summary>
        /// Run a cycle to process any pending events for the connexions or
        /// other related objects for this instance.  This method is <strong>not</strong> 
        /// re-entrant and should not be called from GT callbacks.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Sleep for the <see cref="TickInterval"/>.  Return false if the sleep 
        /// finished early, such as because some event caused the instance to wake early.
        /// </summary>
        public virtual bool Sleep()
        {
            return Sleep(TickInterval);
        }

        /// <summary>
        /// Sleep for the specified amount of time.  Return false if the sleep 
        /// finished early, such as because some event caused the instance to wake early.
        /// </summary>
        /// <param name="sleepTime">the amount of time to sleep</param>
        public virtual bool Sleep(TimeSpan sleepTime)
        {
            if (sleepTime.CompareTo(TimeSpan.Zero) > 0)
            {
                // FIXME: this should do something smarter
                // Socket.Select(listenList, null, null, 1000);
                Thread.Sleep(sleepTime);
            }
            return true;
        }

        /// <summary>
        /// Starts a new thread that listens to periodically call 
        /// <see cref="Update"/>.  This thread instance will be stopped
        /// on <see cref="Stop"/> or <see cref="Dispose"/>.
        /// The frequency between calls to <see cref="Update"/> is controlled
        /// by the configuration's <see cref="BaseConfiguration.TickInterval"/>.
        /// </summary>
        public virtual Thread StartSeparateListeningThread()
        {
            // must ensure that this instance is started before exiting 
            // this method; otherwise can have a race condition
            Start();
            listeningThread = new Thread(StartListening);
            listeningThread.Name = "Listening Thread[" + ToString() + "]";
            listeningThread.IsBackground = true;
            listeningThread.Start();
            return listeningThread;
        }

        protected virtual void StopListeningThread()
        {
            Thread t = listeningThread;
            listeningThread = null;
            if(t != null && t != Thread.CurrentThread) { t.Abort(); }
        }

        /// <summary>Starts an infinite loop to periodically call
        /// <see cref="Update"/> based on the current <see cref="TickInterval"/>.</summary>
        public virtual void StartListening()
        {
            Start();
            while (Active)
            {
                try
                {
                    // tick count is in milliseconds
                    int oldTickCount = Environment.TickCount;

                    Update();

                    int newTickCount = Environment.TickCount;
                    int sleepCount = Math.Max(0,
                        (int)TickInterval.TotalMilliseconds - (newTickCount - oldTickCount));

                    Sleep(TimeSpan.FromMilliseconds(sleepCount));
                }
                catch (ThreadAbortException)
                {
                    log.Trace(String.Format("{0}: listening thread stopped", this));
                    Stop();
                    return;
                }
                catch (Exception e)
                {
                    log.Warn(String.Format("Exception in listening loop: {0}", this), e);
                    // FIXME: should we notify of such conditions?
                    NotifyError(new ErrorSummary(Severity.Warning,
                                SummaryErrorCode.RemoteUnavailable,
                                "Exception occurred processing a connexion", e));
                }
            }
        }


        /// <summary>
        /// Process the connexions lists to remove dead connexions.
        /// </summary>
        protected void RemoveDeadConnexions()
        {
            for (int i = 0; i < connexions.Count; )
            {
                if (connexions[i].Active && connexions[i].Transports.Count > 0)
                {
                    i++;
                }
                else
                {
                    IConnexion c = connexions[i];
                    connexions.RemoveAt(i);
                    RemovedConnexion(c);
                    try { c.Dispose(); }
                    catch (Exception e) {
                        log.Info("Exception thrown while disposing connexion", e);
                    }
                }
            }
        }

        protected void Stop<T>(IEnumerable<T> elements)
            where T : IStartable
        {
            if (elements == null) { return; }
            foreach(T stoppable in elements)
            {
                try { stoppable.Stop(); }
                catch (Exception e)
                {
                    log.Warn(String.Format("exception thrown when stopping {0}", stoppable), e);
                }
            }
        }

        protected void Dispose<T>(IEnumerable<T> elements)
            where T : IDisposable
        {
            if (elements == null) { return; }
            foreach (T disposable in elements)
            {
                try { disposable.Dispose(); }
                catch (Exception e)
                {
                    log.Warn(String.Format("exception thrown when disposing of {0}",
                        disposable), e);
                }
            }
        }

        protected virtual void AddConnexion(IConnexion cnx)
        {
            connexions.Add(cnx);
            if(ConnexionAdded != null)
            {
                try
                {
                    ConnexionAdded(this, cnx);
                }
                catch(Exception e)
                {
                    log.Info("An exception occurred when notifying ConnexionAdded", e);
                    NotifyError(new ErrorSummary(Severity.Information,
                        SummaryErrorCode.UserException,
                        "An exception occurred when notifying ConnexionAdded", e));
                }
            }
        }

        /// <summary>
        /// Remove the connexion at the provided index.  Overrides of
        /// this method should remove any state associated with the connexion
        /// at the provided index.
        /// </summary>
        /// <param name="cnx">the connexion being removed</param>
        protected virtual void RemovedConnexion(IConnexion cnx)
        {
            if(ConnexionRemoved != null)
            {
                try
                {
                    ConnexionRemoved(this, cnx);
                }
                catch(Exception e)
                {
                    log.Info("An exception occurred when notifying ConnexionRemoved", e);
                    NotifyError(new ErrorSummary(Severity.Information,
                        SummaryErrorCode.UserException,
                        "An exception occurred when notifying ConnexionRemoved", e));
                }
            }
        }

        /// <summary>
        /// Notify any listeners to the <see cref="Tick"/> event that this
        /// instance has seen a tick of <see cref="Update"/>.
        /// </summary>
        protected void NotifyTick()
        {
            if (Tick != null) { Tick(this); }
        }

        bool nullWarningIssued = false;

        protected void NotifyError(ErrorSummary es)
        {
            es.LogTo(log);
            if (ErrorEvent == null) {
                if (!nullWarningIssued)
                {
                    log.Warn(String.Format("{0}: no ErrorEvent handler registered; redirecting all ErrorEvents to console", this));
                    nullWarningIssued = true;
                }
                return; 
            }
            try { ErrorEvent(es); }
            catch (Exception e)
            {
                log.Warn("Exception occurred when processing application ErrorEvent handlers", e);
                ErrorEvent(new ErrorSummary(Severity.Information, SummaryErrorCode.UserException,
                    "Exception occurred when processing application ErrorEvent handlers", e));
            }
        }
    }

    public abstract class BaseConfiguration : IComparer<ITransport> 
    {
        public BaseConfiguration()
        {
            TickInterval = TimeSpan.FromMilliseconds(10);
            PingInterval = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// The time between pings to clients.  This must be greater than 0.
        /// </summary>
        public virtual TimeSpan PingInterval { get; set; }

        /// <summary>
        /// The time between server ticks.  This must be greater than 0.
        /// </summary>
        public virtual TimeSpan TickInterval { get; set; }

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

        /// <summary>
        /// Provide an opportunity to change configuration parameters or even wrap 
        /// or replace a transport instance.
        /// </summary>
        /// <param name="t">the transport to configure</param>
        /// <returns>the possibly reconfigured or replaced transport</returns>
        public virtual ITransport ConfigureTransport(ITransport t)
        {
            return t;
        }
    }

    /// <summary>
    /// An actionable code describing the error.
    /// </summary>
    public enum SummaryErrorCode {
        /// <summary>
        /// An exception was raised in an user-provided event handler
        /// </summary>
        UserException,
        /// <summary>
        /// A remote side could not be communicated with.
        /// </summary>
        RemoteUnavailable,
        /// <summary>
        /// A collection of messages could not be sent to the remote;
        /// these messages will not be resent.
        /// </summary>
        MessagesCannotBeSent,
        /// <summary>
        /// An incoming message was in an invalid format and could not be
        /// decoded.
        /// </summary>
        InvalidIncomingMessage,
        /// <summary>
        /// A transport cannot cope with the traffic being directed to
        /// it and any pending traffic is being backed up.
        /// </summary>
        TransportBacklogged,
        /// <summary>
        /// There was a configuration problem in a component.
        /// </summary>
        Configuration
    }

    /// <summary>
    /// Represents a summary of an error or warning situation that has occurred
    /// during GT execution.
    /// </summary>
    public struct ErrorSummary
    {
        public ErrorSummary(Severity sev, SummaryErrorCode sec, string msg, Exception ctxt)
            : this(sev, sec, msg, null, ctxt) {}

        public ErrorSummary(Severity sev, SummaryErrorCode sec, string msg, object subj, Exception ctxt)
        {
            Severity = sev;
            ErrorCode = sec;
            Message = msg;
            Subject = subj;
            Context = ctxt;
        }

        public Severity Severity;
        public SummaryErrorCode ErrorCode;
        public string Message;
        public Exception Context;
        public object Subject;

        public override string ToString()
        {
            StringBuilder results = new StringBuilder();
            results.Append(Severity);
            results.Append('[');
            results.Append(ErrorCode);
            results.Append("]: ");
            results.Append(Message);
            if (Subject != null)
            {
                results.Append(" {");
                results.Append(Subject);
                results.Append('}');
            }
            if (Context != null)
            {
                results.Append(Context.GetType());
                results.Append(Context.Message);
            }
            return results.ToString();
        }

        /// <summary>
        /// Log this instance to the provided logger.
        /// </summary>
        /// <param name="log"></param>
        public void LogTo(ILog log)
        {
            switch (Severity)
            {
                case Severity.Fatal: log.Fatal(this); break;
                case Severity.Error: log.Error(this); break;
                case Severity.Warning: log.Warn(this); break;
                default: log.Info(this); break;
            }
        }
    }

    #region Delegate Definitions

    /// <summary>Notification of an error event on a connexion.</summary>
    /// <param name="summary">A summary of the error event.</param>
    public delegate void ErrorEventNotication(ErrorSummary summary);

    /// <summary>Notification of a message having been sent or received.</summary>
    /// <param name="m">The message.</param>
    /// <param name="client">The source or destination of the message</param>
    /// <param name="transport">How the message was sent</param>
    public delegate void MessageHandler(Message m, IConnexion client, ITransport transport);

    /// <summary>
    /// Notification that a transport was either added or removed.
    /// </summary>
    /// <param name="connexion"></param>
    /// <param name="newTransport"></param>
    public delegate void TransportLifecyleNotification(IConnexion connexion, ITransport newTransport);

    /// <summary>
    /// Notification of a ping having been sent.
    /// </summary>
    /// <param name="transport">The transport used for the ping</param>
    /// <param name="sequence">The sequence number for this ping.</param>
    public delegate void PingingNotification(ITransport transport, uint sequence);

    /// <summary>
    /// Notification of a response to a ping request.
    /// </summary>
    /// <param name="transport">The transport from which the ping was received</param>
    /// <param name="sequence">The sequence number of the ping</param>
    /// <param name="roundtrip">The round-trip time between issuing the ping to 
    /// the response being received</param>
    public delegate void PingedNotification(ITransport transport, uint sequence, TimeSpan roundtrip);

    #endregion

    /// <summary>
    /// Connexions represent a communication connection between a client and server.
    /// Using a connexion, a client can send a message or messages to a server, and
    /// vice-versa.  Note that the <see cref="IDisposable.Dispose"/> method
    /// does not perform a friendly shutdown, such that the opposite side will be 
    /// notified of the closing of this connexion; use <see cref="ShutDown"/> instead.
    /// </summary>
    public interface IConnexion : IDisposable
    {
        /// <summary>
        /// The (possibly / likely) smoothed delay seen for this connexion (in milliseconds).
        /// </summary>
        float Delay { get; }

        /// <summary>
        /// Return true if this instance is active
        /// </summary>
        bool Active { get; }

        /// <summary>The server-unique identity of this client</summary>
        int Identity { get; }

        /// <summary>
        /// Notification of fatal errors occurring on the connexion.
        /// </summary>
        event ErrorEventNotication ErrorEvents;

        /// <summary>Triggered when a message is received.</summary>
        event MessageHandler MessageReceived;

        /// <summary>Triggered when a message is sent.</summary>
        event MessageHandler MessageSent;

        event TransportLifecyleNotification TransportAdded;
        event TransportLifecyleNotification TransportRemoved;

        event PingingNotification PingRequested;
        event PingedNotification PingReceived;

        // public StatisticalMoments DelayStatistics { get; }

        /// <summary>
        /// The list of currently-connected transports.  Transports are ordered as
        /// determined by this connexion's owner (see <c>BaseConfguration</c>).
        /// </summary>
        IList<ITransport> Transports { get; }

        /// <summary>
        /// Run a cycle to process any pending events for the transports and
        /// other related objects for this instance.  This method is <strong>not</strong> 
        /// re-entrant and should not be called from GT callbacks.
        /// </summary>
        void Update();

        /// <summary>
        /// Close this connexion, while telling the other side.
        /// </summary>
        void ShutDown();

        /// <summary>
        /// Close this connection immediately.  See <c>ShutDown()</c> for a kinder
        /// variant that notifies the other side.
        /// </summary>
        //void Dispose();

        /// <summary>Send a message using these parameters.  At least one of <c>mdr</c> and
        /// <c>cdr</c> are expected to be specified (i.e., be non-null).</summary>
        /// <param name="msg">The message to send.</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(Message msg, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        /// <summary>Send a set of messages using these parameters.  
        /// At least one of <c>mdr</c> and <c>cdr</c> are expected to be specified 
        /// (i.e., be non-null).</summary>
        /// <param name="msgs">The message to send.</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(IList<Message> msgs, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        /// <summary>Send a byte array on <see cref="channel"/>.
        /// At least one of <c>mdr</c> and <c>cdr</c> are expected to be specified 
        /// (i.e., be non-null).</summary>
        /// <param name="buffer">The byte array to send</param>
        /// <param name="channel">The channel to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(byte[] buffer, byte channel, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        /// <summary>Send a string on <see cref="channel"/>.
        /// At least one of <c>mdr</c> and <c>cdr</c> are expected to be specified 
        /// (i.e., be non-null).</summary>
        /// <param name="s">The string to send</param>
        /// <param name="channel">The channel to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(string s, byte channel, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        /// <summary>Sends an bject on <see cref="channel"/>.
        /// At least one of <c>mdr</c> and <c>cdr</c> are expected to be specified 
        /// (i.e., be non-null).</summary>
        /// <param name="o">The object to send</param>
        /// <param name="channel">The channel to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        void Send(object o, byte channel, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        /// <summary>
        /// Flush all pending messages on this connexion.
        /// </summary>
        void Flush();

        /// <summary>
        /// Flush all pending messages for the specified channel on this connexion.
        /// </summary>
        /// <param name="channel">the channel for flushing</param>
        void FlushChannel(byte channel);

        #region Internal Use Only

        /// <summary>
        /// A supplementary interface to be implemented by all <see cref="IConnexion"/>,
        /// used by packet schedulers to marshal
        /// </summary>
        IMarshalledResult Marshal(Message m, ITransportDeliveryCharacteristics tdc);

        /// <summary>
        /// A supplementary interface for use by <see cref="IPacketScheduler"/>.
        /// Sends a packet on the provided transport.
        /// </summary>
        /// <param name="transport">the transport to be sent</param>
        /// <param name="packet">the packet to be sent</param>
        /// <returns>true if successfully sent, false otherwise</returns>
        /// <exception cref="TransportError">thrown on send error; such errors are
        ///     fatal and indicate the transport can no longer be used</exception>
        void SendPacket(ITransport transport, TransportPacket packet);

        /// <summary>
        /// Find a transport that meets the requirements specified by
        /// <see cref="mdr"/> or <see cref="cdr"/>.
        /// </summary>
        /// <param name="mdr">the requirements specific with the message; this overrides
        ///     the channel requirements</param>
        /// <param name="cdr">the requirements associated with the channel</param>
        /// <returns>a transport that meets these requirements</returns>
        /// <exception cref="NoMatchingTransport">thrown if there is no matching transport</exception>
        ITransport FindTransport(MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr);

        #endregion

    }

    public abstract class BaseConnexion : IConnexion, IComparer<ITransport>
    {
	    #region Events

        /// <summary>
        /// Notification of fatal errors occurring on the connexion.
        /// </summary>
        public event ErrorEventNotication ErrorEvents;

        /// <summary>Triggered when a message is received.</summary>
        public event MessageHandler MessageReceived;

        /// <summary>Triggered when a message is sent.</summary>
        public event MessageHandler MessageSent;

        public event TransportLifecyleNotification TransportAdded;
        public event TransportLifecyleNotification TransportRemoved;

        public event PingingNotification PingRequested;
        public event PingedNotification PingReceived;

	    #endregion

        protected ILog log;
        
        protected bool active = false;
        protected List<ITransport> transports = new List<ITransport>();
        protected uint pingSequence = 0;
        protected IPacketScheduler scheduler;

        /// <summary>
        /// The server's unique identifier for this connexion; this
	    /// identifier is only unique within the server's client
	    /// group and is not globally unique.
        /// </summary>
        protected int identity;

        public BaseConnexion()
        {
            log = LogManager.GetLogger(GetType());
            scheduler = CreatePacketScheduler();
            scheduler.ErrorEvent += NotifyError;
            scheduler.MessagesSent += NotifyMessagesSent;
        }

        /// <summary>
	    /// Return the appropriate marshaller for this connexion.
	    /// </summary>
        abstract public IMarshaller Marshaller { get; }

	    /// <summary>
	    /// Retrieve the transports associated with this connexion.
	    /// Intended only for statistical use.
	    /// </summary>
        public IList<ITransport> Transports { get { return transports; } }

        /// <summary>
        /// Return the server-unique identity for the client represented 
        /// by *this connexion*.
        /// </summary>
        /// <seealso cref="SendingIdentity"/>
        public int Identity
        {
            get { return identity; }
        }

        /// <summary>
        /// Return the globally unique identifier for the client
        /// represented by this connexion.
        /// </summary>
        abstract public Guid ClientGuid { get; }

        /// <summary>
        /// Return the identity to be used for sending messages across this connexion.
        /// For a connexion representing a client's interface to the server 
        /// (i.e., a GT.Net.ServerConnexion), this is the server's id for 
        /// this connexion to the client (and should be the same as 
        /// <see cref="Identity"/>).  For a connexion representing a server's
        /// interface to a client (i.e., a GT.Net.ClientConnexion), this is the 
        /// server's id for itself.
        /// </summary>
        public abstract int SendingIdentity { get; }

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
                // FIXME: should rearrange code so that transports is never
                // modified except from Update(), Stop(), and ShutDown()
                // See ServerConnexion.Start()
                foreach (ITransport t in new List<ITransport>(transports))
                {
                    if (t.Active)
                    {
                        try
                        {
                            FastpathSendMessage(t, new SystemMessage(SystemMessageType.ConnexionClosing));
                         }
                        catch(CannotSendMessagesError)
                        {
                            // ignore since we're shutting down anyways:
                            // the ConnexionClosing is sent as a courtesy
                        }
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
            if (scheduler != null) { scheduler.Dispose(); }
            scheduler = null;
        }

        /// <summary>Occurs when there is an error.</summary>
        protected internal void NotifyError(ErrorSummary summary)
        {
            if (ErrorEvents != null)
            {
                try { ErrorEvents(summary); }
                catch (Exception e)
                {
                    log.Warn("ErrorEvents event handler threw an exception", e);
                }
            }
        }

        /// <summary>Ping the other side to determine delay, as well as act as a keep-alive.</summary>
        public void Ping()
        {
            // need to create new list as the ping may lead to a send error,
            // resulting in the transport being removed from underneath us.
            pingSequence++;
            foreach (ITransport t in new List<ITransport>(transports))
            {
                try
                {
                    byte[] pingmsg = new byte[8];
                    BitConverter.GetBytes(Environment.TickCount).CopyTo(pingmsg, 0);
                    BitConverter.GetBytes(pingSequence).CopyTo(pingmsg, 4);
                    Send(new SystemMessage(SystemMessageType.PingRequest, pingmsg),
                        new SpecificTransportRequirement(t), null);
                    if (PingRequested != null) { PingRequested(t, pingSequence); }
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
                // modification problems.  Create list lazily to minimize garbage creation.
                IDictionary<ITransport,GTException> toRemove = null;
                foreach (ITransport t in transports)
                {
                    if (!t.Active)
                    {
                        if (toRemove == null) { toRemove = new Dictionary<ITransport,GTException>(); }
                        toRemove[t] = null; 
                        continue;
                    }

                    try { t.Update(); }
                    catch (TransportError e)
                    {
                        // FIXME: Log the error
                        // Console.WriteLine("{0} {1} WARNING: Transport error [{2}]: {3}", 
                        //    DateTime.Now, this, t, e);
                        if (toRemove == null) { toRemove = new Dictionary<ITransport,GTException>(); }
                        toRemove[t] = e;
                    }
                }
                if (toRemove != null)
                {
                    foreach(ITransport t in toRemove.Keys)
                    {
                        HandleTransportDisconnect(t);
                        if(toRemove[t] != null)
                        {
                            NotifyError(new ErrorSummary(Severity.Warning,
                                SummaryErrorCode.RemoteUnavailable,
                                "Transport failed", toRemove[t]));
                        }
                    }
                }

                /// And give the scheduler an opportunity to do something too
                if (scheduler != null) { scheduler.Update(); }
            }
        }

        public void Flush()
        {
            // must be locked as is called by AbstractStream implementations
            lock (this)
            {
                scheduler.Flush();
            }
        }

        public void FlushChannel(byte channel)
        {
            // must be locked as is called by AbstractStream implementations
            lock (this)
            {
                scheduler.FlushChannelMessages(channel);
            }
        }


        protected abstract IPacketScheduler CreatePacketScheduler();
 
        /// <summary>
        /// Add the provided transport to this connexion.
        /// </summary>
        /// <param name="t">the transport to add</param>
        public virtual void AddTransport(ITransport t)
        {
            if (log.IsTraceEnabled)
            {
                log.Trace(String.Format("{0}: added new transport: {1}", this, t));
            }
            t.PacketReceivedEvent += HandleNewPacket;
            transports.Add(t);
            transports.Sort(this);
            if (TransportAdded != null) { TransportAdded(this, t); }
        }

        /// <summary>
        /// Remove the provided transport from this connexion's list.
        /// </summary>
        /// <param name="t">the transport to remove</param>
        /// <returns></returns>
        public virtual bool RemoveTransport(ITransport t) 
        {
            if (log.IsTraceEnabled)
            {
                log.Trace(String.Format("{0}: removing transport: {1}", this, t));
            }
            bool removed = transports.Remove(t);
            t.PacketReceivedEvent -= HandleNewPacket;
            if (TransportRemoved != null) { TransportRemoved(this, t); }
            t.Dispose();
            return removed;
        }

        abstract public int Compare(ITransport a, ITransport b);

        protected virtual ITransport HandleTransportDisconnect(ITransport transport)
        {
            Debug.Assert(transport != null, "we shouldn't receive a null transport!");
            RemoveTransport(transport);
            // FIXME: We can't reconnect unconditionally: consider where a server goes down,
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
            switch (message.Descriptor)
            {
            case SystemMessageType.PingRequest:
                Send(new SystemMessage(SystemMessageType.PingResponse, message.data),
                        new SpecificTransportRequirement(transport), null);
                break;

            case SystemMessageType.PingResponse:
                // record the difference; half of it is the latency between this client and the server
                // Tickcount is the # milliseconds (fixme: this could wrap...)
                int endCount = Environment.TickCount;
                    int startCount = BitConverter.ToInt32(message.data, 0);
                int roundtrip = endCount >= startCount ? endCount - startCount 
                    : (int.MaxValue - startCount) + endCount;
                // NB: transport.Delay set may (and probably will) scale this value
                transport.Delay = roundtrip / 2f;
                if (PingReceived != null)
                {
                    uint sequence = BitConverter.ToUInt32(message.data, 4);
                    PingReceived(transport, sequence, TimeSpan.FromMilliseconds(roundtrip));
                }
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
                    message.Descriptor);
                break;
            }
        }


        virtual protected void HandleNewPacket(TransportPacket packet, ITransport transport)
        {
            while (packet.Length > 0)
            {
                Marshaller.Unmarshal(packet, transport, _marshaller_MessageAvailable);
            }
            //DebugUtils.DumpMessage("ClientConnexionConnexion.PostNewlyReceivedMessage", m);
        }

        virtual protected void _marshaller_MessageAvailable(object marshaller, MessageEventArgs mea)
        {
            if (mea.Message.MessageType == MessageType.System)
            {
                //System messages are special!  Yay!
                HandleSystemMessage((SystemMessage)mea.Message, mea.Transport);
            }
            else
            {
                if (MessageReceived == null)
                {
                    log.Warn(String.Format("{0}: no MessageReceived listener!", this));
                }
                else
                {
                    MessageReceived(mea.Message, this, mea.Transport);
                }
            }
        }
        
        /// <remarks>
        /// This implementation effectively delegates the resolving to the MDR and
        /// CDR instances.
        /// </remarks>
        public virtual ITransport FindTransport(MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            ITransport t = null;
            if (mdr != null) { t = mdr.SelectTransport(transports); }
            if (t != null) { return t; }
            if (t == null && cdr != null) { t = cdr.SelectTransport(transports); }
            if (t != null) { return t; }
            throw new NoMatchingTransport(this, mdr, cdr);
        }

        #region Sending

        /// <summary>Send a byte array on <see cref="channel"/>.</summary>
        /// <param name="buffer">The byte array to send</param>
        /// <param name="channel">The channel to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public void Send(byte[] buffer, byte channel, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new BinaryMessage(channel, buffer), mdr, cdr);
        }

        /// <summary>Send a string on <see cref="channel"/>.</summary>
        /// <param name="s">The string to send</param>
        /// <param name="channel">The channel to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public void Send(string s, byte channel, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new StringMessage(channel, s), mdr, cdr);
        }

        /// <summary>Sends an bject on <see cref="channel"/>.</summary>
        /// <param name="o">The object to send</param>
        /// <param name="channel">The channel to be sent on</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public void Send(object o, byte channel, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new ObjectMessage(channel, o), mdr, cdr);
        }

        public virtual IMarshalledResult Marshal(Message m, ITransportDeliveryCharacteristics tdc)
        {
            return Marshaller.Marshal(SendingIdentity, m, tdc);
        }

        /// <summary>Send a message.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public virtual void Send(Message message, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            // must be locked as is called by AbstractStream implementations
            lock(this)
            {
                scheduler.Schedule(message, mdr, cdr);
            }
        }

        /// <summary>Send a set of messages.</summary>
        /// <param name="messages">The messages to send.</param>
        /// <param name="mdr">Requirements for this particular message; may be null.</param>
        /// <param name="cdr">Requirements for the message's channel.</param>
        public virtual void Send(IList<Message> messages, MessageDeliveryRequirements mdr,
            ChannelDeliveryRequirements cdr)
        {
            // must be locked as is called by AbstractStream implementations
            lock (this)
            {
                foreach(Message m in messages)
                {
                    scheduler.Schedule(m, mdr, cdr);
                }
            }
        }

        /// <summary>
        /// Short-circuit operation to send a message with no fuss, no muss, and no waiting.
        /// This should be used very sparingly.
        /// </summary>
        /// <param name="transport">Where to send it</param>
        /// <param name="msg">What to send</param>
        protected virtual void FastpathSendMessage(ITransport transport, Message msg)
        {
            //pack main message into a buffer and send it right away
            // assumes this is not an infinite message!
            IMarshalledResult result = Marshal(msg, transport);
            try
            {
                while (result.HasPackets)
                {
                    try
                    {
                        SendPacket(transport, result.RemovePacket());
                    }
                    catch (TransportError e)
                    {
                        throw new CannotSendMessagesError(this, e, msg);
                    }
                }
            }
            finally
            {
                result.Dispose();
            }
            NotifyMessageSent(msg, transport);
        }

        public virtual void SendPacket(ITransport transport, TransportPacket packet)
        {
            try
            {
                transport.SendPacket(packet);
            }
            catch (TransportError e)
            {
                NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.RemoteUnavailable,
                    e.Message, e));
                HandleTransportDisconnect(transport);
                throw;
            }
        }

        public void NotifyMessagesSent(ICollection<Message> messages, ITransport t)
        {
            if(MessageSent == null) return;
            try
            {
                foreach (Message msg in messages) { MessageSent(msg, this, t); }
            }
            catch (Exception e)
            {
                log.Info("An exception occurred when notifying MessageSent", e);
                NotifyError(new ErrorSummary(Severity.Information,
                    SummaryErrorCode.UserException,
                    "An exception occurred when notifying MessageSent", e));
            }
        }

        protected void NotifyMessageSent(Message message, ITransport t) {
            if (MessageSent == null) return;
            try
            {
                MessageSent(message, this, t);
            }
            catch(Exception e)
            {
                log.Info("An exception occurred when notifying MessageSent", e);
                NotifyError(new ErrorSummary(Severity.Information,
                    SummaryErrorCode.UserException,
                    "An exception occurred when notifying MessageSent", e));
            }
        }

        #endregion

        override public string ToString()
        {
            return GetType().Name + "(" + identity + ")";
        }
    }

    /// <summary>
    /// A useful class for testing some of the acceptors and connectors
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TransportFactory<T>
    {
        public byte[] ProtocolDescriptor { get; internal set; }
        protected Converter<T, ITransport> creator;
        protected Predicate<ITransport> responsible;

        public TransportFactory(byte[] descriptor, 
            Converter<T, ITransport> creator,
            Predicate<ITransport> responsible)
        {
            ProtocolDescriptor = descriptor;
            this.creator = creator;
            this.responsible = responsible;
        }

        public ITransport CreateTransport(T handle)
        {
            return creator(handle);
        }

        public bool Responsible(ITransport transport)
        {
            return responsible(transport);
        }
    }

}
