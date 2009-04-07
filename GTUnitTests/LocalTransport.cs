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
        protected int connectionNumber = 1;
        protected List<LocalHalfPipe> pending;
        protected List<LocalHalfPipe> toBeRemoved;
       
        public event NewTransportHandler NewTransportAccepted;

        public LocalAcceptor(string name)
        {
            this.name = name;
        }

        public bool Active
        {
            get { return LocalTransport.IsRegistered(name, this); }
        }

        public void Start()
        {
            LocalTransport.RegisterAcceptor(name, this);
            pending = new List<LocalHalfPipe>();
            toBeRemoved = new List<LocalHalfPipe>();
        }

        public void Stop()
        {
            LocalTransport.RemoveAcceptor(name);
        }

        public void Dispose()
        {
            Stop();
        }

        internal LocalHalfPipe Connect()
        {
            Queue<byte[]> first = new Queue<byte[]>();
            Queue<byte[]> second = new Queue<byte[]>();
            ++connectionNumber;
            LocalHalfPipe listener = new LocalHalfPipe(connectionNumber, first, second);
            LocalHalfPipe client = new LocalHalfPipe(-connectionNumber, second, first);
            pending.Add(listener);
            return client;
        }

        public void Update()
        {
            toBeRemoved.Clear();
            foreach (LocalHalfPipe hp in pending)
            {
                byte[] message = hp.Check();
                if (message != null)
                {
                    try
                    {
                        Dictionary<string, string> capabilities =
                             ByteUtils.DecodeDictionary(new MemoryStream(message));
                        NewTransportAccepted(new LocalTransport(hp), capabilities);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("WARNING: {0}: connection negotiation failed: {1}", 
                            hp, e);
                    }
                    toBeRemoved.Add(hp);
                }
            }
            pending.RemoveAll(delegate(LocalHalfPipe o) { return toBeRemoved.Contains(o); });
        }

    }

    public class LocalConnector : IConnector
    {
        protected bool active = false;

        public ITransport Connect(string address, string port, IDictionary<string, string> capabilities)
        {
            LocalHalfPipe hp;
            if ((hp = LocalTransport.OpenConnection(address + ":" + port, capabilities)) == null) {
                CannotConnectException error = new CannotConnectException(
                    String.Format("no local connection named '{0}:{1}'",
                        address, port));
                error.SourceComponent = this;
                throw error;
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

        public bool Responsible(ITransport transport)
        {
            return transport is LocalTransport;
        }


    }

    public class LocalTransport : BaseTransport
    {
        #region Static members
        public static Dictionary<string, LocalAcceptor> OpenConnections =
            new Dictionary<string, LocalAcceptor>();

        internal static LocalHalfPipe OpenConnection(string key, IDictionary<string,string> capabilities)
        {
            LocalHalfPipe hp;
            lock (OpenConnections)
            {
                LocalAcceptor acceptor;
                if (!OpenConnections.TryGetValue(key, out acceptor)) { return null; }
                hp = acceptor.Connect();
            }
            // gotta negotiate
            MemoryStream ms = new MemoryStream();
            ByteUtils.EncodeDictionary(capabilities, ms);
            hp.Put(ms.ToArray());
            return hp;
        }

        internal static void RegisterAcceptor(string key, LocalAcceptor acceptor)
        {
            lock (OpenConnections)
            {
                OpenConnections[key] = acceptor;
            }
        }

        internal static void RemoveAcceptor(string key)
        {
            lock (OpenConnections)
            {
                OpenConnections.Remove(key);
            }
        }

        internal static bool IsRegistered(string key, LocalAcceptor acceptor)
        {
            LocalAcceptor v;
            if (!OpenConnections.TryGetValue(key, out v)) { return false; }
            return v == acceptor;
        }

        #endregion

        protected LocalHalfPipe handle;

        public LocalTransport(LocalHalfPipe hp) 
            : base(0)
        {
            delay = 0f;             // dude, we're fast
            handle = hp;
            MaximumPacketSize = 2048;
        }

        public override string Name
        {
            get { return "Local"; }
        }

        public override Reliability Reliability
        {
            get { return Reliability.Reliable; }
        }

        public override Ordering Ordering
        {
            get { return Ordering.Ordered; }
        }

        public override uint Backlog { get { return 0; } }

        public override bool Active
        {
            get { return handle != null; }
        }

        public override void SendPacket(TransportPacket packet)
        {
            ContractViolation.Assert(packet.Length > 0, "cannot send 0-byte packets");
            ContractViolation.Assert(packet.Length <= MaximumPacketSize,
                String.Format("packet exceeds maximum packet size: {0} > {1}", packet.Length, MaximumPacketSize));
            handle.Put(packet.ToArray());
            NotifyPacketSent(packet);
        }

        public override void Update()
        {
            byte[] packet;
            while ((packet = handle.Check()) != null)
            {
                NotifyPacketReceived(new TransportPacket(packet));
            }
        }

        public override uint MaximumPacketSize { get; set; }

        public override string ToString()
        {
            return "Local[" + handle.Identifier + "]";
        }
    }


    public class LocalHalfPipe
    {
        protected int identifier;
        protected Queue<byte[]> readQueue;
        protected Queue<byte[]> writeQueue;

        public LocalHalfPipe(int id, Queue<byte[]> rq, Queue<byte[]> wq)
        {
            identifier = id;
            readQueue = rq;
            writeQueue = wq;
        }

        public int Identifier { get { return identifier; } }

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