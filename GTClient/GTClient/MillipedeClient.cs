using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GT.Net;

/// Client side of the Millipede debugger
namespace GT.Millipede
{
    /// <summary>
    /// Configuration of the millipede debugger
    /// </summary>
    public class MillipedeConfiguration : DefaultClientConfiguration
    {
        private readonly Mode m_Mode = Mode.Idle;

        /// <summary>
        /// Instanciates the millipede debugger configuration in idle mode.
        /// </summary>
        /// <see cref="Mode"/>
        public MillipedeConfiguration() : this(Mode.Idle)
        {
        }

        /// <summary>
        /// Instanciates the millipede debugger configuration.
        /// </summary>
        /// <param name="mode">The current operation mode</param>
        public MillipedeConfiguration(Mode mode)
        {
            m_Mode = mode;
        }

        /// <summary>
        /// Creates an ICollection of IConnector. Every IConnector is wrapped in a
        /// MillipedeConnector.
        /// </summary>
        /// <returns>Collection of connectors</returns>
        public override ICollection<IConnector> CreateConnectors()
        {
            ICollection<IConnector> connectors = new List<IConnector>
            {
                new MillipedeConnector(new TcpConnector(), m_Mode),
                new MillipedeConnector(new UdpConnector(), m_Mode),
            };
            return connectors;
        }
    }

    /// <summary>
    /// Connector for the millipede debugger. It wrapps around an existing underlying IConnector
    /// and adds file in-/output facilities.
    /// </summary>
    class MillipedeConnector : IConnector
    {
        private readonly Mode m_Mode = Mode.Idle;
        private readonly Stopwatch m_StartTime = new Stopwatch();
        private readonly MemoryStream m_DataSink = null;
        private readonly IList<NetworkEvent> m_DataSource = new List<NetworkEvent>();
        private readonly IConnector m_UnderlyingConnector = null;
        private ITransport m_Transport = null;

        /// <summary>
        /// Instanciates a millipede connection and wrapps it around an existing underlying
        /// IConnector.
        /// </summary>
        /// <param name="underlyingConnector">The existing underlying IConnector</param>
        /// <param name="mode">The current operation mode</param>
        public MillipedeConnector(IConnector underlyingConnector, Mode mode)
        {
            m_Mode = mode;
            m_UnderlyingConnector = underlyingConnector;
            m_StartTime.Start();
            switch (m_Mode)
            {
                case Mode.Record:
                    m_DataSink = new MemoryStream();
                    break;
                case Mode.Playback:
                    Stream dataSource = File.OpenRead(m_UnderlyingConnector.GetType().ToString());
                    dataSource.Position = 0;
                    while (dataSource.Position != dataSource.Length)
                        m_DataSource.Add(NetworkEvent.DeSerialize(dataSource));
                    dataSource.Close();
                    break;
            }
        }

        /// <summary>
        /// Wrapps IConnector.Connect. In addition, writes data to a sink if MillipedeConnector is
        /// initialized with Mode.Record. The returning ITransport is wrapped in a
        /// MillipedeTransport.
        /// <see cref="IConnector.Connect"/>
        public ITransport Connect(string address, string port, IDictionary<string, string> capabilities)
        {
            switch (m_Mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "Connect")).Serialize(m_DataSink);
                    break;
            }
            m_Transport = new MillipedeTransport(m_UnderlyingConnector.Connect(address, port, capabilities), m_StartTime, m_DataSink, m_DataSource, m_Mode);
            return m_Transport;
        }

        /// <summary>
        /// Wrapps IConnector.Responsible.
        /// </summary>
        /// <see cref="IConnector.Responsible"/>
        public bool Responsible(ITransport transport)
        {
            return m_UnderlyingConnector.Responsible(transport);
        }

        /// <summary>
        /// Wrapps IConnector.Start. In addition, writes data to a sink if MillipedeTransport
        /// initialized with Mode.Record.
        /// </summary>
        /// <see cref="IStartable.Start"/>
        public void Start()
        {
            switch (m_Mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "Start")).Serialize(m_DataSink);
                    break;
            }
            m_UnderlyingConnector.Start();
        }

        /// <summary>
        /// Wrapps IConnector.Stop. In addition, writes data to a sink if MillipedeTransport
        /// initialized with Mode.Record.
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
            m_UnderlyingConnector.Stop();
        }

        /// <summary>
        /// Wrapps IConnector.Active.
        /// </summary>
        /// <see cref="IStartable.Active"/>
        public bool Active
        {
            get { return m_UnderlyingConnector.Active; }
        }

        /// <summary>
        /// Wrapps IConnector.Dispose. In addition, writes data to a sink if MillipedeTransport
        /// initialized with Mode.Record and stores the data of the disposed IConnection
        /// persistantly.
        /// </summary>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            switch (m_Mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)m_StartTime.ElapsedMilliseconds, "Dispose")).Serialize(m_DataSink);

                    FileStream sinkFile = File.Create(m_UnderlyingConnector.GetType().ToString());
                    lock (m_DataSink)
                    {
                        m_DataSink.Position = 0;
                        m_DataSink.WriteTo(sinkFile);
                        sinkFile.Close();
                        m_DataSink.Close();
                    }
                    break;
            }
            m_UnderlyingConnector.Dispose();
        }
    }
}
