using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Common.Logging;

namespace GT.Net
{
    public delegate void NewClientHandler(ITransport transport, Dictionary<string,string> capabilities);

    /// <summary>
    /// An object responsible for negotiating and accepting incoming connections.
    /// The remote service is often implemented using an <c>IConnector</c>.
    /// See
    ///    DC Schmidt (1997). Acceptor and connector: A family of object 
    ///    creational patterns for initializing communication services. 
    ///    In R Martin, F Buschmann, D Riehle (Eds.), Pattern Languages of 
    ///    Program Design 3. Addison-Wesley
    ///    http://www.cs.wustl.edu/~schmidt/PDF/Acc-Con.pdf
    /// </summary>
    public interface IAcceptor : IStartable
    {
        event NewClientHandler NewClientEvent;

        void Update();
    }

    public abstract class BaseAcceptor : IAcceptor
    {
        protected ILog log;

        public event NewClientHandler NewClientEvent;
        protected IPAddress address;
        protected int port;

        public BaseAcceptor(IPAddress address, int port)
        {
            log = LogManager.GetLogger(GetType());

            this.address = address;
            this.port = port;
        }

        public abstract void Update();

        public abstract bool Active { get; }
        public abstract void Start();
        public abstract void Stop();
        public abstract void Dispose();

        public void NotifyNewClient(ITransport t, Dictionary<string,string> capabilities)
        {
            NewClientEvent(t, capabilities);
        }
    }
}
