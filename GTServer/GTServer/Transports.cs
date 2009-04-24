using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Common.Logging;

namespace GT.Net
{
    public delegate void NewTransportHandler(ITransport transport, IDictionary<string,string> capabilities);

    /// <summary>
    /// An object responsible for negotiating and accepting incoming connections.
    /// The remote service is often implemented using an <c>IConnector</c>.
    /// Acceptors should throw a <see cref="TransportError"/> if they
    /// cannot be successfully started.
    /// See
    /// <blockquote>
    ///    DC Schmidt (1997). Acceptor and connector: A family of object 
    ///    creational patterns for initializing communication services. 
    ///    In R Martin, F Buschmann, D Riehle (Eds.), Pattern Languages of 
    ///    Program Design 3. Addison-Wesley
    ///    &lt;http://www.cs.wustl.edu/~schmidt/PDF/Acc-Con.pdf&gt;
    /// </blockquote>
    /// </summary>
    public interface IAcceptor : IStartable
    {
        /// <summary>
        /// Provide an opportunity for callers to validate an incoming transport.  
        /// If the transport fails, set <see cref="ValidateTransportArgs.Valid"/>
        /// to false and or call <see cref="ValidateTransportArgs.Reject"/> to
        /// also add a reason.
        /// </summary>
        event EventHandler<ValidateTransportArgs> ValidateTransport;

        /// <summary>
        /// Triggered when a new incoming connection has been successfully
        /// negotiated.
        /// </summary>
        event NewTransportHandler NewTransportAccepted;

        /// <summary>
        /// Run a cycle to process any pending connection negotiations.  
        /// This method is <strong>not</strong> re-entrant and should not 
        /// be called from GT callbacks.
        /// </summary>
        /// <exception cref="TransportError">thrown in case of an error</exception>
        void Update();
    }

    /// <summary>
    /// A class for representing the state required for validating a
    /// new transport.  An assessor wishing to reject an incoming transport
    /// should either call <see cref="Reject"/> with a human-readable explanation 
    /// or set <see cref="Valid"/> to false.
    /// </summary>
    public class ValidateTransportArgs : EventArgs 
    {
        /// <summary>
        /// The new transport in question
        /// </summary>
        public ITransport Transport { get; protected set; }

        /// <summary>
        /// The capabilities list provided from the remote
        /// </summary>
        public IDictionary<string, string> Capabilities { get; protected set; }

        /// <summary>
        /// The assessment of whether this transport is valid.  An assessor
        /// deciding that the tranport is invalid should either call <see cref="Reject"/>
        /// with a human-readable explanation or set <see cref="Valid"/> to false.
        /// </summary>
        public bool Valid { get; set; }

        /// <summary>
        /// This collection contains human-readable explanation as to why the transport
        /// has been deemed invalid.  This property may be null.
        /// </summary>
        public IList<string> Reasons { get; protected set; }

        public ValidateTransportArgs(ITransport t, IDictionary<string,string> capabilities) {
            Valid = true;
            Transport = t;
            Capabilities = capabilities;
        }

        /// <summary>
        /// Indicate that this transport should be rejected, with a human-readable
        /// explanation.
        /// </summary>
        /// <param name="reason">the human-readable explanation as to the rejection</param>
        public void Reject(string reason)
        {
            Valid = false;
            if(Reasons == null)
            {
                Reasons = new List<string>();
            }
            Reasons.Add(reason);
        }
    }

        /// <summary>
    /// A base class for acceptor implementations.
    /// </summary>
    public abstract class BaseAcceptor : IAcceptor
    {
        protected ILog log;

        public event EventHandler<ValidateTransportArgs> ValidateTransport;

        public event NewTransportHandler NewTransportAccepted;

        protected BaseAcceptor()
        {
            log = LogManager.GetLogger(GetType());
        }

        /// <summary>
        /// Run a cycle to process any pending events for this acceptor.
        /// This method is <strong>not</strong> re-entrant.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Indicate whether this instance is currently active (i.e., started).
        /// </summary>
        public abstract bool Active { get; }

        /// <exception cref="TransportError">thrown if the acceptor is
        /// unable to initialize</exception>
        public abstract void Start();

        public abstract void Stop();

        public abstract void Dispose();

        /// <summary>
        /// Check to see if the new incoming transport <see cref="transport"/> should
        /// be accepted.  If accepted, then call <see cref="TransportAccepted"/>.  
        /// Otherwise call <see cref="TransportRejected"/>.
        /// </summary>
        /// <param name="transport">the candidate transport</param>
        /// <param name="capabilities">the capabilities of the remote</param>
        protected void CheckAndNotify(ITransport transport, IDictionary<string, string> capabilities) 
        {
            if (ShouldAcceptTransport(transport, capabilities))
            {
                TransportAccepted(transport, capabilities);
            }
            else
            {
                TransportRejected(transport, capabilities);
            }
        }

        /// <summary>
        /// The transport has passed the validation steps; trigger the
        /// <see cref="NewTransportAccepted"/> event.  Subclasses may
        /// override but must ensure the <see cref="NewTransportAccepted"/>
        /// event is still triggered.
        /// </summary>
        /// <param name="transport">the candidate transport</param>
        /// <param name="capabilities">the capabilities of the remote</param>
        protected virtual void TransportAccepted(ITransport transport, IDictionary<string, string> capabilities)
        {
            NotifyNewTransport(transport, capabilities);
        }

        /// <summary>
        /// The transport has failed the validation steps and must be disposed of. 
        /// Subclasses may override but must ensure the transport is disposed.
        /// </summary>
        /// <param name="transport">the candidate transport</param>
        /// <param name="capabilities">the capabilities of the remote</param>
        protected virtual void TransportRejected(ITransport transport, IDictionary<string, string> capabilities)
        {
            transport.Dispose();
        }

        /// <summary>
        /// Consult interested parties to see whether the incoming transport <see cref="t"/>
        /// should be accepted.  Subclasses may choose to override this method to add
        /// additional checks.  This method will log a false result with any accompanying
        /// reasons as to why the connection was rejected.
        /// </summary>
        /// <param name="transportw incoming transport</param>
        /// <param name="capabilities">the capabilities from the remote</param>
        /// <returns>true if the new transport passes muster, false otherwise</returns>
        protected virtual bool ShouldAcceptTransport(ITransport transport, 
            IDictionary<string, string> capabilities)
        {
            if (ValidateTransport == null) { return true; }
            ValidateTransportArgs vta = new ValidateTransportArgs(transport, capabilities);
            NotifyValidateTransport(this, vta);
            if (vta.Valid) { return true; }
            if (vta.Reasons == null)
            {
                log.Warn("Incoming connection rejected; no reason provided");
            }
            else if (vta.Reasons.Count == 1)
            {
                log.Warn("Incoming connection rejected: " + vta.Reasons[0]);
            }
            else
            {
                StringBuilder result = new StringBuilder("Incoming connection rejected: ");
                for (int i = 0; i < vta.Reasons.Count; i++)
                {
                    result.Append(i + 1);
                    result.Append(". ");
                    result.Append(vta.Reasons[i]);
                    if (i +1 != vta.Reasons.Count) { result.Append("  "); }
                }
                log.Warn(result.ToString());
            }
            return false;
        }

        /// <summary>
        /// This only triggers the <see cref="ValidateTransport"/> event; subclasses
        /// of <see cref="BaseAcceptor"/> likely mean to call <see cref="ShouldAcceptTransport"/>
        /// instead.
        /// </summary>
        /// <param name="acceptor">the acceptor triggering the query</param>
        /// <param name="args">the validation state</param>
        protected void NotifyValidateTransport(object acceptor, ValidateTransportArgs args)
        {
            if(ValidateTransport == null) { return; }
            ValidateTransport(acceptor, args);
        }

        /// <summary>
        /// Notify interested parties that a new transport connection has been
        /// successfully negotiated.
        /// </summary>
        /// <param name="t">the newly-negotiated transport</param>
        /// <param name="capabilities">a dictionary describing the
        ///     capabilities of the remote system</param>
        protected void NotifyNewTransport(ITransport t, IDictionary<string, string> capabilities)
        {
            if (NewTransportAccepted == null)
            {
                log.Warn("Acceptor has no listeners for new transports");
                return;
            }
            NewTransportAccepted(t, capabilities);
        }
    }

    /// <summary>
    /// A base class for IP-based acceptors.
    /// </summary>
    public abstract class IPBasedAcceptor : BaseAcceptor
    {
        protected IPAddress address;
        protected int port;

        protected IPBasedAcceptor(IPAddress address, int port)
        {
            this.address = address;
            this.port = port;
        }

        public override string ToString()
        {
            return String.Format("{0}({1}:{2})", GetType().FullName, address, port);
        }
    }
}
