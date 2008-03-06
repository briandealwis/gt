using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net;
using GT;
using System.IO;

namespace GT
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
        bool Started { get; }
    }

    public interface IClientTransport : ITransport, IServerSurrogate, IStartable
    {
        ServerStream Server { get; set; }
    }

    public abstract class BaseClientTransport : BaseTransport, IClientTransport
    {
        protected ServerStream server;
        protected string address, port;
        protected IPEndPoint endPoint;

        public abstract bool Started { get; }
        public abstract void Start();
        public abstract void Stop();
        public void Dispose() { /* empty implementation */ }

        /// <summary>The last error encountered</summary>
        public Exception LastError = null;

        protected BaseClientTransport() {}

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
            get { return address; }
            set { address = value; }
        }

        /// <summary>What is the TCP port of the server.  Thread-safe.</summary>
        public string Port
        {
            get { return port; }
            set { port = value; }
        }

        protected void NotifyError(Exception e, SocketError se, string explanation)
        {
            server.NotifyError(this, e, se, explanation);
        }
    }
}
