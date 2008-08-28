using System;
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
        }

        public void SendPacket(Stream stream)
        {
        }

        public Stream GetPacketStream()
        {
            return new MemoryStream();
        }

        public void Update()
        {
        }

        public int MaximumPacketSize
        {
            get { return 1024; }
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

    /// <summary>
    /// Test GT transports functionality
    /// </summary>
    [TestFixture]
    public class ZMTransportTests
    {
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
                DebugUtils.WriteLine("server: received expected packet#{0}",
                    (byte)(serverPacketCount + serverMissedOffset));
            }
            CheckPacket(received, "server");
            if (serverPacketCount % 2 == 0)
            {
                DebugUtils.WriteLine("==> server: replying with byte array");
                server.SendPacket(buffer, offset, count);
            }
            else
            {
                DebugUtils.WriteLine("==> server: replying with stream");
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
                DebugUtils.WriteLine(prefix + ": received packet checked ok");
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
                DebugUtils.WriteLine("client: received expected packet#{0}",
                    (byte)(clientPacketCount + clientMissedOffset));
            }
            Array.Copy(buffer, offset, received, 0, count);
            if (!CheckPacket(received, "client")) { ok = false; }
            Console.Write(ok ? '+' : '!');
            clientPacketCount++;
        }

        protected void SetupServer(ITransport t, Dictionary<string, string> capabilities)
        {
            DebugUtils.WriteLine("Server: connected to client by " + t);
            server = t;
            serverThread = new Thread(new ThreadStart(RunServer));
            serverThread.Name = "Server";
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        protected void RunAcceptor()
        {
            DebugUtils.WriteLine("Checking acceptor...");
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
            acceptor.NewClientEvent += new NewClientHandler(SetupServer);
            acceptorThread = new Thread(new ThreadStart(RunAcceptor));
            acceptorThread.Name = "Acceptor";
            acceptorThread.IsBackground = true;
            acceptorThread.Start();

            client = connector.Connect(address, port, new Dictionary<string, string>());
            Assert.IsNotNull(client);
            client.PacketReceivedEvent += new PacketHandler(ClientReceivedPacket);

            for (int i = 0; i < 10; i++)
            {
                sourceData[0] = (byte)i;
                DebugUtils.WriteLine("client: sending packet#" + i + ": "
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
                client.SendPacket(new byte[client.MaximumPacketSize * 2], 0, client.MaximumPacketSize * 2);
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
    }
}