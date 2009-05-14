//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;
using System.Diagnostics;
using System.Collections.Generic;
using Common.Logging;
using GT.Utils;

namespace GT.Net
{

    public delegate void PacketHandler(TransportPacket packet, ITransport transport);

    /// <remarks>
    /// Represents a connection to either a server or a client.
    /// Errors should be notified by throwing a <see cref="TransportError"/>;
    /// warnings and recovered errors are notified through the <see cref="ErrorEvent"/>.
    /// Transports are responsible for disposing of any <see cref="TransportPacket"/>
    /// instances provided to <see cref="SendPacket"/>.
    /// <see cref="TransportPacket"/> instances provided through the <see cref="PacketSent"/>
    /// and <see cref="PacketReceived"/> are disposed of upon the completion of the
    /// callback, and thus only have a lifetime of the callback
    /// unless they are first attached to using <see cref="TransportPacket.Retain"/>.
    /// </remarks>
    public interface ITransport : ITransportDeliveryCharacteristics, IDisposable
    {
        /// <summaryas>
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

        /// <summary>
        /// An event triggered on receiving an incoming packet.
        /// The <see cref="TransportPacket"/> instance generally only has a 
        /// lifetime of the callback; be sure to first call <see cref="TransportPacket.Retain"/>
        /// to reference the packet beyond the callback.
        /// </summary>
        event PacketHandler PacketReceived;
        
        /// <summary>
        /// An event triggered having sent a packet.
        /// The <see cref="TransportPacket"/> instance generally only has a 
        /// lifetime of the callback; be sure to first call <see cref="TransportPacket.Retain"/>
        /// to reference the packet beyond the callback.
        /// </summary>
        event PacketHandler PacketSent;

        /// <summary>
        /// Raised to notify of warnings or recovered errors arising from the use of
        /// this transport.  Unrecoverable errors are indicated by <see cref="TransportError"/>.
        /// Primarily used for indicating that the instance has become backlogged.
        /// different transports handle backlog differently; reports with
        /// <see cref="Severity.Information"/> generally indicates that the packet was
        /// be queued or buffered to be resent on subsequent calls to <see cref="SendPacket"/>
        /// and <see cref="Update"/>;  <see cref="Severity.Warning"/> generally indicates that 
        /// the packet has been discarded.
        /// </summary>
        event ErrorEventNotication ErrorEvent;

        /// <summary>
        /// A set of tags describing the capabilities of the transport and of expectations/capabilities
        /// of the users of this transport.
        /// </summary>
        IDictionary<string,string> Capabilities { get; }

        /// <summary>
        /// Send the given packet to the server.  This transport will call
        /// <see cref="TransportPacket.Dispose"/> once completed.
        /// </summary>
        /// <param name="packet">the packet to send</param>
        /// <exception cref="TransportError">thrown on a fatal transport error.</exception>
        void SendPacket(TransportPacket packet);

        /// <summary>
        /// Process any events pertaining to this instance; also flushes any 
        /// backlogged outgoing packets.
        /// </summary>
        /// <exception cref="TransportError">thrown on a fatal transport error.</exception>
        void Update();

        /// <summary>
        /// The maximum packet size supported by this transport instance (in bytes).
        /// Particular transports may enforce a cap on this value.
        /// </summary>
        uint MaximumPacketSize { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// <see cref="NotifyPacketSent"/> and <see cref="NotifyPacketReceived"/> 
    /// will ensure that the packet is disposed after the callbacks have been
    /// issued.  Listeners wishing to hold onto the packet should call
    /// <see cref="TransportPacket.Retain"/>.
    /// </remarks>
    public abstract class BaseTransport : ITransport
    {
        protected ILog log;

        private Dictionary<string, string> capabilities = new Dictionary<string, string>();
        public event PacketHandler PacketReceived;
        public event PacketHandler PacketSent;
        public event ErrorEventNotication ErrorEvent;
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

        public abstract void SendPacket(TransportPacket packet);

        public abstract void Update();

        protected virtual void NotifyPacketReceived(TransportPacket packet)
        {
            // DebugUtils.DumpMessage(this.ToString() + " notifying of received message", buffer, offset, count);
            if (PacketReceived == null)
            {
                NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.Configuration,
                    "transport has no listeners for receiving incoming messages!", this, null));
            } 
            else
            {
                PacketReceived(packet, this);
            }
            // event listeners are responsible for calling Retain() if they
            // want to use it for longer.
            packet.Dispose();   
        }

        protected virtual void NotifyPacketSent(TransportPacket packet)
        {
            if (PacketSent != null) { PacketSent(packet, this); }
            packet.Dispose();
        }

        protected virtual void NotifyError(ErrorSummary es)
        {
            es.LogTo(log);
            if(ErrorEvent != null)
            {
                ErrorEvent(es);
            }
        }
    }

    /// <summary>
    /// A basic transport wrapper.
    /// </summary>
    public class WrappedTransport : ITransport
    {
        public event PacketHandler PacketReceived;
        public event PacketHandler PacketSent;
        public event ErrorEventNotication ErrorEvent;

        protected ILog log;

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
            log = LogManager.GetLogger(GetType());

            Wrapped = wrapped;

            Wrapped.PacketReceived += _wrapped_PacketReceivedEvent;
            Wrapped.PacketSent += _wrapped_PacketSentEvent;
            Wrapped.ErrorEvent += NotifyError;
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

        virtual public void SendPacket(TransportPacket packet)
        {
            Wrapped.SendPacket(packet);
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

        virtual protected void _wrapped_PacketSentEvent(TransportPacket packet, ITransport transport)
        {
            NotifyPacketSent(packet, transport);
        }

        protected void NotifyPacketSent(TransportPacket packet, ITransport transport)
        {
            if (PacketSent != null)
            {
                PacketSent(packet, this);
            }
        }

        virtual protected void _wrapped_PacketReceivedEvent(TransportPacket packet, ITransport transport)
        {
            NotifyPacketReceived(packet, transport);
        }

        protected void NotifyPacketReceived(TransportPacket packet, ITransport transport)
        {
            if (PacketReceived == null)
            {
                NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.Configuration,
                    "transport has no listeners for receiving incoming messages!", this, null));
            }
            else
            {
                PacketReceived(packet, this);
            }
        }

        protected void NotifyError(ErrorSummary summary)
        {
            if (ErrorEvent != null)
            {
                ErrorEvent(summary);
            }
            else
            {
                summary.LogTo(log);
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
        protected Queue<TransportPacket> bucketContents;

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
            
            bucketContents = new Queue<TransportPacket>();
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

        override public void SendPacket(TransportPacket packet)
        {
            Debug.Assert(packet.Length <= DrainageAmount,
                "leaky bucket will never have sufficient capacity to send a packet of size " 
                + packet.Length);
            CheckDrain();
            DrainPackets();
            QueuePacket(packet);
            DrainPackets();
        }

        override public void Update()
        {
            base.Update();
            CheckDrain();
            DrainPackets();
        }


        protected void QueuePacket(TransportPacket packet)
        {
            if (contentsSize + packet.Length > MaximumCapacity)
            {
                NotifyError(new ErrorSummary(Severity.Warning,
                    SummaryErrorCode.TransportBacklogged,
                    "Capacity exceeded: packet discarded", this, null));
                return;
            }

            bucketContents.Enqueue(packet);
            contentsSize += (uint)packet.Length;
        }

        protected void DrainPackets()
        {
            while (availableCapacity > 0 && bucketContents.Count > 0)
            {
                TransportPacket packet = bucketContents.Peek();
                if (packet.Length > availableCapacity) { return; }
                int packetLength = packet.Length;   // must stash as packet is disposed in SendPacket
                bucketContents.Dequeue();
                base.SendPacket(packet);
                contentsSize -= (uint)packetLength;
                availableCapacity -= (uint)packetLength;
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
        protected Queue<TransportPacket> queuedPackets;

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

            queuedPackets = new Queue<TransportPacket>();
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

        override public void SendPacket(TransportPacket packet)
        {
            Debug.Assert(packet.Length < MaximumCapacity, 
                "transport can never have sufficient capacity to send this message");

            QueuePacket(packet);
            if (!TrySending())
            {
                NotifyError(new ErrorSummary(Severity.Information,
                    SummaryErrorCode.TransportBacklogged,
                    "Capacity exceeded: packet queued", this, null));
                return;
            }
        }

        protected void QueuePacket(TransportPacket packet)
        {
            queuedPackets.Enqueue(packet);
        }

        /// <summary>
        /// Could throw an exception.
        /// </summary>
        /// <returns>true if all packets were sent, false if there are still some
        ///     packets queued</returns>
        protected bool TrySending()
        {
            // add to capacity to the maximum
            AccmulateNewTokens();
            while (queuedPackets.Count > 0)
            {
                TransportPacket packet = queuedPackets.Peek();
                if(capacity < packet.Length)
                {
                    return false;
                }
                capacity -= packet.Length;
                queuedPackets.Dequeue();
                base.SendPacket(packet);
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
