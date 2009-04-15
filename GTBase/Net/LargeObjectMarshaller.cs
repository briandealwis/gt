using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Common.Logging;
using GT.Utils;

namespace GT.Net
{
    /// <summary>
    /// A marshaller that wraps another marshaller to break down large messages 
    /// into manageable chunks for the provided transport.  Each manageable chunk
    /// is called a fragment.  A message may be discarded if the underlying transport
    /// is sequenced and a fragment is received out of order or an intermediate fragment
    /// has been dropped), or if the message has passed out of the sequence window.
    /// </summary>
    /// <remarks>
    /// The LOM operates on the results from a different marshaller.
    /// This sub-marshaller is used to transform objects, etc. to bytes.
    /// The LOM uses the high bit of the MessageType byte to encode whether 
    /// a message has been fragmented.
    /// 
    /// The LOM uses the <see cref="LightweightDotNetSerializingMarshaller.LwdnContainerDescriptor11"/>
    /// LWDNv1.1 message container format</see>, regardless of the primitive message 
    /// container format used by the sub-marshaller.  When used with a LWDNv1.1 sub-marshaller,
    /// the LOM is able to optimize packet layout by removing the duplicate header.
    /// 
    /// Each message is broken down as follows:
    /// <list>
    /// <item> if the message fits into a transport packet, then the message 
    ///     is returned as-is:
    ///     <pre>[byte:message-type] [byte:channel] [uint32:packet-size] 
    ///         [bytes:original packet]</pre>
    /// </item>
    /// <item> if the message is the first fragment, then the high-bit is
    ///     set on the message-type; the number of fragments is encoded using
    ///     the adaptive <see cref="ByteUtils.EncodeLength(int)"/> format.
    ///     <pre>[byte:message-type'] [byte:channel] [uint32:packet-size] 
    ///         [byte:seqno] [bytes:encoded-#-fragments] [bytes:frag]</pre>
    /// </item>
    /// <item> for all subsequent fragments; seqno' = seqno | 128;
    ///     the number of fragments is encoded using the adaptive 
    ///     <see cref="ByteUtils.EncodeLength(int)"/> format.
    ///     <pre>[byte:message-type'] [byte:channel] [uint32:packet-size] 
    ///         [byte:seqno'] [bytes:encoded-fragment-#] [bytes:frag]</pre>
    /// </item>
    ///  </list>
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
        private readonly bool subMarshallerIsLwdn11;

        /// <summary>
        /// The maximum number of messages maintained; messages that are not
        /// completed within this window are discarded.
        /// </summary>
        public uint WindowSize { get { return windowSize; } }
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
                if (value < 2 || value > MaxWindowSize)
                {
                    throw new ArgumentException(String.Format(
                        "Invalid window size: must be on [2,{0}]", MaxWindowSize));
                }
                _defaultWindowSize = value;
            }
        }

        // SeqNo must be expressible in a byte.  Top bit used to distinguish 
        // between first fragment in a sequence and subsequent fragments.
        // This leaves 7 bits for the sequence number; need at least 1 bit for
        // outside of the window.
        public const uint SeqNoCapacity = 128;
        public const uint MaxWindowSize = SeqNoCapacity - 1;

        /// <summary>
        /// The sequence number for the next outgoing message on a particular transport
        /// </summary>
        private WeakKeyDictionary<ITransportDeliveryCharacteristics, uint> outgoingSeqNo =
            new WeakKeyDictionary<ITransportDeliveryCharacteristics, uint>();

        /// <summary>
        /// Bookkeeping data for in-progress messages received.
        /// </summary>
        private WeakKeyDictionary<ITransportDeliveryCharacteristics, Sequences> accumulatedReceived =
            new WeakKeyDictionary<ITransportDeliveryCharacteristics, Sequences>();

        protected ILog log;

        public LargeObjectMarshaller(IMarshaller submarshaller)
            : this(submarshaller, DefaultWindowSize) {}

        public LargeObjectMarshaller(IMarshaller submarshaller, uint windowSize)
        {
            log = LogManager.GetLogger(GetType());

            this.subMarshaller = submarshaller;
            subMarshallerIsLwdn11 = submarshaller.Descriptors.Length > 0 &&
                LWDNv11.Descriptor.Equals(submarshaller.Descriptors[0]);

            InvalidStateException.Assert(windowSize >= 2 && windowSize <= MaxWindowSize, 
                String.Format("Invalid window size: must be on [2,{0}]",  MaxWindowSize), this);
            this.windowSize = windowSize;
        }

        public string[] Descriptors
        {
            get
            {
                List<string> descriptors = new List<string>(1 + subMarshaller.Descriptors.Length);
                // The LOM packages its message content using the 
                // LightweightDotNet Message Container Format v1.1
                descriptors.Add(LWDNv11.Descriptor);
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

        public IMarshalledResult Marshal(int senderIdentity, Message message, ITransportDeliveryCharacteristics tdc)
        {
            IMarshalledResult submr = subMarshaller.Marshal(senderIdentity, message, tdc);
            // Hmm, maybe this would be better as a generator, just in case the
            // sub-marshaller returns an infinite message.
            MarshalledResult mr = new MarshalledResult();
            while (submr.HasPackets)
            {
                TransportPacket packet = submr.RemovePacket();
                // Need to account for LWDNv1.1 header for non-LWDNv1.1 marshallers
                uint contentLength = (uint)packet.Length -
                    (subMarshallerIsLwdn11 ? LWDNv11.HeaderSize : 0);
                // If this packet fits within the normal transport packet length
                // then we don't have to fragment it.
                if (LWDNv11.HeaderSize + contentLength < tdc.MaximumPacketSize)
                {
                    /// Message fits within the transport packet length, so sent unmodified
                    ///     <pre>[byte:message-type] [byte:channel] [uint32:packet-size] 
                    ///         [bytes:content]</pre>
                    if (!subMarshallerIsLwdn11) 
                    {
                        // need to prefix the LWDNv1.1 header
                        packet.Prepend(LWDNv11.EncodeHeader(message.MessageType,
                            message.Channel, (uint)packet.Length));
                    }
                    mr.AddPacket(packet);
                }
                else
                {
                    FragmentMessage(message, tdc, packet, mr);
                }
            }
            submr.Dispose();
            return mr;
        }

        private void FragmentMessage(Message message, ITransportDeliveryCharacteristics tdc, 
            TransportPacket packet, MarshalledResult mr)
        {
            /// <item> if the message is the first fragment, then the high-bit is
            ///     set on the message-type; the number of fragments is encoded using
            ///     the adaptive <see cref="ByteUtils.EncodeLength(int)"/> format.
            ///     <pre>[byte:message-type'] [byte:channel] [uint32:packet-size] 
            ///         [byte:seqno] [bytes:encoded-#-fragments] [bytes:frag]</pre>
            /// </item>
            /// <item> for all subsequent fragments; seqno' = seqno | 128;
            ///     the number of fragments is encoded using the adaptive 
            ///     <see cref="ByteUtils.EncodeLength(int)"/> format.
            ///     <pre>[byte:message-type'] [byte:channel] [uint32:packet-size] 
            ///         [byte:seqno'] [bytes:encoded-fragment-#] [bytes:frag]</pre>

            // Although we use an adaptive scheme for encoding the number,
            // we assume a maximum of 4 bytes for encoding # frags
            // for a total of 4 extra bytes bytes; we determine the frag
            // size from the message size - MaxHeaderSize
            const uint maxFragHeaderSize = 1 /*seqno*/ + 4;
            const uint maxHeaderSize = LWDNv11.HeaderSize + maxFragHeaderSize;
            uint maxPacketSize = Math.Max(maxHeaderSize, tdc.MaximumPacketSize - maxHeaderSize);
            // round up the number of possible fragments
            uint numFragments = (uint)(packet.Length + maxPacketSize - 1) / maxPacketSize;
            Debug.Assert(numFragments > 1);
            uint seqNo = AllocateOutgoingSeqNo(tdc);
            for (uint fragNo = 0; fragNo < numFragments; fragNo++)
            {
                TransportPacket newPacket = new TransportPacket();
                Stream s = newPacket.AsWriteStream();
                uint fragSize = (uint)Math.Min(maxPacketSize,
                    packet.Length - (fragNo * maxPacketSize));
                if (fragNo == 0)
                {
                    s.WriteByte((byte)seqNo);
                    ByteUtils.EncodeLength(numFragments, s);
                }
                else
                {
                    s.WriteByte((byte)(seqNo | 128));
                    ByteUtils.EncodeLength(fragNo, s);
                }
                newPacket.Prepend(LWDNv11.EncodeHeader((MessageType)((byte)message.MessageType | 128),
                    message.Channel, (uint)(fragSize + s.Length)));
                newPacket.Add(packet, (int)(fragNo * maxPacketSize), (int)fragSize);
                mr.AddPacket(newPacket);
            }
            packet.Dispose();
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
                outgoingSeqNo[tdc] = (seqNo + 1) % SeqNoCapacity;
                return seqNo;
            }
        }

        public void Unmarshal(TransportPacket input, ITransportDeliveryCharacteristics tdc, EventHandler<MessageEventArgs> messageAvailable)
        {
            /// <item>Message fits within the transport packet length, so sent unmodified
            ///     <pre>[byte:message-type] [byte:channel] [uint32:packet-size] 
            ///         [bytes:content]</pre>
            /// </item>
            /// <item> if the message is the first fragment, then the high-bit is
            ///     set on the message-type; the number of fragments is encoded using
            ///     the adaptive <see cref="ByteUtils.EncodeLength(int)"/> format.
            ///     <pre>[byte:message-type'] [byte:channel] [uint32:packet-size] 
            ///         [byte:seqno] [bytes:encoded-#-fragments] [bytes:frag]</pre>
            /// </item>
            /// <item> for all subsequent fragments; seqno' = seqno | 128;
            ///     the number of fragments is encoded using the adaptive 
            ///     <see cref="ByteUtils.EncodeLength(int)"/> format.
            ///     <pre>[byte:message-type'] [byte:channel] [uint32:packet-size] 
            ///         [byte:seqno'] [bytes:encoded-fragment-#] [bytes:frag]</pre>

            if (subMarshallerIsLwdn11 && (input.ByteAt(0) & 128) == 0)
            {
                subMarshaller.Unmarshal(input, tdc, messageAvailable);
                return;
            }

            MessageType type;
            byte channel;
            uint contentLength;

            Stream s = input.AsReadStream();
            LWDNv11.DecodeHeader(out type, out channel, out contentLength, s);
            byte seqNo = (byte)s.ReadByte();
            TransportPacket subPacket;
            if ((seqNo & 128) == 0)
            {
                // This starts a new message
                uint numFrags = (uint)ByteUtils.DecodeLength(s);
                subPacket = UnmarshalFirstFragment(seqNo, numFrags,
                    input.SplitOut((int)(s.Length - s.Position)), tdc);
            }
            else 
            {
                // This is a message in progress
                seqNo = (byte)(seqNo & ~128);
                uint fragNo = (uint)ByteUtils.DecodeLength(s);
                subPacket = UnmarshalFragInProgress(seqNo, fragNo,
                    input.SplitOut((int)(s.Length - s.Position)), tdc);
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
                accumulatedReceived[tdc] = sequences = Sequences.ForTransport(tdc, windowSize, SeqNoCapacity);
            }
            return sequences.ReceivedFirstFrag(seqNo, numFrags, frag);
        }


        private TransportPacket UnmarshalFragInProgress(byte seqNo, uint fragNo, TransportPacket frag,
            ITransportDeliveryCharacteristics tdc)
        {
            Sequences sequences;
            if (!accumulatedReceived.TryGetValue(tdc, out sequences))
            {
                accumulatedReceived[tdc] = sequences = Sequences.ForTransport(tdc, windowSize, SeqNoCapacity);
            }
            return sequences.ReceivedFrag(seqNo, fragNo, frag);
        }

        public void Dispose()
        {
            subMarshaller.Dispose();
            foreach (Sequences seq in accumulatedReceived.Values)
            {
                seq.Dispose();
            }
            accumulatedReceived.Clear();
        }
    }

    abstract class Sequences : IDisposable
    {
        internal static Sequences ForTransport(ITransportDeliveryCharacteristics tdc, 
            uint seqWindowSize, uint seqCapacity)
        {
            if(tdc.Reliability == Reliability.Reliable)
            {
                return new ReliableSequences(tdc); // seqWindowSize, seqCapacity 
            }
            return new DiscardingSequences(tdc, seqWindowSize, seqCapacity);
        }

        protected ITransportDeliveryCharacteristics tdc;

        protected Sequences(ITransportDeliveryCharacteristics tdc)
        {
            this.tdc = tdc;
        }

        public abstract TransportPacket ReceivedFirstFrag(uint seqNo, uint numFrags, TransportPacket fragment);
        public abstract TransportPacket ReceivedFrag(uint seqNo, uint fragNo, TransportPacket fragment);
        public abstract void Dispose();
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
                pendingMessages[seqNo] = inProgress = NewFragmentedMessage(numFrags);
            }
            else
            {
                inProgress.SetNumberFragments(numFrags);
            }
            inProgress.RecordFragment(0, fragment);  // FragmentedMessage will Retain()
            if (!inProgress.Finished) { return null; }
            pendingMessages.Remove(seqNo);
            return inProgress.Assemble();   // properly disposes too
        }

        public override TransportPacket ReceivedFrag(uint seqNo, uint fragNo, TransportPacket fragment)
        {
            FragmentedMessage inProgress;
            if (!pendingMessages.TryGetValue(seqNo, out inProgress))
            {
                pendingMessages[seqNo] = inProgress = NewFragmentedMessage(0);
            }
            inProgress.RecordFragment(fragNo, fragment);  // FragmentedMessage should Retain()
            if (!inProgress.Finished) { return null; }
            pendingMessages.Remove(seqNo);
            return inProgress.Assemble();   // properly disposes too
        }

        public override void Dispose()
        {
            foreach (FragmentedMessage fm in pendingMessages.Values)
            {
                fm.Dispose();
            }
            pendingMessages.Clear();
        }

        protected virtual FragmentedMessage NewFragmentedMessage(uint numFrags)
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
            sw.FrameExpired += _ExpireFrame;
        }

        private void _ExpireFrame(uint seqNo)
        {
            FragmentedMessage fm;
            if (pendingMessages.TryGetValue(seqNo, out fm)) { fm.Dispose(); }
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
            fragments = null;
            return packet;
        }

        public void Dispose()
        {
            if(fragments != null)
            {
                foreach(TransportPacket frag in fragments)
                {
                    if (frag != null) { frag.Dispose(); }
                }
            }
            fragments = null;
        }

        public void SetNumberFragments(uint frags)
        {
            Debug.Assert(numberFragments == 0);
            numberFragments = frags;
            Array.Resize(ref fragments, (int)frags);
        }
    }
}