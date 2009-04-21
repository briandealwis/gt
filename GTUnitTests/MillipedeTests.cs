using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using GT.Net;
using GT.Millipede;

namespace GT.UnitTests
{
    [TestFixture]
    public class ZPMillipedeTests
    {
        IList<string> toBeDeleted = new List<string>();
        MillipedeRecorder recorder;

        [SetUp]
        protected void SetUp()
        {
            toBeDeleted.Clear();
            recorder = new MillipedeRecorder();
        }

        [TearDown]
        protected void TearDown()
        {
            MillipedeRecorder.Singleton.Dispose();
            foreach (string fn in toBeDeleted)
            {
                try { File.Delete(fn); }
                catch { /* ignore */ }
            }
        }


        [Test]
        public void BaseTestUniqueStableDescriptor() {
            string[] values = { typeof(String).FullName, "test", "bar", "test" };
            foreach (string value in values) {
                object d1 = recorder.GenerateDescriptor(value);
                recorder.Dispose();
                recorder = new MillipedeRecorder();
                object d2 = recorder.GenerateDescriptor(value);
                recorder.Dispose();
                Assert.AreEqual(d1,d2);
                recorder = new MillipedeRecorder();
            }

            Dictionary<object, object> descriptors = new Dictionary<object, object>();
            foreach (string value in values)
            {
                object d1 = recorder.GenerateDescriptor(value);
                Assert.IsFalse(descriptors.ContainsKey(d1));
                descriptors[d1] = d1;
            }
        }

        [Test]
        public void TestMillipedeExceptions()
        {
            TryMillipedeException(new CannotConnectException("test"));
            TryMillipedeException(new TransportError("this", "foo", "this"));
        }

        protected void TryMillipedeException(Exception exception)
        {
            string tempFileName = Path.GetTempFileName();
            toBeDeleted.Add(tempFileName);

            // Create the mock connector
            MockConnector mockConnector = new MockConnector();
            mockConnector.CreateTransport = delegate { throw exception; };
            mockConnector.Start();

            recorder = new MillipedeRecorder();
            recorder.StartRecording(tempFileName);
            Assert.AreEqual(MillipedeMode.Record, recorder.Mode);
            Assert.AreEqual(0, recorder.NumberEvents);
            MillipedeConnector connector =
                (MillipedeConnector)MillipedeConnector.Wrap(mockConnector, recorder);
            try
            {
                connector.Connect("localhost", "9999", new Dictionary<string, string>());
                Assert.Fail("should have thrown CannotConnectException");
            }
            catch(GTException) { /* ignored */ }
            Assert.AreEqual(1, recorder.NumberEvents);

            recorder.Dispose(); // don't want the disposes add to the list
            mockConnector.Dispose();
            recorder = new MillipedeRecorder();
            recorder.StartReplaying(tempFileName);
            Assert.AreEqual(0, recorder.NumberEvents);
            connector = (MillipedeConnector)MillipedeConnector.Wrap(mockConnector = new MockConnector(),
                recorder);
            try
            {
                connector.Connect("localhost", "9999", new Dictionary<string, string>());
                Assert.Fail("should have thrown " + exception.GetType());
            }
            catch (Exception e)
            {
                Assert.AreEqual(exception.GetType(), e.GetType());
            }
            Assert.AreEqual(1, recorder.NumberEvents);

            recorder.Dispose();
        }

        [Test]
        public void TestMillipedeConnector()
        {
            string tempFileName = Path.GetTempFileName();
            toBeDeleted.Add(tempFileName);

            // Create the mock connector and transport
            MockTransport mockTransport = new MockTransport("MOCK", new Dictionary<string, string>(),
                Reliability.Reliable, Ordering.Ordered, 1024);
            uint packetsSent = 0;
            mockTransport.PacketSentEvent += delegate { packetsSent++; };
            MockConnector mockConnector = new MockConnector();
            mockConnector.CreateTransport = delegate { return mockTransport; };
            mockConnector.Start();

            recorder.StartRecording(tempFileName);
            Assert.AreEqual(MillipedeMode.Record, recorder.Mode);
            Assert.AreEqual(0, recorder.NumberEvents);
            MillipedeConnector connector =
                (MillipedeConnector)MillipedeConnector.Wrap(mockConnector, recorder);
            ITransport transport = connector.Connect("localhost", "9999", new Dictionary<string, string>());
            Assert.IsInstanceOfType(typeof(MillipedeTransport), transport);
            Assert.AreEqual(1, recorder.NumberEvents);
            transport.SendPacket(new TransportPacket(new byte[10]));
            Assert.AreEqual(2, recorder.NumberEvents);
            Assert.AreEqual(1, packetsSent, "millipede didn't pass down packet-send in record");
            Thread.Sleep(50);  // give a good amount of time for the test below
            mockTransport.InjectReceivedPacket(new byte[5]);
            Assert.AreEqual(3, recorder.NumberEvents);

            recorder.Dispose(); // don't want the disposes add to the list
            mockConnector.Dispose();
            mockTransport.Dispose();


            recorder = new MillipedeRecorder();
            recorder.StartReplaying(tempFileName);
            Assert.AreEqual(0, recorder.NumberEvents);
            connector = (MillipedeConnector)MillipedeConnector.Wrap(mockConnector = new MockConnector(),
                recorder);
            transport = connector.Connect("localhost", "9999", new Dictionary<string, string>());
            transport.PacketReceivedEvent += (packet, t) =>
            {
                Assert.AreEqual(new byte[5], packet.ToArray());
                Assert.AreEqual(3, recorder.NumberEvents);
            };

            Assert.IsInstanceOfType(typeof(MillipedeTransport), transport);
            Assert.AreEqual(1, recorder.NumberEvents);
            transport.SendPacket(new TransportPacket(new byte[10]));
            Assert.AreEqual(2, recorder.NumberEvents);
            transport.Update();
            for (int i = 0; i < 5 && recorder.NumberEvents == 2; i++)
            {
                Thread.Sleep(100);
                transport.Update();
            }
            Assert.AreEqual(3, recorder.NumberEvents);  // should have received the packet too
        }

        [Test]
        public void TestMillipedeAcceptor()
        {
            string tempFileName = Path.GetTempFileName();
            toBeDeleted.Add(tempFileName);

            // Create the mock connector and transport
            MockTransport mockTransport = new MockTransport("MOCK", new Dictionary<string, string>(),
                Reliability.Reliable, Ordering.Ordered, 1024);
            uint packetsSent = 0;
            mockTransport.PacketSentEvent += delegate { packetsSent++; };
            MockAcceptor mockAcceptor = new MockAcceptor();
            mockAcceptor.Start();

            recorder.StartRecording(tempFileName);
            Assert.AreEqual(MillipedeMode.Record, recorder.Mode);
            Assert.AreEqual(0, recorder.NumberEvents);
            ITransport transport = null;
            MillipedeAcceptor acceptor =
                (MillipedeAcceptor)MillipedeAcceptor.Wrap(mockAcceptor, recorder);
            acceptor.NewTransportAccepted += delegate(ITransport t, IDictionary<string, string> cap)
            {
                transport = t;
            };
            Thread.Sleep(5);
            mockAcceptor.Trigger(mockTransport, new Dictionary<string, string>());
            Thread.Sleep(5);
            Assert.IsNotNull(transport);
            Assert.IsInstanceOfType(typeof(MillipedeTransport), transport);
            Assert.AreEqual(1, recorder.NumberEvents);

            transport.SendPacket(new TransportPacket(new byte[10]));
            Assert.AreEqual(2, recorder.NumberEvents);
            Assert.AreEqual(1, packetsSent, "millipede didn't pass down packet-send in record");
            Thread.Sleep(50);  // give a good amount of time for the test below
            mockTransport.InjectReceivedPacket(new byte[5]);
            Assert.AreEqual(3, recorder.NumberEvents);

            recorder.Dispose(); // don't want the disposes add to the list
            mockAcceptor.Dispose(); mockAcceptor = null;
            mockTransport.Dispose(); mockTransport = null;

            transport = null;

            recorder = new MillipedeRecorder();
            acceptor = (MillipedeAcceptor)MillipedeAcceptor.Wrap(new MockAcceptor(), recorder);
            acceptor.NewTransportAccepted += delegate(ITransport t, IDictionary<string, string> cap)
            {
                transport = t;
            };
            recorder.StartReplaying(tempFileName);
            acceptor.Update();
            for (int i = 0; i < 5 && transport == null; i++)
            {
                Thread.Sleep(100);
                acceptor.Update();
            }
            Assert.IsNotNull(transport);
            Assert.IsInstanceOfType(typeof(MillipedeTransport), transport);
            Assert.AreEqual(1, recorder.NumberEvents);

            transport.SendPacket(new TransportPacket(new byte[10]));
            Assert.AreEqual(2, recorder.NumberEvents);
            transport.Update();
            for (int i = 0; i < 5 && recorder.NumberEvents == 2; i++)
            {
                Thread.Sleep(100);
                transport.Update();
            }
            Assert.AreEqual(3, recorder.NumberEvents);  // should have received the packet too
        }

        [Test]
        public void TestSingletonReplayConfiguration()
        {
            string tmppath = Path.GetTempFileName();
            FileStream tmpFile = File.OpenWrite(tmppath);
            new MillipedeEvent("test", MillipedeEventType.Disposed).Serialize(tmpFile);
            tmpFile.Close();

            Environment.SetEnvironmentVariable("GTMILLIPEDE", "play:" + tmppath);
            try
            {
                MillipedeRecorder.Singleton.Dispose();
                Assert.IsTrue(MillipedeRecorder.Singleton.Active);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GTMILLIPEDE", null);
                MillipedeRecorder.Singleton.Dispose();
                File.Delete(tmppath);
            }
        }

        [Test]
        public void TestSingletonRecordConfiguration()
        {
            string tmppath = Path.GetTempFileName();
            Environment.SetEnvironmentVariable("GTMILLIPEDE", "record:" + tmppath);
            try
            {
                MillipedeRecorder.Singleton.Dispose();
                Assert.IsTrue(MillipedeRecorder.Singleton.Active);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GTMILLIPEDE", null);
                MillipedeRecorder.Singleton.Dispose();
                File.Delete(tmppath);
            }
        }

        [Test]
        public void TestSingletonPassthroughConfiguration()
        {
            Environment.SetEnvironmentVariable("GTMILLIPEDE", "passthrough");
            try
            {
                MillipedeRecorder.Singleton.Dispose();
                Assert.IsFalse(MillipedeRecorder.Singleton.Active);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GTMILLIPEDE", null);
                MillipedeRecorder.Singleton.Dispose();
            }
        }

        [Test]
        public void TestSingletonBlankConfiguration()
        {
            Environment.SetEnvironmentVariable("GTMILLIPEDE", "  ");    // should be trimmed
            try
            {
                MillipedeRecorder.Singleton.Dispose();
                Assert.IsFalse(MillipedeRecorder.Singleton.Active);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GTMILLIPEDE", null);
                MillipedeRecorder.Singleton.Dispose();
            }
        }

        [Test]
        public void TestSingletonInvalidConfiguration()
        {
            Environment.SetEnvironmentVariable("GTMILLIPEDE", "blahblah");
            try
            {
                try
                {
                    MillipedeRecorder.Singleton.Dispose();
                    Assert.IsTrue(MillipedeRecorder.Singleton.Active);
                    Assert.Fail("should have thrown an error");
                } catch(ArgumentException e) { /* ignore */ }
            }
            finally
            {
                Environment.SetEnvironmentVariable("GTMILLIPEDE", null);
                MillipedeRecorder.Singleton.Dispose();
            }
        }

    }

    internal delegate R Creator<T1, T2, R>(T1 arg1, T2 arg2);
    internal delegate R Creator<T1, T2, T3, R>(T1 arg1, T2 arg2, T3 arg3);

    internal enum RunningState { Created, Started, Stopped, Disposed }

    internal class MockConnector : IConnector {

        public RunningState State { get; private set; }
        public Creator<string, string, IDictionary<string,string>, ITransport> CreateTransport { get; set; }

        public void Dispose() { State = RunningState.Disposed;}

        public void Start() { State = RunningState.Started; }

        public void Stop() { State = RunningState.Stopped; }

        public bool Active { get { return State == RunningState.Started; } }

        public ITransport Connect(string address, string port, IDictionary<string, string> capabilities)
        {
            Assert.AreEqual(RunningState.Started, State);

            return CreateTransport(address, port, capabilities);
        }

        public bool Responsible(ITransport transport)
        {
            throw new System.NotImplementedException();
        }
    }

    internal class MockAcceptor : BaseAcceptor
    {
        public RunningState State { get; private set; }

        public override void Dispose() { State = RunningState.Disposed; }

        public override void Start() { State = RunningState.Started; }

        public override void Stop() { State = RunningState.Stopped; }

        public override bool Active { get { return State == RunningState.Started; } }
        
        public void Trigger(ITransport transport, IDictionary<string, string> capabilities)
        {
            CheckAndNotify(transport, capabilities);
        }

        public override void Update() { }
    }

    public class MockTransport : ITransport
    {
        RunningState State { get; set; }

        public MockTransport(string name, Dictionary<string, string> cap, Reliability reliability,
            Ordering ordering, uint maxPacketSize)
        {
            State = RunningState.Started;

            Name = name;
            Capabilities = cap;
            Reliability = reliability;
            Ordering = ordering;
            MaximumPacketSize = maxPacketSize;
        }

        public Reliability Reliability { get; set; }

        public Ordering Ordering { get; set; }
        public float Delay { get; set; }

        public void Dispose()
        {
            State = RunningState.Disposed;
        }

        public string Name { get; set; }

        public bool Active { get { return State == RunningState.Started; } }

        public uint Backlog { get { return 0; } }

        public event PacketHandler PacketReceivedEvent;
        public event PacketHandler PacketSentEvent;
        public event ErrorEventNotication ErrorEvent;

        public IDictionary<string, string> Capabilities { get; set; }

        public void SendPacket(TransportPacket packet)
        {
            if (PacketSentEvent != null)
            {
                PacketSentEvent(packet, this);
            }
        }

        public void Update()
        {
            /* do nothing */
        }

        public uint MaximumPacketSize { get; set; }

        public void InjectReceivedPacket(byte[] bytes)
        {
            if (PacketReceivedEvent != null)
            {
                PacketReceivedEvent(new TransportPacket(bytes), this);
            }
        }
    }
}
