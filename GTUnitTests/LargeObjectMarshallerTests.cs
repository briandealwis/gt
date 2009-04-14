using System;
using System.Collections.Generic;
using GT.Net;
using GT.Net.Local;
using NUnit.Framework;

namespace GT.UnitTests
{
    [TestFixture]
    public class LargeObjectMarshallerTests
    {
        IMarshaller dnmarshaller = new DotNetSerializingMarshaller();
        [Test]
        public void TestSingleFragments()
        {
            LargeObjectMarshaller m = new LargeObjectMarshaller(dnmarshaller, 16);
            uint discarded = 0;
            //m.MessageDiscarded += delegate { discarded++; };

            Assert.AreEqual(0, discarded, "should have not discarded");

            MarshalledResult mr = (MarshalledResult)m.Marshal(0, new BinaryMessage(0, new byte[0]), 
                new DummyTransportChar(6000));
            Assert.AreEqual(1, mr.Packets.Count);
            Assert.AreEqual(0, discarded, "should have not discarded");
            
            bool sawResult = false;
            m.Unmarshal(mr.Packets[0], new DummyTransportChar(6000), 
                delegate(object sender, MessageEventArgs e) {
                    sawResult = true;
                    Assert.AreEqual(0, e.Message.Channel);
                    Assert.AreEqual(MessageType.Binary, e.Message.MessageType);
                    Assert.AreEqual(0, ((BinaryMessage)e.Message).Bytes.Length);
                });
            Assert.IsTrue(sawResult);
            Assert.AreEqual(0, discarded, "should have not discarded");
        }

        [Test]
        public void TestMultiFragments()
        {
            LargeObjectMarshaller m = new LargeObjectMarshaller(dnmarshaller, 16);
            ITransportDeliveryCharacteristics tdc = new DummyTransportChar(600);
            byte[] sourceData = new byte[5900];
            for (int i = 0; i < sourceData.Length; i++) { sourceData[i] = (byte)(i % 256); }

            uint discarded = 0;
            //m.MessageDiscarded += delegate { discarded++; };

            Assert.AreEqual(0, discarded, "should have not discarded");

            MarshalledResult mr = (MarshalledResult)m.Marshal(0, new BinaryMessage(0, sourceData),
                tdc);
            Assert.AreEqual(11, mr.Packets.Count);
            // The first packet should not have the high-bit of the seqno set
            Assert.AreEqual(0, mr.Packets[0].ByteAt((int)LWDNv11.HeaderSize));   // seqno
            for (int i = 1; i < 11; i++)
            {
                // The remaining packets should have the high-bit of the seqno set
                Assert.AreEqual(128 | 0, mr.Packets[i].ByteAt((int)LWDNv11.HeaderSize));
            }
            Assert.AreEqual(0, discarded, "should have not discarded");

            bool sawResult = false;
            while (mr.HasPackets)
            {
                TransportPacket packet = mr.RemovePacket();
                m.Unmarshal(packet, tdc,
                    delegate(object sender, MessageEventArgs e) {
                        sawResult = true;
                        Assert.AreEqual(0, e.Message.Channel);
                        Assert.AreEqual(MessageType.Binary, e.Message.MessageType);
                        Assert.AreEqual(sourceData, ((BinaryMessage)e.Message).Bytes);
                    });
                packet.Dispose();
            }
            Assert.IsTrue(sawResult);
            Assert.AreEqual(0, discarded, "should have not discarded");
        }

        [Test]
        public void TestMultiRandomizedFragments()
        {
            LargeObjectMarshaller m = new LargeObjectMarshaller(dnmarshaller, 16);
            ITransportDeliveryCharacteristics tdc = new DummyTransportChar(600);
            byte[] sourceData = new byte[5900];
            for (int i = 0; i < sourceData.Length; i++) { sourceData[i] = (byte)(i % 256); }

            // uint discarded = 0;
            //m.MessageDiscarded += delegate { discarded++; };

            //Assert.AreEqual(0, discarded, "should have not discarded");

            MarshalledResult mr = (MarshalledResult)m.Marshal(0, new BinaryMessage(0, sourceData),
                tdc);
            Assert.AreEqual(11, mr.Packets.Count);
            // The first packet should not have the high-bit of the seqno set
            Assert.AreEqual(0, mr.Packets[0].ByteAt((int)LWDNv11.HeaderSize));     // seqno
            // The remaining packets should have the high-bit of the seqno set
            for (int i = 1; i < 11; i++)
            {
                Assert.AreEqual(128 | 0, mr.Packets[i].ByteAt((int)LWDNv11.HeaderSize));
            }
            //Assert.AreEqual(0, discarded, "should have not discarded");

            Randomize(mr.Packets);
            bool sawResult = false;
            while (mr.HasPackets)
            {
                TransportPacket packet = mr.RemovePacket();
                m.Unmarshal(packet, tdc,
                    delegate(object sender, MessageEventArgs e)
                    {
                        sawResult = true;
                        Assert.AreEqual(0, e.Message.Channel);
                        Assert.AreEqual(MessageType.Binary, e.Message.MessageType);
                        Assert.AreEqual(sourceData, ((BinaryMessage)e.Message).Bytes);
                    });
                packet.Dispose();
            }
            Assert.IsTrue(sawResult);
            //Assert.AreEqual(0, discarded, "should have not discarded");
        }

        [Test]
        public void TestDroppedPackets()
        {
            TransportPacket.TestingDiscardPools();
            ITransport[] transports = new[] {
                new NullTransport(Reliability.Reliable, Ordering.Ordered, 10, 100),
                new NullTransport(Reliability.Reliable, Ordering.Unordered, 10, 100),
                new NullTransport(Reliability.Unreliable, Ordering.Ordered, 10, 100),
                new NullTransport(Reliability.Unreliable, Ordering.Unordered, 10, 100),
                new NullTransport(Reliability.Unreliable, Ordering.Sequenced, 10, 100)
            };
            byte[] sourceData = new byte[5900];
            for (int i = 0; i < sourceData.Length; i++) { sourceData[i] = (byte)(i % 256); }

            LargeObjectMarshaller m = new LargeObjectMarshaller(dnmarshaller, 16);

            Random r = new Random();
            // Ensure that the sequence numbers wrap at least twice
            for (int iteration = 0; iteration < transports.Length * 
                2 * LargeObjectMarshaller.SeqNoCapacity + 10; iteration++)
            {
                ITransport t = transports[iteration % transports.Length];
                bool shouldBeSuccessful = t.Reliability == Reliability.Reliable
                    || (iteration % transports.Length) == 0;
                int expectedSeqNo = (int)((iteration / transports.Length) 
                    % LargeObjectMarshaller.SeqNoCapacity);
                MarshalledResult mr = (MarshalledResult)m.Marshal(0, 
                    new BinaryMessage(0, sourceData), t);
                Assert.IsTrue(mr.Packets.Count > 1);

                for (int i = 0; i < mr.Packets.Count; i++)
                {
                    mr.Packets[i].BytesAt(0, (int)LWDNv11.HeaderSize,
                        delegate(byte[] buffer, int offset) {
                            MessageType mt;
                            byte channel;
                            uint len;
                            LWDNv11.DecodeHeader(out mt, out channel, out len, buffer, offset);
                            Assert.IsTrue(((byte)mt & 128) != 0, 
                                "fragmented messages should have high-bit set");
                            Assert.AreEqual(MessageType.Binary, (MessageType)((byte)mt & 127));
                            Assert.AreEqual(0, channel);
                            Assert.AreEqual(mr.Packets[i].Length - LWDNv11.HeaderSize, len);
                        });
                    
                    if (i == 0)
                    {
                        // The first packet should not have the high-bit of the seqno set
                        Assert.AreEqual(expectedSeqNo,
                            mr.Packets[0].ByteAt((int)LWDNv11.HeaderSize)); // seqno
                    }
                    else
                    {
                        // The remaining packets should have the high-bit of the seqno set
                        Assert.AreEqual(128 | expectedSeqNo,
                            mr.Packets[i].ByteAt((int)LWDNv11.HeaderSize));
                    }
                }

                if (t.Ordering == Ordering.Unordered || 
                    (t.Ordering == Ordering.Sequenced && !shouldBeSuccessful))
                {
                    Randomize(mr.Packets);
                }

                bool sawResult = false;
                bool droppedPacket = false;
                while (mr.HasPackets)
                {
                    TransportPacket packet = mr.RemovePacket();
                    if (!shouldBeSuccessful && r.Next(2) == 0)
                    {
                        droppedPacket = true;
                        packet.Dispose();
                        continue;
                    }
                    m.Unmarshal(packet, t,
                        delegate(object sender, MessageEventArgs e)
                        {
                            sawResult = true;
                            Assert.AreEqual(0, e.Message.Channel);
                            Assert.AreEqual(MessageType.Binary, e.Message.MessageType);
                            Assert.AreEqual(sourceData, ((BinaryMessage)e.Message).Bytes);
                        });
                    packet.Dispose();
                }
                if (droppedPacket) { Assert.IsFalse(sawResult); }
                Assert.IsTrue(shouldBeSuccessful == sawResult);
                mr.Dispose();
            }
            m.Dispose();
            AZPacketTests.CheckForUndisposedSegments();
        }

        #region Utility Functions
        private void Randomize<T>(IList<T> packets)
        {
            Random r = new Random();
            for(int i = 0; i < packets.Count - 1; i++)
            {
                int iother = r.Next(i, packets.Count);
                T tmp = packets[i];
                packets[i] = packets[iother];
                packets[iother] = tmp;
            }
        }
        #endregion
    }
    

    internal class DummyTransportChar : ITransportDeliveryCharacteristics {
        public Reliability Reliability { get; set; }
        public Ordering Ordering { get; set; }
        public float Delay { get; set; }
        public uint MaximumPacketSize { get; set; }

        public DummyTransportChar(uint maxPacketSize)
            : this(Reliability.Reliable, Ordering.Ordered, 20f, maxPacketSize) {}

        public DummyTransportChar(Reliability r, Ordering o, float delay, uint maxPacketSize)
        {
            Reliability = r;
            Ordering = o;
            Delay = delay;
            MaximumPacketSize = maxPacketSize;
        }

    }
}