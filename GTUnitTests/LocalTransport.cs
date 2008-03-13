using System;
using GT.Net;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using GT.Utils;

namespace GT.Net.Local
{
    public class LocalAcceptor : IAcceptor
    {
        protected string name;
        protected Semaphore semaphore;
        protected LocalHalfPipe listener;
       
        public event NewClientHandler NewClientEvent;

        public LocalAcceptor(string name)
        {
            this.name = name;
        }

        public void Update()
        {
            if(semaphore.WaitOne(0, false)) {
                Dictionary<string,string> capabilities = 
                    ByteUtils.DecodeDictionary(new MemoryStream(listener.Check()));
                NewClientEvent(new LocalTransport(listener), capabilities);

                RegisterNewListener();
            }
        }

        public void Start()
        {
            RegisterNewListener();
        }

        public void Stop()
        {
            LocalTransport.RemoveListener(name);
            listener = null;
            semaphore = null;
        }

        public bool Active
        {
            get { return listener != null; }
        }

        public void Dispose()
        {
            Stop();
        }

        protected void RegisterNewListener()
        {
            Queue<byte[]> first = new Queue<byte[]>();
            Queue<byte[]> second = new Queue<byte[]>();
            listener = new LocalHalfPipe(first, second);
            LocalHalfPipe client = new LocalHalfPipe(second, first);
            semaphore = LocalTransport.RegisterConnection(name, client);
        }

    }

    public class LocalConnector : IConnector
    {
        protected bool active = false;

        public ITransport Connect(string address, string port, Dictionary<string, string> capabilities)
        {
            LocalHalfPipe hp;
            if ((hp = LocalTransport.OpenConnection(address + ":" + port, capabilities)) == null) {
                return null; 
            }
            return new LocalTransport(hp);
        }

        public void Start()
        {
            active = true;
        }

        public void Stop()
        {
            active = false;
        }

        public bool Active { get { return active; } }

        public void Dispose()
        {
            Stop();
        }

    }

    public class LocalTransport : BaseTransport
    {
        #region Static members
        public static Dictionary<string, KeyValuePair<Semaphore, LocalHalfPipe>> OpenConnections =
            new Dictionary<string, KeyValuePair<Semaphore, LocalHalfPipe>>();

        internal static LocalHalfPipe OpenConnection(string key, Dictionary<string,string> capabilities)
        {
            KeyValuePair<Semaphore, LocalHalfPipe> pair;
            lock (OpenConnections)
            {
                if (!OpenConnections.TryGetValue(key, out pair)) { return null; }
                OpenConnections.Remove(key);
            }
            // gotta negotiate
            MemoryStream ms = new MemoryStream();
            ByteUtils.EncodeDictionary(capabilities, ms);
            pair.Value.Put(ms.ToArray());
            pair.Key.Release();
            return pair.Value;
        }

        internal static Semaphore RegisterConnection(string key, LocalHalfPipe hp)
        {
            lock (OpenConnections)
            {
                Semaphore s = new Semaphore(0, 1);
                OpenConnections[key] = new KeyValuePair<Semaphore, LocalHalfPipe>(s, hp);
                return s;
            }
        }

        internal static void RemoveListener(string name)
        {
            lock (OpenConnections)
            {
                OpenConnections.Remove(name);
            }
        }

        #endregion

        protected LocalHalfPipe handle;

        public LocalTransport(LocalHalfPipe hp)
        {
            PacketHeaderSize = 0;
            handle = hp;
        }

        public override string Name
        {
            get { return "Local"; }
        }

        public override MessageProtocol MessageProtocol
        {
            get { return (MessageProtocol)5; }
        }

        public override bool Active
        {
            get { return handle != null; }
        }

        public override void SendPacket(byte[] message, int offset, int count)
        {
            byte[] data = new byte[count];
            Array.Copy(message, offset, data, 0, count);
            handle.Put(data);
        }

        public override void SendPacket(Stream packetStream)
        {
            handle.Put(((MemoryStream)packetStream).ToArray());
        }

        public override void Update()
        {
            byte[] packet;
            while ((packet = handle.Check()) != null)
            {
                NotifyPacketReceived(packet, 0, packet.Length);
            }
        }

        public override int MaximumPacketSize
        {
            get { return 2048; }
        }
    }


    public class LocalHalfPipe
    {
        protected Queue<byte[]> readQueue;
        protected Queue<byte[]> writeQueue;

        public LocalHalfPipe(Queue<byte[]> rq, Queue<byte[]> wq)
        {
            readQueue = rq;
            writeQueue = wq;
        }

        public byte[] Check()
        {
            lock (this)
            {
                if (readQueue.Count > 0)
                {
                    return readQueue.Dequeue();
                }
            }
            return null;
        }

        public void Put(byte[] packet)
        {
            lock (this)
            {
                writeQueue.Enqueue(packet);
            }
        }
    }
}