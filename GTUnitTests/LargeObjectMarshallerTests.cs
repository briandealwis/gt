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
            Assert.AreEqual(255, mr.Packets[0].ByteAt(0));
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
            Assert.AreEqual(10, mr.Packets.Count);
            Assert.AreEqual(0, mr.Packets[0].ByteAt(0));
            for (int i = 1; i < 10; i++)
            {
                Assert.AreEqual(128 | 0, mr.Packets[i].ByteAt(0));
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

            uint discarded = 0;
            //m.MessageDiscarded += delegate { discarded++; };

            Assert.AreEqual(0, discarded, "should have not discarded");

            MarshalledResult mr = (MarshalledResult)m.Marshal(0, new BinaryMessage(0, sourceData),
                tdc);
            Assert.AreEqual(10, mr.Packets.Count);
            Assert.AreEqual(0, mr.Packets[0].ByteAt(0));
            for (int i = 1; i < 10; i++)
            {
                Assert.AreEqual(128 | 0, mr.Packets[i].ByteAt(0));
            }
            Assert.AreEqual(0, discarded, "should have not discarded");

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
            Assert.AreEqual(0, discarded, "should have not discarded");
        }

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