using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
    /// A base class for IP-based acceptors.
    /// </summary>
    public abstract class IPBasedAcceptor : IAcceptor
    {
        protected ILog log;

        /// <summary>
        /// An event triggered when a new transport has been
        /// successfully negotiated.
        /// </summary>
        public event NewTransportHandler NewTransportAccepted;

        protected IPAddress address;
        protected int port;

        protected IPBasedAcceptor(IPAddress address, int port)
        {
            log = LogManager.GetLogger(GetType());

            this.address = address;
            this.port = port;
        }

        /// <summary>
        /// Run a cycle to process any pending events for this acceptor.
        /// This method is <strong>not</strong> re-entrant.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Indicate whether this instance is currently active (i.e.,
        //  started).
        /// </summary>
        public abstract bool Active { get; }

        /// <exception cref="TransportError">thrown if the acceptor is
        /// unable to initialize</exception>
        public abstract void Start();
        public abstract void Stop();
        public abstract void Dispose();

        /// <summary>
        /// Notify interested parties that a new transport connection has been
        /// successfully negotiated.
        /// </summary>
        /// <param name="t">the newly-negotiated transport</param>
        /// <param name="capabilities">a dictionary describing the
        ///     capabilities of the remote system</param>
        internal void NotifyNewClient(ITransport t, IDictionary<string,string> capabilities)
        {
            NewTransportAccepted(t, capabilities);
        }

        public override string ToString()
        {
            return String.Format("{0}({1}:{2})", GetType().FullName, address, port);
        }
    }
}
