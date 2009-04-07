using System.Collections.Generic;
using System.IO;
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
        public event PingedNotification PingReceived;


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

        public void Send(byte[] buffer, byte channel, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new BinaryMessage(channel, buffer), mdr, cdr);
        }

        public void Send(string s, byte channel, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new StringMessage(channel, s), mdr, cdr);
        }

        public void Send(object o, byte channel, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
        {
            Send(new ObjectMessage(channel, o), mdr, cdr);
        }

        public void Flush()
        {
            Scheduler.Flush();
        }

        public void FlushChannelMessages(byte channel)
        {
            Scheduler.FlushChannelMessages(channel);
        }

        public MarshalledResult Marshal(Message m, ITransportDeliveryCharacteristics tdc)
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
            }
            cnx.Flush();
            Assert.IsTrue(t.BytesSent > 0);
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
            }
            cnx.Flush();
            Assert.IsTrue(t.BytesSent > 0);

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
            }
            cnx.Scheduler.Schedule(new StringMessage(0, "foo"), 
                new MessageDeliveryRequirements(Reliability.Unreliable,
                    MessageAggregation.Immediate, Ordering.Unordered), null);
            Assert.IsTrue(t.BytesSent > 0);

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
                            Assert.AreEqual(msgNo % 2, ((ObjectMessage)mea.Message).Channel);
                        });
                    msgNo++;
                }
            }
        }

    }
}