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
 
namespace GT.UnitTests.BaseTests
{
    /// <remarks>Test basic converter functionality</remarks>
    [TestFixture]
    public class BasicConversionTests
    {
        #region "Tests"
        [Test]
        public void TestConvert()
        {
        }
        #endregion

    }


    /// <summary>
    /// Test wrapped streams
    /// </summary>
    [TestFixture]
    public class WrappedStreamTests
    {
        MemoryStream source;
        WrappedStream ws;
        uint sourceSize = 30;
        uint wrappedStartPosition = 10;
        uint wrappedSize = 10;

        [SetUp]
        public void SetUp()
        {
            source = new MemoryStream(30);
            for (int i = 0; i < source.Capacity; i++) { source.WriteByte(0x55); }
            source.Position = wrappedStartPosition;
            ws = new WrappedStream(source, wrappedSize);
        }

        public void VerifyBounds()
        {
            Assert.AreEqual(sourceSize, source.Capacity);
            Assert.AreEqual(sourceSize, source.Length);
            byte[] bytes = source.ToArray();
            for (uint i = 0; i < wrappedStartPosition; i++)
            {
                Assert.AreEqual(0x55, bytes[i]);
            }
            for (uint i = wrappedStartPosition + wrappedSize; i < bytes.Length; i++)
            {
                Assert.AreEqual(0x55, bytes[i]);
            }
        }

        [Test]
        public void TestWritingCapacity()
        {
            Assert.AreEqual(0, ws.Position);
            for (int i = 0; i < wrappedSize; i++)
            {
                Assert.AreEqual(i, ws.Position);
                ws.WriteByte((byte)i);
                Assert.AreEqual(i+1, ws.Position);
            }
            try
            {
                ws.WriteByte((byte)255);
                Assert.Fail("Wrote over end of wrapped stream!");
            }
            catch (ArgumentException)
            {
                /* should have thrown exception */
            }

            ws.Position = 0;    // rewind
            for (int i = 0; i < wrappedSize; i++)
            {
                Assert.AreEqual(i, ws.Position);
                Assert.AreEqual(i, ws.ReadByte());
                Assert.AreEqual(i+1, ws.Position);
            }
            Assert.AreEqual(-1, ws.ReadByte());

            VerifyBounds();
        }

        [Test]
        public void TestBounds()
        {
            try
            {
                ws.Position = wrappedSize + 1;
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            try
            {
                ws.Seek(-1, SeekOrigin.Begin);
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            try
            {
                ws.Seek(1, SeekOrigin.End);
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            try
            {
                ws.Seek(20, SeekOrigin.Current);
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            VerifyBounds();
        }

        [Test]
        public void TestBounds2()
        {
            try
            {
                ws.Write(new byte[wrappedSize + 1], 0, (int)wrappedSize + 1);
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            Assert.AreEqual(wrappedSize, ws.Read(new byte[wrappedSize + 1], 0, (int)wrappedSize + 1));
            Assert.AreEqual(wrappedSize, ws.Position);
        }

        [Test]
        public void TestBounds3()
        {
            try
            {
                ws.Write(new byte[wrappedSize], 0, (int)wrappedSize);
            }
            catch (Exception) {
                Assert.Fail("should not have thrown an exception");
            }
            Assert.AreEqual(wrappedSize, ws.Position);

            Assert.AreEqual(0, ws.Read(new byte[wrappedSize], 0, (int)wrappedSize));
            Assert.AreEqual(wrappedSize, ws.Position);
            VerifyBounds();
        }

        [Test]
        public void TestBounds4()
        {
            try
            {
                ws.Seek(0, SeekOrigin.Begin);
            }
            catch (ArgumentException)
            {
                Assert.Fail("should not have thrown an exception");
            }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            try
            {
                ws.Seek(0, SeekOrigin.End);
            }
            catch (ArgumentException)
            {
                Assert.Fail("should not have thrown an exception");
            }
            Assert.AreEqual(wrappedSize, ws.Position);
        }
    }

    [TestFixture]
    public class ByteUtilsTests
    {

        #region Adaptive Length Encoding tests
        #region Helper methods
        protected void CheckEncoding(int value, byte[] expectedResult)
        {
            MemoryStream ms = new MemoryStream();
            Assert.AreEqual(0, ms.Length);
            ByteUtils.EncodeLength(value, ms);
            ms.Position = 0;
            Assert.AreEqual(expectedResult.Length, ms.Length);
            Assert.AreEqual(expectedResult, ms.ToArray());
            Assert.AreEqual(value, ByteUtils.DecodeLength(ms));
            Assert.IsTrue(ms.Position == ms.Length);    // check that no turds left on the stream
        }
        #endregion
        #region Tests
        [Test]
        public void CheckLengthEncodingBoundaries()
        {
            CheckEncoding(0, new byte[] { 0 });
            CheckEncoding(63, new byte[] { 63 });
            CheckEncoding(64, new byte[] { 64, 64 });
            CheckEncoding(16383, new byte[] { 127, 255 });
            CheckEncoding(16384, new byte[] { 128, 64, 0 });
            CheckEncoding(4194303, new byte[] { 191, 255, 255 });
            CheckEncoding(4194304, new byte[] { 192, 64, 0, 0 });
            CheckEncoding(1073741823, new byte[] { 255, 255, 255, 255 });

            // bit pattern: 0b10101010 0b10101010 0b10101010
            CheckEncoding(2796202, new byte[] { 170, 170, 170 });
            try
            {
                CheckEncoding(1073741824, new byte[] { 0, 0, 0, 0, 0 });
                Assert.Fail("Encoding should have raised exception: too great to be encoded");
            }
            catch (Exception e) { /* success */ }
        }

        [Test]
        public void CheckLengthEncodingPatterns()
        {
            // bit pattern: 0b10101010 0b10101010 0b10101010
            CheckEncoding(2796202, new byte[] { 170, 170, 170 });

            // bit pattern: 0b10101010 0b01010101 0b10101010
            CheckEncoding(2796202, new byte[] { 170, 170, 170 });

            // bit pattern: 0b11110000 0b11001100 0b00110011 0b00001111
            CheckEncoding(818688783, new byte[] { 240, 204, 51, 15 });

        }

        [Test]
        public void TestEOF()
        {
            MemoryStream ms = new MemoryStream(new byte[] { 64 });
            try
            {
                ByteUtils.DecodeLength(ms);
                Assert.Fail("Should throw exception on EOF");
            }
            catch (InvalidDataException) { }
        }


        #endregion
        #endregion

        #region Encoded String-String Dictionary Tests

        [Test]
        public void TestDictionaryEncoding()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict["foo"] = "bar";
            dict["nog"] = Guid.NewGuid().ToString("N");

            // # bytes
            // 1    1 byte to represent 2 keys
            // 4    'foo': 1 byte for length + 3 bytes for key 'foo'
            // 4    'bar': 1 byte for length + 3 bytes for value 'bar'
            // 4    'nog': 1 byte for length + 3 bytes for key 'nog'
            // 33   guid: 1 byte for length + 32 bytes for guid ("N" is most compact)
            // 46 bytes total
            MemoryStream ms = new MemoryStream();
            ByteUtils.EncodeDictionary(dict, ms);
            Assert.AreEqual(46, ms.Length);
            Assert.AreEqual(46, ByteUtils.EncodedDictionaryByteCount(dict));

            ms.Position = 0;
            Dictionary<string,string> decoded = ByteUtils.DecodeDictionary(ms);
            Assert.AreEqual(dict, decoded);
        }
        #endregion

    }
    /// <remarks>Test basic marshalling functionality</remarks>
    [TestFixture]
    public class DotNetSerializingMarshallerTests
    {
        #region DotNetSerializingMarshaller tests
        [Test]
        public void TestObjectMessage()
        {
            MemoryStream ms = new MemoryStream();
            IMarshaller m = new DotNetSerializingMarshaller();
            object o = new List<object>();
            m.Marshal(new ObjectMessage(0, o), ms, new NullTransport());
            ms.Seek(0, SeekOrigin.Begin);
            Message msg = m.Unmarshal(ms, new NullTransport());
            Assert.IsInstanceOfType(typeof(ObjectMessage), msg);
            Assert.AreEqual(0, msg.Id);
            Assert.AreEqual(MessageType.Object, msg.MessageType);
            Assert.IsInstanceOfType(typeof(List<object>), ((ObjectMessage)msg).Object);
            Assert.AreEqual(0, ((List<object>)((ObjectMessage)msg).Object).Count);
        }

        [Test]
        public void TestSystemMessage()
        {
            MemoryStream ms = new MemoryStream();
            IMarshaller m = new DotNetSerializingMarshaller();
            m.Marshal(new SystemMessage(SystemMessageType.UniqueIDRequest, new byte[4]),
                ms, new NullTransport());
            ms.Seek(0, SeekOrigin.Begin);
            Message msg = m.Unmarshal(ms, new NullTransport());
            Assert.IsInstanceOfType(typeof(SystemMessage), msg);
            Assert.AreEqual((byte)SystemMessageType.UniqueIDRequest, msg.Id);
            Assert.AreEqual(MessageType.System, msg.MessageType);
            Assert.AreEqual(new byte[4], ((SystemMessage)msg).data);
        }

        [Test]
        public void TestStringMessage()
        {
            MemoryStream ms = new MemoryStream();
            IMarshaller m = new DotNetSerializingMarshaller();
            string s = "this is a test of faith";
            m.Marshal(new StringMessage(0, s), ms, new NullTransport());
            ms.Seek(0, SeekOrigin.Begin);
            Message msg = m.Unmarshal(ms, new NullTransport());
            Assert.IsInstanceOfType(typeof(StringMessage), msg);
            Assert.AreEqual(0, msg.Id);
            Assert.AreEqual(MessageType.String, msg.MessageType);
            Assert.AreEqual(s, ((StringMessage)msg).Text);
        }

        [Test]
        public void TestBinaryMessage()
        {
            MemoryStream ms = new MemoryStream();
            IMarshaller m = new DotNetSerializingMarshaller();
            byte[] bytes = new byte[10];
            for (int i = 0; i < bytes.Length; i++) { bytes[i] = (byte)i; }
            m.Marshal(new BinaryMessage(0, bytes), ms, new NullTransport());
            ms.Seek(0, SeekOrigin.Begin);
            Message msg = m.Unmarshal(ms, new NullTransport());
            Assert.IsInstanceOfType(typeof(BinaryMessage), msg);
            Assert.AreEqual(0, msg.Id);
            Assert.AreEqual(MessageType.Binary, msg.MessageType);
            Assert.AreEqual(bytes, ((BinaryMessage)msg).Bytes);
        }

        #endregion

        #region Lightweight Marshalling Tests
        [Test]
        public void TestRawMessage()
        {
            MemoryStream ms = new MemoryStream();
            IMarshaller m = new DotNetSerializingMarshaller();
            string s = "this is a test of faith";
            m.Marshal(new StringMessage(0, s), ms, new NullTransport());
            ms.Seek(0, SeekOrigin.Begin);
            int originalLength = (int)ms.Length;

            IMarshaller rawm = new LightweightDotNetSerializingMarshaller();
            Message msg = rawm.Unmarshal(ms, new NullTransport());
            Assert.IsInstanceOfType(typeof(RawMessage), msg);
            Assert.AreEqual(0, msg.Id);
            Assert.AreEqual(MessageType.String, msg.MessageType);

            ms = new MemoryStream();
            rawm.Marshal(msg, ms, new NullTransport());
            ms.Seek(0, SeekOrigin.Begin);
            Assert.AreEqual(originalLength, (int)ms.Length);

            msg = m.Unmarshal(ms, new NullTransport());
            Assert.IsInstanceOfType(typeof(StringMessage), msg);
            Assert.AreEqual(0, msg.Id);
            Assert.AreEqual(MessageType.String, msg.MessageType);
            Assert.AreEqual(s, ((StringMessage)msg).Text);
        }

        #endregion
    }

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

        public event TransportErrorHandler TransportErrorEvent;
        public event PacketReceivedHandler PacketReceivedEvent;

        public MessageProtocol MessageProtocol
        {
            get { return MessageProtocol.Tcp; }
        }

        public float Delay
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        public Dictionary<string, string> Capabilities
        {
            get { return new Dictionary<string,string>(); }
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
    }

        /// <summary>
    /// Test basic GT functionality
    /// </summary>
    [TestFixture]
    public class ZZATransportTests
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

        protected void RunServer() {
            server.PacketReceivedEvent += new PacketReceivedHandler(ServerReceivedPacket);
            while(server.Active) { 
                server.Update();
                Thread.Sleep(50);
            }
        }

        protected void ServerReceivedPacket(byte[] buffer, int offset, int count, ITransport transport) {
            byte[] received = new byte[count];
            Array.Copy(buffer, offset, received, 0, count);
            if (buffer[offset] != serverPacketCount + serverMissedOffset)
            {
                Console.WriteLine("server: expected packet#" + (serverPacketCount + serverMissedOffset)
                    + " but received packet #" + buffer[offset]);
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
            if (buffer[offset] != clientPacketCount + clientMissedOffset)
            {
                Console.WriteLine("client: expected packet#" + (clientPacketCount + clientMissedOffset)
                    + " but received packet #" + buffer[offset]);
                ok = false;
            }
            Array.Copy(buffer, offset, received, 0, count);
            if (!CheckPacket(received, "client")) { ok = false; }
            Console.Write(ok ? '+' : '!');
            clientPacketCount++;
        }

        protected void SetupServer(ITransport t, Dictionary<string,string> capabilities) {
            DebugUtils.WriteLine("Server: connected to client by " + t);
            server = t;
            serverThread = new Thread(new ThreadStart(RunServer));
            serverThread.Name = "Server";
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        protected void RunAcceptor() {
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
            client.PacketReceivedEvent += new PacketReceivedHandler(ClientReceivedPacket);

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

    /// <summary>
    /// Test basic GT functionality
    /// </summary>
    [TestFixture]
    public class ZZZStreamTests
    {

        private Boolean errorOccurred;
        private Boolean responseReceived;
        private Client client;

        private static string EXPECTED_GREETING = "Hello!";
        private static string EXPECTED_RESPONSE = "Go Away!";
        private static int ServerSleepTime = 20;
        private static int ClientSleepTime = 25;

        [SetUp]
        public void SetUp()
        {
            errorOccurred = false;
            responseReceived = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (serverThread != null)
            {
                // Killing the serverThread when it uses StartListener() automatically
                // stops the server too
                serverThread.Abort();
                serverThread.Join();
            }
            serverThread = null;
            if (server != null) { server.Stop(); }
            server = null;

            if (client != null) { client.Dispose(); }
            client = null;
            Console.WriteLine(this + " TearDown() complete");
        }

        #region "Server Stuff"

        public class TestServerConfiguration : DefaultServerConfiguration
        {
            public TestServerConfiguration(int port)
                : base(port)
            { 
            }

            public override ICollection<IAcceptor> CreateAcceptors()
            {
                ICollection<IAcceptor> acceptors = base.CreateAcceptors();
                acceptors.Add(new LocalAcceptor("127.0.0.1:9999")); // whatahack!
                return acceptors;
            }

        }

        public class TestClientConfiguration : DefaultClientConfiguration
        {
            public override ICollection<IConnector> CreateConnectors()
            {
                ICollection<IConnector> connectors = base.CreateConnectors();
                connectors.Add(new LocalConnector());
                return connectors;
            }
        }

        private Server server;
        private Thread serverThread;

        // FIXME: Currently ignores expected and response.  
        // Should be implemented as a new object
        private void StartExpectedResponseServer(string expected, string response)
        {
            if (serverThread != null)
            {
                Assert.Fail("server already started");
            }
            server = new TestServerConfiguration(9999).BuildServer();
            server.StringMessageReceived += new StringMessageHandler(ServerStringMessageReceived);
            server.BinaryMessageReceived += new BinaryMessageHandler(ServerBinaryMessageReceived);
            server.ObjectMessageReceived += new ObjectMessageHandler(ServerObjectMessageReceived);
            server.SessionMessageReceived += new SessionMessageHandler(ServerSessionMessageReceived);
            server.ErrorEvent += new ErrorClientHandler(server_ErrorEvent);
            server.Start();
            serverThread = server.StartSeparateListeningThread(ServerSleepTime);
            Console.WriteLine("Server started: " + server.ToString() + " [" + serverThread.Name + "]");
        }

        private void ServerStringMessageReceived(Message m, ClientConnexion client, MessageProtocol protocol)
        {
            string s = ((StringMessage)m).Text;
            if (!s.Equals(EXPECTED_GREETING))
            {
                Console.WriteLine("Server: expected '" + EXPECTED_GREETING + 
                    "' but received '" + s + "'");
                errorOccurred = true;
            }
            Console.WriteLine("Server: received greeting '" + s + "' on " + protocol);
            Console.WriteLine("Server: sending response: '" + EXPECTED_RESPONSE + "'");
            List<ClientConnexion> clientGroup = new List<ClientConnexion>(1);
            clientGroup.Add(client);
            server.Send(EXPECTED_RESPONSE, m.Id, clientGroup, protocol);
        }

        private void ServerBinaryMessageReceived(Message m, ClientConnexion client, MessageProtocol protocol)
        {
            byte[] buffer = ((BinaryMessage)m).Bytes;
            Console.WriteLine("Server: received binary '" + ByteUtils.DumpBytes(buffer, 0, buffer.Length) + "' on " + protocol);
            Array.Reverse(buffer);
            Console.WriteLine("Server: sending response: '" + ByteUtils.DumpBytes(buffer, 0, buffer.Length) + "'");
            List<ClientConnexion> clientGroup = new List<ClientConnexion>(1);
            clientGroup.Add(client);
            server.Send(buffer, m.Id, clientGroup, protocol);
        }

        private void ServerObjectMessageReceived(Message m, ClientConnexion client, MessageProtocol protocol)
        {
            object o = ((ObjectMessage)m).Object;
            Console.WriteLine("Server: received object '" + o + "' on " + protocol);
            Console.WriteLine("Server: sending object back");
            List<ClientConnexion> clientGroup = new List<ClientConnexion>(1);
            clientGroup.Add(client);
            server.Send(o, m.Id, clientGroup, protocol);
        }

        private void ServerSessionMessageReceived(Message m, ClientConnexion client, MessageProtocol protocol)
        {
            SessionMessage sm = (SessionMessage)m;
            if (!sm.Action.Equals(SessionAction.Joined))
            {
                Console.WriteLine("Server: expected '" + SessionAction.Joined +
                    "' but received '" + sm.Action + "'");
                errorOccurred = true;
            }
            Console.WriteLine("Server: received  '" + sm.Action + "' on " + protocol);
            Console.WriteLine("Server: sending back as response");
            List<ClientConnexion> clientGroup = new List<ClientConnexion>(1);
            clientGroup.Add(client);
            server.Send(m, clientGroup, protocol);
        }

        /// <summary>This is triggered if something goes wrong</summary>
        void server_ErrorEvent(Exception e, SocketError se, ClientConnexion c, string explanation)
        {
            Console.WriteLine("Server: Error: " + explanation + "\n" + e.ToString());
            errorOccurred = true;
        }
        #endregion

        #region "Tests"
        [Test]
        public void EchoStringViaLocal()
        {
            PerformEchos((MessageProtocol)5);
        }

        [Test]
        public void EchoStringViaTCP()
        {
            PerformEchos(MessageProtocol.Tcp);
        }

        [Test]
        public void EchoStringViaUDP()
        {
            PerformEchos(MessageProtocol.Udp);
        }

        protected void CheckForResponse()
        {
            Assert.IsFalse(errorOccurred, "Client: error occurred while sending greeting");
            int repeats = 10;
            while (!responseReceived && repeats-- > 0)
            {
                client.Update();  // let the client check the network
                Assert.IsFalse(errorOccurred);
                client.Sleep(ClientSleepTime);
            }
            Assert.IsTrue(responseReceived, "Client: no response received from server");
        }

        protected void PerformEchos(MessageProtocol protocol) {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new TestClientConfiguration().BuildClient();  //this is a client
            client.ErrorEvent += new GT.Net.ErrorEventHandler(client_ErrorEvent);  //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            Console.WriteLine("Client: sending greeting: " + EXPECTED_GREETING);
            IStringStream strStream = client.GetStringStream("127.0.0.1", "9999", 0);  //connect here
            strStream.StringNewMessageEvent += new StringNewMessage(ClientStringMessageReceivedEvent);
            strStream.Send(EXPECTED_GREETING, protocol);  //send a string
            CheckForResponse();
            Assert.AreEqual(1, strStream.Messages.Count);
            string s = strStream.DequeueMessage(0);
            Assert.IsNotNull(s);
            Assert.AreEqual(EXPECTED_RESPONSE, s);

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            Console.WriteLine("Client: sending byte message: [0 1 2 3 4 5 6 7 8 9]");
            IBinaryStream binStream = client.GetBinaryStream("127.0.0.1", "9999", 0);  //connect here
            binStream.BinaryNewMessageEvent += new BinaryNewMessage(ClientBinaryMessageReceivedEvent);
            binStream.Send(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, protocol);
            CheckForResponse();
            Assert.AreEqual(1, binStream.Messages.Count);
            byte[] bytes = binStream.DequeueMessage(0);
            Assert.IsNotNull(bytes);
            Assert.AreEqual(new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }, bytes);

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            Console.WriteLine("Client: sending greeting: list(\"hello\",\"world\")");
            IObjectStream objStream = client.GetObjectStream("127.0.0.1", "9999", 0);  //connect here
            objStream.ObjectNewMessageEvent += new ObjectNewMessage(ClientObjectMessageReceivedEvent);
            objStream.Send(new List<string>(new string[] { "hello", "world" }), protocol);  //send a string
            CheckForResponse();
            Assert.AreEqual(1, objStream.Messages.Count);
            object o = objStream.DequeueMessage(0);
            Assert.IsNotNull(o);
            Assert.IsInstanceOfType(typeof(List<string>), o);
            Assert.AreEqual(new List<string>(new string[] { "hello", "world" }), o);

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            Console.WriteLine("Client: sending greeting: SessionAction.Joined");
            ISessionStream sessStream = client.GetSessionStream("127.0.0.1", "9999", 0);  //connect here
            sessStream.SessionNewMessageEvent += new SessionNewMessage(ClientSessionMessageReceivedEvent);
            sessStream.Send(SessionAction.Joined, protocol);  //send a string
            CheckForResponse();
            Assert.AreEqual(1, sessStream.Messages.Count);
            SessionMessage sm = sessStream.DequeueMessage(0);
            Assert.IsNotNull(sm);
            Assert.AreEqual(sm.Action, SessionAction.Joined);
        }


        void ClientStringMessageReceivedEvent(IStringStream stream)
        {
            Console.WriteLine("Client: received a response\n");
            responseReceived = true;
        }

        void ClientBinaryMessageReceivedEvent(IBinaryStream stream)
        {
            Console.WriteLine("Client: received a response\n");
            responseReceived = true;
        }

        void ClientObjectMessageReceivedEvent(IObjectStream stream)
        {
            Console.WriteLine("Client: received a response\n");
            responseReceived = true;
        }

        void ClientSessionMessageReceivedEvent(ISessionStream stream)
        {
            Console.WriteLine("Client: received a response\n");
            responseReceived = true;
        }

        /// <summary>This is triggered if something goes wrong</summary>
        void client_ErrorEvent(Exception e, SocketError se, ServerConnexion ss, string explanation)
        {
            Console.WriteLine("Client Error: " + explanation + "\n" + e);
            errorOccurred = true;
        }

        #endregion
    }
}
