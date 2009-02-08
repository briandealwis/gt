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
    /// A recorder is able to record or replay a stream of <see cref="NetworkEvent"/>.
    /// </summary>
    public class MillipedeRecorder : IDisposable
    {

        #region Singleton

        private static MillipedeRecorder singleton;

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
                    string envvar = Environment.GetEnvironmentVariable("GTMILLIPEDE");
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
        private NetworkEvent nextEvent = null;

        /// <summary>
        /// The time in milliseconds when <see cref="nextEvent"/> *should* be released,
        /// as opposed to the timestamp in its timestamp, <see cref="NetworkEvent.Time"/>.
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
            timer = Stopwatch.StartNew();
            mode = MillipedeMode.PassThrough;
            dataSink = null;
            sinkFile = null;
            syncingTimer = null;
        }

        /// <summary>
        /// Generate a unique descriptor for the provided object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public object GenerateDescriptor(object obj)
        {
            string typeName = obj.GetType().FullName;
            string toString = obj.ToString();
            if (toString.Equals(typeName))
            {
                return typeName + "@" + Interlocked.Increment(ref uniqueCount);
            }
            return typeName + ":" + toString;
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
            }
            mode = MillipedeMode.Unconfigured;
        }

        public void Record(NetworkEvent networkEvent)
        {
            if(mode == MillipedeMode.Record)
            {
                if (dataSink == null) { return; }
                networkEvent.Time = timer.ElapsedMilliseconds;
                lock (this)
                {
                    int eventNo = Interlocked.Increment(ref NumberEvents); // important for replaying too
                    if (log.IsTraceEnabled)
                    {
                        log.Trace(String.Format("[{2}] Recording event #{0}: {1}",
                            eventNo, networkEvent, networkEvent.Time));
                    }
                    if(dataSink != null) { networkEvent.Serialize(dataSink); }
                }
            }
            else if(mode == MillipedeMode.Playback)
            {
                NetworkEvent e = nextEvent;
                if(e == null)
                {
                    log.Trace("Millipede Playback: no matching event! (nextEvent == null)");
                    // although this may be of interest, it's likely because the recorder
                    // was explicitly stopped 
                }
                else if(!e.Type.Equals(networkEvent.Type))
                {
                    if (log.IsInfoEnabled)
                    {
                        log.Trace("Millipede Playback: different type of operation than expected!");
                        log.Trace("   expected: " + nextEvent.Type);
                        log.Trace("   provided: " + e.Type);
                    }
                }
                else if(!e.ObjectDescriptor.Equals(networkEvent.ObjectDescriptor))
                {
                    if (log.IsInfoEnabled)
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
        /// <returns>the next event for the recordable object, or null if there is no
        /// such event waiting</returns>
        public NetworkEvent CheckReplayEvent(object descriptor)
        {
            Debug.Assert(Mode == MillipedeMode.Playback, "Can only check replay events in playback mode!");
            lock (this)
            {
                if(!Active) { return null; }
                if (nextEvent == null) { return null; }
                if (!nextEvent.ObjectDescriptor.Equals(descriptor)) { return null; }
                if (nextEventReleaseTime > timer.ElapsedMilliseconds) { return null; }
                NetworkEvent e = nextEvent;
                Interlocked.Increment(ref NumberEvents);
                //Console.WriteLine("Message returned to waiting object " + descriptor);
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
        /// <returns>the event or null</returns>
        public NetworkEvent WaitForReplayEvent(object descriptor) {
            Debug.Assert(Mode == MillipedeMode.Playback, "Can only check replay events in playback mode!");
            lock (this)
            {
                if(!Active) { return null; }
                if (nextEvent == null) { return null; }
                while(!nextEvent.ObjectDescriptor.Equals(descriptor))
                {
                    //Console.WriteLine("Waiting for message from {0}: pulsing", descriptor);
                    Monitor.Pulse(this);
                    if (!Active || nextEvent == null) { return null; }
                }
                int remainingTime = (int)(nextEventReleaseTime - timer.ElapsedMilliseconds);
                if(remainingTime > 0) { Monitor.Wait(this, remainingTime); }
                NetworkEvent e = nextEvent;
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
                nextEvent = NetworkEvent.DeSerialize(sinkFile);
                nextEventReleaseTime = nextEvent.Time + delayOffset;
                //Console.WriteLine("Loaded event {0}: recorded at {1}ms, shifted by {2}ms to replay at {3}ms",
                //    NumberEvents + 1, nextEvent.Time, delayOffset, nextEventReleaseTime);
                //Console.WriteLine("  " + nextEvent);
            }
        }

        public override string ToString()
        {
            return GetType().Name + "(mode: " + Mode + ")";
        }

        private void Configure(string envvar)
        {
            envvar = envvar == null ? "" : envvar.Trim();

            if(envvar.StartsWith("record"))
            {
                string[] splits = envvar.Split(new[] { ':' }, 2);
                if (envvar.StartsWith("record:") && splits.Length == 2)
                {
                    StartRecording(splits[1]);
                }
                else
                {
                    Console.WriteLine("FIXME: unknown Millipede configuration directive: " + envvar);
                }
            }
            else if(envvar.StartsWith("play"))
            {
                string[] splits = envvar.Split(new[] { ':' }, 2);
                if (envvar.StartsWith("play:") && splits.Length == 2)
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
                Console.WriteLine("FIXME: unknown Millipede configuration directive: " + envvar);
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
            recorder.Record(new NetworkEvent(milliDescriptor, NetworkEventType.PacketReceived, 
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
            recorder.Record(new NetworkEvent(milliDescriptor, NetworkEventType.SentPacket, packet, offset, count));
            if (recorder.Mode != MillipedeMode.Playback)
            {
                underlyingTransport.SendPacket(packet, 0, packet.Length);
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
            recorder.Record(new NetworkEvent(milliDescriptor, NetworkEventType.SentPacket, buffer));
            if (recorder.Mode != MillipedeMode.Playback)
            {
                underlyingTransport.SendPacket(packetStream);
            }
        }

        /// <summary>
        /// Wraps ITransport.Update.
        /// </summary>
        /// <see cref="ITransport.Update"/>
        public void Update()
        {
            if (recorder.Mode != MillipedeMode.Playback)
            {
                underlyingTransport.Update();
                return;
            }
            NetworkEvent e = recorder.CheckReplayEvent(milliDescriptor);
            if (e == null) { return; }
            if (e.Type == NetworkEventType.Disposed)
            {
                running = false;
            }
            else if (e.Type == NetworkEventType.PacketReceived)
            {
                if(PacketReceivedEvent != null)
                {
                    PacketReceivedEvent(e.Message, 0, e.Message.Length, this);
                }
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
    public enum NetworkEventType
    {
        Started,
        PacketReceived,
        SentPacket,
        NewClient,
        Connected,
        Stopped,
        Disposed,
    }

    /// <summary>
    /// Holds all necessary information about an network event.
    /// </summary>
    [Serializable]
    public class NetworkEvent
    {
        public long Time { get; set; }
        public object ObjectDescriptor { get; private set; }
        public NetworkEventType Type { get; private set; }
        public byte[] Message { get; private set; }
        [NonSerialized] private static readonly IFormatter formatter = new BinaryFormatter();

        /// <summary>
        /// Creates a NetworkEvent with null as Message.
        /// </summary>
        /// <param name="obj">the object descriptor generating this event</param>
        /// <param name="type">Method name from where the constructor is called</param>
        public NetworkEvent(object obj, NetworkEventType type)
        {
            ObjectDescriptor = obj;
            Type = type;
            Message = null;
        }

        /// <summary>
        /// Creates a NetworkEvent
        /// </summary>
        /// <param name="obj">the object descriptor generating this event</param>
        /// <param name="type">Method name from where the constructor is called</param>
        /// <param name="message">Message to be stored</param>
        public NetworkEvent(object obj, NetworkEventType type, byte[] message)
        {
            ObjectDescriptor = obj;
            Type = type;
            Message = message;
        }

        /// <summary>
        /// Creates a NetworkEvent
        /// </summary>
        /// <param name="obj">the object descriptor generating this event</param>
        /// <param name="type">Method name from where the constructor is called</param>
        /// <param name="message">Message, of which a subset is to be stored</param>
        /// <param name="offset">the offset of the bytes to store</param>
        /// <param name="count">the number of bytes of message to store</param>
        public NetworkEvent(object obj, NetworkEventType type, byte[] message, int offset, int count)
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

        public override string ToString()
        {
            return GetType().Name + ": " + " " + ObjectDescriptor + ": " + Type 
                + " " + ByteUtils.DumpBytes(Message ?? new byte[0]);
        }
    }
}
