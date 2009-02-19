using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Common.Logging;
using GT.Utils;

namespace GT.Net
{

    public delegate void PacketHandler(byte[] buffer, int offset, int count, ITransport transport);

    /// <remarks>
    /// Represents a connection to either a server or a client.
    /// Errors should be notified by throwing an instanceof TransportError.
    /// Should the transport have been cleanly shutdown by the remote side, then
    /// throw a TransportDecomissionedException.
    /// </remarks>
    public interface ITransport : ITransportDeliveryCharacteristics, IDisposable
    {
        /// <summary>
        /// A simple identifier for this transport.  This name should uniquely identify this
        /// transport.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Is this instance active?
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// How many packets are backlogged waiting to be sent?
        /// </summary>
        uint Backlog { get; }

        event PacketHandler PacketReceivedEvent;
        event PacketHandler PacketSentEvent;

        /// <summary>
        /// A set of tags describing the capabilities of the transport and of expectations/capabilities
        /// of the users of this transport.
        /// </summary>
        IDictionary<string,string> Capabilities { get; }

        /// <summary>
        /// Send the given message to the server.
        /// </summary>
        /// <param name="packet">the packet of message(s) to send</param>
        /// <param name="offset">the offset into the packet to send</param>
        /// <param name="count">the number of bytes within the packet to send</param>
        /// <exception cref="TransportError">thrown on a fatal transport error.</exception>
        void SendPacket(byte[] packet, int offset, int count);

        /// <summary>
        /// Send the given message to the server.  The stream is sent 
        /// <b>from the stream's current position</b> to the end of the stream.  
        /// <b>It is not sent from position 0.</b>
        /// </summary>
        /// <param name="stream">the stream encoding the packet</param>
        /// <exception cref="TransportError">thrown on a fatal transport error.</exception>
        void SendPacket(Stream stream);

        /// <summary>
        /// Get a suitable stream for the transport.
        /// </summary>
        Stream GetPacketStream();

        /// <exception cref="TransportError">thrown on a fatal transport error.</exception>
        void Update();

        /// <summary>
        /// The maximum packet size supported by this transport instance (in bytes).
        /// Particular transports may enforce a cap on this value.
        /// </summary>
        uint MaximumPacketSize { get; set; }
    }

    public abstract class BaseTransport : ITransport
    {
        protected ILog log;

        private Dictionary<string, string> capabilities = new Dictionary<string, string>();
        public event PacketHandler PacketReceivedEvent;
        public event PacketHandler PacketSentEvent;
        public abstract string Name { get; }
        public abstract uint Backlog { get; }
        public abstract bool Active { get; }

        protected readonly uint PacketHeaderSize;
        protected uint AveragePacketSize = 64; // guestimate on avg packet size

        protected BaseTransport(uint packetHeaderSize)
        {
            log = LogManager.GetLogger(GetType());

            PacketHeaderSize = packetHeaderSize;
        }

        virtual public void Dispose() { /* empty implementation */ }

        #region Transport Characteristics

        public abstract Reliability Reliability { get; }
        public abstract Ordering Ordering { get; }
        public abstract uint MaximumPacketSize { get; set;  }

        /// <summary>The average amount of latency between this server 
        /// and the client (in milliseconds).</summary>
        protected float delay = 20f;
        protected float delayMemory = 0.95f;
        protected StatisticalMoments delayStats = new StatisticalMoments();

        public virtual float Delay
        {
            get { return delay; }
            set {
                delayStats.Accumulate(value);
                Debug.Assert(delayMemory >= 0f && delayMemory <= 1.0f);
                delay = delayMemory * delay + (1f - delayMemory) * value; 
            }
        }

        #endregion

        public IDictionary<string, string> Capabilities
        {
            get { return capabilities; }
        }

        public abstract void SendPacket(byte[] packet, int offset, int count);
        public abstract void SendPacket(Stream packetStream);

        // Must be kept in sync with CheckValidPacketStream()
        virtual public Stream GetPacketStream()
        {
            MemoryStream ms = new MemoryStream((int)(PacketHeaderSize + AveragePacketSize));

            // Encode a known byte pattern to verify that is same stream provided to SendPacket.
            byte[] name = Encoding.UTF8.GetBytes(Name);
            for (int i = 0; i < PacketHeaderSize; i++)
            {
                ms.WriteByte(name[i % name.Length]);
            }

            Debug.Assert(ms.Position == PacketHeaderSize);
            return ms;
        }

        /// <summary>
        /// Check that this stream is a stream as obtained from <see cref="GetPacketStream"/>.
        /// </summary>
        /// <param name="stream">the stream to check</param>
        protected virtual void CheckValidPacketStream(Stream stream)
        {
            ContractViolation.Assert(stream is MemoryStream, "stream should be a MemoryStream");
            // Encode a known byte pattern to verify that is same stream provided to SendPacket.
            byte[] name = Encoding.UTF8.GetBytes(Name);
            long savedPosition = stream.Position;
            stream.Position = 0;
            for (int i = 0; i < PacketHeaderSize; i++)
            {
                ContractViolation.Assert(stream.ReadByte() == name[i % name.Length], "Stream header appears to have been overwritten");
            }
            stream.Position = savedPosition;
        }

        public abstract void Update();

        protected virtual void NotifyPacketReceived(byte[] buffer, int offset, int count)
        {
            // DebugUtils.DumpMessage(this.ToString() + " notifying of received message", buffer, offset, count);
            if (PacketReceivedEvent == null)
            {
                Debug.WriteLine(DateTime.Now + " WARNING: transport has nobody to receive incoming messages!");
                return;
            }
            PacketReceivedEvent(buffer, offset, count, this);
        }

        protected virtual void NotifyPacketSent(byte[] buffer, int offset, int count)
        {
            if (PacketSentEvent == null) { return; }
            PacketSentEvent(buffer, offset, count, this);
        }
    }

    /// <summary>
    /// A basic transport wrapper.
    /// </summary>
    public class WrappedTransport : ITransport
    {
        public event PacketHandler PacketReceivedEvent;
        public event PacketHandler PacketSentEvent;

        /// <summary>
        /// Return the wrapper transport instance
        /// </summary>
        public ITransport Wrapped { get; internal set; }

        /// <summary>
        /// Wrap the provided transport.
        /// </summary>
        /// <param name="wrapped">the transport to be wrapped</param>
        public WrappedTransport(ITransport wrapped)
        {
            Wrapped = wrapped;

            Wrapped.PacketReceivedEvent += _wrapped_PacketReceivedEvent;
            Wrapped.PacketSentEvent += _wrapped_PacketSentEvent;
        }

        virtual public Reliability Reliability
        {
            get { return Wrapped.Reliability; }
        }

        virtual public Ordering Ordering
        {
            get { return Wrapped.Ordering; }
        }

        virtual public float Delay
        {
            get { return Wrapped.Delay; }
            set { Wrapped.Delay = value; }
        }

        virtual public void Dispose()
        {
            Wrapped.Dispose();
        }

        virtual public string Name
        {
            get { return Wrapped.Name; }
        }

        virtual public bool Active
        {
            get { return Wrapped.Active; }
        }

        virtual public uint Backlog
        {
            get { return Wrapped.Backlog; }
        }

        virtual public IDictionary<string, string> Capabilities
        {
            get { return Wrapped.Capabilities; }
        }

        virtual public void SendPacket(byte[] packet, int offset, int count)
        {
            Wrapped.SendPacket(packet, offset, count);
        }

        virtual public void Update()
        {
            Wrapped.Update();
        }

        virtual public uint MaximumPacketSize
        {
            get { return Wrapped.MaximumPacketSize; }
            set { Wrapped.MaximumPacketSize = value; }
        }

        // Use our own memory streams as we don't know what kind of
        // packet header the wrapped transport might place on the stream.
        public void SendPacket(Stream stream)
        {
            byte[] packet = ((MemoryStream)stream).ToArray();
            SendPacket(packet, 0, packet.Length);
        }

        virtual public Stream GetPacketStream()
        {
            return new MemoryStream();
        }

        virtual protected void _wrapped_PacketSentEvent(byte[] buffer, int offset, 
            int count, ITransport transport)
        {
            if (PacketSentEvent != null)
            {
                PacketSentEvent(buffer, offset, count, this);
            }
        }

        virtual protected void _wrapped_PacketReceivedEvent(byte[] buffer, int offset, 
            int count, ITransport transport)
        {
            if (PacketReceivedEvent != null)
            {
                PacketReceivedEvent(buffer, offset, count, this);
            }
        }
    }

    /// <summary>
    /// The leaky bucket algorithm is a method for shaping traffic, modelled
    /// on a bucket with a maximum capacity that drains at a fixed rate 
    /// (# bytes for some time unit). Packets to be sent are
    /// added to the bucket (buffered) until the bucket overflows, and
    /// and subsequent packets are discarded until the bucket has drained
    /// sufficiently. The leaky bucket algorithm thus imposes a hard limit 
    /// on the data transmission rate.
    /// </summary>
    public class LeakyBucketTransport : WrappedTransport
    {
        /// <summary>
        /// The capacity of the bucket, in bytes.  Packets to be sent are
        /// added to the bucket (buffered) until the bucket overflows, and
        /// and subsequent packets are discarded until the bucket drains
        /// sufficiently.
        /// </summary>
        public uint MaximumCapacity { get; set; }

        /// <summary>
        /// The drainage rate, in bytes per second
        /// </summary>
        public float DrainRate
        {
            get { return (int)(DrainageAmount / TimeUnit.TotalSeconds); }
        }

        /// <summary>
        /// The number of bytes that drain per time unit
        /// </summary>
        public uint DrainageAmount { get; set; }

        /// <summary>
        /// The time period for assessing drainage.  That is, <see cref="DrainageAmount"/>
        /// bytes is allowed to drain out per <see cref="TimeUnit"/>.
        /// </summary>
        public TimeSpan TimeUnit { get; set; }

        /// <summary>
        /// The number of bytes available for sending for the remainder
        /// of the current time unit.
        /// </summary>
        protected uint availableCapacity;

        /// <summary>
        /// Tracks the elapsed time for the current time unit.
        /// </summary>
        protected Stopwatch timer;

        /// <summary>
        /// The bucket contents
        /// </summary>
        protected Queue<byte[]> bucketContents;

        protected uint contentsSize;

        /// <summary>
        /// Wrap the provided transport.  This leaky bucket allows up to <see cref="drainageAmount"/>
        /// bytes to be sent every <see cref="drainageTimeUnit"/> time units.  The bucket is
        /// configured to buffer up to <see cref="bucketCapacity"/> bytes.
        /// </summary>
        /// <param name="wrapped">the transport to be wrapped</param>
        /// <param name="drainageAmount">the number of bytes drained per drainage time unit</param>
        /// <param name="drainageTimeUnit">the time unit to reset the available drainage amount</param>
        /// <param name="bucketCapacity">the maximum capacity of this leaky bucket</param>
        public LeakyBucketTransport(ITransport wrapped, uint drainageAmount, TimeSpan drainageTimeUnit,
            uint bucketCapacity)
            : base(wrapped)
        {
            DrainageAmount = drainageAmount;
            TimeUnit = drainageTimeUnit;
            MaximumCapacity = bucketCapacity;
            
            bucketContents = new Queue<byte[]>();
            contentsSize = 0;

            availableCapacity = DrainageAmount;
            timer = Stopwatch.StartNew();
        }

        /// <summary>
        /// How many bytes are available to send during the remainder of this time unit?
        /// </summary>
        public uint AvailableCapacity
        {
            get
            {
                CheckDrain();
                return availableCapacity;
            }
        }

        /// <summary>
        /// How much capacity remains in this bucket (in bytes)?
        /// </summary>
        public uint RemainingBucketCapacity { get { return MaximumCapacity - contentsSize; } }

        public override uint MaximumPacketSize
        {
            get
            {
                return Math.Min(DrainageAmount, base.MaximumPacketSize);
            }
            set
            {
                base.MaximumPacketSize = Math.Min(DrainageAmount, value);
            }
        }

        public override Reliability Reliability
        {
            get { return Reliability.Unreliable; }
        }

        public override uint Backlog
        {
            get
            {
                return (uint)bucketContents.Count + base.Backlog;
            }
        }

        override public void SendPacket(byte[] packet, int offset, int count)
        {
            Debug.Assert(count <= DrainageAmount,
                "leaky bucket will never have sufficient capacity to send a packet of size " + count);
            CheckDrain();
            DrainPackets();
            QueuePacket(packet, offset, count);
            DrainPackets();
        }

        override public void Update()
        {
            base.Update();
            CheckDrain();
            DrainPackets();
        }


        protected void QueuePacket(byte[] packet, int offset, int count)
        {
            if (contentsSize + count > MaximumCapacity)
            {
                throw new TransportBackloggedWarning(this);
            }

            if (offset == 0 && count == packet.Length)
            {
                bucketContents.Enqueue(packet);
            }
            else
            {
                byte[] ba = new byte[count];
                Array.Copy(packet, offset, ba, 0, count);
                bucketContents.Enqueue(ba);
            }
            contentsSize += (uint)count;
        }

        protected void DrainPackets()
        {
            while (availableCapacity > 0 && bucketContents.Count > 0)
            {
                byte[] packet = bucketContents.Peek();
                if (packet.Length > availableCapacity) { return; }
                base.SendPacket(packet, 0, packet.Length);
                bucketContents.Dequeue();
                contentsSize -= (uint)packet.Length;
                availableCapacity -= (uint)packet.Length;
            }
        }

        /// <summary>
        /// Check to see if we've passed the time unit.  If so, make more
        /// drainage space available.
        /// </summary>
        protected void CheckDrain()
        {
            if (TimeUnit.CompareTo(timer.Elapsed) <= 0)
            {
                availableCapacity = DrainageAmount;
                timer.Reset();
                timer.Start();
            }
        }
    }


    /// <summary>
    /// The token bucket algorithm is a method for shaping traffic.  Tokens, 
    /// representing some transmission capacity, are added to the bucket on a 
    /// periodic basis.  There is a maximum capacity.  Each byte sent requires
    /// available tokens in the bucket.  If there are no tokens available, 
    /// the packet is queued. The token bucket allows bursty traffic.
    /// Note that this implementation is expressed in <em>bytes</em> per second, and not
    /// packets per second.
    /// </summary>
    public class TokenBucketTransport : WrappedTransport
    {
        /// <summary>
        /// The rate at which the bucket refills.
        /// This value is expressed in bytes per second.
        /// </summary>
        public float RefillRate { get; set; }

        /// <summary>
        /// The maximum sustained capacity; this is the bucket maximum beyond which
        /// it cannot fill up any further.  This value is expressed in bytes.
        /// </summary>
        public uint MaximumCapacity { get; set; }

        /// <summary>
        /// The currently available capacity.
        /// </summary>
        protected float capacity;

        /// <summary>
        /// Track the time elapsed since the last token-accumulation check.
        /// </summary>
        protected Stopwatch timer;

        /// <summary>
        /// The packets that are queued for sending.
        /// </summary>
        protected Queue<byte[]> queuedPackets;

        /// <summary>
        /// Wrap the provided transport.
        /// </summary>
        /// <param name="wrapped">the transport to be wrapped</param>
        /// <param name="refillRate">the token refilling rate, in bytes per second</param>
        /// <param name="maximumCapacity">the maximum transmission capacity, expressed
        /// in bytes</param>
        public TokenBucketTransport(ITransport wrapped, float refillRate, uint maximumCapacity)
            : base(wrapped)
        {
            RefillRate = refillRate;
            MaximumCapacity = maximumCapacity;

            queuedPackets = new Queue<byte[]>();
            timer = Stopwatch.StartNew();
            capacity = maximumCapacity;
        }

        public override uint MaximumPacketSize
        {
            get
            {
                return Math.Min(MaximumCapacity, base.MaximumPacketSize);
            }
            set
            {
                base.MaximumPacketSize = Math.Min(MaximumCapacity, value);
            }
        }

        public override uint Backlog
        {
            get
            {
                return (uint)queuedPackets.Count + base.Backlog;
            }
        }

        /// <summary>
        /// How many bytes are available to send at this very moment?
        /// </summary>
        public int AvailableCapacity
        {
            get
            {
                AccmulateNewTokens();
                return (int)capacity;
            }
        }

        override public void SendPacket(byte[] packet, int offset, int count)
        {
            Debug.Assert(count < MaximumCapacity, 
                "transport can never have sufficient capacity to send this message");

            QueuePacket(packet, offset, count);
            if (!TrySending())
            {
                throw new TransportBackloggedWarning(this);
            }
        }

        protected void QueuePacket(byte[] packet, int offset, int count)
        {
            if (offset == 0 && count == packet.Length)
            {
                queuedPackets.Enqueue(packet);
            }
            else
            {
                byte[] ba = new byte[count];
                Array.Copy(packet, offset, ba, 0, count);
                queuedPackets.Enqueue(ba);
            }
        }

        /// <summary>
        /// Could throw an exception.
        /// </summary>
        /// <returns></returns>
        protected bool TrySending()
        {
            // add to capacity to the maximum
            AccmulateNewTokens();
            while (queuedPackets.Count > 0)
            {
                byte[] packet = queuedPackets.Peek();
                if(capacity < packet.Length)
                {
                    return false;
                }
                capacity -= packet.Length;
                queuedPackets.Dequeue();
                base.SendPacket(packet, 0, packet.Length);
            }
            return true;
        }

        protected void AccmulateNewTokens()
        {
            float elapsedMillis = timer.ElapsedMilliseconds;
            if (elapsedMillis > 0f)
            {
                capacity = Math.Min(MaximumCapacity,
                    capacity + (RefillRate * elapsedMillis / 1000f));
                timer.Reset();
                timer.Start();
            }
        }

        override public void Update()
        {
            try
            {
                TrySending();
            } catch(TransportError) { /* ignore */ }
            base.Update();
        }
    }
}
