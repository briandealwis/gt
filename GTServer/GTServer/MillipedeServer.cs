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
        public event NewTransportHandler NewTransportAccepted;

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
            if (recorder.Mode == MillipedeMode.PassThrough)
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
            if (recorder.Mode == MillipedeMode.PassThrough)
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
        /// Instanciates a millipede acceptor and wraps it around an existing underlying
        /// IAcceptor.
        /// </summary>
        /// <param name="underlyingAcceptor">The existing underlying IAcceptor</param>
        /// <param name="recorder">The Millipede Replayer/Recorder</param>
        protected MillipedeAcceptor(IAcceptor underlyingAcceptor, MillipedeRecorder recorder)
        {
            this.underlyingAcceptor = underlyingAcceptor;

            this.recorder = recorder;
            milliDescriptor = recorder.GenerateDescriptor(underlyingAcceptor);
            if (recorder.Mode != MillipedeMode.Playback)
            {
                // we only pass-through recorded connections in playback mode
                this.underlyingAcceptor.NewTransportAccepted += UnderlyingAcceptor_NewTransportEvent;
            }
        }

        /// <summary>
        /// ITransports use a observer-pattern (implemented with events & callbacks) to notify
        /// other GT2 components. Since these other componets register to the MillipedeAcceptor,
        /// there must be a mechanism to forward notifications from the IAcceptor to other GT2
        /// components.
        /// </summary>
        /// <see cref="IAcceptor.NewTransportAccepted"/>
        private void UnderlyingAcceptor_NewTransportEvent(ITransport transport, IDictionary<string, string> capabilities)
        {
            switch (recorder.Mode)
            {
            case MillipedeMode.PassThrough:
            case MillipedeMode.Unconfigured:
                if(NewTransportAccepted != null)
                {
                    NewTransportAccepted(transport, capabilities);
                }
                return;

            case MillipedeMode.Playback:
                throw new InvalidStateException("Invalid mode", recorder.Mode);

            case MillipedeMode.Record:
                // If we support mode-switching in the future, we need to consider
                // what to do if in playback mode

                object milliTransportDescriptor = recorder.GenerateDescriptor(transport);
                MemoryStream stream = new MemoryStream();
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, milliTransportDescriptor);
                formatter.Serialize(stream, transport.Name);
                formatter.Serialize(stream, capabilities);
                formatter.Serialize(stream, transport.Reliability);
                formatter.Serialize(stream, transport.Ordering);
                formatter.Serialize(stream, transport.MaximumPacketSize);

                recorder.Record(new MillipedeEvent(milliDescriptor, MillipedeEventType.NewClient,
                    stream.ToArray()));
                if(NewTransportAccepted != null)
                {
                    NewTransportAccepted(
                        new MillipedeTransport(transport, recorder, milliTransportDescriptor),
                        capabilities);
                }
                return;
            }
        }

        /// <summary>
        /// Wraps IAcceptor.Update.
        /// </summary>
        /// <see cref="IAcceptor.Update"/>
        public void Update()
        {
            switch (recorder.Mode)
            {
            case MillipedeMode.Unconfigured:
            case MillipedeMode.PassThrough:
            default:
                underlyingAcceptor.Update();
                return;

            case MillipedeMode.Record:
                try
                {
                    underlyingAcceptor.Update();
                }
                catch(GTException ex)
                {
                    recorder.Record(new MillipedeEvent(milliDescriptor,
                        MillipedeEventType.Exception, ex));
                    throw;
                }
                return;

            case MillipedeMode.Playback:
                if(NewTransportAccepted == null)
                {
                    return;
                } // or if recorder.Mode == MillipedeMode.PassThrough?
                // See if there's an event and process it if so
                MillipedeEvent e = recorder.CheckReplayEvent(milliDescriptor,
                    MillipedeEventType.NewClient, MillipedeEventType.Exception);
                if(e == null)
                {
                    return;
                }
                if(e.Type == MillipedeEventType.Exception)
                {
                    throw (Exception)e.Context;
                }
                Debug.Assert(e.Type == MillipedeEventType.NewClient);

                MemoryStream stream = new MemoryStream(e.Message);
                BinaryFormatter formatter = new BinaryFormatter();
                object milliTransportDescriptor = formatter.Deserialize(stream);
                string transportName = (string)formatter.Deserialize(stream);
                Dictionary<string, string> capabilities =
                    (Dictionary<string, string>)formatter.Deserialize(stream);
                Reliability reliability = (Reliability)formatter.Deserialize(stream);
                Ordering ordering = (Ordering)formatter.Deserialize(stream);
                uint maxPacketSize = (uint)formatter.Deserialize(stream);

                ITransport mockTransport = new MillipedeTransport(recorder, milliTransportDescriptor,
                    transportName, capabilities, reliability, ordering, maxPacketSize);
                NewTransportAccepted(mockTransport, capabilities);
                return;
            }
        }

        /// <summary>
        /// Wraps IAcceptor.Start.
        /// </summary>
        /// <see cref="IStartable.Start"/>
        public void Start()
        {
            switch (recorder.Mode)
            {
            case MillipedeMode.Unconfigured:
            case MillipedeMode.PassThrough:
            default:
                underlyingAcceptor.Start();
                return;

            case MillipedeMode.Record:
                try
                {
                    underlyingAcceptor.Start();
                    recorder.Record(new MillipedeEvent(milliDescriptor, MillipedeEventType.Started));
                }
                catch(GTException ex)
                {
                    recorder.Record(new MillipedeEvent(milliDescriptor,
                        MillipedeEventType.Exception, ex));
                    throw;
                }
                return;

            case MillipedeMode.Playback:
                MillipedeEvent e = recorder.WaitForReplayEvent(milliDescriptor,
                    MillipedeEventType.Started, MillipedeEventType.Exception);
                if(e.Type == MillipedeEventType.Exception)
                {
                    throw (Exception)e.Context;
                }
                return;
            }
        }

        /// <summary>
        /// Wraps IAcceptor.Stop.
        /// </summary>
        /// <see cref="IStartable.Stop"/>
        public void Stop()
        {
            recorder.Record(new MillipedeEvent(milliDescriptor, MillipedeEventType.Stopped));
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
            recorder.Record(new MillipedeEvent(milliDescriptor, MillipedeEventType.Disposed));
            underlyingAcceptor.Dispose();
        }
    }
}
