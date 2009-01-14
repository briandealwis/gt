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
        private readonly Mode m_Mode = Mode.Idle;
        private readonly Stopwatch m_StartTime = null;
        private readonly Stream m_DataSink = null;
        private readonly ITransport m_UnderlyingTransports = null;
        private readonly IList<NetworkEvent> m_DataSource = null;
        private bool m_Running = false;

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
            m_UnderlyingTransports = underlyingTransport;
            m_StartTime = startTime;
            m_DataSink = dataSink;
            m_DataSource = dataSource;
            m_Mode = mode;
            switch (m_Mode)
            {
                case Mode.Record:
                    m_UnderlyingTransports.PacketReceivedEvent += new PacketHandler(UnderlyingTransports_PacketReceivedEvent);
                    m_UnderlyingTransports.PacketSentEvent += new PacketHandler(UnderlyingTransports_PacketSentEvent);
                    break;
                case Mode.Playback:
                    m_Running = true;
                    ThreadPool.QueueUserWorkItem(new WaitCallback(InjectRecordedPackages));
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
            switch (m_Mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "PacketReceivedEvent", buffer)).Serialize(m_DataSink);
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
            if (PacketReceivedEvent != null && m_DataSource.Count != 0 && m_DataSource[0].Time < m_StartTime.ElapsedMilliseconds)
            {
                if (m_DataSource[0].Method == "PacketReceivedEvent")
                    PacketReceivedEvent(m_DataSource[0].Message, 0, m_DataSource[0].Message.Length, this);
                m_DataSource.RemoveAt(0);
            }
            if (m_Running) ThreadPool.QueueUserWorkItem(new WaitCallback(InjectRecordedPackages));
        }

        /// <summary>
        /// Wrapps ITransport.Name.
        /// </summary>
        /// <see cref="ITransport.Name"/>
        public string Name
        {
            get { return "Millipede" + m_UnderlyingTransports.Name; }
        }

        /// <summary>
        /// Wrapps ITransport.Backlog.
        /// </summary>
        /// <see cref="ITransport.Backlog"/>
        public uint Backlog
        {
            get { return m_UnderlyingTransports.Backlog; }
        }

        /// <summary>
        /// Wrapps ITransport.Active.
        /// </summary>
        /// <see cref="ITransport.Active"/>
        public bool Active
        {
            get { return m_UnderlyingTransports.Active; }
        }

        /// <summary>
        /// Wrapps ITransport.Reliability.
        /// </summary>
        /// <see cref="ITransportDeliveryCharacteristics.Reliability"/>
        public Reliability Reliability
        {
            get { return m_UnderlyingTransports.Reliability; }
        }

        /// <summary>
        /// Wrapps ITransport.Ordering.
        /// </summary>
        /// <see cref="ITransportDeliveryCharacteristics.Ordering"/>
        public Ordering Ordering
        {
            get { return m_UnderlyingTransports.Ordering; }
        }

        /// <summary>
        /// Wrapps ITransport.MaximumPacketSize.
        /// </summary>
        /// <see cref="ITransport.MaximumPacketSize"/>
        public int MaximumPacketSize
        {
            get { return m_UnderlyingTransports.MaximumPacketSize; }
        }

        /// <summary>
        /// Wrapps ITransport.SendPacket(byte[],int,int). In addition, writes data to a sink if
        /// MillipedeTransport initialized with Mode.Record.
        /// </summary>
        /// <see cref="ITransport.SendPacket(byte[],int,int)"/>
        public void SendPacket(byte[] packet, int offset, int count)
        {
            switch (m_Mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "SendPacket", packet)).Serialize(m_DataSink);
                    break;
            }
            m_UnderlyingTransports.SendPacket(packet, 0, packet.Length);
        }

        /// <summary>
        /// Wrapps ITransport.SendPacket(Stream). In addition, it writes data to a sink if
        /// MillipedeTransport initialized with Mode.Record.
        /// </summary>
        /// <see cref="ITransport.SendPacket(Stream)"/>
        public void SendPacket(Stream packetStream)
        {
            switch (m_Mode)
            {
                case Mode.Record:
                    byte[] tmpmsg = new byte[packetStream.Length];
                    packetStream.Position = 0;
                    packetStream.Read(tmpmsg, 0, (int)packetStream.Length);

                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "SendPacket", tmpmsg)).Serialize(m_DataSink);
                    break;
            }
            m_UnderlyingTransports.SendPacket(packetStream);
        }

        /// <summary>
        /// Wrapps ITransport.Update.
        /// </summary>
        /// <see cref="ITransport.Update"/>
        public void Update()
        {
            m_UnderlyingTransports.Update();
        }

        /// <summary>
        /// Wrapps ITransport.Capabilities.
        /// </summary>
        /// <see cref="ITransport.Capabilities"/>
        public IDictionary<string, string> Capabilities
        {
            get { return m_UnderlyingTransports.Capabilities; }
        }
        
        /// <summary>
        /// Wrapps ITransport.GetPacketStream.
        /// </summary>
        /// <see cref="ITransport.GetPacketStream"/>
        public Stream GetPacketStream()
        {
            return m_UnderlyingTransports.GetPacketStream();
        }

        /// <summary>
        /// Wrapps ITransport.Delay.
        /// </summary>
        /// <see cref="ITransportDeliveryCharacteristics.Delay"/>
        public float Delay
        {
            get { return m_UnderlyingTransports.Delay; }
            set { m_UnderlyingTransports.Delay = value; }
        }

        /// <summary>
        /// Wrapps ITransport.Dispose.
        /// </summary>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            m_Running = false;
            m_UnderlyingTransports.Dispose();
        }
    }

    /// <summary>
    /// Holds all necessary information about an network event.
    /// </summary>
    [Serializable]
    public class NetworkEvent
    {
        internal int Time;
        internal string Method;
        internal byte[] Message;
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
