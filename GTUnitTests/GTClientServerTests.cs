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
            Type transportType;

            public TestClientConfiguration(Type transportType)
            {
                this.transportType = transportType;
            }

            public override ICollection<IConnector> CreateConnectors()
            {
                ICollection<IConnector> connectors = base.CreateConnectors();
                connectors.Add(new LocalConnector());
                return connectors;
            }

            public override ITransport SelectTransport(IList<ITransport> transports, MessageDeliveryRequirements mdr, ChannelDeliveryRequirements cdr)
            {
                foreach (ITransport t in transports)
                {
                    if (transportType.IsInstanceOfType(t)) { return t; }
                }
                throw new NoMatchingTransport("I must insist on an instance of " + transportType);
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
            server.MessageReceived += ServerGeneralMessageReceived;
            server.ErrorEvent += new ErrorClientHandler(server_ErrorEvent);
            server.Start();
            serverThread = server.StartSeparateListeningThread(ServerSleepTime);
            Console.WriteLine("Server started: " + server.ToString() + " [" + serverThread.Name + "]");
        }

        private void ServerStringMessageReceived(Message m, ClientConnexion client, ITransport t)
        {
            string s = ((StringMessage)m).Text;
            if (!s.Equals(EXPECTED_GREETING))
            {
                Console.WriteLine("Server: expected '" + EXPECTED_GREETING +
                    "' but received '" + s + "'");
                errorOccurred = true;
            }
            Console.WriteLine("Server: received greeting '" + s + "' on " + t);
            Console.WriteLine("Server: sending response: '" + EXPECTED_RESPONSE + "'");
            List<ClientConnexion> clientGroup = new List<ClientConnexion>(1);
            clientGroup.Add(client);
            server.Send(EXPECTED_RESPONSE, m.Id, clientGroup,
                new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
        }

        private void ServerBinaryMessageReceived(Message m, ClientConnexion client, ITransport t)
        {
            byte[] buffer = ((BinaryMessage)m).Bytes;
            Console.WriteLine("Server: received binary '" + ByteUtils.DumpBytes(buffer, 0, buffer.Length) + "' on " + t);
            Array.Reverse(buffer);
            Console.WriteLine("Server: sending response: '" + ByteUtils.DumpBytes(buffer, 0, buffer.Length) + "'");
            List<ClientConnexion> clientGroup = new List<ClientConnexion>(1);
            clientGroup.Add(client);
            server.Send(buffer, m.Id, clientGroup,
                new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
        }

        private void ServerObjectMessageReceived(Message m, ClientConnexion client, ITransport t)
        {
            object o = ((ObjectMessage)m).Object;
            Console.WriteLine("Server: received object '" + o + "' on " + t);
            Console.WriteLine("Server: sending object back");
            List<ClientConnexion> clientGroup = new List<ClientConnexion>(1);
            clientGroup.Add(client);
            server.Send(o, m.Id, clientGroup,
                new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
        }

        private void ServerSessionMessageReceived(Message m, ClientConnexion client, ITransport t)
        {
            SessionMessage sm = (SessionMessage)m;
            if (!sm.Action.Equals(SessionAction.Joined))
            {
                Console.WriteLine("Server: expected '" + SessionAction.Joined +
                    "' but received '" + sm.Action + "'");
                errorOccurred = true;
            }
            Console.WriteLine("Server: received  '" + sm.Action + "' on " + t);
            Console.WriteLine("Server: sending back as response");
            List<ClientConnexion> clientGroup = new List<ClientConnexion>(1);
            clientGroup.Add(client);
            server.Send(m, clientGroup,
                new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
        }

        private void ServerGeneralMessageReceived(Message m, ClientConnexion client, ITransport t)
        {
            switch (m.MessageType)
            {
            case MessageType.Tuple1D:
            case MessageType.Tuple2D:
            case MessageType.Tuple3D:
                Console.WriteLine("Server: received  tuple message on " + t);
                List<ClientConnexion> clientGroup = new List<ClientConnexion>(1);
                clientGroup.Add(client);
                server.Send(m, clientGroup,
                    new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
                break;
            }

        }

        /// <summary>This is triggered if something goes wrong</summary>
        void server_ErrorEvent(ClientConnexion c, string explanation, object context)
        {
            Console.WriteLine("Server: Error: " + explanation + "\n   context: " + context.ToString());
            errorOccurred = true;
        }
        #endregion

        #region "Tests"
        [Test]
        public void EchoStringViaLocal()
        {
            PerformEchos(typeof(LocalTransport), ChannelDeliveryRequirements.Data);
        }

        [Test]
        public void EchoStringViaTCP()
        {
            PerformEchos(typeof(TcpTransport), ChannelDeliveryRequirements.Data);
        }

        [Test]
        public void EchoStringViaUDP()
        {
            PerformEchos(typeof(BaseUdpTransport), ChannelDeliveryRequirements.Data);
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

        protected void PerformEchos(Type protocolClass, ChannelDeliveryRequirements cdr)
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new TestClientConfiguration(protocolClass).BuildClient();  //this is a client
            client.ErrorEvent += new GT.Net.ErrorEventHandler(client_ErrorEvent);  //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            {
                Console.WriteLine("Client: sending greeting: " + EXPECTED_GREETING);
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
                Console.WriteLine("Client: sending byte message: [0 1 2 3 4 5 6 7 8 9]");
                IBinaryStream binStream = client.GetBinaryStream("127.0.0.1", "9999", 0, cdr);  //connect here
                binStream.BinaryNewMessageEvent += ClientBinaryMessageReceivedEvent;
                binStream.Send(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                CheckForResponse();
                Assert.AreEqual(1, binStream.Messages.Count);
                byte[] bytes = binStream.DequeueMessage(0);
                Assert.IsNotNull(bytes);
                Assert.AreEqual(new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }, bytes);
                binStream.BinaryNewMessageEvent -= ClientBinaryMessageReceivedEvent;
            }

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            {
                Console.WriteLine("Client: sending greeting: list(\"hello\",\"world\")");
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
                Console.WriteLine("Client: sending greeting: SessionAction.Joined");
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
                Console.WriteLine("Client: sending tuple message: [-1, 0, 1]");
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
            Console.WriteLine("Client: received a string response\n");
            responseReceived = true;
        }

        void ClientBinaryMessageReceivedEvent(IBinaryStream stream)
        {
            Console.WriteLine("Client: received a byte[] response\n");
            responseReceived = true;
        }

        void ClientObjectMessageReceivedEvent(IObjectStream stream)
        {
            Console.WriteLine("Client: received a object response\n");
            responseReceived = true;
        }

        void ClientSessionMessageReceivedEvent(ISessionStream stream)
        {
            Console.WriteLine("Client: received a session response\n");
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
        void client_ErrorEvent(ServerConnexion ss, string explanation, object context)
        {
            Console.WriteLine("Client Error: " + explanation + "\n   context: " + context);
            errorOccurred = true;
        }

        #endregion
    }
}
