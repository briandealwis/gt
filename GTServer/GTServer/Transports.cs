using System;
using System.Net.Sockets;
using GT.Common;
using System.Net;

namespace GT.Servers
{
    public delegate void MessageReceivedHandler(byte id, MessageType type, byte[] buffer, MessageProtocol p);

    public interface IServerTransport : IDisposable
    {
        string Name { get; }

        bool Dead { get; }

        event MessageReceivedHandler MessageReceivedEvent;

        /* FIXME: Stop-gap solution until we have proper QoS descriptors */
        MessageProtocol MessageProtocol { get; }

        void SendPacket(byte[] packet);

        void Update();

        int MaximumPacketSize { get; }
    }

    public abstract class BaseServerTransport : IServerTransport
    {
        public event MessageReceivedHandler MessageReceivedEvent;
        public object LastError = null;

        public abstract string Name { get; }
        public abstract bool Dead { get; }

        public abstract MessageProtocol MessageProtocol { get; }

        public abstract void SendPacket(byte[] message);
        public abstract void Update();
        public abstract int MaximumPacketSize { get; }

        public abstract void Dispose();

        protected void NotifyMessageReceived(byte id, MessageType type, byte[] buffer, MessageProtocol p)
        {
            if (MessageReceivedEvent == null) {
                Console.WriteLine(DateTime.Now + " ERROR: transport has nobody to receive incoming messages!");
                return; 
            }
            MessageReceivedEvent(id, type, buffer, p);
        }

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