using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using GT.Net;
using GT.Utils;

/// Server side of the Millipede debugger
namespace GT.Millipede
{
    /// <summary>
    /// Acceptor for the millipede packet recorder/replayer. It wrapps around an 
    /// existing underlying <see cref="IAcceptor"/>.
    /// </summary>
    public class MillipedeAcceptor : IAcceptor
    {
        private readonly IAcceptor underlyingAcceptor = null;
        private object milliDescriptor = null;
        private MillipedeRecorder recorder;
        public event NewClientHandler NewClientEvent;

        /// <summary>
        /// Instanciates a millipede acceptor and wrapps it around an existing underlying
        /// IAcceptor.
        /// </summary>
        /// <param name="underlyingAcceptor">The existing underlying IAcceptor</param>
        /// <param name="recorder">The Millipede Replayer/Recorder</param>
        public MillipedeAcceptor(IAcceptor underlyingAcceptor, MillipedeRecorder recorder)
        {
            this.underlyingAcceptor = underlyingAcceptor;

            this.recorder = recorder;
            milliDescriptor = underlyingAcceptor.GetType() + underlyingAcceptor.ToString();
            this.underlyingAcceptor.NewClientEvent += UnderlyingAcceptor_NewClientEvent;
            recorder.Notify(milliDescriptor, InjectRecordedEvent);
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
            if (recorder.Mode == MillipedeMode.PassThrough)
            {
                if(NewClientEvent != null) { NewClientEvent(transport, capabilities); }
                return;
            }

            object milliTransportDescriptor = MillipedeTransport.GenerateDescriptor(transport);
            MemoryStream stream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, milliTransportDescriptor);
            formatter.Serialize(stream, transport.Name);
            formatter.Serialize(stream, capabilities);
            formatter.Serialize(stream, transport.Reliability);
            formatter.Serialize(stream, transport.Ordering);
            formatter.Serialize(stream, transport.MaximumPacketSize);

            recorder.Record(new NetworkEvent(milliDescriptor, NetworkEventType.NewClient, stream.ToArray()));
            if (NewClientEvent == null) { return; }
            NewClientEvent(new MillipedeTransport(transport, recorder, milliTransportDescriptor), capabilities);
        }

        private void InjectRecordedEvent(NetworkEvent e)
        {
            if (NewClientEvent == null) { return; } // or if recorder.Mode == MillipedeMode.PassThrough?

            MemoryStream stream = new MemoryStream(e.Message);
            BinaryFormatter formatter = new BinaryFormatter();
            object milliTransportDescriptor = formatter.Deserialize(stream);
            string transportName = (string)formatter.Deserialize(stream);
            Dictionary<string, string> capabilities = (Dictionary<string, string>)formatter.Deserialize(stream);
            Reliability reliability = (Reliability)formatter.Deserialize(stream);
            Ordering ordering = (Ordering)formatter.Deserialize(stream);
            int maxPacketSize = (int)formatter.Deserialize(stream);

            ITransport mockTransport = new MillipedeTransport(recorder, milliTransportDescriptor,
                transportName, capabilities, reliability, ordering, maxPacketSize);
            NewClientEvent(mockTransport, capabilities);
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
            recorder.Record(new NetworkEvent(milliDescriptor, NetworkEventType.Started));
            underlyingAcceptor.Start();
        }

        /// <summary>
        /// Wraps IAcceptor.Stop.
        /// </summary>
        /// <see cref="IStartable.Stop"/>
        public void Stop()
        {
            recorder.Record(new NetworkEvent(milliDescriptor, NetworkEventType.Stopped));
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
            recorder.Record(new NetworkEvent(milliDescriptor, NetworkEventType.Disposed));
            underlyingAcceptor.Dispose();
        }
    }
}
