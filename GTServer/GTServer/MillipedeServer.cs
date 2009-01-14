using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using GT.Net;

/// Server side of the Millipede debugger
namespace GT.Millipede
{
    /// <summary>
    /// Configuration of the millipede debugger
    /// </summary>
    public class MillipedeConfiguration : DefaultServerConfiguration
    {
        private readonly Mode m_Mode = Mode.Idle;

        /// <summary>
        /// Instanciates the millipede debugger configuration in idle mode
        /// </summary>
        /// <see cref="DefaultServerConfiguration"/>
        public MillipedeConfiguration()
        {
        }

        /// <summary>
        /// Overrides DefaultServerConfiguration(int port)
        /// </summary>
        /// <param name="mode">The current operation mode</param>
        /// <see cref="DefaultServerConfiguration"/>
        public MillipedeConfiguration(int port, Mode mode) : base(port)
        {
            m_Mode = mode;
        }
        
        /// <summary>
        /// Overrides DefaultServerConfiguration(int port, int pingInterval)
        /// </summary>
        /// <param name="mode">The current operation mode</param>
        /// <see cref="DefaultServerConfiguration"/>
        public MillipedeConfiguration(int port, int pingInterval, Mode mode) : base(port, pingInterval)
        {
            m_Mode = mode;
        }

        /// <summary>
        /// Creates an ICollection of IAcceptor. Every IAcceptor is wrapped in a
        /// MillipedeAcceptor.
        /// </summary>
        /// <returns>Collection of acceptors</returns>
        public override ICollection<IAcceptor> CreateAcceptors()
        {
            ICollection<IAcceptor> acceptors = new List<IAcceptor>
            {
                new MillipedeAcceptor(new TcpAcceptor(IPAddress.Any, port), m_Mode),
                new MillipedeAcceptor(new UdpAcceptor(IPAddress.Any, port), m_Mode),
            };
            return acceptors;
        }
    }

    /// <summary>
    /// Acceptor for the millipede debugger. It wrapps around an existing underlying IAcceptor
    /// and adds file in-/output facilities.
    /// </summary>
    class MillipedeAcceptor : IAcceptor
    {
        private readonly Mode m_Mode = Mode.Idle;
        private readonly Stopwatch m_StartTime = new Stopwatch();
        private readonly MemoryStream m_DataSink = null;
        private readonly IList<NetworkEvent> m_DataSource = new List<NetworkEvent>();
        private readonly IAcceptor m_UnderlyingAcceptor = null;
        public event NewClientHandler NewClientEvent;

        /// <summary>
        /// Instanciates a millipede acceptor and wrapps it around an existing underlying
        /// IAcceptor.
        /// </summary>
        /// <param name="underlyingAcceptor">The existing underlying IAcceptor</param>
        /// <param name="mode">The current operation mode</param>
        public MillipedeAcceptor(IAcceptor underlyingAcceptor, Mode mode)
        {
            m_Mode = mode;
            m_UnderlyingAcceptor = underlyingAcceptor;
            m_StartTime.Start();
            switch (m_Mode)
            {
                case Mode.Record:
                    m_DataSink = new MemoryStream();
                    m_UnderlyingAcceptor.NewClientEvent += new NewClientHandler(m_UnderlyingAcceptor_NewClientEvent);
                    break;
                case Mode.Playback:
                    Stream dataSource = File.OpenRead(m_UnderlyingAcceptor.GetType().ToString());
                    dataSource.Position = 0;
                    while (dataSource.Position != dataSource.Length)
                        m_DataSource.Add(NetworkEvent.DeSerialize(dataSource));
                    dataSource.Close();
                    break;
            }
        }

        /// <summary>
        /// ITransports use a observer-pattern (implemented with events & callbacks) to notify
        /// other GT2 components. Since these other componets register to the MillipedeAcceptor,
        /// there must be a mechanism to forward notifications from the IAcceptor to other GT2
        /// components.
        /// </summary>
        /// <see cref="IAcceptor.NewClientEvent"/>
        private void m_UnderlyingAcceptor_NewClientEvent(ITransport transport, Dictionary<string, string> capabilities)
        {
            if (NewClientEvent == null)
            {
                return;
            }
            switch (m_Mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "NewClientEvent_" + transport.Name)).Serialize(m_DataSink);
                    break;
            }
            NewClientEvent(new MillipedeTransport(transport, m_StartTime, m_DataSink, m_DataSource, m_Mode), capabilities);
        }

        /// <summary>
        /// Wrapps IAcceptor.Update.
        /// </summary>
        /// <see cref="IAcceptor.Update"/>
        public void Update()
        {
            m_UnderlyingAcceptor.Update();
        }

        /// <summary>
        /// Wrapps IAcceptor.Start.
        /// </summary>
        /// <see cref="IStartable.Start"/>
        public void Start()
        {
            m_StartTime.Start();
            switch (m_Mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "Start")).Serialize(m_DataSink);
                    break;
            }
            m_UnderlyingAcceptor.Start();
        }

        /// <summary>
        /// Wrapps IAcceptor.Stop.
        /// </summary>
        /// <see cref="IStartable.Stop"/>
        public void Stop()
        {
            switch (m_Mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "Stop")).Serialize(m_DataSink);
                    break;
            }
            m_UnderlyingAcceptor.Stop();
        }

        /// <summary>
        /// Wrapps IAcceptor.Active.
        /// </summary>
        /// <see cref="IStartable.Active"/>
        public bool Active
        {
            get { return m_UnderlyingAcceptor.Active; }
        }

        /// <summary>
        /// Wrapps IAcceptor.Dispose.
        /// </summary>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            switch (m_Mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "Dispose")).Serialize(m_DataSink);

                    FileStream sinkFile = File.Create(m_UnderlyingAcceptor.GetType().ToString());
                    lock (m_DataSink)
                    {
                        m_DataSink.Position = 0;
                        m_DataSink.WriteTo(sinkFile);
                        sinkFile.Close();
                        m_DataSink.Close();
                    }
                    break;
            }
            m_UnderlyingAcceptor.Dispose();
        }
    }
}
