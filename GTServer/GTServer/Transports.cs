using System;
using System.Net.Sockets;
using System.Net;
using GT;

namespace GT
{
    public interface IServerTransport : ITransport, IDisposable
    {
        bool Dead { get; }
    }

    public abstract class BaseServerTransport : BaseTransport, IServerTransport
    {
        public object LastError = null;

        public abstract bool Dead { get; }

        public abstract void Dispose();


        protected void NotifyError(Exception e, SocketError err, object context, string message)
        {
            /* do nothing */
        }
    }

    public delegate void NewClientHandler(IServerTransport transport, int uniqueId);

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

        public abstract bool Started { get; }
        public abstract void Start();
        public abstract void Stop();
        public abstract void Dispose();

        public void NotifyNewClient(IServerTransport t, int clientId)
        {
            NewClientEvent(t, clientId);
        }
    }
}
