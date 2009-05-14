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
using System.Collections.Generic;
using System.Diagnostics;
using GT.Utils;
using Common.Logging;

namespace GT.Net.Utils
{
    /// <summary>
    /// A simple utility class that drops transports that do not respond to
    /// GT's periodic heart-beat within a certain time.  This utility uses
    /// GT's events to automatically become aware of new connexions and
    /// transports.  The utility is installed onto a <see cref="Client"/> 
    /// or <see cref="Server"/> through <see cref="Install(GT.Net.Communicator,System.TimeSpan)"/>.
    /// </summary>
    public class PingBasedDisconnector
    {
        /// <summary>
        /// Install a disconnector on the provided communicator.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public static PingBasedDisconnector Install(Communicator c, TimeSpan timeout)
        {
            PingBasedDisconnector instance = new PingBasedDisconnector(c, timeout);
            instance.Start();
            return instance;
        }


        /// <summary>
        /// Provide notification of any errors that may occur.
        /// </summary>
        public event ErrorEventNotication ErrorEvent;
        
        protected ILog log;
        protected Communicator comm;
        protected TimeSpan timeout;
        protected WeakKeyDictionary<ITransport, Stopwatch> timers =
            new WeakKeyDictionary<ITransport, Stopwatch>();

        protected PingBasedDisconnector(Communicator c, TimeSpan timeout)
        {
            log = LogManager.GetLogger(GetType());

            comm = c;
            this.timeout = timeout;
        }

        /// <summary>
        /// Start the disconnector.
        /// </summary>
        public void Start()
        {
            if (timeout.CompareTo(comm.PingInterval) < 0)
            {
                NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.Configuration, 
                    "Timeout period is less than the ping time", null));
            }
            comm.Tick += _comm_Tick;
            comm.ConnexionAdded += _comm_ConnexionAdded;
            comm.ConnexionRemoved += _comm_ConnexionRemoved;
            foreach (IConnexion cnx in comm.Connexions)
            {
                _comm_ConnexionAdded(comm, cnx);
            }
        }

        /// <summary>
        /// Stop the disconnector.
        /// </summary>
        public void Stop()
        {
            comm.Tick -= _comm_Tick;
            comm.ConnexionAdded -= _comm_ConnexionAdded;
            comm.ConnexionRemoved -= _comm_ConnexionRemoved;
            foreach(IConnexion cnx in comm.Connexions)
            {
                _comm_ConnexionRemoved(comm, cnx);
            }
        }

        private void _comm_Tick(Communicator obj)
        {
            foreach (ITransport t in timers.Keys) 
            {
                Stopwatch sw = timers[t];
                if (timeout.CompareTo(sw.Elapsed) >= 0)
                {
                    log.Warn(String.Format("Stopped transport {0}: no ping received in {1}",
                        t, sw.Elapsed));
                    t.Dispose();
                }
            }
        }

        private void _comm_ConnexionAdded(Communicator c, IConnexion cnx)
        {
            cnx.TransportAdded += _cnx_TransportAdded;
            cnx.TransportRemoved += _cnx_TransportRemoved;
            cnx.PingRequested += _cnx_PingRequested;
            cnx.PingReplied += _cnx_PingReceived;
            foreach(ITransport t in cnx.Transports)
            {
                _cnx_TransportAdded(cnx, t);
            }
        }

        private void _cnx_TransportAdded(IConnexion connexion, ITransport transport)
        {
            timers[transport] = Stopwatch.StartNew();
        }

        private void _cnx_TransportRemoved(IConnexion connexion, ITransport transport)
        {
            timers.Remove(transport);
        }

        private void _comm_ConnexionRemoved(Communicator c, IConnexion conn)
        {
            conn.PingRequested -= _cnx_PingRequested;
            conn.PingReplied -= _cnx_PingReceived;
        }

        private void _cnx_PingReceived(ITransport transport, uint sequence, TimeSpan roundtrip)
        {
            Stopwatch sw;
            if (!timers.TryGetValue(transport, out sw))
            {
                log.Warn("Unexpected: no record of pinged transport: " + transport);
                return;
            }
            sw.Reset();
            sw.Start();
        }

        private void _cnx_PingRequested(ITransport transport, uint sequence)
        {
            if (!timers.ContainsKey(transport))
            {
                timers[transport] = Stopwatch.StartNew();
            }
        }

        protected void NotifyError(ErrorSummary es)
        {
            if (ErrorEvent == null)
            {
                es.LogTo(log);
                return;
            }

            try { ErrorEvent(es); }
            catch (Exception e)
            {
                log.Warn("Exception occurred when processing application ErrorEvent handlers", e);
                ErrorEvent(new ErrorSummary(Severity.Information, SummaryErrorCode.UserException,
                    "Exception occurred when processing application ErrorEvent handlers", e));
            }
        }
    }


    /// <summary>
    /// A simple class for simulating certain network transmission characteristics
    /// onto a transport.  This class was inspired by the Linux WanEm network emulator.
    /// Please note that this interface is likely to change in subsequent releases.  
    /// Packet loss is only done for those transports that either unreliable or not ordered.
    /// Packet reordering is only done for transports that are unordered.
    /// 
    /// <para>
    /// Packet delay is calculated from a delay provider; this provider could be
    /// calculated from a probability distribution, such as a Gaussian or Poisson.
    /// For example:
    /// </para>
    /// <code>
    ///     GaussianRandomNumberGenerator grng = new GaussianRandomNumberGenerator(200, 30);
    ///     transport.DelayProvider = () => TimeSpan.FromMilliseconds(grng.NextDouble());
    /// </code>
    /// An alternative might be to model a Poisson process by sampling from a negative 
    /// exponential probability distribution to simulate interarrival times:
    /// <code>
    ///     Random r = new Random();
    ///     transport.DelayProvider = 
    ///         () => TimeSpan.FromMilliseconds(Math.Min(-Math.Log(random.NextDouble()), 5.0) * 1000);
    /// </code>
    /// This class does the right thing for ordered transports.
    /// </summary>
    public class NetworkEmulatorTransport : WrappedTransport
    {
        public enum PacketMode { Sent, Received };
        public enum PacketEffect { None, Dropped, Delayed, Reordered };

        /// <summary>
        /// Provide notification as to the disposition taken for each packet sent.
        /// </summary>
        public event Action<PacketMode, PacketEffect, TransportPacket> PacketDisposition;

        public NetworkEmulatorTransport(ITransport wrapped) : base(wrapped)
        {
            DelayProvider = () => TimeSpan.Zero;
            PacketLoss = 0;
            PacketLossCorrelation = 0;
            PacketReordering = 0;

            urng = new Random();

            lastPacketTime = new Dictionary<PacketMode, long>();
            foreach(PacketMode mode in Enum.GetValues(typeof(PacketMode)))
            {
                lastPacketTime[mode] = timer.TimeInMilliseconds;
            }
        }
        
        /// <summary>
        /// A delegate for calculating a delay to be applied to a packet.
        /// </summary>
        public Returning<TimeSpan> DelayProvider { get; set; }

        /// <summary>
        /// The probability that a packet is dropped.
        /// If <see cref="PacketLossCorrelation"/> is non-zero, then the probability
        /// of a packet being dropped will be statistically correlated with
        /// the probability that the previous packet was dropped.
        /// Applies both to packets sent and received.
        /// </summary>
        public double PacketLoss
        {
            get { return packetLoss; }
            set
            {
                if (value < 0 || value > 100)
                {
                    throw new ArgumentException("must be on [0,100]");
                }
                packetLoss = value;
            }
        }

        /// <summary>
        /// Make the probability of a packet being dropped be statistically
        /// correlated with the probability that the previous packet was dropped.
        /// Applies both to packets sent and received.
        /// </summary>
        public double PacketLossCorrelation
        {
            get { return packetLossCorrelation; }
            set
            {
                if (value < 0 || value > 100)
                {
                    throw new ArgumentException("must be on [0,100]");
                }
                packetLossCorrelation = value;
            }
        }

        
        /// <summary>
        /// The probability of a packet being reordered.
        /// Applies both to packets sent and received.
        /// </summary>
        public double PacketReordering
        {
            get { return packetReordering; }
            set
            {
                if (value < 0 || value > 100)
                {
                    throw new ArgumentException("must be on [0,100]");
                }
                packetReordering = value;
            }
        }


        protected DelayQueue<TransportPacket> delayedSendQueue = new DelayQueue<TransportPacket>();
        protected DelayQueue<TransportPacket> delayedReceiveQueue = new DelayQueue<TransportPacket>();
        protected Random urng;
        protected HPTimer timer = HPTimer.StartNew();
        // scheduled time for last packet (timer + delay)
        protected IDictionary<PacketMode, long> lastPacketTime;  
        protected Returning<TimeSpan> delayWizard;
        protected double packetLoss;    // set by PacketLoss
        protected double packetLossCorrelation;    // set by PacketLossCorrelation
        protected double packetReordering;    // set by PacketReordering
        /// <summary>
        /// The probability that the last packet was dropped, kept for
        /// statistical correlation of packet loss.
        /// </summary>
        /// <seealso cref="PacketLossCorrelation"/>
        protected double pLastDropped = 0d;



        public override uint Backlog
        {
            get { return base.Backlog + delayedSendQueue.Count; }
        }

        public override void SendPacket(TransportPacket packet)
        {
            timer.Update();
            CheckDelayedPackets((uint)timer.ElapsedInMilliseconds);
            ProcessPacket(PacketMode.Sent, packet, delayedSendQueue);
            CheckDelayedPackets(0);  // in case of any 0-delayed packets
        }

        public override void Update()
        {
            timer.Update();
            CheckDelayedPackets((uint)timer.ElapsedInMilliseconds);
            base.Update();
            CheckDelayedPackets(0);  // in case of any 0-delayed packets
        }

        protected override void _wrapped_PacketReceivedEvent(TransportPacket packet, ITransport transport)
        {
            timer.Update();
            CheckDelayedPackets((uint)timer.ElapsedInMilliseconds);
            ProcessPacket(PacketMode.Received, packet, delayedReceiveQueue);
            CheckDelayedPackets(0);  // in case of any 0-delayed packets
        }

        private void CheckDelayedPackets(uint millisecondsElapsed)
        {
            delayedSendQueue.Dequeue(millisecondsElapsed, 
                p => base.SendPacket(p));
            delayedReceiveQueue.Dequeue(millisecondsElapsed,
                p => NotifyPacketReceived(p, this));
        }

        private void ProcessPacket(PacketMode mode, TransportPacket packet, DelayQueue<TransportPacket> queue)
        {
            uint delay = (uint)Math.Max(0, CalculateDelay());
            if (Ordering != Ordering.Unordered)
            {
                // if this transport is ordered or sequenced, then the packets 
                // must be sent in order, and thus must be delayed in order.
                // Thus when figuring out the possible delay of a packet,
                // we must ensure it's at least as delayed as the previous
                // packet, which must be the most-delayed packet.
                delay = (uint)Math.Max(delay, lastPacketTime[mode] - timer.TimeInMilliseconds);
            }

            if (Reliability != Reliability.Reliable || Ordering != Ordering.Ordered)
            {
                double pDropped = PacketLossCorrelation * pLastDropped +
                    (1d - PacketLossCorrelation) * urng.NextDouble();
                pLastDropped = pDropped;
                if(pDropped < PacketLoss)
                {
                    NotifyPacketDisposition(mode, PacketEffect.Dropped, packet);
                    return;
                }
            }

            // don't include reordering time when recording the scheduled
            // time for this current packet -- if we reorder by delaying,
            // then we *don't* want subsequent packets
            lastPacketTime[mode] = timer.TimeInMilliseconds + delay;

            // Sequenced and ordered transports cannot have out-of-order packets
            if (Ordering == Ordering.Unordered && urng.NextDouble() < PacketReordering)
            {
                // reorder packets by delaying this current packet
                delay += queue.MaximumDelay == 0 ? 10u : queue.MaximumDelay / 2;
                NotifyPacketDisposition(mode, PacketEffect.Reordered, packet);
            }
            else if(delay > 0)
            {
                NotifyPacketDisposition(mode, PacketEffect.Delayed, packet);
            } 
            else
            {
                NotifyPacketDisposition(mode, PacketEffect.None, packet);
            }
            queue.Enqueue(packet, delay);
        }

        private void NotifyPacketDisposition(PacketMode mode, PacketEffect effect, TransportPacket packet)
        {
            if(PacketDisposition != null)
            {
                PacketDisposition(mode, effect, packet);
            }
        }

        private double CalculateDelay()
        {
            if (DelayProvider == null) { return 0; }
            return DelayProvider().TotalMilliseconds;
        }
    }
}
