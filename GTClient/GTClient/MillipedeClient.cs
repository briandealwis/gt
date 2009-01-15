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
        private readonly Mode mode = Mode.Idle;
        private readonly Stopwatch startTime = new Stopwatch();
        private readonly MemoryStream dataSink = null;
        private readonly IList<NetworkEvent> dataSource = new List<NetworkEvent>();
        private readonly IConnector underlyingConnector = null;

        /// <summary>
        /// Instanciates a millipede connection and wrapps it around an existing underlying
        /// IConnector.
        /// </summary>
        /// <param name="underlyingConnector">The existing underlying IConnector</param>
        /// <param name="mode">The current operation mode</param>
        public MillipedeConnector(IConnector underlyingConnector, Mode mode)
        {
            this.mode = mode;
            this.underlyingConnector = underlyingConnector;
            startTime.Start();
            switch (this.mode)
            {
                case Mode.Record:
                    dataSink = new MemoryStream();
                    break;
                case Mode.Playback:
                    Stream dataSource = File.OpenRead(this.underlyingConnector.GetType().ToString());
                    dataSource.Position = 0;
                    while (dataSource.Position != dataSource.Length)
                    {
                        this.dataSource.Add(NetworkEvent.DeSerialize(dataSource));
                    }
                    dataSource.Close();
                    break;
            }
        }

        /// <summary>
        /// Wraps IConnector.Connect. In addition, writes data to a sink if MillipedeConnector is
        /// initialized with Mode.Record. The returning ITransport is wrapped in a
        /// MillipedeTransport.
        /// <see cref="IConnector.Connect"/>
        public ITransport Connect(string address, string port, IDictionary<string, string> capabilities)
        {
            switch (mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_Connected)).Serialize(dataSink);
                    break;
            }
            return new MillipedeTransport(underlyingConnector.Connect(address, port, capabilities), startTime, dataSink, dataSource, mode);
        }

        /// <summary>
        /// Wraps IConnector.Responsible.
        /// </summary>
        /// <see cref="IConnector.Responsible"/>
        public bool Responsible(ITransport transport)
        {
            return underlyingConnector.Responsible(transport);
        }

        /// <summary>
        /// Wraps IConnector.Start. In addition, writes data to a sink if MillipedeTransport
        /// initialized with Mode.Record.
        /// </summary>
        /// <see cref="IStartable.Start"/>
        public void Start()
        {
            switch (mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_Started)).Serialize(dataSink);
                    break;
            }
            underlyingConnector.Start();
        }

        /// <summary>
        /// Wraps IConnector.Stop. In addition, writes data to a sink if MillipedeTransport
        /// initialized with Mode.Record.
        /// </summary>
        /// <see cref="IStartable.Stop"/>
        public void Stop()
        {
            switch (mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_Stopped)).Serialize(dataSink);
                    break;
            }
            underlyingConnector.Stop();
        }

        /// <summary>
        /// Wraps IConnector.Active.
        /// </summary>
        /// <see cref="IStartable.Active"/>
        public bool Active
        {
            get { return underlyingConnector.Active; }
        }

        /// <summary>
        /// Wraps IConnector.Dispose. In addition, writes data to a sink if MillipedeTransport
        /// initialized with Mode.Record and stores the data of the disposed IConnection
        /// persistantly.
        /// </summary>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            switch (mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_Disposed)).Serialize(dataSink);

                    FileStream sinkFile = File.Create(underlyingConnector.GetType().ToString());
                    lock (dataSink)
                    {
                        dataSink.Position = 0;
                        dataSink.WriteTo(sinkFile);
                        sinkFile.Close();
                        dataSink.Close();
                    }
                    break;
            }
            underlyingConnector.Dispose();
        }
    }
}
