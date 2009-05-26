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

using System.Collections.Generic;
using GT.Net;
using NUnit.Framework;

namespace GT.UnitTests
{
    class MockConnexion : IConnexion
    {
        public event ErrorEventNotication ErrorEvents;
        public event MessageHandler MessageReceived;
        public event MessageHandler MessageSent;
        public event TransportLifecyleNotification TransportAdded;
        public event TransportLifecyleNotification TransportRemoved;
        public event PingingNotification PingRequested;
        public event PingedNotification PingReplied;


        public bool Active { get; set; }
        public int Identity { get; set; }
        public float Delay { get; set; }

        public IList<ITransport> Transports { get; protected set; }
        public IPacketScheduler Scheduler { get; set; }
        public IMarshaller Marshaller { get; set; }

        public IList<TransportPacket> SentPackets { get; protected set; }

        public MockConnexion()
        {
            Active = false;
            Identity = 0;
            SentPackets = new List<TransportPacket>();
            Marshaller = new DotNetSerializingMarshaller();
            Transports = new List<ITransport>();
        }

        public void Update()
        {
            // do nothing
        }

        public void Ping()
        {
        }

        public void Dispose()
        {
            if (Scheduler != null) { Scheduler.Dispose(); }
            Scheduler = null;
        }
        
        public void ShutDown()
        {
            Active = false;
        }

        public void Send(Message msg, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Scheduler.Schedule(msg, mdr, cdr);
        }

        public void Send(IList<Message> msgs, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            foreach (Message m in msgs)
            {
                Scheduler.Schedule(m, mdr, cdr);
            }
        }

        public void Send(byte[] buffer, byte channelId, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new BinaryMessage(channelId, buffer), mdr, cdr);
        }

        public void Send(string s, byte channelId, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new StringMessage(channelId, s), mdr, cdr);
        }

        public void Send(object o, byte channelId, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new ObjectMessage(channelId, o), mdr, cdr);
        }

        public void Flush()
        {
            Scheduler.Flush();
        }

        public void FlushChannel(byte channelId)
        {
            Scheduler.FlushChannelMessages(channelId);
        }

        public IMarshalledResult Marshal(Message m, ITransportDeliveryCharacteristics tdc)
        {
            return Marshaller.Marshal(0, m, tdc);
        }

        public void SendPacket(ITransport transport, TransportPacket packet)
        {
            try
            {
                packet.Retain();
                SentPackets.Add(packet);
                transport.SendPacket(packet);
            }
            catch (TransportError e) { }
        }

        public ITransport FindTransport(MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            ITransport t = null;
            if (mdr != null) { t = mdr.SelectTransport(Transports); }
            if (t != null) { return t; }
            if (t == null && cdr != null) { t = cdr.SelectTransport(Transports); }
            if (t != null) { return t; }
            throw new NoMatchingTransport(this, mdr, cdr);
        }

        public void ClearSentPackets()
        {
            foreach (TransportPacket tp in SentPackets)
            {
                tp.Dispose();
            }
            SentPackets.Clear();
        }
    }


    [TestFixture]
    public class ZMImmediatePacketSchedulerTests
    {
        [Test]
        public void TestOrder()
        {
            NullTransport t = new NullTransport();
            MockConnexion cnx = new MockConnexion();
            cnx.Scheduler = new ImmediatePacketScheduler(cnx);
            cnx.Transports.Add(t);

            uint lastBytesSent = 0;
            for (int i = 0; i < 6; i++)
            {
                cnx.Scheduler.Schedule(new ObjectMessage(0, i), MessageDeliveryRequirements.LeastStrict, null);
                Assert.IsTrue(lastBytesSent < t.BytesSent);
                lastBytesSent = t.BytesSent;
            }
            cnx.Flush();
            Assert.IsTrue(t.BytesSent == lastBytesSent, "Flush() should have made no difference to bytes sent");

            // check order
            int msgNo = 0;
            foreach (TransportPacket tp in cnx.SentPackets)
            {
                while (tp.Length > 0)
                {
                    cnx.Marshaller.Unmarshal(tp, t, delegate(object sender, MessageEventArgs mea) {
                        Assert.AreEqual(msgNo, ((ObjectMessage)mea.Message).Object,
                            "message was sent out of order!");
                    });
                    msgNo++;
                }
            }
        }

    }

    [TestFixture]
    public class ZMRoundRobinPacketSchedulerTests
    {

        [Test]
        public void TestAggregationSendsNothingUntilFlush()
        {
            NullTransport t = new NullTransport();
            MockConnexion cnx = new MockConnexion();
            cnx.Scheduler = new RoundRobinPacketScheduler(cnx);
            cnx.Transports.Add(t);

            for (int i = 0; i < 6; i++)
            {
                cnx.Scheduler.Schedule(new ObjectMessage(0, i), MessageDeliveryRequirements.LeastStrict, null);
                Assert.AreEqual(0, t.BytesSent);
                Assert.AreEqual(0, cnx.SentPackets.Count);
            }
            cnx.Flush();
            Assert.IsTrue(t.BytesSent > 0);
            Assert.IsTrue(cnx.SentPackets.Count > 0);
        }

        [Test]
        public void TestSendsOnSameChannelInOrder()
        {
            NullTransport t = new NullTransport();
            MockConnexion cnx = new MockConnexion();
            cnx.Scheduler = new RoundRobinPacketScheduler(cnx);
            cnx.Transports.Add(t);

            for (int i = 0; i < 6; i++)
            {
                cnx.Scheduler.Schedule(new ObjectMessage(0, i), MessageDeliveryRequirements.LeastStrict, null);
                Assert.AreEqual(0, t.BytesSent);
                Assert.AreEqual(0, cnx.SentPackets.Count);
            }
            cnx.Flush();
            Assert.IsTrue(t.BytesSent > 0);
            Assert.IsTrue(cnx.SentPackets.Count > 0);

            int msgNo = 0;
            foreach (TransportPacket tp in cnx.SentPackets)
            {
                while (tp.Length > 0)
                {
                    cnx.Marshaller.Unmarshal(tp, t, delegate(object sender, MessageEventArgs mea)
                    {
                        Assert.AreEqual(msgNo, ((ObjectMessage)mea.Message).Object);
                    });
                    msgNo++;
                }
            }
        }

        [Test]
        public void TestImmediateComesFirst()
        {
            NullTransport t = new NullTransport();
            MockConnexion cnx = new MockConnexion();
            cnx.Scheduler = new RoundRobinPacketScheduler(cnx);
            cnx.Transports.Add(t);

            /// Send a bunch of messages that should be aggregated.
            /// Then send a message that should go immediately (i.e., sent
            /// before any of the aggregated messages).
            for (int i = 1; i < 6; i++)
            {
                cnx.Scheduler.Schedule(new ObjectMessage((byte)(i % 2), i), MessageDeliveryRequirements.LeastStrict, null);
                Assert.AreEqual(0, t.BytesSent);
                Assert.AreEqual(0, cnx.SentPackets.Count);
            }
            cnx.Scheduler.Schedule(new StringMessage(0, "foo"), 
                new MessageDeliveryRequirements(Reliability.Unreliable,
                    MessageAggregation.Immediate, Ordering.Unordered), null);
            Assert.IsTrue(t.BytesSent > 0);
            Assert.IsTrue(cnx.SentPackets.Count > 0);

            int msgNo = 0;
            foreach (TransportPacket tp in cnx.SentPackets)
            {
                if (msgNo == 0)
                {
                    // Firt message should be the immediate message
                    cnx.Marshaller.Unmarshal(tp, t, delegate(object sender, MessageEventArgs mea)
                    {
                        Assert.IsInstanceOfType(typeof(StringMessage), mea.Message);
                        Assert.AreEqual("foo", ((StringMessage)mea.Message).Text);
                    });
                    msgNo++;
                }
                while(tp.Length > 0)
                {
                    cnx.Marshaller.Unmarshal(tp, t,
                        delegate(object sender, MessageEventArgs mea) {
                            Assert.AreEqual(msgNo, ((ObjectMessage)mea.Message).Object);
                            Assert.AreEqual(msgNo % 2, ((ObjectMessage)mea.Message).ChannelId);
                        });
                    msgNo++;
                }
            }
        }


        [Test]
        public void TestSendFragmentedPacket()
        {
            NullTransport t = new NullTransport();
            t.MaximumPacketSize = 100;
            t.Reliability = Reliability.Reliable;
            t.Ordering = Ordering.Ordered;
            MockConnexion cnx = new MockConnexion();
            cnx.Marshaller = new LargeObjectMarshaller(new DotNetSerializingMarshaller());
            cnx.Scheduler = new RoundRobinPacketScheduler(cnx);
            cnx.Transports.Add(t);

            cnx.ErrorEvents += delegate(ErrorSummary es) { Assert.Fail(es.ToString()); };

            byte[] sentData = new byte[500];
            for (int i = 0; i < 6; i++)
            {
                uint bytesSent = t.BytesSent;
                cnx.Scheduler.Schedule(new BinaryMessage(0, sentData), MessageDeliveryRequirements.MostStrict, null);
                Assert.IsTrue(cnx.SentPackets.Count > 0);
                Assert.IsTrue(t.BytesSent - bytesSent > 500); // should be 500 + message headers
            }
            int lastSentPacketsCount = cnx.SentPackets.Count;
            cnx.Flush();
            Assert.IsTrue(cnx.SentPackets.Count == lastSentPacketsCount, "there should not be any data remaining");

            int msgNo = 0;
            foreach (TransportPacket tp in cnx.SentPackets)
            {
                while (tp.Length > 0)
                {
                    cnx.Marshaller.Unmarshal(tp, t, delegate(object sender, MessageEventArgs mea)
                    {
                        Assert.IsInstanceOfType(typeof(BinaryMessage), mea.Message);
                        Assert.AreEqual(sentData, ((BinaryMessage)mea.Message).Bytes);
                        msgNo++;
                    });
                }
            }
            Assert.AreEqual(6, msgNo);
        }


    }
}
