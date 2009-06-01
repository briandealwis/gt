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
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using GT.Net;
using GT.Net.Utils;
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

        #region ITransportDeliveryCharacteristics Members

        public Reliability Reliability { get; internal set; }
        public Ordering Ordering { get; internal set; }
        public float Delay { get; set; }
        public uint MaximumPacketSize { get; set; }

        #endregion

        public string Name
        {
            get { return "NULL"; }
        }

        public bool Active { get; internal set; }
        public uint Backlog { get { return 0; } }

        public event PacketHandler PacketReceived;
        public event PacketHandler PacketSent;
        public event ErrorEventNotication ErrorEvent;

        public NullTransport() : this(Net.Reliability.Unreliable, Net.Ordering.Unordered, 10, 1024)
        {
        }

        public NullTransport(Reliability reliability, Ordering ordering, int delay, uint maxPacketSize)
        {
            Active = true;
            Reliability = reliability;
            Ordering = ordering;
            Delay = delay;
            MaximumPacketSize = maxPacketSize;
        }

        public IDictionary<string, string> Capabilities
        {
            get { return new Dictionary<string, string>(); }
        }

        public void SendPacket(TransportPacket packet)
        {
            Debug.Assert(packet.Length != 0, "Shouldn't send zero-length packets!");
            bytesSent += (uint)packet.Length;
            if (PacketSent != null) { PacketSent(packet, this); }
            packet.Dispose();
        }

        public void Update()
        {
        }

        public void Dispose()
        {
            Active = false;
        }

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

        [Test]
        public void TestUdpNegotiationTimeout()
        {
            UdpClient serverSocket = new UdpClient(9999);
            UdpConnector conn = new UdpConnector();

            try {
                conn.Start();
                try
                {
                    conn.Connect("127.0.0.1", "9999", new Dictionary<string, string>());
                    Assert.Fail("Should have timed out");
                } catch(CannotConnectException e)
                {
                    // expected
                }
            } finally
            {
                serverSocket.Close();
                conn.Dispose();
            }
        }

        [Test]
        public void TestUdpAutoDetermination()
        {
            UdpAcceptor acc = new UdpAcceptor(IPAddress.Loopback, 9999);
            Thread acceptorThread = null;
            try
            {
                acc.Start();
                acceptorThread = new Thread(delegate()
                {
                    while(acc.Active)
                    {
                        try
                        {
                            acc.Update();
                            Thread.Sleep(50);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Acceptor Thread: " + e);
                        }
                    }
                });
                acceptorThread.IsBackground = true;
                acceptorThread.Name = "Acceptor Thread";
                acceptorThread.Start();
                Thread.Sleep(50);

                UdpConnector conn = new UdpConnector(Ordering.Unordered);
                conn.Start();
                try
                {
                    ITransport t = conn.Connect("127.0.0.1", "9999", new Dictionary<string, string>());
                    Assert.AreEqual(Ordering.Unordered, t.Ordering);
                }
                catch (CannotConnectException) { Assert.Fail("Should have worked"); }
                finally { conn.Dispose(); }

                conn = new UdpConnector(Ordering.Sequenced);
                conn.Start();
                try
                {
                    ITransport t = conn.Connect("127.0.0.1", "9999", new Dictionary<string, string>());
                    Assert.AreEqual(Ordering.Sequenced, t.Ordering);
                }
                catch (CannotConnectException) { Assert.Fail("Should have worked"); }
                finally { conn.Dispose(); }
            }
            finally
            {
                if (acceptorThread != null) { acceptorThread.Abort(); }
                if (acc != null) { acc.Dispose(); }
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
            server.PacketReceived += ServerReceivedPacket;
            while (server.Active)
            {
                server.Update();
                Thread.Sleep(50);
            }
        }

        protected void ServerReceivedPacket(TransportPacket packet, ITransport transport)
        {
            byte[] received = packet.ToArray();
            if (received[0] != (byte)(serverPacketCount + serverMissedOffset))
            {
                Console.WriteLine("server: ERROR: expected packet#" + (serverPacketCount + serverMissedOffset)
                    + " but received packet #" + received[0]);
            }
            else
            {
                Debug("server: received expected packet#{0}",
                    (byte)(serverPacketCount + serverMissedOffset));
            }
            CheckPacket(received, "server");
            Debug("==> server: replying with byte array");
            packet.Retain();    // must retain since SendPacket() will dispose
            server.SendPacket(packet);
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

        protected void ClientReceivedPacket(TransportPacket packet, ITransport transport)
        {
            byte[] received = packet.ToArray();
            bool ok = true;
            if (received[0] != (byte)(clientPacketCount + clientMissedOffset))
            {
                Console.WriteLine("client: ERROR: expected packet#" + (clientPacketCount + clientMissedOffset)
                    + " but received packet #" + received[0]);
                ok = false;
            }
            else
            {
                Debug("client: received expected packet#{0}",
                    (byte)(clientPacketCount + clientMissedOffset));
            }
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
            client.PacketReceived += ClientReceivedPacket;

            for (int i = 0; i < 10; i++)
            {
                sourceData[0] = (byte)i;
                Debug("client: sending packet#" + i + ": "
                    + ByteUtils.DumpBytes(sourceData, 0, sourceData.Length));
                client.SendPacket(new TransportPacket(sourceData));
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
                client.SendPacket(new TransportPacket(new byte[client.MaximumPacketSize * 2]));
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
            TestTransport(new LocalAcceptor("9999"), new LocalConnector(), "127.0.0.1", "9999");
        }

        [Test]
        public void TestSequencedUdpTransport()
        {
            Console.Write("\nTesting Sequenced UDP Transport: ");
            TestTransport(new UdpAcceptor(IPAddress.Any, port), 
                new UdpConnector(Ordering.Sequenced), "127.0.0.1", port.ToString());
        }
    }

    [TestFixture]
    public class ZBSequencedUdpTransport
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
            Thread acceptorThread = new Thread(delegate()
            {
                for (int i = 0; serverTransport == null && i < 100; i++)
                {
                    try
                    {
                        acceptor.Update();
                        Thread.Sleep(50);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Acceptor Thread: " + e);
                    }
                }
            });
            acceptorThread.IsBackground = true;
            acceptorThread.Name = "Acceptor Thread";
            acceptorThread.Start();
            Thread.Sleep(50);

            clientTransport = (UdpSequencedClientTestTransport)connector.Connect("127.0.0.1", "8765", new Dictionary<string, string>());
            acceptorThread.Join();
            Assert.IsNotNull(clientTransport);
            Assert.IsNotNull(serverTransport);

            acceptor.Dispose();
            connector.Dispose();
        }

        [TearDown]
        public void TearDown() 
        {
            serverTransport.Dispose();
            clientTransport.Dispose();
            clientTransport = null;
            serverTransport = null;
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
            clientTransport.PacketReceived += delegate { packetReceived = true; };

            clientTransport.Inject(0, new TransportPacket());
            Assert.IsTrue(packetReceived);

            packetReceived = false;
            clientTransport.Inject(0, new TransportPacket());
            Assert.IsFalse(packetReceived);

            packetReceived = false;
            clientTransport.Inject(1, new TransportPacket());
            Assert.IsTrue(packetReceived);

            packetReceived = false;
            clientTransport.Inject(0, new TransportPacket());
            Assert.IsFalse(packetReceived);
            clientTransport.Inject(1, new TransportPacket());
            Assert.IsFalse(packetReceived);
            clientTransport.Inject(2, new TransportPacket());
            Assert.IsTrue(packetReceived);

            // now test wrap-around
            packetReceived = false;
            clientTransport.Inject(UInt32.MaxValue, new TransportPacket());
            Assert.IsTrue(packetReceived);
            clientTransport.Inject(0, new TransportPacket());
            Assert.IsTrue(packetReceived);
        }

        [Test]
        public void TestServerTransport()
        {
            bool packetReceived = false;
            serverTransport.PacketReceived += delegate { packetReceived = true; };

            serverTransport.Inject(0, new TransportPacket());
            Assert.IsTrue(packetReceived);

            packetReceived = false;
            serverTransport.Inject(0, new TransportPacket());
            Assert.IsFalse(packetReceived);

            packetReceived = false;
            serverTransport.Inject(1, new TransportPacket());
            Assert.IsTrue(packetReceived);

            packetReceived = false;
            serverTransport.Inject(0, new TransportPacket());
            Assert.IsFalse(packetReceived);
            serverTransport.Inject(1, new TransportPacket());
            Assert.IsFalse(packetReceived);
            serverTransport.Inject(2, new TransportPacket());
            Assert.IsTrue(packetReceived);

            // now test wrap-around
            packetReceived = false;
            serverTransport.Inject(UInt32.MaxValue, new TransportPacket());
            Assert.IsTrue(packetReceived);
            serverTransport.Inject(0, new TransportPacket());
            Assert.IsTrue(packetReceived);
        }

        public class UdpSequencedServerTestTransport : UdpSequencedServerTransport
        {
            public UdpSequencedServerTestTransport(UdpHandle h) : base(h)
            {
            }

            public void Inject(uint seqNo, TransportPacket payload)
            {
                Debug.Assert(PacketHeaderSize == 4);
                payload.Prepend(DataConverter.Converter.GetBytes(seqNo));
                NotifyPacketReceived(payload);
            }
        }

        public class UdpSequencedClientTestTransport : UdpSequencedClientTransport
        {
            public UdpSequencedClientTestTransport(UdpClient h)
                : base(h)
            {
            }

            public void Inject(uint seqNo, TransportPacket payload)
            {
                Debug.Assert(PacketHeaderSize == 4);
                payload.Prepend(DataConverter.Converter.GetBytes(seqNo));
                NotifyPacketReceived(payload);
            }
        }

    }

    [TestFixture]
    public class ZBUdpInvalidConnectionTests
    {
        IAcceptor acceptor;
        IConnector connector;
        ITransport serverTransport;
        ITransport clientTransport;

        [SetUp]
        public void SetUp()
        {
            serverTransport = null;
            clientTransport = null;
            acceptor = null;
            connector = null;
        }

        [TearDown]
        public void TearDown()
        {
            if(acceptor != null) { acceptor.Dispose(); }
            acceptor = null;
            if(connector != null) { connector.Dispose(); }
            connector = null;
            if(serverTransport != null) { serverTransport.Dispose(); }
            serverTransport = null;
            if(clientTransport != null) { clientTransport.Dispose(); }
            clientTransport = null;
        }


        [Test]
        public void TestUdpNonSequenced()
        {
            acceptor = new UdpAcceptor(IPAddress.Any, 8765,
                new TransportFactory<UdpHandle>(BaseUdpTransport.SequencedProtocolDescriptor,
                    h => new ZBSequencedUdpTransport.UdpSequencedServerTestTransport(h),
                    t => t is ZBSequencedUdpTransport.UdpSequencedServerTestTransport));
            connector = new UdpConnector(
                new TransportFactory<UdpClient>(BaseUdpTransport.UnorderedProtocolDescriptor,
                    h => new UdpClientTransport(h),
                    t => t is UdpClientTransport));
            CheckNonConnection("localhost", "8765");
        }

        protected void CheckNonConnection(string host, string port)
        {
            acceptor.NewTransportAccepted += 
                ((transport, capabilities) => serverTransport = transport);
            acceptor.Start();
            Thread acceptorThread = new Thread(delegate()
            {
                for (int i = 0; serverTransport == null && i < 100; i++)
                {
                    try
                    {
                        acceptor.Update();
                        Thread.Sleep(50);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Acceptor Thread: " + e);
                    }
                }
            });
            acceptorThread.IsBackground = true;
            acceptorThread.Name = "Acceptor Thread";
            acceptorThread.Start();
            Thread.Sleep(50);

            try
            {
                clientTransport = connector.Connect("127.0.0.1", "8765",
                    new Dictionary<string, string>());
                Assert.Fail("connection should have failed");
            } catch(CannotConnectException e) { /* expected */ }
            acceptorThread.Join();
        }

    }

    [TestFixture]
    public class ZABucketTransportTests
    {
        [Test]
        public void TestLeakyBucketTransport()
        {
            NullTransport nt = new NullTransport();
            // drain at 128 bytes every 100 milliseconds, and buffer up to 
            // 384 bytes (or 3 packets of 128 bytes)
            // (100ms may seem long, but it seems to help avoid possible 
            // race conditions in the test such as where the AvailableCapacity
            // is is suddenly recalcd and available)
            LeakyBucketTransport lbt = new LeakyBucketTransport(nt, 128, 
                TimeSpan.FromMilliseconds(100), 384);
            bool backlogged = false;
            lbt.ErrorEvent += delegate(ErrorSummary es)
            {
                if (es.ErrorCode == SummaryErrorCode.TransportBacklogged)
                {
                    backlogged = true;
                }
            };

            for(int i = 0; i < 5; i++)
            {
                Assert.IsFalse(backlogged);
                uint startBytesSent = nt.BytesSent;
                Assert.AreEqual(128, lbt.AvailableCapacity);
                Assert.AreEqual(384, lbt.RemainingBucketCapacity);
                Assert.IsFalse(backlogged);
                
                lbt.SendPacket(new TransportPacket(new byte[128]));  // should send
                Assert.AreEqual(0, lbt.AvailableCapacity);
                Assert.AreEqual(384, lbt.RemainingBucketCapacity);
                Assert.IsFalse(backlogged);

                lbt.SendPacket(new TransportPacket(new byte[128]));  // should be buffered
                Assert.AreEqual(0, lbt.AvailableCapacity);
                Assert.AreEqual(256, lbt.RemainingBucketCapacity);
                Assert.IsFalse(backlogged);

                lbt.SendPacket(new TransportPacket(new byte[128]));  // should be buffered
                Assert.AreEqual(0, lbt.AvailableCapacity);
                Assert.AreEqual(128, lbt.RemainingBucketCapacity);
                Assert.IsFalse(backlogged);

                lbt.SendPacket(new TransportPacket(new byte[128]));  // should be buffered
                Assert.AreEqual(0, lbt.AvailableCapacity);
                Assert.AreEqual(0, lbt.RemainingBucketCapacity);
                Assert.IsFalse(backlogged);
                    
                lbt.SendPacket(new TransportPacket(new byte[128]));  // should exceed bucket capacity
                Assert.IsTrue(backlogged);
                backlogged = false;

                Assert.AreEqual(128, nt.BytesSent - startBytesSent);

                // Drain the bucket
                do
                {
                    Thread.Sleep(20);
                    lbt.Update();
                    Assert.IsFalse(backlogged);
                }
                while (lbt.RemainingBucketCapacity < lbt.MaximumCapacity 
                    || lbt.AvailableCapacity != 128);
            }
        }

        [Test]
        public void TestTokenBucketTransport()
        {
            NullTransport nt = new NullTransport();
            TokenBucketTransport tbt = new TokenBucketTransport(nt, 512, 1024);
            bool backlogged = false;
            tbt.ErrorEvent += delegate(ErrorSummary es)
            {
                if (es.ErrorCode == SummaryErrorCode.TransportBacklogged)
                {
                    backlogged = true;
                }
            };
            Assert.IsTrue(tbt.AvailableCapacity == 1024, "starting capacity is the maximum capacity");
            for (int i = 0; i < 5; i++)
            {
                uint startBytesSent = nt.BytesSent;
                Assert.IsFalse(backlogged);
                Assert.IsTrue(tbt.AvailableCapacity >= tbt.MaximumCapacity);
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsFalse(backlogged);
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsFalse(backlogged);
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsFalse(backlogged);
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsFalse(backlogged);
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsFalse(backlogged);
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsFalse(backlogged);
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsFalse(backlogged);
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsFalse(backlogged);
                Assert.AreEqual(1024, nt.BytesSent - startBytesSent);

                // this packet will not be sent though
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsTrue(backlogged);
                backlogged = false;

                Assert.AreEqual(1024, nt.BytesSent - startBytesSent);

                // Build up some capacity
                while(tbt.AvailableCapacity < 128)
                {
                    Thread.Sleep(200);
                    tbt.Update();
                }
                //Console.WriteLine("Test: slept {0}ms", timer.ElapsedMilliseconds);
                // plus the outstanding packet
                while (tbt.AvailableCapacity > 128)
                {
                    Assert.IsFalse(backlogged);
                    tbt.SendPacket(new TransportPacket(new byte[128]));
                    Assert.IsFalse(backlogged);
                }

                // we should now have little capacity again, so sending another packet will backlog
                Assert.IsTrue(tbt.AvailableCapacity < 128);

                // this packet will not be sent next though
                Assert.IsFalse(backlogged);
                tbt.SendPacket(new TransportPacket(new byte[128]));
                Assert.IsTrue(backlogged);

                backlogged = false;
                // Sleep until there is plenty of capacity
                do
                {
                    Thread.Sleep(200);
                    tbt.Update();
                    Assert.IsFalse(backlogged);
                }
                while (tbt.AvailableCapacity < tbt.MaximumCapacity);
            }
        }
    }

    [TestFixture]
    public class ZBNetworkEmulatorTransportTests
    {
        NullTransport wrapped;
        uint wrappedPacketsSent;
        NetworkEmulatorTransport transport;
        Bag<NetworkEmulatorTransport.PacketEffect> effects;

        [SetUp]
        public void SetUp() {
            wrapped = new NullTransport(Reliability.Unreliable, Ordering.Unordered, 0, 1024);
            wrappedPacketsSent = 0;
            wrapped.PacketSent += delegate { wrappedPacketsSent++; };
            transport = new NetworkEmulatorTransport(wrapped);
            effects = new Bag<NetworkEmulatorTransport.PacketEffect>();
            transport.PacketDisposition += (mode, effect, packet) => effects.Add(effect);
        }

        [Test]
        public void TestNoConfiguredEffects()
        {
            for (int i = 0; i < 100; i++)
            {
                transport.SendPacket(new TransportPacket(new byte[4]));
            }
            Assert.AreEqual(100, effects.Occurrences(NetworkEmulatorTransport.PacketEffect.None));
            Assert.AreEqual(100, wrappedPacketsSent);
        }


        [Test]
        public void TestSettingInvalidFixedDelay()
        {
            transport.PacketFixedDelay = TimeSpan.Zero;
            Assert.AreEqual(TimeSpan.Zero, transport.PacketFixedDelay);
            transport.PacketFixedDelay = TimeSpan.FromMilliseconds(500);
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), transport.PacketFixedDelay);
            try
            {
                transport.PacketFixedDelay = TimeSpan.FromMilliseconds(-500);
                Assert.Fail("should not allow delays < 0");
            }
            catch (ArgumentException) { /* expected */ }
            transport.DelayProvider = () => TimeSpan.Zero;
            Assert.IsTrue(transport.PacketFixedDelay.CompareTo(TimeSpan.Zero) < 0);
        }


        [Test]
        public void TestDelay()
        {
            transport.DelayProvider = () => TimeSpan.FromMilliseconds(100);
            for (int i = 0; i < 100; i++)
            {
                transport.SendPacket(new TransportPacket(new byte[4]));
            }
            Assert.AreEqual(100, effects.Occurrences(NetworkEmulatorTransport.PacketEffect.Delayed));
            Thread.Sleep(200);
            transport.Update();
            Assert.AreEqual(100, wrappedPacketsSent);
        }

        [Test]
        public void TestPacketLoss()
        {
            transport.PacketLoss = 100;
            for (int i = 0; i < 100; i++)
            {
                transport.SendPacket(new TransportPacket(new byte[4]));
            }
            Assert.AreEqual(100, effects.Occurrences(NetworkEmulatorTransport.PacketEffect.Dropped));
            Assert.AreEqual(0, wrappedPacketsSent);
        }

        [Test]
        public void TestPacketReordering()
        {
            transport.PacketReordering = 100;
            for (int i = 0; i < 100; i++)
            {
                transport.SendPacket(new TransportPacket(new byte[4]));
            }
            Assert.AreEqual(100, effects.Occurrences(NetworkEmulatorTransport.PacketEffect.Reordered));
            Thread.Sleep(200);
            transport.Update();
            Assert.AreEqual(100, wrappedPacketsSent);
        }

    }
}
