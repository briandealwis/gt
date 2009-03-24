using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Common.Logging;
using GT.Utils;
using GT.Weak;

namespace GT.Net
{
    /// <summary>
    /// A marshaller that wraps another marshaller to break down large messages 
    /// into manageable chunks for the provided transport.  Each manageable chunk
    /// is called a fragment.  A message may be discarded if the underlying transport
    /// is sequenced and a fragment is received out of order (or rather, a fragment
    /// has been dropped), or if the message has passed our of the sequence window.
    /// </summary>
    /// <remarks>
    /// The LOM operates on the results from a different marshaller.
    /// This sub-marshaller is used to transform objects, etc. to bytes.
    /// Each message is broken down into:
    ///   [255] [packet-size] [original packet]                 if the message fits into a transport packet
    ///   [seqno] [encoded-#-fragments] [frag-0-size] [frag]    if the message is the first fragment
    ///   [seqno'] [fragment-#] [frag-size] [frag]              for all subsequent fragments; seqno' = seqno | 128
    /// </remarks>
    public class LargeObjectMarshaller : IMarshaller
    {
        public enum DiscardReason
        {
            MissedFragment,
            MessageTimedout,
            TransportDisconnected,
        }

        /// <summary>
        /// The submarshaller used for encoding messages into packets.
        /// </summary>
        private readonly IMarshaller subMarshaller;

        /// <summary>
        /// The maximum number of messages maintained; messages that are not
        /// completed within this window are discarded.
        /// </summary>
        private readonly uint windowSize;

        /// <summary>
        /// The default window size to be used, if a window size is unspecified
        /// at creation time.
        /// </summary>
        private static uint _defaultWindowSize = 16;
        public static uint DefaultWindowSize
        {
            get { return _defaultWindowSize; }
            set
            {
                if (value >= MaxSequenceCapacity)
                {
                    throw new ArgumentException("Cannot be larger than " + MaxSequenceCapacity);
                }
                _defaultWindowSize = value;
            }
        }

        // SeqNo must be expressible in a byte.  Top bit used to distinguish 
        // between first fragment in a sequence and subsequent fragments.
        // (And 255 = unfragmented packet)
        // This leaves 7 bits for the sequence number.
        public const uint MaxSequenceCapacity = 127;    // 2^7 - 1

        /// <summary>
        /// The sequence number for the next outgoing message on a particular transport
        /// </summary>
        private WeakKeyDictionary<ITransportDeliveryCharacteristics,uint> outgoingSeqNo =
            new WeakKeyDictionary<ITransportDeliveryCharacteristics, uint>();

        /// <summary>
        /// Bookkeeping data for in-progress messages received.
        /// </summary>
        private WeakKeyDictionary<ITransportDeliveryCharacteristics,Sequences> accumulatedReceived =
            new WeakKeyDictionary<ITransportDeliveryCharacteristics, Sequences>();

        protected ILog log;

        public LargeObjectMarshaller(IMarshaller submarshaller)
            : this(submarshaller, DefaultWindowSize) {}

        public LargeObjectMarshaller(IMarshaller submarshaller, uint windowSize)
        {
            log = LogManager.GetLogger(GetType());

            this.subMarshaller = submarshaller;
            Debug.Assert(windowSize >= 2 && windowSize <= MaxSequenceCapacity, 
                "Invalid window size: must be a power of 2 and on [2,64]");
            this.windowSize = windowSize;
        }

        public string[] Descriptors
        {
            get
            {
                List<string> descriptors = new List<string>();
                foreach (string subdescriptor in subMarshaller.Descriptors)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < subdescriptor.Length; i++)
                    {
                        sb.Append(Char.ConvertFromUtf32(Char.ConvertToUtf32(subdescriptor, i) ^ (int)'L'));
                    }
                    descriptors.Add(sb.ToString());
                }
                return descriptors.ToArray();
            }
        }

        public MarshalledResult Marshal(int senderIdentity, Message message, ITransportDeliveryCharacteristics tdc)
        {
            MarshalledResult submr = subMarshaller.Marshal(senderIdentity, message, tdc);
            MarshalledResult mr = new MarshalledResult();
            while (submr.HasPackets)
            {
                TransportPacket packet = submr.RemovePacket();
                if (packet.Length < tdc.MaximumPacketSize - 5)
                {
                    ///   [255] [packet-size] [original packet]  if the message fits into a transport packet
                    MemoryStream ms = new MemoryStream(5);
                    ms.WriteByte(255);
                    ByteUtils.EncodeLength(packet.Length, ms);
                    packet.Prepend(ms.GetBuffer(), 0, (int)ms.Length);
                    mr.AddPacket(packet);
                }
                else
                {
                    ///   [seqno] [encoded-#-fragments] [frag-0-size] [frag]    if the message is the first fragment
                    ///   [seqno'] [fragment-#] [frag-size] [frag]              for all subsequent fragments; seqno' = seqno | 128
                    // Although we use an adaptive scheme for encoding the number,
                    // we assume a maximum of 4 bytes for encoding both # frags and frag size
                    // for a total of 9 bytes.
                    uint MaxHeaderSize = 9;
                    uint MaxPacketSize = Math.Max(MaxHeaderSize, tdc.MaximumPacketSize - MaxHeaderSize);
                    uint numFragments = (uint)(packet.Length - 1 + MaxPacketSize) / MaxPacketSize;
                    Debug.Assert(numFragments > 1);
                    uint seqNo = AllocateOutgoingSeqNo(tdc);
                    for(uint fragNo = 0; fragNo < numFragments; fragNo++)
                    {
                        TransportPacket newPacket = new TransportPacket();
                        Stream s = newPacket.AsWriteStream();
                        uint fragSize = (uint)Math.Min(MaxPacketSize, 
                            packet.Length - (fragNo * MaxPacketSize));
                        if(fragNo == 0)
                        {
                            s.WriteByte((byte)seqNo);
                            ByteUtils.EncodeLength((int)numFragments, s);
                        } 
                        else
                        {
                            s.WriteByte((byte)(seqNo | 128));
                            ByteUtils.EncodeLength((int)fragNo, s);
                        }
                        ByteUtils.EncodeLength((int)fragSize, s);
                        newPacket.Add(packet, (int)(fragNo * MaxPacketSize), (int)fragSize);
                        mr.AddPacket(newPacket);
                    }
                    packet.Dispose();
                }
            }
            submr.Dispose();
            return mr;
        }

        private uint AllocateOutgoingSeqNo(ITransportDeliveryCharacteristics tdc)
        {
            lock (this)
            {
                uint seqNo;
                if (!outgoingSeqNo.TryGetValue(tdc, out seqNo))
                {
                    seqNo = 0;
                }
                outgoingSeqNo[tdc] = (seqNo + 1) % (MaxSequenceCapacity * 2);
                return seqNo;
            }
        }

        public void Unmarshal(TransportPacket input, ITransportDeliveryCharacteristics tdc, EventHandler<MessageEventArgs> messageAvailable)
        {
            ///   [255] [packet-size] [original packet]                 if the message fits into a transport packet
            ///   [seqno] [encoded-#-fragments] [frag-0-size] [frag]    if the message is the first fragment
            ///   [seqno'] [fragment-#] [frag-size] [frag]              for all subsequent fragments; seqno' = seqno | 128
            Stream s = input.AsReadStream();
            byte seqNo = (byte)s.ReadByte();
            TransportPacket subPacket;
            if (seqNo == 255)
            {
                uint length = (uint)ByteUtils.DecodeLength(s);
                subPacket = input.SplitOut((int)length);
            }
            else if ((seqNo & 128) == 0)
            {
                // This starts a new message
                uint numFrags = (uint)ByteUtils.DecodeLength(s);
                uint fragLength = (uint)ByteUtils.DecodeLength(s);
                subPacket = UnmarshalFirstFragment(seqNo, numFrags, input.SplitOut((int)fragLength), tdc);
            }
            else 
            {
                // This is a message in progress
                seqNo = (byte)(seqNo & ~128);
                uint fragNo = (uint)ByteUtils.DecodeLength(s);
                uint fragLength = (uint)ByteUtils.DecodeLength(s);
                subPacket = UnmarshalFragInProgress(seqNo, fragNo, input.SplitOut((int)fragLength), tdc);
            }
            if (subPacket != null)
            {
                subMarshaller.Unmarshal(subPacket, tdc, messageAvailable);
                subPacket.Dispose();
            }
        }

        private TransportPacket UnmarshalFirstFragment(byte seqNo, uint numFrags, TransportPacket frag,
            ITransportDeliveryCharacteristics tdc)
        {
            Sequences sequences;
            if (!accumulatedReceived.TryGetValue(tdc, out sequences))
            {
                accumulatedReceived[tdc] = sequences = Sequences.ForTransport(tdc, windowSize, MaxSequenceCapacity);
            }
            return sequences.ReceivedFirstFrag(seqNo, numFrags, frag);
        }


        private TransportPacket UnmarshalFragInProgress(byte seqNo, uint fragNo, TransportPacket frag,
            ITransportDeliveryCharacteristics tdc)
        {
            Sequences sequences;
            if (!accumulatedReceived.TryGetValue(tdc, out sequences))
            {
                accumulatedReceived[tdc] = sequences = Sequences.ForTransport(tdc, windowSize, MaxSequenceCapacity);
            }
            return sequences.ReceivedFrag(seqNo, fragNo, frag);
        }

        public void Dispose()
        {
            subMarshaller.Dispose();
        }
    }

    abstract class Sequences
    {
        internal static Sequences ForTransport(ITransportDeliveryCharacteristics tdc, 
            uint seqWindowSize, uint maxSeqCapacity)
        {
            if(tdc.Reliability == Reliability.Reliable)
            {
                return new ReliableSequences(tdc); // seqWindowSize, maxSeqCapacity 
            }
            return new DiscardingSequences(tdc, seqWindowSize, maxSeqCapacity);
        }

        protected ITransportDeliveryCharacteristics tdc;

        protected Sequences(ITransportDeliveryCharacteristics tdc)
        {
            this.tdc = tdc;
        }

        public abstract TransportPacket ReceivedFirstFrag(uint seqNo, uint numFrags, TransportPacket fragment);
        public abstract TransportPacket ReceivedFrag(uint seqNo, uint fragNo, TransportPacket fragment);
    }

    /// <summary>
    /// Fragments sent using a reliable transport will always come through.
    /// Just gotta have faith.
    /// </summary>
    class ReliableSequences : Sequences
    {
        protected IDictionary<uint, FragmentedMessage> pendingMessages = 
            new Dictionary<uint, FragmentedMessage>();

        public ReliableSequences(ITransportDeliveryCharacteristics tdc) : base(tdc) {}

        public override TransportPacket ReceivedFirstFrag(uint seqNo, uint numFrags, TransportPacket fragment)
        {
            FragmentedMessage inProgress;
            if (!pendingMessages.TryGetValue(seqNo, out inProgress))
            {
                pendingMessages[seqNo] = inProgress = newFragmentedMessage(numFrags);
            }
            else
            {
                inProgress.SetNumberFragments(numFrags);
            }
            inProgress.RecordFragment(0, fragment);  // FragmentedMessage should Retain()
            return inProgress.Finished ? inProgress.Assemble() : null;
        }

        public override TransportPacket ReceivedFrag(uint seqNo, uint fragNo, TransportPacket fragment)
        {
            FragmentedMessage inProgress;
            if (!pendingMessages.TryGetValue(seqNo, out inProgress))
            {
                pendingMessages[seqNo] = inProgress = newFragmentedMessage(0);
            }
            inProgress.RecordFragment(fragNo, fragment);  // FragmentedMessage should Retain()
            return inProgress.Finished ? inProgress.Assemble() : null;
        }

        protected virtual FragmentedMessage newFragmentedMessage(uint numFrags)
        {
            return new FragmentedMessage(numFrags);
        }
    }

    class DiscardingSequences : ReliableSequences
    {
        protected SlidingWindowManager sw;

        public DiscardingSequences(ITransportDeliveryCharacteristics tdc, uint windowSize, uint capacity) 
            : base(tdc) 
        {
            sw = new SlidingWindowManager(windowSize, capacity);
            sw.FrameExpired += _expireFrame;
        }

        private void _expireFrame(uint seqNo)
        {
            pendingMessages.Remove(seqNo);
        }

        public override TransportPacket ReceivedFirstFrag(uint seqNo, uint numFrags, TransportPacket fragment)
        {
            if (!sw.Seen(seqNo)) { return null; }
            return base.ReceivedFirstFrag(seqNo, numFrags, fragment);
        }

        public override TransportPacket ReceivedFrag(uint seqNo, uint fragNo, TransportPacket fragment)
        {
            if (!sw.Seen(seqNo)) { return null; }
            return base.ReceivedFrag(seqNo, fragNo, fragment);
        }
    }

    /// <summary>
    /// Represent a message being progressively reassembled from a set of packet
    /// fragments.  Because the fragments may come out of order, we support assembling
    /// a message when the number of fragments is unknown; this is less optimal, however.
    /// If a provided fragment is referenced, then we must call <see cref="TransportPacket.Retain"/>.
    /// Similarly on <see cref="Assemble"/>, we must <see cref="TransportPacket.Dispose"/>.
    /// </summary>
    internal class FragmentedMessage
    {
        protected uint numberFragments;
        private TransportPacket[] fragments;
        private uint seen;

        public FragmentedMessage(uint numFrags)
        {
            numberFragments = numFrags;
            if (numFrags == 0) { numFrags = 100; }
            fragments = new TransportPacket[numFrags];
            seen = 0;
        }

        /// <summary>
        /// Record the fragment at the position.  If recorded, then
        /// must retain the fragment.
        /// </summary>
        /// <param name="fragNo"></param>
        /// <param name="frag"></param>
        /// <returns>true if the fragment hadn't been previously seen</returns>
        public bool RecordFragment(uint fragNo, TransportPacket frag)
        {
            ResizeIfNecessary(fragNo);
            if (fragments[fragNo] != null) { return false; }
            fragments[fragNo] = frag;
            frag.Retain();
            seen++;
            return true;
        }

        private void ResizeIfNecessary(uint fragNo)
        {
            if (numberFragments != 0 || fragments.Length > fragNo) { return; }
            Array.Resize(ref fragments, (int)fragNo + 10);
        }

        public bool Finished { get { return numberFragments > 0 && seen == numberFragments; } }

        public TransportPacket Assemble()
        {
            Debug.Assert(Finished);
            TransportPacket packet = new TransportPacket();
            foreach (TransportPacket frag in fragments)
            {
                packet.Add(frag, 0, frag.Length);
                frag.Dispose();
            }
            return packet;
        }

        public void SetNumberFragments(uint frags)
        {
            Debug.Assert(numberFragments == 0);
            numberFragments = frags;
            Array.Resize(ref fragments, (int)frags);
        }
    }
}