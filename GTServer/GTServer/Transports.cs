using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace GT.Net
{
    public abstract class BaseServerTransport : BaseTransport, ITransport, IDisposable
    {
        public object LastError = null;

        public abstract void Dispose();

        protected void NotifyError(Exception e, SocketError err, object context, string message)
        {
            /* do nothing */
        }
    }

    public delegate void NewClientHandler(ITransport transport, Dictionary<string,string> capabilities);

    public interface IAcceptor : IStartable
    {
        event NewClientHandler NewClientEvent;

        void Update();
    }

    public abstract class BaseAcceptor : IAcceptor
    {
        public event NewClientHandler NewClientEvent;
        protected IPAddress address;
        protected int port;

        public BaseAcceptor(IPAddress address, int port)
        {
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
