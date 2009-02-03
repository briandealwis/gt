using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using GT.Net;
using GT.Utils;
using GT.Net.Local;

namespace GT.UnitTests
{

    #region Useful Client and Server testing configurations
    #region Transport-specific configurations
    public class TestServerConfiguration : DefaultServerConfiguration
    {
        public TestServerConfiguration(int port)
            : base(port)
        {
        }

        public override ICollection<IAcceptor> CreateAcceptors()
        {
            ICollection<IAcceptor> acceptors = base.CreateAcceptors();
            acceptors.Add(new LocalAcceptor("127.0.0.1:" + port)); // whatahack!
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

    public class TestChannelDeliveryRequirements : ChannelDeliveryRequirements
    {
        protected Type transportType;

        public TestChannelDeliveryRequirements(Type transportType)
            : base(Reliability.Unreliable, MessageAggregation.Immediate, Ordering.Unordered)
        {
            this.transportType = transportType;
        }

        public override bool MeetsRequirements(ITransport transport)
        {
            return transportType.IsInstanceOfType(transport);
        }
    }
    #endregion

    #region Local-transport configurations 
    public class LocalServerConfiguration : DefaultServerConfiguration
    {
        public LocalServerConfiguration(int port)
            : base(port)
        {
        }

        public override ICollection<IAcceptor> CreateAcceptors()
        {
            ICollection<IAcceptor> acceptors = new List<IAcceptor>();
            acceptors.Add(new LocalAcceptor("127.0.0.1:" + port)); // whatahack!
            return acceptors;
        }

    }

    public class LocalClientConfiguration : DefaultClientConfiguration
    {
        public override ICollection<IConnector> CreateConnectors()
        {
            ICollection<IConnector> connectors = new List<IConnector>();
            connectors.Add(new LocalConnector());
            return connectors;
        }
    }
    #endregion

    #endregion

    #region "Server Stuff"

    public class EchoingServer
    {
        private bool verbose = false;
        private ServerConfiguration config;
        private Server server;
        private Thread serverThread;
        private string expected;
        private string response;
        protected bool errorOccurred;

        public EchoingServer(int port, string expected, string response)
        {
            this.config = new TestServerConfiguration(port);
            this.expected = expected;
            this.response = response;
        }

        public EchoingServer(int port) : this(port, null, null) {}

        public bool ErrorOccurred { get { return errorOccurred; } }

        protected void Debug(string text, params object[] args)
        {
            if (verbose)
            {
                Console.WriteLine(text, args);
            }
        }

        public Server Server { get { return server; } }

        public ICollection<IConnexion> Connexions { get { return server.Clients; } }

        public int ServerSleepTime {
            get { return (int)config.TickInterval.TotalMilliseconds; }
            set { config.TickInterval = TimeSpan.FromMilliseconds(value); }
        }

        virtual public void Start()
        {
            server = config.BuildServer();
            server.StringMessageReceived += ServerStringMessageReceived;
            server.BinaryMessageReceived += ServerBinaryMessageReceived;
            server.ObjectMessageReceived += ServerObjectMessageReceived;
            server.SessionMessageReceived += ServerSessionMessageReceived;
            server.MessageReceived += ServerGeneralMessageReceived;
            server.ErrorEvent += ServerErrorEvent;
            server.Start();
            serverThread = server.StartSeparateListeningThread();
            Console.WriteLine("Server started: " + server.ToString() + " [" + serverThread.Name + "]");
        }

        virtual public void Stop()
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
        }

        virtual public void Dispose()
        {
            Stop();
        }

        private void ServerStringMessageReceived(Message m, IConnexion client, ITransport t)
        {
            string s = ((StringMessage)m).Text;
            if (expected != null && !s.Equals(expected))
            {
                Console.WriteLine("Server: expected '" + expected +
                    "' but received '" + s + "'");
                errorOccurred = true;
            }
            Debug("Server: received greeting '" + s + "' on " + t);
            Debug("Server: sending response: '" + response + "'");
            server.Send(response != null ? response : s, m.Channel, new SingleItem<IConnexion>(client),
                new MessageDeliveryRequirements(t.Reliability,
                    MessageAggregation.Immediate, Ordering.Unordered));
        }

        private void ServerBinaryMessageReceived(Message m, IConnexion client, ITransport t)
        {
            byte[] buffer = ((BinaryMessage)m).Bytes;
            Debug("Server: received binary message from {0}", t);
            Debug(ByteUtils.HexDump(buffer));
            // Array.Reverse(buffer);
            Debug("Server: sending binary message in response");
            Debug(ByteUtils.HexDump(buffer));
            server.Send(buffer, m.Channel, new SingleItem<IConnexion>(client),
                new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
        }

        private void ServerObjectMessageReceived(Message m, IConnexion client, ITransport t)
        {
            object o = ((ObjectMessage)m).Object;
            Debug("Server: received object '" + o + "' on " + t);
            Debug("Server: sending object back");
            server.Send(o, m.Channel, new SingleItem<IConnexion>(client),
                new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
        }

        private void ServerSessionMessageReceived(Message m, IConnexion client, ITransport t)
        {
            SessionMessage sm = (SessionMessage)m;
            if (!sm.Action.Equals(SessionAction.Joined))
            {
                Console.WriteLine("Server: expected '" + SessionAction.Joined +
                    "' but received '" + sm.Action + "'");
                errorOccurred = true;
            }
            Debug("Server: received  '" + sm.Action + "' on " + t);
            Debug("Server: sending back as response");
            server.Send(m, new SingleItem<IConnexion>(client),
                new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
        }

        private void ServerGeneralMessageReceived(Message m, IConnexion client, ITransport t)
        {
            switch (m.MessageType)
            {
            case MessageType.Tuple1D:
            case MessageType.Tuple2D:
            case MessageType.Tuple3D:
                Debug("Server: received  tuple message on " + t);
                server.Send(m, new SingleItem<IConnexion>(client),
                    new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
                break;
            }

        }

        /// <summary>This is triggered if something goes wrong</summary>
        void ServerErrorEvent(ErrorSummary es)
        {
            Debug("Server: {0}: {1}[{2}]: {3}: {4}", DateTime.Now, es.Severity, es.ErrorCode,
                es.Message, es.Context);
            errorOccurred = true;
        }
    }

    #endregion

    /// <summary>
    /// Test server basics
    /// </summary>
    [TestFixture]
    public class ZAServerBasics
    {
        [Test]
        public void TestRestartingServer()
        {
            Server s = new Server(9876);
            s.Start();
            s.StartSeparateListeningThread();
            Thread.Sleep(200);
            s.Stop();
            Thread.Sleep(200);
            try
            {
                s.Start();
                s.StartSeparateListeningThread();
                Thread.Sleep(200);
            }
            catch (Exception e)
            {
                Assert.Fail("Unexpected exception: {0}", e);
            }
            finally
            {
                s.Stop();
            }
        }
    }

    /// <summary>
    /// Test basic GT functionality
    /// </summary>
    [TestFixture]
    public class ZSStreamTests
    {
        private bool verbose = false;
        private bool errorOccurred;
        private bool responseReceived;
        private Client client;
        private EchoingServer server;

        private static int ServerSleepTime = 20;
        private static int ClientSleepTime = 25;
        private static string EXPECTED_GREETING = "Hello!";
        private static string EXPECTED_RESPONSE = "Go away!";

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
            errorOccurred = false;
            responseReceived = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (server != null) { server.Dispose(); }
            server = null;

            if (client != null) { client.Dispose(); }
            client = null;
            Debug(this + " TearDown() complete");
        }

        // FIXME: Currently ignores expected and response.  
        // Should be implemented as a new object
        private void StartExpectedResponseServer(string expected, string response)
        {
            if (server != null)
            {
                Assert.Fail("server already started");
            }
            server = new EchoingServer(9999, expected, response);
            server.ServerSleepTime = ServerSleepTime;
            server.Start();
        }

        #region "Tests"
        [Test]
        public void EchoStringViaLocal()
        {
            PerformEchos(new TestChannelDeliveryRequirements(typeof(LocalTransport)));
        }

        [Test]
        public void EchoStringViaTCP()
        {
            PerformEchos(new TestChannelDeliveryRequirements(typeof(TcpTransport)));
        }

        [Test]
        public void EchoStringViaUDP()
        {
            PerformEchos(new TestChannelDeliveryRequirements(typeof(BaseUdpTransport)));
        }

        [Test]
        public void TestMessageEvents()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new TestClientConfiguration().BuildClient();  //this is a client
            client.ErrorEvent += client_ErrorEvent;  //triggers if there is an error

            bool connexionAdded = false, connexionRemoved = false;
            bool messageReceived = false, messageSent = false;
            bool pingRequested = false, pingReceived = false;
            client.ConnexionAdded += delegate(Communicator ignored, IConnexion c)
            {
                connexionAdded = true;
                c.MessageReceived += delegate(Message m, IConnexion conn, ITransport transport) { messageReceived = true; };
                c.MessageSent += delegate(Message m, IConnexion conn, ITransport transport) { messageSent = true; };
                c.PingRequested += delegate(ITransport transport, uint sequence) { pingRequested = true; };
                c.PingReceived += delegate(ITransport transport, uint sequence, int milliseconds) { pingReceived = true; };
            };
            client.ConnexionRemoved += delegate(Communicator ignored, IConnexion c) { connexionRemoved = true; };

            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            {
                Debug("Client: sending greeting: " + EXPECTED_GREETING);
                IStringStream strStream = client.GetStringStream("127.0.0.1", "9999", 0,
                    new TestChannelDeliveryRequirements(typeof(BaseUdpTransport)));  //connect here
                strStream.StringNewMessageEvent += ClientStringMessageReceivedEvent;
                strStream.Send(EXPECTED_GREETING);  //send a string
                CheckForResponse();
                Assert.AreEqual(1, strStream.Messages.Count);
                string s = strStream.DequeueMessage(0);
                Assert.IsNotNull(s);
                Assert.AreEqual(EXPECTED_RESPONSE, s);
                strStream.StringNewMessageEvent -= ClientStringMessageReceivedEvent;
            }

            foreach (IConnexion c in client.Connexions) { ((ConnexionToServer)c).Ping(); }
            for (int i = 0; i < 10 && !pingReceived; i++)
            {
                client.Update();
                Thread.Sleep(server.ServerSleepTime);
            }

            // Trigger connexion removed -- as not sent from client.Stop()
            foreach(IConnexion c in client.Connexions) { c.Dispose(); }
            client.Update();

            client.Stop();
            // Unfortunately waiting for the message to percolate through can take time
            for (int i = 0; i < 10 && server.Connexions.Count > 0; i++)
            {
                Thread.Sleep(server.ServerSleepTime);
            }
            Assert.IsTrue(connexionAdded);
            Assert.IsTrue(messageSent);
            Assert.IsTrue(messageReceived);
            Assert.IsTrue(pingRequested);
            Assert.IsTrue(pingReceived);
            Assert.IsTrue(connexionRemoved);
        }

        [Test]
        public void TestClientReceivesId()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new TestClientConfiguration().BuildClient(); //this is a client
            client.ErrorEvent += client_ErrorEvent; //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            IStringStream strStream = client.GetStringStream("127.0.0.1", "9999", 0,
                new TestChannelDeliveryRequirements(typeof(BaseUdpTransport))); //connect here
            strStream.StringNewMessageEvent += ClientStringMessageReceivedEvent;
            strStream.Send(EXPECTED_GREETING); //send a string
            CheckForResponse();

            Assert.IsFalse(strStream.Identity == 0, "Unique identity should be received");
        }

        [Test]
        public void TestGetStreamDisconnects()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new TestClientConfiguration().BuildClient(); //this is a client
            client.ErrorEvent += client_ErrorEvent; //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            IStringStream origStream = client.GetStringStream("127.0.0.1", "9999", 0,
                new TestChannelDeliveryRequirements(typeof(TcpTransport))); //connect here

            Assert.IsTrue(client.Connexions.Count == 1);
            foreach (IConnexion cnx in client.Connexions) {
                cnx.ShutDown(); cnx.Dispose();
            }
            Assert.IsFalse(origStream.Connexion.Active, "connexion should now be closed");
            
            IStringStream newStream = client.GetStringStream("127.0.0.1", "9999", 0,
                new TestChannelDeliveryRequirements(typeof(TcpTransport))); //connect here
            Assert.IsTrue(newStream.Connexion.Active, "a new stream should have been provided");
        }

        [Test]
        public void TestClientShutDown()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new TestClientConfiguration().BuildClient();  //this is a client
            client.ErrorEvent += client_ErrorEvent;  //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            {
                Debug("Client: sending greeting: " + EXPECTED_GREETING);
                IStringStream strStream = client.GetStringStream("127.0.0.1", "9999", 0,
                    new TestChannelDeliveryRequirements(typeof(BaseUdpTransport)));  //connect here
                strStream.StringNewMessageEvent += ClientStringMessageReceivedEvent;
                strStream.Send(EXPECTED_GREETING);  //send a string
                CheckForResponse();
                Assert.AreEqual(1, strStream.Messages.Count);
                string s = strStream.DequeueMessage(0);
                Assert.IsNotNull(s);
                Assert.AreEqual(EXPECTED_RESPONSE, s);
                strStream.StringNewMessageEvent -= ClientStringMessageReceivedEvent;
            }

            client.Stop();
            // Unfortunately waiting for the message to percolate through can take time
            for (int i = 0; i < 10 && server.Connexions.Count > 0; i++)
            {
                Thread.Sleep(server.ServerSleepTime);
            }
            Assert.AreEqual(0, server.Connexions.Count);
        }

        [Test]
        public void TestAggregatedMessagesProperlyCounted()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new TestClientConfiguration().BuildClient(); //this is a client
            client.ErrorEvent += client_ErrorEvent; //triggers if there is an error

            bool connexionAdded = false;
            int messagesReceived = 0, messagesSent = 0;
            client.ConnexionAdded += delegate(Communicator ignored, IConnexion c)
            {
                connexionAdded = true;
                c.MessageReceived += delegate { messagesReceived++; };
                c.MessageSent += delegate { messagesSent++; };
            };

            client.Start();
            ProcessEvents();

            {
                Debug("Client: sending greeting: " + EXPECTED_GREETING);
                IStringStream strStream = client.GetStringStream("127.0.0.1", "9999", 0,
                    new TestChannelDeliveryRequirements(typeof(BaseUdpTransport))); //connect here
                strStream.StringNewMessageEvent += ClientStringMessageReceivedEvent;
                Assert.IsTrue(connexionAdded, "should have connected");

                for (int i = 0; i < 5; i++)
                {
                    strStream.Send(EXPECTED_GREETING,
                        new MessageDeliveryRequirements(Reliability.Reliable,
                            MessageAggregation.Aggregatable, Ordering.Unordered));
                    ProcessEvents();
                    Assert.AreEqual(0, messagesReceived);
                    Assert.AreEqual(0, messagesSent);
                    Assert.AreEqual(0, strStream.Messages.Count);
                }

                strStream.Send(EXPECTED_GREETING);
                Assert.AreEqual(6, messagesSent);
                ProcessEvents();
                Assert.AreEqual(6, messagesReceived);
                Assert.AreEqual(6, strStream.Messages.Count);
            }
        }

        #endregion

        #region Tests Infrastructure 

        protected void CheckForResponse()
        {
            Assert.IsFalse(errorOccurred, "Client: error occurred while sending greeting");
            int repeats = 10;
            while (!responseReceived && repeats-- > 0)
            {
                client.Update();  // let the client check the network
                Assert.IsFalse(errorOccurred);
                Assert.IsFalse(server.ErrorOccurred);
                client.Sleep(ClientSleepTime);
            }
            Assert.IsTrue(responseReceived, "Client: no response received from server");
        }

        protected void ProcessEvents()
        {
            Assert.IsFalse(errorOccurred, "Client: error occurred while sending greeting");
            int repeats = 10;
            while (repeats-- > 0)
            {
                client.Update();  // let the client check the network
                Assert.IsFalse(errorOccurred);
                Assert.IsFalse(server.ErrorOccurred);
                client.Sleep(ClientSleepTime);
            }
        }

        protected void PerformEchos(ChannelDeliveryRequirements cdr)
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new TestClientConfiguration().BuildClient();  //this is a client
            client.ErrorEvent += client_ErrorEvent;  //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            {
                Debug("Client: sending greeting: " + EXPECTED_GREETING);
                IStringStream strStream = client.GetStringStream("127.0.0.1", "9999", 0, cdr);  //connect here
                strStream.StringNewMessageEvent += ClientStringMessageReceivedEvent;
                strStream.Send(EXPECTED_GREETING);  //send a string
                CheckForResponse();
                Assert.AreEqual(1, strStream.Messages.Count);
                string s = strStream.DequeueMessage(0);
                Assert.IsNotNull(s);
                Assert.AreEqual(EXPECTED_RESPONSE, s);
                strStream.StringNewMessageEvent -= ClientStringMessageReceivedEvent;
            }

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            {
                byte[] sentBytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                Debug("Client: sending byte message: [0 1 2 3 4 5 6 7 8 9]");
                IBinaryStream binStream = client.GetBinaryStream("127.0.0.1", "9999", 0, cdr);  //connect here
                binStream.BinaryNewMessageEvent += ClientBinaryMessageReceivedEvent;
                binStream.Send(sentBytes);
                CheckForResponse();
                Assert.AreEqual(1, binStream.Messages.Count);
                byte[] bytes = binStream.DequeueMessage(0);
                Assert.IsNotNull(bytes);
                Assert.AreEqual(sentBytes, bytes);
                binStream.BinaryNewMessageEvent -= ClientBinaryMessageReceivedEvent;
            }

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            {
                Debug("Client: sending greeting: list(\"hello\",\"world\")");
                IObjectStream objStream = client.GetObjectStream("127.0.0.1", "9999", 0, cdr);  //connect here
                objStream.ObjectNewMessageEvent += ClientObjectMessageReceivedEvent;
                objStream.Send(new List<string>(new string[] { "hello", "world" }));  //send a string
                CheckForResponse();
                Assert.AreEqual(1, objStream.Messages.Count);
                object o = objStream.DequeueMessage(0);
                Assert.IsNotNull(o);
                Assert.IsInstanceOfType(typeof(List<string>), o);
                Assert.AreEqual(new List<string>(new string[] { "hello", "world" }), o);
                objStream.ObjectNewMessageEvent -= ClientObjectMessageReceivedEvent;
            }

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            {
                Debug("Client: sending greeting: SessionAction.Joined");
                ISessionStream sessStream = client.GetSessionStream("127.0.0.1", "9999", 0, cdr);  //connect here
                sessStream.SessionNewMessageEvent += ClientSessionMessageReceivedEvent;
                sessStream.Send(SessionAction.Joined);  //send a string
                CheckForResponse();
                Assert.AreEqual(1, sessStream.Messages.Count);
                SessionMessage sm = sessStream.DequeueMessage(0);
                Assert.IsNotNull(sm);
                Assert.AreEqual(sm.Action, SessionAction.Joined);
                sessStream.SessionNewMessageEvent -= ClientSessionMessageReceivedEvent;
            }

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            {
                Debug("Client: sending tuple message: [-1, 0, 1]");
                IStreamedTuple<int, int, int> tupleStream = 
                    client.GetStreamedTuple<int, int, int>("127.0.0.1", "9999", 0, 20, cdr);
                tupleStream.StreamedTupleReceived += ClientTupleMessageReceivedEvent;
                tupleStream.X = -1;
                tupleStream.Y = 0;
                tupleStream.Z = 1;
                CheckForResponse();
                Assert.AreEqual(-1, tupleStream.X);
                Assert.AreEqual(0, tupleStream.Y);
                Assert.AreEqual(1, tupleStream.Z);
                tupleStream.StreamedTupleReceived -= ClientTupleMessageReceivedEvent;
            }
        }

        void ClientStringMessageReceivedEvent(IStringStream stream)
        {
            Debug("Client: received a string response\n");
            responseReceived = true;
        }

        void ClientBinaryMessageReceivedEvent(IBinaryStream stream)
        {
            Debug("Client: received a byte[] response\n");
            responseReceived = true;
        }

        void ClientObjectMessageReceivedEvent(IObjectStream stream)
        {
            Debug("Client: received a object response\n");
            responseReceived = true;
        }

        void ClientSessionMessageReceivedEvent(ISessionStream stream)
        {
            Debug("Client: received a session response\n");
            responseReceived = true;
        }

        void ClientTupleMessageReceivedEvent<T_X,T_Y,T_Z>(RemoteTuple<T_X, T_Y, T_Z> tuple, int clientId)
            where T_X : IConvertible
            where T_Y : IConvertible
            where T_Z : IConvertible
        {
            Console.WriteLine("Client: received a tuple response\n");
            responseReceived = true;
        }


        /// <summary>This is triggered if something goes wrong</summary>
        void client_ErrorEvent(ErrorSummary es)
        {
            Debug("Client: {0}[{1}]: {2}: {3}", es.Severity, es.ErrorCode,
                es.Message, es.Context);
            errorOccurred = true;
        }

        #endregion
    }

    [TestFixture]
    public class ZTSharedDictionaryTests
    {
        ClientRepeater server;
        List<Client> clients;
        List<AggregatingSharedDictionary> dictionaries;
        bool errorOccurred = false;
        int numberClientDictionaries = 3;

        [SetUp]
        public void SetUpClientsAndServer()
        {
            errorOccurred = false;

            // ServerConfiguration sc = new TestServerConfiguration(9678);
            ServerConfiguration sc = new DefaultServerConfiguration(9678);
            sc.PingInterval = TimeSpan.FromMinutes(60);
            server = new ClientRepeater(sc);
            server.Start();
            clients = new List<Client>();
            dictionaries = new List<AggregatingSharedDictionary>();

            // First set up the dictionaries
            for (int i = 0; i < numberClientDictionaries; i++)
            {
                // Client c = new LocalClientConfiguration().BuildClient();
                ClientConfiguration cc = new DefaultClientConfiguration();
                cc.PingInterval = TimeSpan.FromMinutes(60);

                Client c = cc.BuildClient();
                c.ErrorEvent += ClientErrorEvent;
                clients.Add(c);
                c.Start();
                AggregatingSharedDictionary d =
                    new AggregatingSharedDictionary(c.GetObjectStream("127.0.0.1", "9678", 0,
                        new ChannelDeliveryRequirements(Reliability.Reliable, MessageAggregation.Aggregatable,
                            Ordering.Ordered)), 20);
                d.ChangeEvent += DictionaryChanged;
                dictionaries.Add(d);
                UpdateClients();
            }

            for (int i = 0; i < numberClientDictionaries; i++)
            {
                AggregatingSharedDictionary d = dictionaries[i];
                Assert.IsFalse(d.ContainsKey("foo"));
                Assert.IsFalse(d.ContainsKey("client" + i + "/name"));
                d["client" + i + "/name"] = i;
                Assert.IsTrue(d.ContainsKey("client" + i + "/name"));
            }
        }

        protected void DictionaryChanged(string key)
        {
            Console.WriteLine("Dictionary: key changed: {0}", key);
        }

        public void ClientErrorEvent(ErrorSummary es)
        {
            Console.WriteLine("Client: {0}[{1}]: {2}: {3}", es.Severity, es.ErrorCode,
                es.Message, es.Context);
            errorOccurred = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (server != null) { server.Dispose(); }
            if (clients != null)
            {
                foreach (Client c in clients) { c.Dispose(); }
            }
        }

        protected void UpdateClients()
        {
            // repeat for 3 occurrences
            for (int i = 0; i < 3; i++) 
            {
                foreach (Client c in clients) {
                    c.Update(); c.Sleep(10);  
                }
            }
        }

        [Test]
        public void TestAddsAndRemoves()
        {
            //DebugUtils.Verbose = true;
            //server.Verbose = true;
            Assert.IsFalse(errorOccurred);
            foreach (AggregatingSharedDictionary d in dictionaries)
            {
                Assert.IsFalse(d.ContainsKey("foo"));
            }
            Assert.IsFalse(errorOccurred);
            dictionaries[0]["foo"] = "bar";
            Assert.IsFalse(errorOccurred);
            for (int i = 1; i < dictionaries.Count; i++)
            {
                Assert.IsNull(dictionaries[i]["foo"]);
            }
            Assert.IsFalse(errorOccurred);
            UpdateClients();
            Assert.IsFalse(errorOccurred);
            foreach (AggregatingSharedDictionary d in dictionaries)
            {
                Assert.IsTrue(d.ContainsKey("foo"));
                Assert.AreEqual("bar", d["foo"]);
            }

        }
    }

    public class DebuggingServerConnexion : ConnexionToServer
    {
        public DebuggingServerConnexion(Client owner, string address, string port)
            : base(owner, address, port) {}

        internal void TriggerTransportError(ITransport transport)
        {
            HandleTransportDisconnect(transport);
        }
    }

    public class TcpClientConfiguration : DefaultClientConfiguration
    {
        public override ICollection<IConnector> CreateConnectors()
        {
            List<IConnector> connectors = new List<IConnector>(1);
            connectors.Add(new TcpConnector());
            return connectors;
        }

        public override Client BuildClient()
        {
            return new Client(this);
        }

        public override ConnexionToServer CreateServerConnexion(Client owner, string address, string port)
        {
            return new DebuggingServerConnexion(owner, address, port);
        }
    }

    [TestFixture]
    [Ignore("Disabled AutoReconnecting")]
    public class ZTTransportReconnects
    {
        ClientRepeater cr;
        Client client;

        [SetUp]
        public void SetUp()
        {
            cr = new ClientRepeater(9999);
            cr.Start();
        }

        [TearDown]
        public void TearDown()
        {
            if (cr != null) { cr.Dispose(); }
            cr = null;
            if (client != null) { client.Dispose(); }
            client = null;
        }

        public void CheckUpdates()
        {
            for (int i = 0; i < 3; i++)
            {
                client.Update();
                client.Sleep(20);
            }
            client.Update();
        }

        [Test]
        public void TestReconnect()
        {
            client = new TcpClientConfiguration().BuildClient();
            client.Start();

            IStringStream stream = client.GetStringStream("localhost", "9999", 0, 
                ChannelDeliveryRequirements.MostStrict);
            Assert.AreEqual(1, client.Connexions.Count);
            Assert.IsInstanceOfType(typeof(DebuggingServerConnexion), new List<IConnexion>(client.Connexions)[0]);
            DebuggingServerConnexion sc = (DebuggingServerConnexion)new List<IConnexion>(client.Connexions)[0];
            Assert.AreEqual(1, sc.Transports.Count);
            Assert.IsInstanceOfType(typeof(TcpTransport), sc.Transports[0]);
            TcpTransport transport = (TcpTransport)sc.Transports[0];

            stream.Send("foo");
            CheckUpdates();
            Assert.IsTrue(stream.Count > 0);
            Assert.AreEqual("foo", stream.DequeueMessage(0));

            sc.TriggerTransportError(transport);

            Assert.AreEqual(1, sc.Transports.Count);
            Assert.IsInstanceOfType(typeof(TcpTransport), sc.Transports[0]);
            Assert.IsFalse(transport == sc.Transports[0]);  // should be a new connection

            stream.Send("foo");
            CheckUpdates();
            Assert.IsTrue(stream.Count > 0);
            Assert.AreEqual("foo", stream.DequeueMessage(0));


        }
    }
}
