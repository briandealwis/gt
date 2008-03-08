using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using NUnit.Framework;
using GT;
using GT;
using GT;
using System.IO;
using System.Net;
 
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
            Assert.AreEqual(0, msg.id);
            Assert.AreEqual(MessageType.Object, msg.type);
            Assert.IsInstanceOfType(typeof(List<object>), ((ObjectMessage)msg).obj);
            Assert.AreEqual(0, ((List<object>)((ObjectMessage)msg).obj).Count);
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
            Assert.AreEqual((byte)SystemMessageType.UniqueIDRequest, msg.id);
            Assert.AreEqual(MessageType.System, msg.type);
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
            Assert.AreEqual(0, msg.id);
            Assert.AreEqual(MessageType.String, msg.type);
            Assert.AreEqual(s, ((StringMessage)msg).text);
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
            Assert.AreEqual(0, msg.id);
            Assert.AreEqual(MessageType.Binary, msg.type);
            Assert.AreEqual(bytes, ((BinaryMessage)msg).data);
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
    public class ZZATCPTransprtTests
    {
        int port = 9876;
        Thread serverThread, acceptorThread;
        IAcceptor acceptor;
        ITransport server;
        ITransport client;
        byte[] sourceData = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        string failure;
        int serverPacketCount;
        int clientPacketCount;

        [SetUp]
        public void SetUp()
        {
            serverPacketCount = 0;
            clientPacketCount = 0;
            failure = "";
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
            CheckPacket(received, "server");
            if (serverPacketCount % 2 == 0)
            {
                Console.WriteLine("==> server: replying with byte array");
                server.SendPacket(buffer, offset, count);
            }
            else
            {
                Console.WriteLine("==> server: replying with stream");
                Stream ms = server.GetPacketStream();
                ms.Write(buffer, offset, count);
                server.SendPacket(ms);
            }
            serverPacketCount++;
        }

        protected void CheckPacket(byte[] incoming, string prefix)
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
                for (int i = 0; i < sourceData.Length; i++)
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
                        failure += prefix + ": mismatch in received data values: " + debug + "\n";
                        failed = true;
                    }
                }
            }
            if (!failed)
            {
                Console.WriteLine(prefix + ": received packet checked ok");
            }
        }

        protected void ClientReceivedPacket(byte[] buffer, int offset, int count, ITransport transport)
        {
            byte[] received = new byte[count];
            Array.Copy(buffer, offset, received, 0, count);
            CheckPacket(received, "client");
            clientPacketCount++;
        }

        protected void SetupServer(ITransport t, Dictionary<string,string> capabilities) {
            Console.WriteLine("Server: connected to client by " + t);
            server = t;
            serverThread = new Thread(new ThreadStart(RunServer));
            serverThread.Name = "Server";
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        protected void RunAcceptor() {
            Console.WriteLine("Checking acceptor...");
            while (acceptor.Active)
            {
                acceptor.Update();
                Thread.Sleep(200);
            }
        }

        [Test]
        public void TestTcpTransport()
        {
            acceptor = new TcpAcceptor(IPAddress.Any, port);
            acceptor.Start();
            acceptor.NewClientEvent += new NewClientHandler(SetupServer);
            acceptorThread = new Thread(new ThreadStart(RunAcceptor));
            acceptorThread.Name = "Acceptor";
            acceptorThread.IsBackground = true;
            acceptorThread.Start();

            client = new TcpConnector().Connect("127.0.0.1", port.ToString(), new Dictionary<string, string>());
            Assert.IsNotNull(client);
            client.PacketReceivedEvent += new PacketReceivedHandler(ClientReceivedPacket);

            for (int i = 0; i < 10; i++)
            {
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
        public void TestUdpTransport()
        {
            acceptor = new UdpAcceptor(IPAddress.Any, port);
            acceptor.Start();
            acceptor.NewClientEvent += new NewClientHandler(SetupServer);
            acceptorThread = new Thread(new ThreadStart(RunAcceptor));
            acceptorThread.Name = "Acceptor";
            acceptorThread.IsBackground = true;
            acceptorThread.Start();

            client = new UdpConnector().Connect("127.0.0.1", port.ToString(), new Dictionary<string, string>());
            Assert.IsNotNull(client);
            client.PacketReceivedEvent += new PacketReceivedHandler(ClientReceivedPacket);

            for (int i = 0; i < 10; i++)
            {
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


        [TearDown]
        public void TearDown() {
            if (server != null) { server.Dispose(); }
            if (acceptor != null) { acceptor.Stop(); }
            if (client != null) { client.Dispose(); }
            if (acceptorThread != null) { acceptorThread.Abort(); }
            if(serverThread != null) { serverThread.Abort(); }
            server = null;
            acceptor = null;
            client = null;
            acceptorThread = null;
            serverThread = null;
        }
    }

    /// <summary>
    /// Test basic GT functionality
    /// </summary>
    [TestFixture]
    public class ZZZStringStreamTests
    {

        private Boolean errorOccurred;
        private Boolean responseReceived;
        private Client client;

        private static string EXPECTED_GREETING = "Hello!";
        private static string EXPECTED_RESPONSE = "Go Away!";
        private static int ServerSleepTime = 13;
        private static int ClientSleepTime = 19;

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
            if (server != null)
            {
                server.Stop();
            }
            server = null;

            if (client != null)
            {
                client.Stop();
                client.Dispose();
            }
            client = null;
            Console.WriteLine(this + " TearDown() complete");
        }

        #region "Server Stuff"

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
            server = new Server(9999);
            server.StringMessageReceived += new StringMessageHandler(ServerStringMessageReceived);
            server.ErrorEvent += new ErrorClientHandler(server_ErrorEvent);
            serverThread = server.StartSeparateListeningThread(ServerSleepTime);
            Console.WriteLine("Server started: " + server.ToString() + " [" + serverThread.Name + "]");
        }

        private void ServerStringMessageReceived(Message m, Server.Client client, MessageProtocol protocol)
        {
            string s = ((StringMessage)m).text;
            if (!s.Equals(EXPECTED_GREETING))
            {
                Console.WriteLine("Server: expected '" + EXPECTED_GREETING + 
                    "' but received '" + s + "'");
                errorOccurred = true;
            }
            Console.WriteLine("Server: received greeting '" + s + "'");
            Console.WriteLine("Server: sending response: '" + EXPECTED_RESPONSE + "'");
            List<Server.Client> clientGroup = new List<Server.Client>(1);
            clientGroup.Add(client);
            server.Send(EXPECTED_RESPONSE, m.id, clientGroup, protocol);
        }

        /// <summary>This is triggered if something goes wrong</summary>
        void server_ErrorEvent(Exception e, SocketError se, Server.Client c, string explanation)
        {
            Console.WriteLine("Server: Error: " + explanation + "\n" + e.ToString());
            errorOccurred = true;
        }
        #endregion

        #region "Tests"

        [Test]
        public void EchoStringViaTCP()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new Client();  //this is a client
            client.ErrorEvent += new GT.ErrorEventHandler(client_ErrorEvent);  //triggers if there is an error
            StringStream stream = client.GetStringStream("127.0.0.1", "9999", 0);  //connect here
            stream.StringNewMessageEvent += new StringNewMessage(ClientStringMessageReceivedEvent);
            Assert.IsFalse(errorOccurred);

            Console.WriteLine("Client: sending greeting: " + EXPECTED_GREETING);
            stream.Send(EXPECTED_GREETING, MessageProtocol.Tcp);  //send a string
            Assert.IsFalse(errorOccurred, "Client: error occurred while sending greeting");

            int repeats = 10;
            while (!responseReceived && repeats-- > 0)
            {
                client.Update();  // let the client check the network
                Assert.IsFalse(errorOccurred);
                client.Sleep(ClientSleepTime);
            }
            Assert.IsTrue(responseReceived, "Client: no response received from server");
            string s = stream.DequeueMessage(0);
            Assert.IsNotNull(s);
            Assert.AreEqual(EXPECTED_RESPONSE, s);
        }

        [Test]
        public void EchoStringViaUDP()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new Client();  //this is a client
            client.ErrorEvent += new GT.ErrorEventHandler(client_ErrorEvent);  //triggers if there is an error
            StringStream stream = client.GetStringStream("127.0.0.1", "9999", 0);  //connect here
            stream.StringNewMessageEvent += new StringNewMessage(ClientStringMessageReceivedEvent);
            Assert.IsFalse(errorOccurred);

            Console.WriteLine("Client: sending greeting: " + EXPECTED_GREETING);
            stream.Send(EXPECTED_GREETING, MessageProtocol.Udp);  //send a string
            Assert.IsFalse(errorOccurred, "Client: error occurred while sending greeting");

            int repeats = 10;
            while (!responseReceived && repeats-- > 0)
            {
                client.Update();  // let the client check the network
                Assert.IsFalse(errorOccurred);
                client.Sleep(ClientSleepTime);
            }
            Assert.IsTrue(responseReceived, "Client: no response received from server");
            string s = stream.DequeueMessage(0);
            Assert.IsNotNull(s);
            Assert.AreEqual(EXPECTED_RESPONSE, s);
        }


        void ClientStringMessageReceivedEvent(IStringStream stream)
        {
            Console.WriteLine("Client: received a response\n");
            responseReceived = true;
        }

        /// <summary>This is triggered if something goes wrong</summary>
        void client_ErrorEvent(Exception e, SocketError se, ServerStream ss, string explanation)
        {
            Console.WriteLine("Error: " + explanation + "\n" + e);
            errorOccurred = true;
        }

        #endregion
    }
}
