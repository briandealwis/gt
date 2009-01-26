﻿using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using GT.Net;
using GT.Millipede;

namespace GT.UnitTests
{
    [TestFixture]
    public class MillipedeTests
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
                try { File.Delete(fn); } catch { /* ignore */ }
            }
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
            transport.SendPacket(new byte[10], 0, 10);
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
            connector = (MillipedeConnector)MillipedeConnector.Wrap(mockConnector = new MockConnector(), 
                recorder);
            transport = connector.Connect("localhost", "9999", new Dictionary<string, string>());
            transport.PacketReceivedEvent += ((packet, offset, count, t) => {
                Assert.AreEqual(new byte[5], packet);
                Assert.AreEqual(3, recorder.NumberEvents);
            });

            Assert.IsInstanceOfType(typeof(MillipedeTransport), transport);
            Assert.AreEqual(1, recorder.NumberEvents);
            transport.SendPacket(new byte[10], 0, 10);
            Thread.Sleep(10);   // the NumberEvent is incremented on playback, and may need time to playback
            Assert.AreEqual(2, recorder.NumberEvents);
            Thread.Sleep(100);
            Assert.AreEqual(3, recorder.NumberEvents);  // should have received the packet too
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

    public class MockTransport : ITransport
    {
        RunningState State { get; set; }

        public MockTransport(string name, Dictionary<string, string> cap, Reliability reliability, Ordering ordering, 
            int maxPacketSize)
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

        public IDictionary<string, string> Capabilities { get; set; }

        public void SendPacket(byte[] packet, int offset, int count)
        {
            if (PacketSentEvent != null)
            {
                PacketSentEvent(packet, 0, count, this);
            }
        }

        public void SendPacket(Stream stream)
        {
            if (PacketSentEvent != null)
            {
                byte[] packet = ((MemoryStream)stream).ToArray();
                PacketSentEvent(packet, 0, packet.Length, this);
            }
        }

        public Stream GetPacketStream()
        {
            return new MemoryStream();
        }

        public void Update()
        {
            /* do nothing */
        }

        public int MaximumPacketSize { get; set; }

        public void InjectReceivedPacket(byte[] bytes)
        {
            if (PacketReceivedEvent != null)
            {
                PacketReceivedEvent(bytes, 0, bytes.Length, this);
            }
        }
    }
}