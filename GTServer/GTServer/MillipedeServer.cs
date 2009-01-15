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
        private readonly Mode mode = Mode.Idle;

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
            this.mode = mode;
        }
        
        /// <summary>
        /// Overrides DefaultServerConfiguration(int port, int pingInterval)
        /// </summary>
        /// <param name="mode">The current operation mode</param>
        /// <see cref="DefaultServerConfiguration"/>
        public MillipedeConfiguration(int port, int pingInterval, Mode mode) : base(port, pingInterval)
        {
            this.mode = mode;
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
                new MillipedeAcceptor(new TcpAcceptor(IPAddress.Any, port), mode),
                new MillipedeAcceptor(new UdpAcceptor(IPAddress.Any, port), mode),
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
        private readonly Mode mode = Mode.Idle;
        private readonly Stopwatch startTime = new Stopwatch();
        private readonly MemoryStream dataSink = null;
        private readonly IList<NetworkEvent> dataSource = new List<NetworkEvent>();
        private readonly IAcceptor underlyingAcceptor = null;
        public event NewClientHandler NewClientEvent;

        /// <summary>
        /// Instanciates a millipede acceptor and wrapps it around an existing underlying
        /// IAcceptor.
        /// </summary>
        /// <param name="underlyingAcceptor">The existing underlying IAcceptor</param>
        /// <param name="mode">The current operation mode</param>
        public MillipedeAcceptor(IAcceptor underlyingAcceptor, Mode mode)
        {
            this.mode = mode;
            this.underlyingAcceptor = underlyingAcceptor;
            startTime.Start();
            switch (this.mode)
            {
                case Mode.Record:
                    dataSink = new MemoryStream();
                    this.underlyingAcceptor.NewClientEvent += UnderlyingAcceptor_NewClientEvent;
                    break;
                case Mode.Playback:
                    Stream dataSource = File.OpenRead(this.underlyingAcceptor.GetType().ToString());
                    dataSource.Position = 0;
                    while (dataSource.Position != dataSource.Length)
                        this.dataSource.Add(NetworkEvent.DeSerialize(dataSource));
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
        private void UnderlyingAcceptor_NewClientEvent(ITransport transport, Dictionary<string, string> capabilities)
        {
            if (NewClientEvent == null)
            {
                return;
            }
            switch (mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_NewClient + transport.Name)).Serialize(dataSink);
                    break;
            }
            NewClientEvent(new MillipedeTransport(transport, startTime, dataSink, dataSource, mode), capabilities);
        }

        /// <summary>
        /// Wraps IAcceptor.Update.
        /// </summary>
        /// <see cref="IAcceptor.Update"/>
        public void Update()
        {
            underlyingAcceptor.Update();
        }

        /// <summary>
        /// Wraps IAcceptor.Start.
        /// </summary>
        /// <see cref="IStartable.Start"/>
        public void Start()
        {
            startTime.Start();
            switch (mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_Started)).Serialize(dataSink);
                    break;
            }
            underlyingAcceptor.Start();
        }

        /// <summary>
        /// Wraps IAcceptor.Stop.
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
            underlyingAcceptor.Stop();
        }

        /// <summary>
        /// Wraps IAcceptor.Active.
        /// </summary>
        /// <see cref="IStartable.Active"/>
        public bool Active
        {
            get { return underlyingAcceptor.Active; }
        }

        /// <summary>
        /// Wraps IAcceptor.Dispose.
        /// </summary>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            switch (mode)
            {
                case Mode.Record:
                    (new NetworkEvent((int)startTime.ElapsedMilliseconds, 
                        NetworkEvent.Event_Disposed)).Serialize(dataSink);

                    FileStream sinkFile = File.Create(underlyingAcceptor.GetType().ToString());
                    lock (dataSink)
                    {
                        dataSink.Position = 0;
                        dataSink.WriteTo(sinkFile);
                        sinkFile.Close();
                        dataSink.Close();
                    }
                    break;
            }
            underlyingAcceptor.Dispose();
        }
    }
}
