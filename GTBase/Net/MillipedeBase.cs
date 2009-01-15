using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using GT.Net;

/// General (client & server) part of the Millipede debugger
namespace GT.Millipede
{
    /// <summary>
    /// Determines the mode of the millipede debugger:
    /// </summary>
    public enum Mode
    {
        /// <summary>
        /// Does nothing
        /// </summary>
        Idle,
        ///<summary>
        /// Writes incoming and outgoing network traffic to a file
        ///</summary>
        Record,
        ///<summary>
        /// Injects recorded network traffic from a file
        ///</summary>
        Playback
    };

    /// <summary>
    /// Wrapper class for any given GT.Net.ITransport
    /// </summary>
    /// <see cref="GT.Net.ITransport"/>
    public class MillipedeTransport : ITransport
    {
        private readonly Mode mode = Mode.Idle;
        private readonly Stopwatch startTime = null;
        private readonly Stream dataSink = null;
        private readonly ITransport underlyingTransport = null;
        private readonly IList<NetworkEvent> dataSource = null;
        private bool running = false;

        public event PacketHandler PacketReceivedEvent;
        public event PacketHandler PacketSentEvent;

        /// <summary>
        /// Creates a wrapper for any given ITransport
        /// </summary>
        /// <param name="underlyingTransport">The underlying ITransport</param>
        /// <param name="startTime">Millepede's stopwatch for time recording</param>
        /// <param name="dataSink">A stream that saves data</param>
        /// <param name="dataSource">A stream that contains previously recorded data</param>
        /// <param name="mode">Millipede's current mode</param>
        public MillipedeTransport(ITransport underlyingTransport, Stopwatch startTime, Stream dataSink, IList<NetworkEvent> dataSource, Mode mode)
        {
            this.underlyingTransport = underlyingTransport;
            this.startTime = startTime;
            this.dataSink = dataSink;
            this.dataSource = dataSource;
            this.mode = mode;
            switch (this.mode)
            {
                case Mode.Record:
                    this.underlyingTransport.PacketReceivedEvent += UnderlyingTransports_PacketReceivedEvent;
                    this.underlyingTransport.PacketSentEvent += UnderlyingTransports_PacketSentEvent;
                    break;
                case Mode.Playback:
                    running = true;
                    ThreadPool.QueueUserWorkItem(InjectRecordedPackages);
                    break;
            }
        }

        /// <summary>
        /// ITransports use a observer-pattern (implemented with events & callbacks) to notify
        /// other GT2 components. Since these other componets register to the MillipedeTransport,
        /// there must be a mechanism to forward notifications from the ITransport to other GT2
        /// components.
        /// </summary>
        /// <see cref="ITransport.PacketSentEvent"/>
        private void UnderlyingTransports_PacketSentEvent(byte[] buffer, int offset, int count, ITransport transport)
        {
            if (PacketSentEvent == null)
            {
                return;
            }
            PacketSentEvent(buffer, offset, count, this);
        }

        /// <summary>
        /// ITransports use a observer-pattern (implemented with events & callbacks) to notify
        /// other GT2 components. Since these other componets register to the MillipedeTransport,
        /// there must be a mechanism to forward notifications from the ITransport to other GT2
        /// components.
        /// </summary>
        /// <see cref="ITransport.PacketReceivedEvent"/>
        private void UnderlyingTransports_PacketReceivedEvent(byte[] buffer, int offset, int count, ITransport transport)
        {
            if (PacketReceivedEvent == null)
            {
                return;
            }
            switch (mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_PacketReceived, buffer)).Serialize(dataSink);
                    break;
            }
            PacketReceivedEvent(buffer, offset, count, this);
        }

        /// <summary>
        /// On playback mode (Mode.Playback), this method is used to inject previoulsy recorded
        /// network traffic to other registered GT2 comonents via the ITransport.PacketReceivedEvent.
        /// </summary>
        /// <param name="unused"></param>
        private void InjectRecordedPackages(Object unused)
        {
            if (PacketReceivedEvent != null && dataSource.Count != 0 
                && dataSource[0].Time < startTime.ElapsedMilliseconds)
            {
                if (dataSource[0].Method == NetworkEvent.Event_PacketReceived)
                {
                    PacketReceivedEvent(dataSource[0].Message, 0, dataSource[0].Message.Length, this);
                }
                dataSource.RemoveAt(0);
            }
            if (running)
            {
                ThreadPool.QueueUserWorkItem(InjectRecordedPackages);
            }
        }

        /// <summary>
        /// Wraps ITransport.Name.
        /// </summary>
        /// <see cref="ITransport.Name"/>
        public string Name
        {
            get { return "Millipede{" + underlyingTransport.Name + "}"; }
        }

        /// <summary>
        /// Wraps ITransport.Backlog.
        /// </summary>
        /// <see cref="ITransport.Backlog"/>
        public uint Backlog
        {
            get { return underlyingTransport.Backlog; }
        }

        /// <summary>
        /// Wraps ITransport.Active.
        /// </summary>
        /// <see cref="ITransport.Active"/>
        public bool Active
        {
            get { return underlyingTransport.Active; }
        }

        /// <summary>
        /// Wraps ITransport.Reliability.
        /// </summary>
        /// <see cref="ITransportDeliveryCharacteristics.Reliability"/>
        public Reliability Reliability
        {
            get { return underlyingTransport.Reliability; }
        }

        /// <summary>
        /// Wraps ITransport.Ordering.
        /// </summary>
        /// <see cref="ITransportDeliveryCharacteristics.Ordering"/>
        public Ordering Ordering
        {
            get { return underlyingTransport.Ordering; }
        }

        /// <summary>
        /// Wraps ITransport.MaximumPacketSize.
        /// </summary>
        /// <see cref="ITransport.MaximumPacketSize"/>
        public int MaximumPacketSize
        {
            get { return underlyingTransport.MaximumPacketSize; }
        }

        /// <summary>
        /// Wraps ITransport.SendPacket(byte[],int,int). In addition, writes data to a sink if
        /// MillipedeTransport initialized with Mode.Record.
        /// </summary>
        /// <see cref="ITransport.SendPacket(byte[],int,int)"/>
        public void SendPacket(byte[] packet, int offset, int count)
        {
            switch (mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_SentPacket, packet)).Serialize(dataSink);
                    break;
            }
            underlyingTransport.SendPacket(packet, 0, packet.Length);
        }

        /// <summary>
        /// Wraps ITransport.SendPacket(Stream). In addition, it writes data to a sink if
        /// MillipedeTransport initialized with Mode.Record.
        /// </summary>
        /// <see cref="ITransport.SendPacket(Stream)"/>
        public void SendPacket(Stream packetStream)
        {
            switch (mode)
            {
                case Mode.Record:
                    byte[] tmpmsg = new byte[packetStream.Length];
                    packetStream.Position = 0;
                    packetStream.Read(tmpmsg, 0, (int)packetStream.Length);

                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_SentPacket, tmpmsg)).Serialize(dataSink);
                    break;
            }
            underlyingTransport.SendPacket(packetStream);
        }

        /// <summary>
        /// Wraps ITransport.Update.
        /// </summary>
        /// <see cref="ITransport.Update"/>
        public void Update()
        {
            underlyingTransport.Update();
        }

        /// <summary>
        /// Wraps ITransport.Capabilities.
        /// </summary>
        /// <see cref="ITransport.Capabilities"/>
        public IDictionary<string, string> Capabilities
        {
            get { return underlyingTransport.Capabilities; }
        }
        
        /// <summary>
        /// Wraps ITransport.GetPacketStream.
        /// </summary>
        /// <see cref="ITransport.GetPacketStream"/>
        public Stream GetPacketStream()
        {
            return underlyingTransport.GetPacketStream();
        }

        /// <summary>
        /// Wraps ITransport.Delay.
        /// </summary>
        /// <see cref="ITransportDeliveryCharacteristics.Delay"/>
        public float Delay
        {
            get { return underlyingTransport.Delay; }
            set { underlyingTransport.Delay = value; }
        }

        /// <summary>
        /// Wraps ITransport.Dispose.
        /// </summary>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            running = false;
            underlyingTransport.Dispose();
        }
    }

    /// <summary>
    /// Holds all necessary information about an network event.
    /// </summary>
    [Serializable]
    public class NetworkEvent
    {
        // Special strings to record some event
        public static readonly string Event_Started = "Start";
        public static readonly string Event_PacketReceived = "PacketReceivedEvent";
        public static readonly string Event_SentPacket = "SendPacket";
        public static readonly string Event_NewClient = "NewClientEvent_";
        public static readonly string Event_Connected = "Connect";
        public static readonly string Event_Stopped = "Stop";
        public static readonly string Event_Disposed = "Dispose";

        public int Time { get; private set; }
        public string Method { get; private set; }
        public byte[] Message { get; private set; }
        [NonSerialized] private static readonly IFormatter formatter = new BinaryFormatter();

        /// <summary>
        /// Creates a NetworkEvent with null as Message.
        /// </summary>
        /// <param name="time">Elapsed time</param>
        /// <param name="method">Method name from where the constructor is called</param>
        public NetworkEvent(int time, string method) { Time = time; Method = method; Message = null; }

        /// <summary>
        /// Creates a NetworkEvent
        /// </summary>
        /// <param name="time">Elapsed time</param>
        /// <param name="method">Method name from where the constructor is called</param>
        /// <param name="message">Message to be stored</param>
        public NetworkEvent(int time, string method, byte[] message) { Time = time; Method = method; Message = message; }


        /// <summary>
        /// Serializes itself to a stream.
        /// </summary>
        /// <param name="sink">Target stream for serialization</param>
        public void Serialize(Stream sink) { formatter.Serialize(sink, this); }

        /// <summary>
        /// Deserializes itself from a stream.
        /// </summary>
        /// <param name="source">Source stream for deserialization</param>
        /// <returns>Deserialized NetworkEvent</returns>
        public static NetworkEvent DeSerialize(Stream source) { return (NetworkEvent)formatter.Deserialize(source); }
    }
}
