using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net;
using GT.Common;

namespace GTClient
{
    /// <summary>
    /// A standard interface for clients to reference a remote
    /// connection for a particular transport.
    /// </summary>
    public interface IServerSurrogate
    {
        string Address { get; }
        string Port { get; }

        /// <summary>Is this connection dead?</summary>
        bool Dead { get; }
    }

    public interface ITransport : IServerSurrogate
    {
        /// <summary>
        /// A simple identifier for this transport
        /// </summary>
        string Name { get; }

        /// <summary>Is this connection dead (stopped)?</summary>
        bool Dead { get; }

        float Delay { get; set; }
        ServerStream Server { get; set; }

        void Start();
        void Stop();

        /*FIXME: Stop-gap solution until we have proper QoS descriptors */
        MessageProtocol MessageProtocol { get; }

        /// <summary>
        /// Send the given message to the server.
        /// </summary>
        /// <param name="buffer"></param>
        void SendMessage(byte[] buffer);

        void Update();

        int MaximumMessageSize { get; }
    }

    public abstract class BaseTransport : ITransport
    {
        protected ServerStream server;
        protected string address, port;
        protected IPEndPoint endPoint;

        /// <summary>The average amount of latency between this client 
        /// and the server (in milliseconds).</summary>
        public float delay = 20f;


        public abstract bool Dead { get; }
        public abstract void Start();
        public abstract void Stop();
        public abstract void SendMessage(byte[] buffer);
        public abstract void Update();
        public abstract int MaximumMessageSize { get; }
        public abstract string Name { get; }

        // FIXME: Stop-gap measure until we have QoS descriptors
        public abstract MessageProtocol MessageProtocol { get; }

        public virtual float Delay
        {
            get { return delay; }
            set { delay = 0.95f * delay + 0.05f * delay; }
        }

        /// <summary>The last error encountered</summary>
        public Exception LastError = null;

        /// <summary>
        ///  Messages that have not been able to be sent yet.
        /// </summary>
        protected List<MessageOut> MessageOutAwayPool = new List<MessageOut>();

        protected BaseTransport() {}

        public ServerStream Server
        {
            get { return server; }
            set {
                if (server != null && server != value)
                {
                    throw new InvalidOperationException("cannot change transport's Server once set");
                }
                server = value;
                address = server.Address;
                port = server.Port;
            }
        }

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

        protected void NotifyError(Exception e, SocketError se, string explanation)
        {
            server.NotifyError(this, e, se, explanation);
        }

   }
}