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
    /// An wrapper around an acceptor for the millipede packet recorder/replayer.
    /// The acceptor wrapper is created using <see cref="Wrap"/>.
    /// </summary>
    public class MillipedeAcceptor : IAcceptor
    {
        private readonly IAcceptor underlyingAcceptor = null;
        private object milliDescriptor = null;
        private MillipedeRecorder recorder;
        public event NewClientHandler NewClientEvent;

        /// <summary>
        /// Wrap the provided acceptor for use with Millipede.
        /// If the Millipede recorder is unconfigured, we cause
        /// a dialog to configure the recorder.
        /// If the Millipede recorder is configured to be passthrough,
        /// we return the acceptor unwrapped.
        /// </summary>
        /// <param name="acceptor">the acceptor to be wrapped</param>
        /// <param name="recorder">the Millipede recorder</param>
        /// <returns>an appropriately configured acceptor</returns>
        public static IAcceptor Wrap(IAcceptor acceptor, MillipedeRecorder recorder) 
        {
            if (recorder.Mode == MillipedeMode.Unconfigured
                || recorder.Mode == MillipedeMode.PassThrough)
            {
                return acceptor;
            }
            return new MillipedeAcceptor(acceptor, recorder);
        }

        /// <summary>
        /// Wrap the provided acceptors for use with Millipede.
        /// If the Millipede recorder is unconfigured, we cause
        /// a dialog to configure the recorder.
        /// If the Millipede recorder is configured to be passthrough,
        /// we leave the acceptors unwrapped.
        /// </summary>
        /// <param name="acceptors">the acceptors to be wrapped</param>
        /// <param name="recorder">the Millipede recorder</param>
        /// <returns>a collection of appropriately configured acceptors</returns>
        public static ICollection<IAcceptor> Wrap(ICollection<IAcceptor> acceptors,
            MillipedeRecorder recorder)
        {
            if (recorder.Mode == MillipedeMode.Unconfigured
                || recorder.Mode == MillipedeMode.PassThrough)
            {
                return acceptors;
            }
            List<IAcceptor> wrappers = new List<IAcceptor>();
            foreach(IAcceptor acc in acceptors)
            {
                wrappers.Add(new MillipedeAcceptor(acc, recorder));
            }
            return wrappers;
        }

        /// <summary>
        /// Instanciates a millipede acceptor and wrapps it around an existing underlying
        /// IAcceptor.
        /// </summary>
        /// <param name="underlyingAcceptor">The existing underlying IAcceptor</param>
        /// <param name="recorder">The Millipede Replayer/Recorder</param>
        protected MillipedeAcceptor(IAcceptor underlyingAcceptor, MillipedeRecorder recorder)
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
        private void UnderlyingAcceptor_NewClientEvent(ITransport transport, IDictionary<string, string> capabilities)
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
            uint maxPacketSize = (uint)formatter.Deserialize(stream);

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
