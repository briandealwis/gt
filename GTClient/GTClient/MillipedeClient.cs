using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using GT.Net;

/// Client side of the Millipede debugger
namespace GT.Millipede
{
    /// <summary>
    /// Connector for the millipede debugger. It wrapps around an existing underlying IConnector
    /// and adds file in-/output facilities.
    /// </summary>
    public class MillipedeConnector : IConnector
    {
        private readonly IConnector underlyingConnector = null;
        private object milliDescriptor;
        private MillipedeRecorder recorder;

        /// <summary>
        /// Wrap the provided connector for use with Millipede.
        /// If the Millipede recorder is unconfigured, we cause
        /// a dialog to configure the recorder.
        /// If the Millipede recorder is configured to be passthrough,
        /// we return the connector unwrapped.
        /// </summary>
        /// <param name="connector">the connector to be wrapped</param>
        /// <param name="recorder">the Millipede recorder</param>
        /// <returns>an appropriately configured connector</returns>
        public static IConnector Wrap(IConnector connector, MillipedeRecorder recorder)
        {
            if (recorder.Mode == MillipedeMode.PassThrough)
            {
                return connector;
            }
            return new MillipedeConnector(connector, recorder);
        }

        /// <summary>
        /// Wrap the provided connectors for use with Millipede.
        /// If the Millipede recorder is unconfigured, we cause
        /// a dialog to configure the recorder.
        /// If the Millipede recorder is configured to be passthrough,
        /// we leave the connectors unwrapped.
        /// </summary>
        /// <param name="connectors">the acceptors to be wrapped</param>
        /// <param name="recorder">the Millipede recorder</param>
        /// <returns>a collection of appropriately configured acceptors</returns>
        public static ICollection<IConnector> Wrap(ICollection<IConnector> connectors,
            MillipedeRecorder recorder)
        {
            if (recorder.Mode == MillipedeMode.PassThrough)
            {
                return connectors;
            }
            List<IConnector> wrappers = new List<IConnector>();
            foreach (IConnector conn in connectors)
            {
                wrappers.Add(new MillipedeConnector(conn, recorder));
            }
            return wrappers;
        }

        /// <summary>
        /// Create a recording recorder that wraps around an existing underlying
        /// IConnector.
        /// </summary>
        /// <param name="underlyingConnector">The existing underlying IConnector</param>
        /// <param name="recorder">The Millipede Replayer/Recorder</param>
        protected MillipedeConnector(IConnector underlyingConnector, MillipedeRecorder recorder)
        {
            milliDescriptor = recorder.GenerateDescriptor(underlyingConnector);
            this.recorder = recorder;
            this.underlyingConnector = underlyingConnector;
        }


        /// <summary>
        /// Wraps IConnector.Connect. In addition, writes data to a sink if MillipedeConnector is
        /// initialized with Mode.Record. The returning ITransport is wrapped in a
        /// MillipedeTransport.
        /// <see cref="IConnector.Connect"/>
        public ITransport Connect(string address, string port, IDictionary<string, string> capabilities)
        {
            switch (recorder.Mode)
            {
            case MillipedeMode.Unconfigured:
            case MillipedeMode.PassThrough:
            default:
                return underlyingConnector.Connect(address, port, capabilities);

            case MillipedeMode.Playback:
                // Wait until a connection event comes in
                MillipedeEvent connectEvent = recorder.WaitForReplayEvent(milliDescriptor,
                    MillipedeEventType.Connected, MillipedeEventType.Exception);

                if(connectEvent.Type == MillipedeEventType.Exception)
                {
                    throw (Exception)connectEvent.Context;
                }
                if(connectEvent.Type == MillipedeEventType.Connected)
                {
                    MemoryStream stream = new MemoryStream(connectEvent.Message);
                    BinaryFormatter formatter = new BinaryFormatter();
                    object milliTransportDescriptor = formatter.Deserialize(stream);
                    string transportName = (string)formatter.Deserialize(stream);
                    Dictionary<string, string> ignored =
                        (Dictionary<string, string>)formatter.Deserialize(stream);
                    Reliability reliability = (Reliability)formatter.Deserialize(stream);
                    Ordering ordering = (Ordering)formatter.Deserialize(stream);
                    uint maxPacketSize = (uint)formatter.Deserialize(stream);

                    // FIXME: should we be checking the capabilities?  Probably...!
                    ITransport mockTransport = new MillipedeTransport(recorder,
                        milliTransportDescriptor,
                        transportName, capabilities, reliability, ordering, maxPacketSize);
                    return mockTransport;
                }
                throw new InvalidStateException("invalid event type for Connect()!", connectEvent);

            case MillipedeMode.Record:
                ITransport transport;
                try
                {
                    transport = underlyingConnector.Connect(address, port, capabilities);
                    object milliTransportDesriptor = recorder.GenerateDescriptor(transport);
                    MemoryStream stream = new MemoryStream();
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(stream, milliTransportDesriptor);
                    formatter.Serialize(stream, transport.Name);
                    formatter.Serialize(stream, capabilities);
                    formatter.Serialize(stream, transport.Reliability);
                    formatter.Serialize(stream, transport.Ordering);
                    formatter.Serialize(stream, transport.MaximumPacketSize);

                    ITransport mockTransport = new MillipedeTransport(transport, recorder,
                        milliTransportDesriptor);
                    recorder.Record(new MillipedeEvent(milliDescriptor, MillipedeEventType.Connected,
                        stream.ToArray()));
                    return mockTransport;
                }
                catch(GTException e)
                {
                    recorder.Record(new MillipedeEvent(milliDescriptor,
                        MillipedeEventType.Exception, e));
                    throw;
                }
            }
        }

        /// <summary>
        /// Wraps IConnector.Responsible.
        /// </summary>
        /// <see cref="IConnector.Responsible"/>
        public bool Responsible(ITransport transport)
        {
            if (transport is MillipedeTransport)
            {
                return underlyingConnector.Responsible(((MillipedeTransport)transport).WrappedTransport);
            }
            return underlyingConnector.Responsible(transport);
        }

        /// <summary>
        /// Wraps IConnector.Start. In addition, writes data to a sink if MillipedeTransport
        /// initialized with Mode.Record.
        /// </summary>
        /// <see cref="IStartable.Start"/>
        public void Start()
        {
            recorder.Record(new MillipedeEvent(milliDescriptor, MillipedeEventType.Started));
            underlyingConnector.Start();
        }

        /// <summary>
        /// Wraps IConnector.Stop. In addition, writes data to a sink if MillipedeTransport
        /// initialized with Mode.Record.
        /// </summary>
        /// <see cref="IStartable.Stop"/>
        public void Stop()
        {
            recorder.Record(new MillipedeEvent(milliDescriptor, MillipedeEventType.Stopped));
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
            recorder.Record(new MillipedeEvent(milliDescriptor, MillipedeEventType.Disposed));
            underlyingConnector.Dispose();
        }
    }
}
