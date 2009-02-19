using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using GT.Net;
using GT.Utils;
using NUnit.Framework;
using GT.Net.Local;
 
namespace GT.UnitTests
{
    #region Helper Classes

    internal class NullTransport : ITransport
    {
        uint bytesSent = 0;
        public uint BytesSent { get { return bytesSent; } }

        public string Name
        {
            get { return "NULL"; }
        }

        public bool Active
        {
            get { return true; }
        }

        public uint Backlog { get { return 0; } }

        public event PacketHandler PacketReceivedEvent;
        public event PacketHandler PacketSentEvent;

        public NullTransport()
        {
            MaximumPacketSize = 1024;
        }

        public float Delay
        {
            get { return 0; }
            set
            {
                /* do nothing */
            }
        }

        public IDictionary<string, string> Capabilities
        {
            get { return new Dictionary<string, string>(); }
        }

        public void SendPacket(byte[] packet, int offset, int count)
        {
            bytesSent += (uint)count;
        }

        public void SendPacket(Stream stream)
        {
            bytesSent += (uint)stream.Length;
        }

        public Stream GetPacketStream()
        {
            return new MemoryStream();
        }

        public void Update()
        {
        }

        public uint MaximumPacketSize
        {
            get;
            set;
        }

        public void Dispose()
        {
        }

        #region ITransportDeliveryCharacteristics Members

        public Reliability Reliability
        {
            get { return Reliability.Unreliable; }
        }

        public Ordering Ordering
        {
            get { return Ordering.Unordered; }
        }

        #endregion
    }
    #endregion

    [TestFixture]
    public class ZAIPBasedComponentTests
    {
        [Test]
        public void TestDualTcpAcceptors() { 
            TcpAcceptor acc1 = new TcpAcceptor(IPAddress.Any, 9999);
            TcpAcceptor acc2 = new TcpAcceptor(IPAddress.Any, 9999);
            try
            {
                acc1.Start();
                try
                {
                    acc2.Start();
                    Assert.Fail("Should have thrown a transport error");
                }
                catch (TransportError) { /* do nothing */ }
            }
            finally
            {
                acc1.Dispose();
                acc2.Dispose();
            }
        }

        [Test]
        public void TestDualUdpAcceptors()
        {
            UdpAcceptor acc1 = new UdpAcceptor(IPAddress.Any, 9999);
            UdpAcceptor acc2 = new UdpAcceptor(IPAddress.Any, 9999);
            try
            {
                acc1.Start();
                try
                {
                    acc2.Start();
                    Assert.Fail("Should have thrown a transport error");
                }
                catch (TransportError) { /* do nothing */ }
            }
            finally
            {
                acc1.Dispose();
                acc2.Dispose();
            }
        }

    }

        /// <summary>
    /// Test GT transports functionality
    /// </summary>
    [TestFixture]
    public class ZMTransportTests
    {
        protected bool verbose = false;

        int port = 9876;
        Thread serverThread, acceptorThread;
        IAcceptor acceptor;
        IConnector connector;
        ITransport server;
        ITransport client;
        byte[] sourceData;
        string failure;
        int serverPacketCount;
        int clientPacketCount;
        int serverMissedOffset;
        int clientMissedOffset;


        protected void Debug(string text, params object[] args)
        {
            if(verbose)
            {
                Console.WriteLine(text, args);
            }
        }

        [SetUp]
        public void SetUp()
        {
            serverPacketCount = 0;
            clientPacketCount = 0;
            serverMissedOffset = 0;
            clientMissedOffset = 0;
            sourceData = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            failure = "";
        }


        [TearDown]
        public void TearDown()
        {
            if (server != null) { server.Dispose(); }
            if (acceptor != null) { acceptor.Dispose(); }
            if (connector != null) { connector.Dispose(); }
            if (client != null) { client.Dispose(); }
            if (acceptorThread != null) { acceptorThread.Abort(); }
            if (serverThread != null) { serverThread.Abort(); }
            server = null;
            acceptor = null;
            client = null;
            acceptorThread = null;
            serverThread = null;
        }

        protected void RunServer()
        {
            server.PacketReceivedEvent += new PacketHandler(ServerReceivedPacket);
            while (server.Active)
            {
                server.Update();
                Thread.Sleep(50);
            }
        }

        protected void ServerReceivedPacket(byte[] buffer, int offset, int count, ITransport transport)
        {
            byte[] received = new byte[count];
            Array.Copy(buffer, offset, received, 0, count);
            if (buffer[offset] != (byte)(serverPacketCount + serverMissedOffset))
            {
                Console.WriteLine("server: ERROR: expected packet#" + (serverPacketCount + serverMissedOffset)
                    + " but received packet #" + buffer[offset]);
            }
            else
            {
                Debug("server: received expected packet#{0}",
                    (byte)(serverPacketCount + serverMissedOffset));
            }
            CheckPacket(received, "server");
            if (serverPacketCount % 2 == 0)
            {
                Debug("==> server: replying with byte array");
                server.SendPacket(buffer, offset, count);
            }
            else
            {
                Debug("==> server: replying with stream");
                Stream ms = server.GetPacketStream();
                ms.Write(buffer, offset, count);
                server.SendPacket(ms);
            }
            serverPacketCount++;
        }

        protected bool CheckPacket(byte[] incoming, string prefix)
        {
            bool failed = false;
            if (sourceData.Length != incoming.Length)
            {
                lock (this)
                {
                    failure += prefix + ": mismatch in received data size (received " +
                        incoming.Length + ", expected " + sourceData.Length + ")\n";
                    failed = true;
                }
            }
            else
            {
                bool ok = true;
                string debug = "";
                // incoming[0] is the packet number
                for (int i = 1; i < sourceData.Length; i++)
                {
                    if (sourceData[i] != incoming[i])
                    {
                        ok = false;
                        debug += " index " + i + " (" + sourceData[i] + " vs " + incoming[i] + ")";
                    }
                }
                if (!ok)
                {
                    lock (this)
                    {
                        failure += prefix + ": packet " + incoming[0] + " mismatch in received data values: " + debug + "\n";
                        failed = true;
                    }
                }
            }
            if (!failed)
            {
                Debug(prefix + ": received packet checked ok");
            }
            return !failed;
        }

        protected void ClientReceivedPacket(byte[] buffer, int offset, int count, ITransport transport)
        {
            byte[] received = new byte[count];
            bool ok = true;
            if (buffer[offset] != (byte)(clientPacketCount + clientMissedOffset))
            {
                Console.WriteLine("client: ERROR: expected packet#" + (clientPacketCount + clientMissedOffset)
                    + " but received packet #" + buffer[offset]);
                ok = false;
            }
            else
            {
                Debug("client: received expected packet#{0}",
                    (byte)(clientPacketCount + clientMissedOffset));
            }
            Array.Copy(buffer, offset, received, 0, count);
            if (!CheckPacket(received, "client")) { ok = false; }
            Console.Write(ok ? '+' : '!');
            clientPacketCount++;
        }

        protected void SetupServer(ITransport t, IDictionary<string, string> capabilities)
        {
            Debug("Server: connected to client by " + t);
            server = t;
            serverThread = new Thread(new ThreadStart(RunServer));
            serverThread.Name = "Server";
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        protected void RunAcceptor()
        {
            Debug("Checking acceptor...");
            while (acceptor.Active)
            {
                acceptor.Update();
                Thread.Sleep(200);
            }
        }

        public void TestTransport(IAcceptor acc, IConnector conn, string address, string port)
        {
            acceptor = acc;
            connector = conn;

            acceptor.Start();
            acceptor.NewTransportAccepted += SetupServer;
            acceptorThread = new Thread(RunAcceptor);
            acceptorThread.Name = "Acceptor";
            acceptorThread.IsBackground = true;
            acceptorThread.Start();

            client = connector.Connect(address, port, new Dictionary<string, string>());
            Assert.IsNotNull(client);
            client.PacketReceivedEvent += ClientReceivedPacket;

            for (int i = 0; i < 10; i++)
            {
                sourceData[0] = (byte)i;
                Debug("client: sending packet#" + i + ": "
                    + ByteUtils.DumpBytes(sourceData, 0, sourceData.Length));
                client.SendPacket(sourceData, 0, sourceData.Length);
                if (failure.Length != 0)
                {
                    Assert.Fail(failure);
                }
                Thread.Sleep(500);
                client.Update();
            }
            Assert.AreEqual(10, serverPacketCount);
            Assert.AreEqual(10, clientPacketCount);

            try
            {
                client.SendPacket(new byte[client.MaximumPacketSize * 2], 0, 
                    (int)client.MaximumPacketSize * 2);
                Assert.Fail("Transport allowed sending packets exceeding its capacity");
            }
            catch (ContractViolation) { /* expected */ }
        }

        [Test]
        public void TestTcpTransport()
        {
            Console.Write("\nTesting TCP Transport: ");
            TestTransport(new TcpAcceptor(IPAddress.Any, port), new TcpConnector(), "127.0.0.1", port.ToString());
        }

        [Test]
        public void TestUdpTransport()
        {
            Console.Write("\nTesting UDP Transport: ");
            TestTransport(new UdpAcceptor(IPAddress.Any, port), new UdpConnector(), "127.0.0.1", port.ToString());
        }

        [Test]
        public void TestLocalTransport()
        {
            Console.Write("\nTesting Local Transport: ");
            TestTransport(new LocalAcceptor("127.0.0.1:9999"), new LocalConnector(), "127.0.0.1", "9999");
        }

        [Test]
        public void TestSequencedUdpTransport()
        {
            Console.Write("\nTesting Sequenced UDP Transport: ");
            TestTransport(new UdpAcceptor(IPAddress.Any, port, Ordering.Sequenced), 
                new UdpConnector(Ordering.Sequenced), "127.0.0.1", port.ToString());
        }
    }

    [TestFixture]
    public class TestSequencedUdpTransport
    {
        IAcceptor acceptor;
        IConnector connector;
        UdpSequencedServerTestTransport serverTransport = null;
        UdpSequencedClientTestTransport clientTransport = null;

        [SetUp]
        public void SetUp() {
            acceptor = new UdpAcceptor(IPAddress.Any, 8765,
                new TransportFactory<UdpHandle>(BaseUdpTransport.SequencedProtocolDescriptor,
                    h => new UdpSequencedServerTestTransport(h),
                    t => t is UdpSequencedServerTestTransport));
            connector = new UdpConnector(  
                new TransportFactory<UdpClient>(BaseUdpTransport.SequencedProtocolDescriptor,
                    h => new UdpSequencedClientTestTransport(h),
                    t => t is UdpSequencedClientTestTransport));

            acceptor.NewTransportAccepted += delegate(ITransport transport, IDictionary<string, string> capabilities)
            {
                serverTransport = (UdpSequencedServerTestTransport)transport;
            };
            acceptor.Start();

            clientTransport = (UdpSequencedClientTestTransport)connector.Connect("127.0.0.1", "8765", new Dictionary<string, string>());
            Assert.IsNotNull(clientTransport);
            for (int i = 0; i < 10; i++) 
            {
                acceptor.Update();
                Thread.Sleep(100);
            }
            Assert.IsNotNull(serverTransport);
        }

        [TearDown]
        public void TearDown() 
        {
            acceptor.Dispose();
            connector.Dispose();
            serverTransport.Dispose();
            clientTransport.Dispose();
        }

        protected void DoUpdates()
        {
            for(int i = 0; i < 50; i++)
            {
                clientTransport.Update();
                serverTransport.Update();
                Thread.Sleep(10);
            }
        }

        [Test]
        public void TestClientTransport()
        {
            bool packetReceived = false;
            clientTransport.PacketReceivedEvent += delegate(byte[] buffer, int offset, int count, ITransport transport) { 
                packetReceived = true;
            };

            clientTransport.Inject(0, new byte[0]);
            Assert.IsTrue(packetReceived);

            packetReceived = false;
            clientTransport.Inject(0, new byte[0]);
            Assert.IsFalse(packetReceived);

            packetReceived = false;
            clientTransport.Inject(1, new byte[0]);
            Assert.IsTrue(packetReceived);

            packetReceived = false;
            clientTransport.Inject(0, new byte[0]);
            Assert.IsFalse(packetReceived);
            clientTransport.Inject(1, new byte[0]);
            Assert.IsFalse(packetReceived);
            clientTransport.Inject(2, new byte[0]);
            Assert.IsTrue(packetReceived);

            // now test wrap-around
            packetReceived = false;
            clientTransport.Inject(UInt32.MaxValue, new byte[0]);
            Assert.IsTrue(packetReceived);
            clientTransport.Inject(0, new byte[0]);
            Assert.IsTrue(packetReceived);
        }

        [Test]
        public void TestServerTransport()
        {
            bool packetReceived = false;
            serverTransport.PacketReceivedEvent += delegate(byte[] buffer, int offset, int count, ITransport transport)
            {
                packetReceived = true;
            };

            serverTransport.Inject(0, new byte[0]);
            Assert.IsTrue(packetReceived);

            packetReceived = false;
            serverTransport.Inject(0, new byte[0]);
            Assert.IsFalse(packetReceived);

            packetReceived = false;
            serverTransport.Inject(1, new byte[0]);
            Assert.IsTrue(packetReceived);

            packetReceived = false;
            serverTransport.Inject(0, new byte[0]);
            Assert.IsFalse(packetReceived);
            serverTransport.Inject(1, new byte[0]);
            Assert.IsFalse(packetReceived);
            serverTransport.Inject(2, new byte[0]);
            Assert.IsTrue(packetReceived);

            // now test wrap-around
            packetReceived = false;
            serverTransport.Inject(UInt32.MaxValue, new byte[0]);
            Assert.IsTrue(packetReceived);
            serverTransport.Inject(0, new byte[0]);
            Assert.IsTrue(packetReceived);
        }

        public class UdpSequencedServerTestTransport : UdpSequencedServerTransport
        {
            public UdpSequencedServerTestTransport(UdpHandle h) : base(h)
            {
            }

            public void Inject(uint seqNo, byte[] payload)
            {
                Debug.Assert(PacketHeaderSize == 4);
                byte[] datagram = new byte[PacketHeaderSize + payload.Length];
                BitConverter.GetBytes(seqNo).CopyTo(datagram, 0);
                payload.CopyTo(datagram, PacketHeaderSize);
                NotifyPacketReceived(datagram, (int)PacketHeaderSize, payload.Length);
            }
        }

        public class UdpSequencedClientTestTransport : UdpSequencedClientTransport
        {
            public UdpSequencedClientTestTransport(UdpClient h)
                : base(h)
            {
            }

            public void Inject(uint seqNo, byte[] payload)
            {
                Debug.Assert(PacketHeaderSize == 4);
                byte[] datagram = new byte[PacketHeaderSize + payload.Length];
                BitConverter.GetBytes(seqNo).CopyTo(datagram, 0);
                payload.CopyTo(datagram, PacketHeaderSize);
                NotifyPacketReceived(datagram, (int)PacketHeaderSize, payload.Length);
            }
        }

    }

    [TestFixture]
    public class ZABucketTransportTests
    {
        [Test]
        public void TestLeakyBucketTransport()
        {
            NullTransport nt = new NullTransport();
            // drain at 128 bytes every 10 milliseconds, and buffer up to 
            // 384 bytes (or 3 packets of 128 bytes)
            LeakyBucketTransport lbt = new LeakyBucketTransport(nt, 128, 
                TimeSpan.FromMilliseconds(10), 384);

            for(int i = 0; i < 5; i++)
            {
                uint startBytesSent = nt.BytesSent;
                Assert.AreEqual(128, lbt.AvailableCapacity);
                Assert.AreEqual(384, lbt.RemainingBucketCapacity);
                
                lbt.SendPacket(new byte[128], 0, 128);  // should send
                Assert.AreEqual(0, lbt.AvailableCapacity);
                Assert.AreEqual(384, lbt.RemainingBucketCapacity);

                lbt.SendPacket(new byte[128], 0, 128);  // should be buffered
                Assert.AreEqual(0, lbt.AvailableCapacity);
                Assert.AreEqual(256, lbt.RemainingBucketCapacity);

                lbt.SendPacket(new byte[128], 0, 128);  // should be buffered
                Assert.AreEqual(0, lbt.AvailableCapacity);
                Assert.AreEqual(128, lbt.RemainingBucketCapacity);

                lbt.SendPacket(new byte[128], 0, 128);  // should be buffered
                Assert.AreEqual(0, lbt.AvailableCapacity);
                Assert.AreEqual(0, lbt.RemainingBucketCapacity);
                try
                {
                    lbt.SendPacket(new byte[128], 0, 128);  // should exceed bucket capacity
                    Assert.Fail("should have thrown a backlogged exception");
                }
                catch(TransportBackloggedWarning)
                {
                    // this was expected
                }
                Assert.AreEqual(128, nt.BytesSent - startBytesSent);

                // Drain the bucket
                while (lbt.RemainingBucketCapacity < lbt.MaximumCapacity)
                {
                    lbt.Update();
                    Thread.Sleep(20);
                }
                Assert.AreEqual(128, lbt.AvailableCapacity);

            }
        }

        [Test]
        public void TestTokenBucketTransport()
        {
            NullTransport nt = new NullTransport();
            TokenBucketTransport tbt = new TokenBucketTransport(nt, 512, 1024);
            Assert.IsTrue(tbt.AvailableCapacity == 1024, "starting capacity is the maximum capacity");
            for (int i = 0; i < 5; i++)
            {
                uint startBytesSent = nt.BytesSent;
                tbt.SendPacket(new byte[128], 0, 128);
                tbt.SendPacket(new byte[128], 0, 128);
                tbt.SendPacket(new byte[128], 0, 128);
                tbt.SendPacket(new byte[128], 0, 128);
                tbt.SendPacket(new byte[128], 0, 128);
                tbt.SendPacket(new byte[128], 0, 128);
                tbt.SendPacket(new byte[128], 0, 128);
                tbt.SendPacket(new byte[128], 0, 128);
                Assert.AreEqual(1024, nt.BytesSent - startBytesSent);
                try
                {
                    tbt.SendPacket(new byte[128], 0, 128); // this packet will be sent though
                    Assert.Fail("should have thrown a backlogged exception");
                }
                catch (TransportBackloggedWarning)
                {
                    // this was expected
                }
                Assert.AreEqual(1024, nt.BytesSent - startBytesSent);

                // Build up some capacity
                while(tbt.AvailableCapacity < 256)
                {
                    Thread.Sleep(200);
                }
                //Console.WriteLine("Test: slept {0}ms", timer.ElapsedMilliseconds);
                tbt.SendPacket(new byte[128], 0, 128);  // plus the outstanding packet
                Assert.AreEqual(1024 + 2 * 128, nt.BytesSent - startBytesSent);

                // we should now have little capacity again, so sending another packet will backlog
                Assert.IsTrue(tbt.AvailableCapacity < 128);
                try
                {
                    tbt.SendPacket(new byte[128], 0, 128); // this packet will be sent next though
                    Assert.Fail("should have thrown a backlogged exception");
                }
                catch (TransportBackloggedWarning)
                {
                    // this was expected
                }

                // Sleep until there is plenty of capacity
                while(tbt.AvailableCapacity < tbt.MaximumCapacity)
                {
                    Thread.Sleep(200);
                    tbt.Update();
                }
            }
        }
    }
}