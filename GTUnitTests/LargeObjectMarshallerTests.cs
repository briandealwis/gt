//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;
using System.Collections.Generic;
using GT.Net;
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
            CheckSmallMessage(new LargeObjectMarshaller(dnmarshaller, 16));
        }

        [Test]
        public void TestSingleFragmentsNonLWMCFv11()
        {
            CheckSmallMessage(new LargeObjectMarshaller(new NonLWMCFWrapper(dnmarshaller), 16));
        }

        protected void CheckSmallMessage(IMarshaller m)
        {
            MarshalledResult mr = (MarshalledResult)m.Marshal(0, new BinaryMessage(0, new byte[0]), 
                new DummyTransportChar(6000));
            Assert.AreEqual(1, mr.Packets.Count);
            
            bool sawResult = false;
            m.Unmarshal(mr.Packets[0], new DummyTransportChar(6000), 
                delegate(object sender, MessageEventArgs e) {
                    sawResult = true;
                    Assert.AreEqual(0, e.Message.ChannelId);
                    Assert.AreEqual(MessageType.Binary, e.Message.MessageType);
                    Assert.AreEqual(0, ((BinaryMessage)e.Message).Bytes.Length);
                });
            Assert.IsTrue(sawResult);
        }

        [Test]
        public void TestMultiFragments()
        {
            LargeObjectMarshaller m = new LargeObjectMarshaller(dnmarshaller, 16);
        }

        [Test]
        public void TestMultiFragmentsNonLWMCFv11()
        {
            LargeObjectMarshaller m = new LargeObjectMarshaller(new NonLWMCFWrapper(dnmarshaller), 16);
        }

        protected void CheckMultiFragments(IMarshaller m) 
        {
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
            Assert.AreEqual(0, mr.Packets[0].ByteAt((int)LWMCFv11.HeaderSize));   // seqno
            for (int i = 1; i < 11; i++)
            {
                // The remaining packets should have the high-bit of the seqno set
                Assert.AreEqual(128 | 0, mr.Packets[i].ByteAt((int)LWMCFv11.HeaderSize));
            }
            Assert.AreEqual(0, discarded, "should have not discarded");

            bool sawResult = false;
            while (mr.HasPackets)
            {
                TransportPacket packet = mr.RemovePacket();
                m.Unmarshal(packet, tdc,
                    delegate(object sender, MessageEventArgs e) {
                        sawResult = true;
                        Assert.AreEqual(0, e.Message.ChannelId);
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
            CheckMultiRandomizedFragments(new LargeObjectMarshaller(dnmarshaller, 16));
        }

        [Test]
        public void TestMultiRandomizedFragmentsNonLWMCFv11()
        {
            CheckMultiRandomizedFragments(new LargeObjectMarshaller(new NonLWMCFWrapper(dnmarshaller), 16));
        }

        protected void CheckMultiRandomizedFragments(IMarshaller m)
        {
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
            Assert.AreEqual(0, mr.Packets[0].ByteAt((int)LWMCFv11.HeaderSize));     // seqno
            // The remaining packets should have the high-bit of the seqno set
            for (int i = 1; i < 11; i++)
            {
                Assert.AreEqual(128 | 0, mr.Packets[i].ByteAt((int)LWMCFv11.HeaderSize));
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
                        Assert.AreEqual(0, e.Message.ChannelId);
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
#if DEBUG
            TransportPacket.TestingDiscardPools();
#endif
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
                    mr.Packets[i].BytesAt(0, (int)LWMCFv11.HeaderSize,
                        delegate(byte[] buffer, int offset) {
                            MessageType mt;
                            byte channelId;
                            uint len;
                            LWMCFv11.DecodeHeader(out mt, out channelId, out len, buffer, offset);
                            Assert.IsTrue(((byte)mt & 128) != 0, 
                                "fragmented messages should have high-bit set");
                            Assert.AreEqual(MessageType.Binary, (MessageType)((byte)mt & 127));
                            Assert.AreEqual(0, channelId);
                            Assert.AreEqual(mr.Packets[i].Length - LWMCFv11.HeaderSize, len);
                        });
                    
                    if (i == 0)
                    {
                        // The first packet should not have the high-bit of the seqno set
                        Assert.AreEqual(expectedSeqNo,
                            mr.Packets[0].ByteAt((int)LWMCFv11.HeaderSize)); // seqno
                    }
                    else
                    {
                        // The remaining packets should have the high-bit of the seqno set
                        Assert.AreEqual(128 | expectedSeqNo,
                            mr.Packets[i].ByteAt((int)LWMCFv11.HeaderSize));
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
                            Assert.AreEqual(0, e.Message.ChannelId);
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

        /// <summary>
        /// Ensure that the fragments produced by the LargeObjectMarshaller 
        /// are valid LWMCFv1.1 messages and can be relayed by the
        /// ClientRepeater, for example.
        /// </summary>
        [Test]
        public void TestLWMCFCompatibility() {
            BaseLWMCFMarshaller lwmcfm = new LightweightDotNetSerializingMarshaller();
            LargeObjectMarshaller lom = new LargeObjectMarshaller(new DotNetSerializingMarshaller());

            ITransportDeliveryCharacteristics tdc = new DummyTransportChar(600);
            byte[] sourceData = new byte[5900];
            for (int i = 0; i < sourceData.Length; i++) { sourceData[i] = (byte)(i % 256); }

            IMarshalledResult mr = lom.Marshal(0, new BinaryMessage(0, sourceData),
                tdc);
            Assert.IsTrue(((MarshalledResult)mr).Packets.Count > 1);

            IList<IMarshalledResult> lwmcfMarshalledResults = new List<IMarshalledResult>();
            while (mr.HasPackets)
            {
                Message lwmcfRawMessage = null;
                lwmcfm.Unmarshal(mr.RemovePacket(), tdc,
                    delegate(object sender, MessageEventArgs mea) {
                        lwmcfRawMessage = mea.Message;
                    });
                Assert.IsNotNull(lwmcfRawMessage);
                lwmcfMarshalledResults.Add(lwmcfm.Marshal(0, lwmcfRawMessage, tdc));
            }

            for (int i = 0; i < lwmcfMarshalledResults.Count; i++) {
                 mr = lwmcfMarshalledResults[i];
                Assert.IsTrue(mr.HasPackets);
                while (mr.HasPackets)
                {
                    lom.Unmarshal(mr.RemovePacket(), tdc, delegate(object sender, MessageEventArgs e) {
                        Assert.IsTrue(i == lwmcfMarshalledResults.Count - 1 && !mr.HasPackets);
                        Assert.IsTrue(e.Message is BinaryMessage);
                        Assert.AreEqual(sourceData, ((BinaryMessage)e.Message).Bytes);
                    });
                }
            }
            lom.Dispose();
            lwmcfm.Dispose();
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

    public class NonLWMCFWrapper : IMarshaller
    {
        IMarshaller submarshaller;
        string descriptor;

        public NonLWMCFWrapper(IMarshaller subm)
        {
            this.submarshaller = subm;
            if (subm.Descriptor.Equals(LWMCFv11.Descriptor) ||
                subm.Descriptor.StartsWith(LWMCFv11.DescriptorAsPrefix))
            {
                descriptor = "FOO:" + subm.Descriptor;
            }
            else
            {
                descriptor = subm.Descriptor;
            }
        }

        public void Dispose()
        {
            submarshaller.Dispose();
        }

        public string Descriptor
        {
            get { return descriptor; }
        }

        public IMarshalledResult Marshal(int senderIdentity, Message message, ITransportDeliveryCharacteristics tdc)
        {
            return submarshaller.Marshal(senderIdentity, message, tdc);
        }

        public void Unmarshal(TransportPacket input, ITransportDeliveryCharacteristics tdc, EventHandler<MessageEventArgs> messageAvailable)
        {
            submarshaller.Unmarshal(input, tdc, messageAvailable);
        }

        public bool IsCompatible(string marshallingDescriptor, ITransport remote)
        {
            return descriptor.Equals(marshallingDescriptor);
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
