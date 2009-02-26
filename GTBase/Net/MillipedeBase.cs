using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Common.Logging;
using GT.Net;
using GT.Utils;

/// General (client & server) part of the Millipede debugger
namespace GT.Millipede
{
    /// <summary>
    /// Determines the mode of the millipede debugger:
    /// </summary>
    public enum MillipedeMode
    {
        ///<summary>
        /// Recorder is currently unconfigured
        ///</summary>
        Unconfigured,

        ///<summary>
        /// Writes incoming and outgoing network traffic to a file
        ///</summary>
        Record,

        ///<summary>
        /// Injects recorded network traffic from a file
        ///</summary>
        Playback,

        /// <summary>
        /// Bypass Millipede entirely.
        /// </summary>
        PassThrough
    }

    /// <summary>
    /// A recorder is able to record or replay a stream of <see cref="MillipedeEvent"/>.
    /// </summary>
    public class MillipedeRecorder : IDisposable
    {

        #region Singleton

        private static MillipedeRecorder singleton;

        /// <summary>
        /// The name of the environment variable used by GT-Millipede for
        /// configuration of the singleton instance.
        /// </summary>
        public static readonly string ConfigurationEnvironmentVariableName = "GTMILLIPEDE";

        /// <summary>
        /// Return the singleton recorder instance.
        /// </summary>
        public static MillipedeRecorder Singleton
        {
            get
            {
                if (singleton != null)
                {
                    return singleton;
                }
                if (Interlocked.Exchange(ref singleton, new MillipedeRecorder()) == null)
                {
                    string envvar = Environment.GetEnvironmentVariable(ConfigurationEnvironmentVariableName);
                    singleton.Configure(envvar);
                }
                return singleton;
            }
        }

        #endregion

        protected ILog log;

        private MillipedeMode mode = MillipedeMode.Unconfigured;
        private Stopwatch timer = null;
        private FileStream sinkFile = null;

        private MemoryStream dataSink = null;
        private Timer syncingTimer;
        private IDictionary<string,object> assignedDescriptors = new Dictionary<string, object>();

        /// <summary>
        /// Used for allocating stable but unique descriptors for recordable objects.
        /// For example, two TcpConnectors have no distinguishing characteristics;
        /// we can't use their ToString() as they'll be identical.  So we instead
        /// increment this value as necessary.  As long as the app's execution path
        /// is stable, then we'll allocate the same descriptors in the same order
        /// during playback as was allocated for the recording run.
        /// </summary>
        private int uniqueCount = 0;

        /// <summary>
        /// The next event to release during playback.
        /// </summary>
        private MillipedeEvent nextEvent = null;

        /// <summary>
        /// The time in milliseconds when <see cref="nextEvent"/> *should* be released,
        /// as opposed to the timestamp in its timestamp, <see cref="MillipedeEvent.Time"/>.
        /// This value helps replay events as according to the logical time: <see cref="timer"/>,
        /// being an instance of <see cref="Stopwatch"/>, is based on wall-clock, and hence
        /// will be thrown off by delays introduced from debugging, such as stepping into
        /// a function.
        /// </summary>
        private long nextEventReleaseTime = 0;

        /// <summary>
        /// Return the number of events replayed or recordered to this point.
        /// </summary>
        public int NumberEvents = 0;

        /// <summary>
        /// Create an instance of a Millipede recorder/replayer.  It is generally
        /// expected that mmode developers will use the singleton instance
        /// <see cref="Singleton"/>.
        /// </summary>
        public MillipedeRecorder()
        {
             log = LogManager.GetLogger(GetType());
        }

        public bool Active
        {
            get { return timer != null; }
        }

        public MillipedeMode Mode
        {
            get { return mode; }
        }

        public string LastFileName { get; protected set; }

        public void StartReplaying(string replayFile) {
            InvalidStateException.Assert(mode == MillipedeMode.Unconfigured,
                "Recorder is already started", mode);
            NumberEvents = 0;
            timer = Stopwatch.StartNew();
            mode = MillipedeMode.Playback;
            dataSink = null;
            sinkFile = File.OpenRead(replayFile);
            LastFileName = replayFile;
            LoadNextEvent();
        }

        public void StartRecording(string recordingFile) {
            InvalidStateException.Assert(mode == MillipedeMode.Unconfigured,
                "Recorder is already started", mode);
            NumberEvents = 0;
            timer = Stopwatch.StartNew();
            mode = MillipedeMode.Record;
            dataSink = new MemoryStream();
            sinkFile = File.Create(recordingFile);
            LastFileName = recordingFile;
            syncingTimer = new Timer(SyncRecording, null, TimeSpan.FromSeconds(10), 
                TimeSpan.FromSeconds(10));
        }

        public void StartPassThrough() {
            InvalidStateException.Assert(mode == MillipedeMode.Unconfigured,
                "Recorder is already started", mode);
            NumberEvents = 0;
            // timer = Stopwatch.StartNew();
            mode = MillipedeMode.PassThrough;
            dataSink = null;
            sinkFile = null;
            syncingTimer = null;
        }

        /// <summary>
        /// Generate a unique descriptor for the provided object.
        /// This method assumes the object's <see cref="ToString"/> is 
        /// stable (i.e., it will produce the same value on subsequent
        /// runs when configured the same way).
        /// For the descriptor to be stable, this method must be called
        /// with the same objects and in the same order.
        /// This method assumes that it is called once per object;
        /// do not call this method multiple times for the same object.
        /// </summary>
        /// <param name="obj">the object needing a descriptor</param>
        /// <returns>a unique descriptor for the provided object</returns>
        public object GenerateDescriptor(object obj)
        {
            string typeName = obj.GetType().FullName;
            string toString = obj.ToString();
            string descriptorBase = typeName + ":" + toString;
            string descriptor = descriptorBase;
            // can't use a GUID as they are never the same
            lock (this)
            {
                if(toString.Equals(typeName))
                {
                    descriptorBase = typeName;
                    descriptor = typeName + "@" + Interlocked.Increment(ref uniqueCount);
                }

                while(assignedDescriptors.ContainsKey(descriptor))
                {
                    descriptor = descriptorBase + "@" + Interlocked.Increment(ref uniqueCount);
                }

                assignedDescriptors[descriptor] = descriptor;
            }
            return descriptor;
        }


        public void Dispose()
        {
            lock (this)
            {
                if(this == singleton) { singleton = null; }
                if(syncingTimer != null) { syncingTimer.Dispose(); }
                syncingTimer = null;

                if(sinkFile != null)
                {
                    if(sinkFile != null && dataSink != null)
                    {
                        SyncRecording(null);
                    }
                    if(sinkFile != null) { sinkFile.Close(); }
                    if(dataSink != null) { dataSink.Dispose(); }
                    sinkFile = null;
                    dataSink = null;
                }
                assignedDescriptors = null;
            }
            mode = MillipedeMode.PassThrough;
        }

        public void Record(MillipedeEvent millipedeEvent)
        {
            if(mode == MillipedeMode.Record)
            {
                if (dataSink == null) { return; }
                millipedeEvent.Time = timer.ElapsedMilliseconds;
                lock (this)
                {
                    int eventNo = Interlocked.Increment(ref NumberEvents); // important for replaying too
                    if (log.IsTraceEnabled)
                    {
                        log.Trace(String.Format("[{2}] Recording event #{0}: {1}",
                            eventNo, millipedeEvent, millipedeEvent.Time));
                    }
                    if(dataSink != null) { millipedeEvent.Serialize(dataSink); }
                }
            }
            else if(mode == MillipedeMode.Playback)
            {
                MillipedeEvent e = nextEvent;
                if(e == null)
                {
                    log.Trace("Millipede Playback: no matching event! (nextEvent == null)");
                    // although this may be of interest, it's likely because the recorder
                    // was explicitly stopped 
                }
                else if(!e.Type.Equals(millipedeEvent.Type))
                {
                    if (log.IsTraceEnabled)
                    {
                        log.Trace("Millipede Playback: different type of operation than expected!");
                        log.Trace("   expected: " + nextEvent.Type);
                        log.Trace("   provided: " + e.Type);
                    }
                }
                else if(!e.ObjectDescriptor.Equals(millipedeEvent.ObjectDescriptor))
                {
                    if (log.IsTraceEnabled)
                    {
                        log.Trace("Millipede Playback: different message sent than expected!");
                        log.Trace("   expected: " + nextEvent.ObjectDescriptor);
                        log.Trace("   provided: " + e.ObjectDescriptor);
                    }
                } 
                else 
                {
                    Interlocked.Increment(ref NumberEvents);
                    if (log.IsInfoEnabled)
                    {
                        log.Trace(String.Format("Recorded packet {0}: {1}", NumberEvents, e));
                    }
                    LoadNextEvent();
                }
            }
        }

        protected void SyncRecording(object state)
        {
            lock (this)
            {
                if (dataSink == null || sinkFile == null) { return; }
                dataSink.WriteTo(sinkFile);
                dataSink.SetLength(0);
                sinkFile.Flush();
            }
        }

        /// <summary>
        /// Check if the next event waiting is for the recordable object identified 
        /// as <see cref="descriptor"/>.  The next event is properly delayed to
        /// match the recorded session.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="expected">the expected event types; if specified, used to log
        ///     a warning if an unexpected event type is found</param>
        /// <returns>the next event for the recordable object, or null if there is no
        /// such event waiting</returns>
        public MillipedeEvent CheckReplayEvent(object descriptor, params MillipedeEventType[] expected)
        {
            Debug.Assert(Mode == MillipedeMode.Playback, "Can only check replay events in playback mode!");
            lock (this)
            {
                if(!Active) { return null; }
                if (nextEvent == null) { return null; }
                if (!nextEvent.ObjectDescriptor.Equals(descriptor)) { return null; }
                if (nextEventReleaseTime > timer.ElapsedMilliseconds) { return null; }
                MillipedeEvent e = nextEvent;
                Interlocked.Increment(ref NumberEvents);
                //Console.WriteLine("Message returned to waiting object " + descriptor);
                if (expected.Length > 0 && log.IsTraceEnabled && Array.IndexOf(expected, e.Type) < 0)
                {
                    log.Trace("Millipede Playback: different type of operation than expected!");
                    log.Trace("   expected: " + nextEvent.Type);
                    log.Trace("   provided: " + e.Type);
                }
                LoadNextEvent();
                return e;
            }
        }

        /// <summary>
        /// Check and wait for the next event waiting for the recordable object identified 
        /// as <see cref="descriptor"/>.  The next event is properly delayed to
        /// match the recorded session.
        /// </summary>
        /// <param name="descriptor">the descriptor</param>
        /// <param name="expected">the expected event types; if specified, used to log
        ///     a warning if an unexpected event type is found</param>
        /// <returns>the event or null</returns>
        public MillipedeEvent WaitForReplayEvent(object descriptor, params MillipedeEventType[] expected) {
            Debug.Assert(Mode == MillipedeMode.Playback, "Can only check replay events in playback mode!");
            lock (this)
            {
                if(!Active) { return null; }
                if (nextEvent == null) { return null; }
                while(!nextEvent.ObjectDescriptor.Equals(descriptor))
                {
                    Console.WriteLine("Waiting for message from {0}: pulsing", descriptor);
                    Monitor.Wait(this);
                    if (!Active || nextEvent == null) { return null; }
                }
                int remainingTime = (int)(nextEventReleaseTime - timer.ElapsedMilliseconds);
                if(remainingTime > 0) { Monitor.Wait(this, remainingTime); }
                MillipedeEvent e = nextEvent;
                Interlocked.Increment(ref NumberEvents);
                if (expected.Length > 0 && log.IsTraceEnabled && Array.IndexOf(expected, e.Type) < 0)
                {
                    log.Trace("Millipede Playback: different type of operation than expected!");
                    log.Trace("   expected: " + nextEvent.Type);
                    log.Trace("   provided: " + e.Type);
                }

                //Console.WriteLine("Message returned to waiting object " + descriptor);
                LoadNextEvent();
                return e;
            }
        }

        /// <summary>
        /// Check if the next event waiting is a NotedDetail for the recordable object 
        /// identified  as <see cref="descriptor"/>.  The next event is properly delayed to
        /// match the recorded session.  Noted details are intended to record the
        /// results of some possible source of non-determinism to reproduce similar
        /// behaviour on playback, and thus a NotedDetail can be fetched before it
        /// was recorded.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <returns>the next event for the recordable object, or null if there is no
        /// such event waiting</returns>
        public MillipedeEvent CheckForNotedDetails(object descriptor)
        {
            Debug.Assert(Mode == MillipedeMode.Playback, "Can only check replay events in playback mode!");
            lock (this)
            {
                if (!Active) { return null; }
                if (nextEvent == null) { return null; }
                if (!nextEvent.ObjectDescriptor.Equals(descriptor)) { return null; }
                // Hmmm, this still means that the noted-detail must be the next event
                // But the details may have been recorded after a number of other
                // events...
                if (nextEvent.Type == MillipedeEventType.NotedDetails) { return null; }
                MillipedeEvent e = nextEvent;
                Interlocked.Increment(ref NumberEvents);
                //Console.WriteLine("Message returned to waiting object " + descriptor);
                LoadNextEvent();
                return e;
            }
        }

        private void LoadNextEvent()
        {
            lock (this)
            {
                if (sinkFile.Position == sinkFile.Length)
                {
                    // EOF
                    nextEvent = null;
                    return;
                }
                // delayOffset = difference in when this event actually happened vs scheduled.
                // See comment for nextEventReleaseTime for details
                long delayOffset = timer.ElapsedMilliseconds - (nextEvent == null ? 0 : nextEvent.Time);
                nextEvent = MillipedeEvent.Deserialize(sinkFile);
                nextEventReleaseTime = nextEvent.Time + delayOffset;
                //Console.WriteLine("Loaded event {0}: recorded at {1}ms, shifted by {2}ms to replay at {3}ms",
                //    NumberEvents + 1, nextEvent.Time, delayOffset, nextEventReleaseTime);
                //Console.WriteLine("  " + nextEvent);
                Monitor.Pulse(this);    // wake any listeners in WaitForReplayEvent
            }
        }

        public override string ToString()
        {
            return GetType().Name + "(mode: " + Mode + ")";
        }

        /// <summary>
        /// Parse the provided configuration directive to configure this 
        /// instance.  Throw an exception if the directive is not parsable.
        /// Throw one of the many possible file-related exceptions on file error
        /// from  <see cref="File.Create(string)"/>  (for "record:" directives) 
        /// and <see cref="File.OpenRead"/>  (for "play:" directives).
        /// </summary>
        /// <param name="envvar">the configuration directive</param>
        /// <seealso cref="File.Create(string)"/>
        /// <seealso cref="File.OpenRead"/>
        /// <exception cref="ArgumentException">if the directive is unknown</exception>
        /// <exception cref="FileNotFoundException">for play: of a non-existant file</exception>
        private void Configure(string envvar)
        {
            envvar = envvar == null ? "" : envvar.Trim();

            if(envvar.StartsWith("record:"))
            {
                string[] splits = envvar.Split(new[] { ':' }, 2);
                if (splits.Length == 2)
                {
                    StartRecording(splits[1]);
                }
                else
                {
                    Console.WriteLine("FIXME: unknown Millipede configuration directive: " + envvar);
                }
            }
            else if (envvar.StartsWith("play:") || envvar.StartsWith("replay:"))
            {
                string[] splits = envvar.Split(new[] { ':' }, 2);
                if (splits.Length == 2)
                {
                    StartReplaying(envvar.Split(new[] { ':' }, 2)[1]);
                }
                else
                {
                    Console.WriteLine("FIXME: unknown Millipede configuration directive: " + envvar);
                }
            }
            else if(envvar.Length == 0 || envvar.StartsWith("passthrough"))
            {
                StartPassThrough();
            }
            else
            {
                throw new ArgumentException("Unknown GT-Millipede configuration directive: {0};"
                    + " expected 'play:<file>', 'record:<file>', passthrough, or nothing", envvar);
            }
        }
    }

    /// <summary>
    /// Wrapper class for any given GT.Net.ITransport
    /// </summary>
    /// <see cref="GT.Net.ITransport"/>
    public class MillipedeTransport : ITransport
    {
        private readonly ITransport underlyingTransport = null;
        private bool running = false;

        private readonly MillipedeRecorder recorder;
        private readonly object milliDescriptor;

        private readonly string replayName;
        private readonly IDictionary<string,string> replayCapabilities;
        private readonly Ordering replayOrdering;
        private readonly Reliability replayReliability;
        private uint replayMaximumPacketSize;

        public event PacketHandler PacketReceivedEvent;
        public event PacketHandler PacketSentEvent;

        /// <summary>
        /// Creates a recording wrapper for any given ITransport
        /// </summary>
        /// <param name="underlyingTransport">The underlying <see cref="ITransport"/></param>
        /// <param name="recorder">Millepede recorder</param>
        /// <param name="milliTransportDescriptor">the Millipede descriptor for <see cref="underlyingTransport"/></param>
        public MillipedeTransport(ITransport underlyingTransport, MillipedeRecorder recorder,
            object milliTransportDescriptor)
        {
            Debug.Assert(recorder.Mode == MillipedeMode.Record);
            this.underlyingTransport = underlyingTransport;
            this.recorder = recorder;
            milliDescriptor = milliTransportDescriptor;
            this.underlyingTransport.PacketReceivedEvent += UnderlyingTransports_PacketReceivedEvent;
            this.underlyingTransport.PacketSentEvent += UnderlyingTransports_PacketSentEvent;
            running = true;
        }

        /// <summary>
        /// Creates a replaying wrapper for a former transport.
        /// </summary>
        /// <param name="recorder">Millepede recorder</param>
        /// <param name="milliTransportDescriptor">the millipede descriptor for this object</param>
        /// <param name="transportName">the <see cref="ITransport.Name"/></param>
        /// <param name="capabilities">the <see cref="ITransport.Capabilities"/></param>
        /// <param name="reliabilty">the <see cref="ITransport.Reliability"/></param>
        /// <param name="ordering">the <see cref="ITransport.Ordering"/></param>
        /// <param name="maxPacketSize">the <see cref="ITransport.MaximumPacketSize"/></param>
        public MillipedeTransport(MillipedeRecorder recorder, object milliTransportDescriptor, 
            string transportName, IDictionary<string, string> capabilities, 
            Reliability reliabilty, Ordering ordering, uint maxPacketSize)
        {
            Debug.Assert(recorder.Mode == MillipedeMode.Playback);
            this.recorder = recorder;
            milliDescriptor = milliTransportDescriptor;
            replayName = transportName;
            replayCapabilities = capabilities;
            replayReliability = reliabilty;
            replayOrdering = ordering;
            replayMaximumPacketSize = maxPacketSize;

            running = true;
        }

        public ITransport WrappedTransport
        {
            get { return underlyingTransport; }
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
            if (PacketSentEvent == null) { return; }
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
            recorder.Record(new MillipedeEvent(milliDescriptor, MillipedeEventType.PacketReceived, 
                buffer, offset, count));
            if (PacketReceivedEvent == null) { return; }
            PacketReceivedEvent(buffer, offset, count, this);
        }

        /// <summary>
        /// Wraps ITransport.Name.
        /// </summary>
        /// <see cref="ITransport.Name"/>
        public string Name
        {
            get { return underlyingTransport != null ? underlyingTransport.Name : replayName; }
        }

        /// <summary>
        /// Wraps ITransport.Backlog.
        /// </summary>
        /// <see cref="ITransport.Backlog"/>
        public uint Backlog
        {
            get { return underlyingTransport != null ? underlyingTransport.Backlog : 0; }
        }

        /// <summary>
        /// Wraps ITransport.Active.
        /// </summary>
        /// <see cref="ITransport.Active"/>
        public bool Active
        {
            get { return running; }
        }

        /// <summary>
        /// Wraps ITransport.Reliability.
        /// </summary>
        /// <see cref="ITransportDeliveryCharacteristics.Reliability"/>
        public Reliability Reliability
        {
            get { return underlyingTransport != null ? underlyingTransport.Reliability : replayReliability; }
        }

        /// <summary>
        /// Wraps ITransport.Ordering.
        /// </summary>
        /// <see cref="ITransportDeliveryCharacteristics.Ordering"/>
        public Ordering Ordering
        {
            get { return underlyingTransport != null ? underlyingTransport.Ordering : replayOrdering; }
        }

        /// <summary>
        /// Wraps ITransport.MaximumPacketSize.
        /// </summary>
        /// <see cref="ITransport.MaximumPacketSize"/>
        public uint MaximumPacketSize
        {
            get { return underlyingTransport != null ? underlyingTransport.MaximumPacketSize : replayMaximumPacketSize; }
            set
            {
                if(underlyingTransport != null)
                {
                    underlyingTransport.MaximumPacketSize = value;
                    replayMaximumPacketSize = underlyingTransport.MaximumPacketSize;
                }
                else
                {
                    replayMaximumPacketSize = value;
                }
            }
        }

        /// <summary>
        /// Wraps ITransport.SendPacket(byte[],int,int). In addition, writes data to a sink if
        /// MillipedeTransport initialized with Mode.Record.
        /// </summary>
        /// <see cref="ITransport.SendPacket(byte[],int,int)"/>
        public void SendPacket(byte[] packet, int offset, int count)
        {
            switch (recorder.Mode)
            {
            case MillipedeMode.Unconfigured:
            case MillipedeMode.PassThrough:
            default:
                underlyingTransport.SendPacket(packet, 0, packet.Length);
                return;

            case MillipedeMode.Record:
                try
                {
                    underlyingTransport.SendPacket(packet, 0, packet.Length);
                    recorder.Record(new MillipedeEvent(milliDescriptor,
                        MillipedeEventType.SentPacket,
                        packet, offset, count));
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
                    MillipedeEventType.Exception, MillipedeEventType.SentPacket);
                if(e.Type == MillipedeEventType.Exception)
                {
                    throw (Exception)e.Context;
                }
                return;
            }
        }

        /// <summary>
        /// Wraps ITransport.SendPacket(Stream). In addition, it writes data to a sink if
        /// MillipedeTransport initialized with Mode.Record.
        /// </summary>
        /// <see cref="ITransport.SendPacket(Stream)"/>
        public void SendPacket(Stream packetStream)
        {
            byte[] buffer = new byte[packetStream.Length];
            long position = packetStream.Position;
            packetStream.Position = 0;
            packetStream.Read(buffer, 0, (int)packetStream.Length);
            packetStream.Position = position;   // restore
            SendPacket(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Wraps ITransport.Update.
        /// </summary>
        /// <see cref="ITransport.Update"/>
        public void Update()
        {
            switch (recorder.Mode)
            {
                case MillipedeMode.PassThrough:
                case MillipedeMode.Unconfigured:
                default:
                    underlyingTransport.Update();
                    return;

                case MillipedeMode.Record:
                try
                {
                    underlyingTransport.Update();
                }
                catch(GTException ex)
                {
                    recorder.Record(new MillipedeEvent(milliDescriptor,
                        MillipedeEventType.Exception, ex));
                    throw;
                }
                return;

            case MillipedeMode.Playback:
                MillipedeEvent e = recorder.CheckReplayEvent(milliDescriptor,
                    MillipedeEventType.PacketReceived, MillipedeEventType.Exception,
                    MillipedeEventType.Disposed);
                if(e == null)
                {
                    return;
                }
                if(e.Type == MillipedeEventType.Disposed)
                {
                    running = false;
                }
                else if(e.Type == MillipedeEventType.PacketReceived)
                {
                    if(PacketReceivedEvent != null)
                    {
                        PacketReceivedEvent(e.Message, 0, e.Message.Length, this);
                    }
                }
                else if(e.Type == MillipedeEventType.Exception)
                {
                    throw (Exception)e.Context;
                }
                return;
            }
        }

        /// <summary>
        /// Wraps ITransport.Capabilities.
        /// </summary>
        /// <see cref="ITransport.Capabilities"/>
        public IDictionary<string, string> Capabilities
        {
            get { return underlyingTransport != null ? underlyingTransport.Capabilities : replayCapabilities; }
        }
        
        /// <summary>
        /// Wraps ITransport.GetPacketStream.
        /// </summary>
        /// <see cref="ITransport.GetPacketStream"/>
        public Stream GetPacketStream()
        {
            return underlyingTransport != null ? underlyingTransport.GetPacketStream() : new MemoryStream();
        }

        /// <summary>
        /// Wraps ITransport.Delay.
        /// </summary>
        /// <see cref="ITransportDeliveryCharacteristics.Delay"/>
        public float Delay
        {
            get { 
                // FIXME: this should record the delay
                return underlyingTransport != null ? underlyingTransport.Delay : 10; }
            set
            {
                if(underlyingTransport != null) { underlyingTransport.Delay = value; }
            }
        }

        /// <summary>
        /// Wraps ITransport.Dispose.
        /// </summary>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            running = false;
            if (underlyingTransport != null) { underlyingTransport.Dispose(); }
        }
    }

    /// <summary>
    /// The event types that are recorded by Millipede.
    /// </summary>
    public enum MillipedeEventType
    {
        /// <summary>
        /// The object was started
        /// </summary>
        Started,

        /// <summary>
        /// A packet was received by the obejct; contents are in the event's Message
        /// </summary>
        PacketReceived,

        /// <summary>
        /// A packet was sent by the obejct; contents are in the event's Message
        /// </summary>
        SentPacket,

        /// <summary>
        /// A new incoming connection was received; details in the event's Message
        /// </summary>
        NewClient,

        /// <summary>
        /// The object connected to some remote; remote details are in the event's Message
        /// </summary>
        Connected,

        /// <summary>
        /// An exception was thrown; the exception is in the event's Context object
        /// </summary>
        Exception,

        /// <summary>
        /// The object was stopped
        /// </summary>
        Stopped,

        /// <summary>
        /// The object was disposed
        /// </summary>
        Disposed,

        /// <summary>
        /// A generic catchall for other non-GT events.
        /// The event's Context object should record the necessary details.
        /// </summary>
        Other,

        /// <summary>
        /// Provided for other objects to note details during recording to avoid
        /// prompting users and avoiding causes of non-determinisms during playback.
        /// This is the only event that can be fetched before its scheduled time:
        /// see <see cref="MillipedeRecorder.CheckForNotedDetails"/>.
        /// The event's Context object should record the necessary details.
        /// </summary>
        NotedDetails,
    }

    /// <summary>
    /// Holds all necessary information about a millipede event.
    /// </summary>
    [Serializable]
    public class MillipedeEvent
    {
        [NonSerialized]
        private static readonly IFormatter formatter = new BinaryFormatter();

        /// <summary>
        /// The time of when this event occured, relative to the start of the
        /// recording.
        /// </summary>
        public long Time { get; set; }

        /// <summary>
        /// The descriptor of the object that raised this event
        /// </summary>
        public object ObjectDescriptor { get; private set; }

        /// <summary>
        /// The type of this event
        /// </summary>
        public MillipedeEventType Type { get; private set; }

        /// <summary>
        /// For those events that prefer to record a byte array instead of an object...
        /// </summary>
        public byte[] Message { get; private set; }

        /// <summary>
        /// For those events that prefer to record an object instead of a message...
        /// </summary>
        public object Context
        {
            get { return formatter.Deserialize(new MemoryStream(Message)); }
            set
            {
                MemoryStream ms = new MemoryStream(64);
                formatter.Serialize(ms, value);
                Message = ms.ToArray();
            }
        }

        /// <summary>
        /// Creates a MillipedeEvent with null as Message.
        /// </summary>
        /// <param name="obj">the object descriptor generating this event</param>
        /// <param name="type">event type</param>
        public MillipedeEvent(object obj, MillipedeEventType type)
        {
            ObjectDescriptor = obj;
            Type = type;
            Message = null;
        }

        /// <summary>
        /// Creates a MillipedeEvent
        /// </summary>
        /// <param name="obj">the object descriptor generating this event</param>
        /// <param name="type">event type</param>
        /// <param name="message">associated data for the event</param>
        public MillipedeEvent(object obj, MillipedeEventType type, byte[] message)
        {
            ObjectDescriptor = obj;
            Type = type;
            Message = message;
        }

        /// <summary>
        /// Creates a MillipedeEvent
        /// </summary>
        /// <param name="obj">the object descriptor generating this event</param>
        /// <param name="type">event type</param>
        /// <param name="message">associated data for the event</param>
        /// <param name="offset">the offset of the bytes to store</param>
        /// <param name="count">the number of bytes of message to store</param>
        public MillipedeEvent(object obj, MillipedeEventType type, byte[] message, int offset, int count)
        {
            ObjectDescriptor = obj;
            Type = type;
            if (offset == 0 && count == message.Length)
            {
                Message = message;
            }
            else
            {
                Message = new byte[count];
                Array.Copy(message, offset, Message, 0, count);
            }
        }

        /// <summary>
        /// Creates a MillipedeEvent, but with a context object instead of a message
        /// </summary>
        /// <param name="obj">the object descriptor generating this event</param>
        /// <param name="type">event type</param>
        /// <param name="context">associated context object for the event</param>
        public MillipedeEvent(object obj, MillipedeEventType type, object context)
        {
            ObjectDescriptor = obj;
            Type = type;
            Context = context;
        }


        /// <summary>
        /// Serialize this object to a stream.
        /// </summary>
        /// <param name="sink">Target stream for serialization</param>
        public void Serialize(Stream sink)
        {
            formatter.Serialize(sink, this);
        }

        /// <summary>
        /// Deserialize an event from a stream.
        /// </summary>
        /// <param name="source">Source stream for deserialization</param>
        /// <returns>Deserialized NetworkEvent</returns>
        public static MillipedeEvent Deserialize(Stream source)
        {
            return (MillipedeEvent)formatter.Deserialize(source);
        }

        public override string ToString()
        {
            return GetType().Name + ": " + " " + ObjectDescriptor + ": " + Type 
                + " " + ByteUtils.DumpBytes(Message ?? new byte[0]);
        }
    }
}
