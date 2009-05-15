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
using System.Threading;
using System.Collections.Generic;
using GT.Net.Utils;
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
            acceptors.Add(new LocalAcceptor(port.ToString())); // whatahack!
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

    public class SpecificTransportChannelDeliveryRequirements : ChannelDeliveryRequirements
    {
        protected Type transportType;

        public SpecificTransportChannelDeliveryRequirements(Type transportType)
            : base(Reliability.Unreliable, MessageAggregation.Immediate, Ordering.Unordered)
        {
            this.transportType = transportType;
        }

        public override bool MeetsRequirements(ITransport transport)
        {
            return transportType.IsInstanceOfType(transport);
        }

        public override string ToString()
        {
            return "TestCDR: for " + transportType;
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
            acceptors.Add(new LocalAcceptor(port.ToString())); // whatahack!
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

        public TimeSpan ServerSleepTime {
            get { return config.TickInterval; }
            set { config.TickInterval = value; }
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
            server.Send(response != null ? response : s, m.ChannelId, new SingleItem<IConnexion>(client),
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
            server.Send(buffer, m.ChannelId, new SingleItem<IConnexion>(client),
                new MessageDeliveryRequirements(t.Reliability, MessageAggregation.Immediate, Ordering.Unordered));
        }

        private void ServerObjectMessageReceived(Message m, IConnexion client, ITransport t)
        {
            object o = ((ObjectMessage)m).Object;
            Debug("Server: received object '" + o + "' on " + t);
            Debug("Server: sending object back");
            server.Send(o, m.ChannelId, new SingleItem<IConnexion>(client),
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
            Console.WriteLine("Server: " + es);
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

        [Test]
        public void TestSendingOnUnstartedServer() {
            Server s = new Server(9876);
            try
            {
                s.Send(new byte[5], 0, null, MessageDeliveryRequirements.MostStrict);
                Assert.Fail("Should not reach here");
            }
            catch (InvalidStateException)
            {
                /* expected result */
            }
            finally
            {
                s.Dispose();
            }
        }

        [Test]
        public void TestSendingOnStoppedServer() {
            Server s = new Server(9876);
            s.Start();
            s.StartSeparateListeningThread();
            Thread.Sleep(200);
            s.Stop();
            try
            {
                s.Send(new byte[5], 0, null, MessageDeliveryRequirements.MostStrict);
                Assert.Fail("Should not reach here");
            }
            catch (InvalidStateException)
            {
                /* expected result */
            }
            finally
            {
                s.Dispose();
            }
        }
    }

    [TestFixture]
    public class ZAClientBasics
    {
        [Test]
        public void TestSendingOnUnstartedClient()
        {
            Client c = new Client();
            try
            {
                c.OpenStringChannel("localhost", "9999", 0, ChannelDeliveryRequirements.Data);
                Assert.Fail("Should not reach here");
            }
            catch (InvalidStateException)
            {
                /* expected result */
            }
            finally
            {
                c.Dispose();
            }
        }

        [Test]
        public void TestSendingOnStoppedClient()
        {
            Client c = new Client();
            c.Start();
            c.StartSeparateListeningThread();
            Thread.Sleep(200);
            c.Stop();
            try
            {
                c.OpenStringChannel("localhost", "9999", 0, ChannelDeliveryRequirements.Data);
                Assert.Fail("Should not reach here");
            }
            catch (InvalidStateException)
            {
                /* expected result */
            }
            finally
            {
                c.Dispose();
            }
        }
    }

    /// <summary>
    /// Test that client-facing channels can handle messages that are
    /// of a different type than expected.
    /// </summary>
    [TestFixture]
    public class ZTMessageHandling
    {
        Client client;
        Server server;
        bool errorOccurred = false;

        [SetUp]
        public void SetUp() {
            errorOccurred = false;
            client = new LocalClientConfiguration().BuildClient();
            client.Start();
            client.ErrorEvent += delegate(ErrorSummary es)
            {
                Console.WriteLine("CLIENT ERROR: " + es);
                errorOccurred = true;
            };
            server = new LocalServerConfiguration(9999).BuildServer();
            server.StartSeparateListeningThread();
            server.ErrorEvent += delegate(ErrorSummary es)
            {
                Console.WriteLine("SERVER ERROR: " + es);
                errorOccurred = true;
            };
        }

        [TearDown]
        public void TearDown() {
            server.Stop();
            client.Stop();
        }

        [Test]
        public void TestObjectMessageMishandling()
        {
            IObjectChannel objChannel = client.OpenObjectChannel("127.0.0.1", "9999", 0, ChannelDeliveryRequirements.LeastStrict);
            for (int i = 0; server.Connexions.Count < 1 && i < 10; i++) { Thread.Sleep(200); }
            Assert.IsNotNull(objChannel);
            Assert.AreEqual(1, server.Connexions.Count);
            bool received = false;
            objChannel.MessagesReceived += delegate { received = true; };
            bool somethingReceived = false;
            foreach (IConnexion c in client.Connexions)
            {
                c.MessageReceived += delegate { somethingReceived = true; };
            }
            Assert.IsFalse(somethingReceived);
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send("this is a test", 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsFalse(received);
            Assert.IsTrue(objChannel.Count == 0);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new object(), 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsTrue(received);
            Assert.IsTrue(objChannel.Count > 0);
            Assert.IsNotNull(objChannel.DequeueMessage(0));
        }

        [Test]
        public void TestStringMessageMishandling()
        {
            IStringChannel strChannel = client.OpenStringChannel("127.0.0.1", "9999", 0, ChannelDeliveryRequirements.LeastStrict);
            for (int i = 0; server.Connexions.Count < 1 && i < 10; i++) { Thread.Sleep(200); }
            Assert.IsNotNull(strChannel);
            Assert.AreEqual(1, server.Connexions.Count);
            bool received = false;
            strChannel.MessagesReceived += delegate { received = true; };
            bool somethingReceived = false;
            foreach (IConnexion c in client.Connexions)
            {
                c.MessageReceived += delegate { somethingReceived = true; };
            }
            Assert.IsFalse(somethingReceived);
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new byte[1], 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsFalse(received);
            Assert.IsTrue(strChannel.Count == 0);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send("this is a test", 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsTrue(received);
            Assert.IsTrue(strChannel.Count > 0);
            Assert.IsNotNull(strChannel.DequeueMessage(0));
        }

        [Test]
        public void TestBinaryMessageMishandling()
        {
            IBinaryChannel binChannel = client.OpenBinaryChannel("127.0.0.1", "9999", 0, ChannelDeliveryRequirements.LeastStrict);
            for (int i = 0; server.Connexions.Count < 1 && i < 10; i++) { Thread.Sleep(200); }
            Assert.IsNotNull(binChannel);
            Assert.AreEqual(1, server.Connexions.Count);
            bool received = false;
            binChannel.MessagesReceived += delegate { received = true; };
            bool somethingReceived = false;
            foreach (IConnexion c in client.Connexions)
            {
                c.MessageReceived += delegate { somethingReceived = true; };
            }
            Assert.IsFalse(somethingReceived);
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send("this is a test", 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsFalse(received);
            Assert.IsTrue(binChannel.Count == 0);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new byte[1], 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsTrue(received);
            Assert.IsTrue(binChannel.Count > 0);
            Assert.IsNotNull(binChannel.DequeueMessage(0));
        }

        [Test]
        public void TestSessionMessageMishandling()
        {
            ISessionChannel sessChannel = client.OpenSessionChannel("127.0.0.1", "9999", 0, ChannelDeliveryRequirements.LeastStrict);
            for (int i = 0; server.Connexions.Count < 1 && i < 10; i++) { Thread.Sleep(200); }
            Assert.IsNotNull(sessChannel);
            Assert.AreEqual(1, server.Connexions.Count);
            bool received = false;
            sessChannel.MessagesReceived += delegate { received = true; };
            bool somethingReceived = false;
            foreach (IConnexion c in client.Connexions)
            {
                c.MessageReceived += delegate { somethingReceived = true; };
            }
            Assert.IsFalse(somethingReceived);
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send("this is a test", 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsFalse(received);
            Assert.IsTrue(sessChannel.Count == 0);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new SessionMessage(0, 0, SessionAction.Left), null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(received);
            Assert.IsTrue(sessChannel.Count > 0);
            Assert.IsNotNull(sessChannel.DequeueMessage(0));
        }

        [Test]
        public void TestStreamed1TupleMishandling()
        {
            IStreamedTuple<int> st = client.OpenStreamedTuple<int>("127.0.0.1", "9999",
                0, TimeSpan.FromSeconds(1), ChannelDeliveryRequirements.LeastStrict);
            for (int i = 0; server.Connexions.Count < 1 && i < 10; i++) { Thread.Sleep(200); }
            Assert.IsNotNull(st);
            Assert.AreEqual(1, server.Connexions.Count);
            bool received = false;
            st.StreamedTupleReceived += delegate { received = true; };
            bool somethingReceived = false;
            foreach (IConnexion c in client.Connexions)
            {
                c.MessageReceived += delegate { somethingReceived = true; };
            }
            Assert.IsFalse(somethingReceived);
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new byte[1], 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new TupleMessage(0, 0, "foo"), null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new TupleMessage(0, 0, 1), null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsTrue(received);
        }

        [Test]
        public void TestStreamed2TupleMishandling()
        {
            IStreamedTuple<int,int> st = client.OpenStreamedTuple<int,int>("127.0.0.1", "9999",
                0, TimeSpan.FromSeconds(1), ChannelDeliveryRequirements.LeastStrict);
            for (int i = 0; server.Connexions.Count < 1 && i < 10; i++) { Thread.Sleep(200); }
            Assert.IsNotNull(st);
            Assert.AreEqual(1, server.Connexions.Count);
            bool received = false;
            st.StreamedTupleReceived += delegate { received = true; };
            bool somethingReceived = false;
            foreach (IConnexion c in client.Connexions)
            {
                c.MessageReceived += delegate { somethingReceived = true; };
            }
            Assert.IsFalse(somethingReceived);
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new byte[1], 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new TupleMessage(0, 0, "foo", "bar"), null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new TupleMessage(0, 0, 1, 1), null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsTrue(received);
        }

        [Test]
        public void TestStreamed3TupleMishandling()
        {
            IStreamedTuple<int, int, int> st = client.OpenStreamedTuple<int, int, int>("127.0.0.1", "9999",
                0, TimeSpan.FromSeconds(1), ChannelDeliveryRequirements.LeastStrict);
            for (int i = 0; server.Connexions.Count < 1 && i < 10; i++) { Thread.Sleep(200); }
            Assert.IsNotNull(st);
            Assert.AreEqual(1, server.Connexions.Count);
            bool received = false;
            st.StreamedTupleReceived += delegate { received = true; };
            bool somethingReceived = false;
            foreach (IConnexion c in client.Connexions)
            {
                c.MessageReceived += delegate { somethingReceived = true; };
            }
            Assert.IsFalse(somethingReceived);
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new byte[1], 0, null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new TupleMessage(0, 0, "foo", "bar", "baz"), null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsFalse(received);

            foreach (IConnexion c in server.Connexions)
            {
                c.Send(new TupleMessage(0, 0, 1, 1, 1), null, ChannelDeliveryRequirements.LeastStrict);
            }
            UpdateClient(() => received);
            Assert.IsTrue(somethingReceived); somethingReceived = false;
            Assert.IsTrue(received);
        }

        private void UpdateClient(Returning<bool> condition)
        {
            for (int i = 0; !condition() && i < 10; i++)
            {
                Assert.IsFalse(errorOccurred, "some error occurred; see console");
                client.Update();
                Thread.Sleep(20);
            }
        }
    }

    [TestFixture]
    public class ZTClientRepeaterTests
    {
        IList<Client> clients = new List<Client>();
        ClientRepeater server;
        bool errorOccurred = false;
        IDictionary<int, IList<SessionMessage>> sessionMessages = new Dictionary<int, IList<SessionMessage>>();

        [SetUp]
        public void SetUp()
        {
            errorOccurred = false;
            server = new ClientRepeater(new LocalServerConfiguration(9999));
            server.ErrorEvent += delegate(ErrorSummary es)
            {
                Console.WriteLine("SERVER ERROR: " + es);
                errorOccurred = true;
            };
            server.Start();
        }

        [TearDown]
        public void TearDown()
        {
            server.Stop();
            foreach (Client client in clients) { client.Stop(); }
        }


        [Test]
        public void TestNewlyConnectedSentLives()
        {
            IList<int> orderedIdentities = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                Client client = new LocalClientConfiguration().BuildClient();
                client.Start();
                client.ErrorEvent += delegate(ErrorSummary es) {
                    Console.WriteLine("CLIENT ERROR: " + es);
                    errorOccurred = true;
                };
                ISessionChannel ss = client.OpenSessionChannel("localhost", "9999", 0, 
                    ChannelDeliveryRequirements.SessionLike);
                ss.MessagesReceived += delegate(ISessionChannel channel) {
                    SessionMessage sm; 
                    while((sm = channel.DequeueMessage(0)) != null)
                    {
                        lock(this)
                        {
                            if(!sessionMessages.ContainsKey(channel.Identity))
                            {
                                sessionMessages[ss.Identity] = new List<SessionMessage>();
                                orderedIdentities.Add(ss.Identity);
                            }
                            sessionMessages[ss.Identity].Add(sm);
                        }
                    }
                };
                Assert.IsFalse(errorOccurred);
                clients.Add(client);
                Thread.Sleep(100);
                foreach (Client c in clients)
                {
                    c.Update();
                    Assert.IsFalse(errorOccurred);
                }
            }

            Assert.AreEqual(orderedIdentities.Count, clients.Count);
            for (int i = 0; i < clients.Count; i++)
            {
                int id = orderedIdentities[i];
                // each client i should have 5 - i Joineds and i Lives
                int joined = 0;
                int lives = 0;
                foreach(SessionMessage sm in sessionMessages[id])
                {
                    if(sm.Action == SessionAction.Joined) { joined++; }
                    if(sm.Action == SessionAction.Lives) { lives++; }
                }
                Assert.AreEqual(clients.Count - i, joined);
                Assert.AreEqual(i, lives);
            }
        }

    }

    /// <summary>
    /// Test basic GT functionality
    /// </summary>
    [TestFixture]
    public class ZSChannelTests
    {
        private bool verbose = false;
        private bool errorOccurred;
        private bool responseReceived;
        private Client client;
        private EchoingServer server;

        private static TimeSpan ServerSleepTime = TimeSpan.FromMilliseconds(20);
        private static TimeSpan ClientSleepTime = TimeSpan.FromMilliseconds(25);
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
            PerformEchos(new SpecificTransportChannelDeliveryRequirements(typeof(LocalTransport)));
        }

        [Test]
        public void EchoStringViaTCP()
        {
            PerformEchos(new SpecificTransportChannelDeliveryRequirements(typeof(TcpTransport)));
        }

        [Test]
        public void EchoStringViaUDP()
        {
            PerformEchos(new SpecificTransportChannelDeliveryRequirements(typeof(BaseUdpTransport)));
        }

        [Test]
        public void TestMessageEvents()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new LocalClientConfiguration().BuildClient();  //this is a client
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
                c.PingReplied += delegate(ITransport transport, uint sequence, TimeSpan roundtrip) { pingReceived = true; };
            };
            client.ConnexionRemoved += delegate(Communicator ignored, IConnexion c) { connexionRemoved = true; };

            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            {
                Debug("Client: sending greeting: " + EXPECTED_GREETING);
                IStringChannel strChannel = client.OpenStringChannel("127.0.0.1", "9999", 0,
                    ChannelDeliveryRequirements.CommandsLike);  //connect here
                strChannel.MessagesReceived += ClientStringMessageReceivedEvent;
                strChannel.Send(EXPECTED_GREETING);  //send a string
                CheckForResponse();
                Assert.AreEqual(1, strChannel.Messages.Count);
                string s = strChannel.DequeueMessage(0);
                Assert.IsNotNull(s);
                Assert.AreEqual(EXPECTED_RESPONSE, s);
                strChannel.MessagesReceived -= ClientStringMessageReceivedEvent;
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

            client = new LocalClientConfiguration().BuildClient(); //this is a client
            client.ErrorEvent += client_ErrorEvent; //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            IStringChannel strChannel = client.OpenStringChannel("127.0.0.1", "9999", 0,
                ChannelDeliveryRequirements.CommandsLike); //connect here
            strChannel.MessagesReceived += ClientStringMessageReceivedEvent;
            strChannel.Send(EXPECTED_GREETING); //send a string
            CheckForResponse();

            Assert.IsFalse(strChannel.Identity == 0, "Unique identity should be received");
        }

        [Test]
        public void TestGetStreamDisconnects()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new LocalClientConfiguration().BuildClient(); //this is a client
            client.ErrorEvent += client_ErrorEvent; //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            IStringChannel origChannel = client.OpenStringChannel("127.0.0.1", "9999", 0,
                ChannelDeliveryRequirements.CommandsLike); //connect here

            Assert.IsTrue(client.Connexions.Count == 1);
            foreach (IConnexion cnx in client.Connexions) {
                cnx.ShutDown(); cnx.Dispose();
            }
            Assert.IsFalse(origChannel.Connexion.Active, "connexion should now be closed");
            
            IStringChannel newChannel = client.OpenStringChannel("127.0.0.1", "9999", 0,
                ChannelDeliveryRequirements.CommandsLike); //connect here
            Assert.IsTrue(newChannel.Connexion.Active, "a new channel should have been provided");
        }

        [Test]
        public void TestClientShutDown()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new LocalClientConfiguration().BuildClient();  //this is a client
            client.ErrorEvent += client_ErrorEvent;  //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            {
                Debug("Client: sending greeting: " + EXPECTED_GREETING);
                IStringChannel strChannel = client.OpenStringChannel("127.0.0.1", "9999", 0,
                    ChannelDeliveryRequirements.CommandsLike);  //connect here
                strChannel.MessagesReceived += ClientStringMessageReceivedEvent;
                strChannel.Send(EXPECTED_GREETING);  //send a string
                CheckForResponse();
                Assert.AreEqual(1, strChannel.Messages.Count);
                string s = strChannel.DequeueMessage(0);
                Assert.IsNotNull(s);
                Assert.AreEqual(EXPECTED_RESPONSE, s);
                strChannel.MessagesReceived -= ClientStringMessageReceivedEvent;
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

            client = new LocalClientConfiguration().BuildClient(); //this is a client
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
                IStringChannel strChannel = client.OpenStringChannel("127.0.0.1", "9999", 0,
                    new SpecificTransportChannelDeliveryRequirements(typeof(LocalTransport))); //connect here
                strChannel.MessagesReceived += ClientStringMessageReceivedEvent;
                Assert.IsTrue(connexionAdded, "should have connected");

                for (int i = 0; i < 5; i++)
                {
                    strChannel.Send(EXPECTED_GREETING,
                        new MessageDeliveryRequirements(Reliability.Reliable,
                            MessageAggregation.Aggregatable, Ordering.Unordered));
                    ProcessEvents();
                    Assert.AreEqual(0, messagesReceived);
                    Assert.AreEqual(0, messagesSent);
                    Assert.AreEqual(0, strChannel.Messages.Count);
                }

                strChannel.Send(EXPECTED_GREETING);
                Assert.AreEqual(6, messagesSent);
                ProcessEvents();
                Assert.AreEqual(6, messagesReceived);
                Assert.AreEqual(6, strChannel.Messages.Count);
            }
        }

        [Test]
        public void TestFreshnessHandling()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new LocalClientConfiguration().BuildClient(); //this is a client
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
                ChannelDeliveryRequirements cdr = new SpecificTransportChannelDeliveryRequirements(typeof(LocalTransport));
                cdr.Aggregation = MessageAggregation.Aggregatable;
                cdr.Freshness = Freshness.IncludeLatestOnly;
                IStringChannel strChannel = client.OpenStringChannel("127.0.0.1", "9999", 0, cdr);
                strChannel.MessagesReceived += ClientStringMessageReceivedEvent;
                Assert.IsTrue(connexionAdded, "should have connected");

                for (int i = 0; i < 5; i++)
                {
                    strChannel.Send(EXPECTED_GREETING);
                    ProcessEvents();
                    Assert.AreEqual(0, messagesReceived);
                    Assert.AreEqual(0, messagesSent);
                    Assert.AreEqual(0, strChannel.Messages.Count);
                }

                strChannel.Flush();
                Assert.AreEqual(1, messagesSent);
                ProcessEvents();
                Assert.AreEqual(1, messagesReceived);
                Assert.AreEqual(1, strChannel.Messages.Count);
            }
        }

        [Test]
        public void TestMultipleChannels()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new LocalClientConfiguration().BuildClient(); //this is a client
            client.ErrorEvent += client_ErrorEvent; //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            IStringChannel ch1 = client.OpenStringChannel("127.0.0.1", "9999", 0,
                ChannelDeliveryRequirements.CommandsLike);
            IStringChannel ch2 = client.OpenStringChannel("127.0.0.1", "9999", 0,
                ChannelDeliveryRequirements.CommandsLike);
            Assert.IsTrue(ch1 != ch2);
            Assert.IsTrue(ch1.Active && ch2.Active);
            Assert.IsTrue(ch1.Connexion == ch2.Connexion);

            int mr = 0;
            ch1.MessagesReceived += ClientStringMessageReceivedEvent;
            ch1.MessagesReceived += delegate { mr++; };
            ch2.MessagesReceived += delegate { mr++; };

            ch1.Send(EXPECTED_GREETING);
            CheckForResponse();
            Assert.AreEqual(2, mr);
            Assert.AreEqual(1, ch1.Count);
            Assert.AreEqual(1, ch2.Count);

            ch1.Dispose();
            ch2.Dispose();
            try
            {
                ch1.Send(EXPECTED_GREETING);
                Assert.Fail("Send on a disposed channel should have failed");
            }
            catch (InvalidStateException)
            {
                /*expected*/
            }
        }

        /// <summary>
        /// Ensure that there is no bleed-through between different channels
        /// on the same connexion
        /// </summary>
        [Test]
        public void TestDifferentChannelsNotConfused()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new LocalClientConfiguration().BuildClient(); //this is a client
            client.ErrorEvent += client_ErrorEvent; //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            IStringChannel ch1 = client.OpenStringChannel("127.0.0.1", "9999", 0,
                ChannelDeliveryRequirements.CommandsLike);
            IStringChannel ch2 = client.OpenStringChannel(ch1.Connexion, 1,
                ChannelDeliveryRequirements.CommandsLike);
            Assert.IsTrue(ch1 != ch2);
            Assert.IsTrue(ch1.Active && ch2.Active);
            Assert.IsTrue(ch1.Connexion == ch2.Connexion);

            int mr = 0;
            ch1.MessagesReceived += ClientStringMessageReceivedEvent;
            ch1.MessagesReceived += delegate { mr++; };
            ch2.MessagesReceived += delegate { mr++; };

            ch1.Send(EXPECTED_GREETING);
            CheckForResponse();
            Assert.AreEqual(1, mr);
            Assert.AreEqual(1, ch1.Count);
            Assert.AreEqual(0, ch2.Count);

            ch1.Dispose();
            ch2.Dispose();
        }

        [Test]
        public void TestRejectAllIncomingConnections()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);
            foreach (IAcceptor acc in server.Server.Acceptors)
            {
                acc.ValidateTransport += ((sender, e) => e.Reject("because I can"));
            }

            client = new TestClientConfiguration().BuildClient(); //this is a client
            client.ErrorEvent += client_ErrorEvent; //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);

            IStringChannel ch1 = client.OpenStringChannel("127.0.0.1", "9999", 0,
                    ChannelDeliveryRequirements.CommandsLike);
            for (int i = 0; i < 20; i++)
            {
                client.Update();
                Assert.AreEqual(0, server.Server.Connexions.Count);
                client.Sleep(TimeSpan.FromMilliseconds(100));
            }
            Assert.AreEqual(0, client.Connexions.Count);
        }

        #endregion

        #region Test Reentrancy

        public void ReentrantFramework(Action<Client> clientSetup, Action<Server> serverSetup)
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new TestClientConfiguration().BuildClient();  //this is a client
            client.ErrorEvent += client_ErrorEvent;  //triggers if there is an error
            client.Start();
            Assert.IsFalse(errorOccurred);
            Assert.IsFalse(responseReceived);
            if (clientSetup != null) { clientSetup(client); }
            if (serverSetup != null) { serverSetup(server.Server); }
            Assert.IsTrue(client.Active);
            {
                // Console.WriteLine("Client: sending greeting: " + EXPECTED_GREETING);
                IStringChannel strStream = client.OpenStringChannel("127.0.0.1", "9999", 0,
                    new SpecificTransportChannelDeliveryRequirements(typeof (TcpTransport))); //connect here
                strStream.MessagesReceived += ClientStringMessageReceivedEvent;
                try
                {
                    strStream.Send(EXPECTED_GREETING); //send a string
                } catch(InvalidStateException e) {
                    // this is ok
                    return;
                }
                int repeats = 10;
                while (client.Active && !responseReceived && repeats-- > 0)
                {
                    client.Update(); // let the client check the network
                    client.Sleep(ClientSleepTime);
                }
                while (strStream.Count > 0)
                {
                    string s = strStream.DequeueMessage(0);
                    Assert.IsNotNull(s);
                    Assert.AreEqual(EXPECTED_RESPONSE, s);
                }
                strStream.MessagesReceived -= ClientStringMessageReceivedEvent;
            }

            client.Stop();
        }

        [Test]
        public void TestClientMessageReceived()
        {
            ReentrantFramework(c => c.ConnexionAdded +=
                delegate(Communicator comm, IConnexion cnx) { cnx.MessageReceived += delegate { c.Dispose(); }; }, null);
        }

        [Test]
        public void TestServerMessageReceived()
        {
            ReentrantFramework(null, s => s.MessageReceived += delegate { s.Dispose(); });
        }

        [Test]
        public void TestClientMessageSent()
        {
            ReentrantFramework(c => c.ConnexionAdded +=
                delegate(Communicator comm, IConnexion cnx) { cnx.MessageSent += delegate { c.Dispose(); }; }, null);
        }

        [Test]
        public void TestServerMessageSent()
        {
            ReentrantFramework(null, s => s.MessagesSent += delegate { s.Dispose(); });
        }

        [Test]
        public void TestClientConnexionAdded()
        {
            ReentrantFramework(c => c.ConnexionAdded += delegate { c.Dispose(); }, null);
        }

        [Test]
        public void TestServerConnexionAdded()
        {
            ReentrantFramework(null, s => s.ClientsJoined += delegate { s.Dispose(); });
        }

        [Test]
        public void TestClientConnexionRemoved()
        {
            ReentrantFramework(c => c.ConnexionRemoved += delegate { c.Dispose(); }, null);
        }

        [Test]
        public void TestServerConnexionRemoved()
        {
            ReentrantFramework(null, s => s.ClientsRemoved += delegate { s.Dispose(); });
        }

        [Test]
        public void TestClientTransportAdded()
        {
            ReentrantFramework(c => c.ConnexionAdded +=
                delegate(Communicator comm, IConnexion cnx) { cnx.TransportAdded += delegate { c.Dispose(); }; }, null);
        }

        [Test]
        public void TestServerTransportAdded()
        {
            ReentrantFramework(null, s => s.ClientsJoined +=
                delegate(ICollection<IConnexion> list)
                {
                    foreach (IConnexion cnx in list) { cnx.TransportAdded += delegate { s.Dispose(); }; }
                });
        }

        [Test]
        public void TestClientTransportRemoved()
        {
            ReentrantFramework(c => c.ConnexionAdded +=
                delegate(Communicator comm, IConnexion cnx) { cnx.TransportRemoved += delegate { c.Dispose(); }; }, null);
        }

        [Test]
        public void TestServerTransportRemoved()
        {
            ReentrantFramework(null, s => s.ClientsJoined +=
                delegate(ICollection<IConnexion> list)
                {
                    foreach(IConnexion cnx in list) { cnx.TransportRemoved += delegate { s.Dispose(); };}
                });
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
                IStringChannel strChannel = client.OpenStringChannel("127.0.0.1", "9999", 0, cdr);  //connect here
                strChannel.MessagesReceived += ClientStringMessageReceivedEvent;
                strChannel.Send(EXPECTED_GREETING);  //send a string
                CheckForResponse();
                Assert.AreEqual(1, strChannel.Messages.Count);
                string s = strChannel.DequeueMessage(0);
                Assert.IsNotNull(s);
                Assert.AreEqual(EXPECTED_RESPONSE, s);
                strChannel.MessagesReceived -= ClientStringMessageReceivedEvent;
            }

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            {
                byte[] sentBytes = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                Debug("Client: sending byte message: [0 1 2 3 4 5 6 7 8 9]");
                IBinaryChannel binChannel = client.OpenBinaryChannel("127.0.0.1", "9999", 0, cdr);  //connect here
                binChannel.MessagesReceived += ClientBinaryMessageReceivedEvent;
                binChannel.Send(sentBytes);
                CheckForResponse();
                Assert.AreEqual(1, binChannel.Messages.Count);
                byte[] bytes = binChannel.DequeueMessage(0);
                Assert.IsNotNull(bytes);
                Assert.AreEqual(sentBytes, bytes);
                binChannel.MessagesReceived -= ClientBinaryMessageReceivedEvent;
            }

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            {
                Debug("Client: sending greeting: list(\"hello\",\"world\")");
                IObjectChannel objChannel = client.OpenObjectChannel("127.0.0.1", "9999", 0, cdr);  //connect here
                objChannel.MessagesReceived += ClientObjectMessageReceivedEvent;
                objChannel.Send(new List<string>(new string[] { "hello", "world" }));  //send a string
                CheckForResponse();
                Assert.AreEqual(1, objChannel.Messages.Count);
                object o = objChannel.DequeueMessage(0);
                Assert.IsNotNull(o);
                Assert.IsInstanceOfType(typeof(List<string>), o);
                Assert.AreEqual(new List<string>(new string[] { "hello", "world" }), o);
                objChannel.MessagesReceived -= ClientObjectMessageReceivedEvent;
            }

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            {
                Debug("Client: sending greeting: SessionAction.Joined");
                ISessionChannel sessChannel = client.OpenSessionChannel("127.0.0.1", "9999", 0, cdr);  //connect here
                sessChannel.MessagesReceived += ClientSessionMessageReceivedEvent;
                sessChannel.Send(SessionAction.Joined);  //send a string
                CheckForResponse();
                Assert.AreEqual(1, sessChannel.Messages.Count);
                SessionMessage sm = sessChannel.DequeueMessage(0);
                Assert.IsNotNull(sm);
                Assert.AreEqual(sm.Action, SessionAction.Joined);
                sessChannel.MessagesReceived -= ClientSessionMessageReceivedEvent;
            }

            responseReceived = false;
            Assert.IsFalse(responseReceived);
            Assert.IsFalse(errorOccurred);

            {
                Debug("Client: sending tuple message: [-1, 0, 1]");
                IStreamedTuple<int, int, int> tupleStream = 
                    client.OpenStreamedTuple<int, int, int>("127.0.0.1", "9999", 0, 
                    TimeSpan.FromMilliseconds(20), cdr);
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

        void ClientStringMessageReceivedEvent(IStringChannel channel)
        {
            Debug("Client: received a string response\n");
            responseReceived = true;
        }

        void ClientBinaryMessageReceivedEvent(IBinaryChannel channel)
        {
            Debug("Client: received a byte[] response\n");
            responseReceived = true;
        }

        void ClientObjectMessageReceivedEvent(IObjectChannel channel)
        {
            Debug("Client: received a object response\n");
            responseReceived = true;
        }

        void ClientSessionMessageReceivedEvent(ISessionChannel channel)
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
            Console.WriteLine("Client: " + es);
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
                    new AggregatingSharedDictionary(c.OpenObjectChannel("127.0.0.1", "9678", 0,
                        new ChannelDeliveryRequirements(Reliability.Reliable, MessageAggregation.Aggregatable,
                            Ordering.Ordered)), TimeSpan.FromMilliseconds(20));
                d.Changed += DictionaryChanged;
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
                    c.Update(); c.Sleep(TimeSpan.FromMilliseconds(10));  
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

        public override IConnexion CreateServerConnexion(Client owner, string address, string port)
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
                client.Sleep(TimeSpan.FromMilliseconds(20));
            }
            client.Update();
        }

        [Test]
        public void TestReconnect()
        {
            client = new TcpClientConfiguration().BuildClient();
            client.Start();

            IStringChannel channel = client.OpenStringChannel("localhost", "9999", 0, 
                ChannelDeliveryRequirements.MostStrict);
            Assert.AreEqual(1, client.Connexions.Count);
            Assert.IsInstanceOfType(typeof(DebuggingServerConnexion), new List<IConnexion>(client.Connexions)[0]);
            DebuggingServerConnexion sc = (DebuggingServerConnexion)new List<IConnexion>(client.Connexions)[0];
            Assert.AreEqual(1, sc.Transports.Count);
            Assert.IsInstanceOfType(typeof(TcpTransport), sc.Transports[0]);
            TcpTransport transport = (TcpTransport)sc.Transports[0];

            channel.Send("foo");
            CheckUpdates();
            Assert.IsTrue(channel.Count > 0);
            Assert.AreEqual("foo", channel.DequeueMessage(0));

            sc.TriggerTransportError(transport);

            Assert.AreEqual(1, sc.Transports.Count);
            Assert.IsInstanceOfType(typeof(TcpTransport), sc.Transports[0]);
            Assert.IsFalse(transport == sc.Transports[0]);  // should be a new connection

            channel.Send("foo");
            CheckUpdates();
            Assert.IsTrue(channel.Count > 0);
            Assert.AreEqual("foo", channel.DequeueMessage(0));
        }
    }

    [TestFixture]
    public class ZTTestPingBasedDisconnect
    {
        [Test]
        public void TestClientBasedDisconnect()
        {
            Server server = new TestServerConfiguration(9854).BuildServer();
            server.Configuration.PingInterval = TimeSpan.FromDays(5);
            server.StartSeparateListeningThread();

            Client client = new TestClientConfiguration().BuildClient();
            client.Configuration.PingInterval = TimeSpan.FromDays(5);
            client.StartSeparateListeningThread();

            IStringChannel ch1 = client.OpenStringChannel("localhost", "9854", 0, ChannelDeliveryRequirements.ChatLike);
            Assert.AreEqual(1, client.Connexions.Count);
            Assert.AreEqual(1, server.Connexions.Count);
            Assert.IsTrue(ch1.Connexion.Transports.Count > 0);

            PingBasedDisconnector pbd = PingBasedDisconnector.Install(client, TimeSpan.FromMilliseconds(500));
            Thread.Sleep(1000);

            Assert.AreEqual(0, client.Connexions.Count);
 
            client.Dispose(); 
            server.Dispose();
        }

        [Test]
        public void TestServerBasedDisconnect()
        {
            Server server = new TestServerConfiguration(9854).BuildServer();
            server.Configuration.PingInterval = TimeSpan.FromDays(5);
            server.StartSeparateListeningThread();

            Client client = new TestClientConfiguration().BuildClient();
            client.Configuration.PingInterval = TimeSpan.FromDays(5);
            client.StartSeparateListeningThread();

            IStringChannel ch1 = client.OpenStringChannel("localhost", "9854", 0, ChannelDeliveryRequirements.ChatLike);
            Assert.AreEqual(1, client.Connexions.Count);
            Assert.AreEqual(1, server.Connexions.Count);
            Assert.IsTrue(ch1.Connexion.Transports.Count > 0);

            PingBasedDisconnector pbd = PingBasedDisconnector.Install(server, TimeSpan.FromMilliseconds(500));
            Thread.Sleep(1000);

            Assert.AreEqual(0, server.Connexions.Count);

            client.Dispose();
            server.Dispose();
        }

        [Test]
        public void TestPBDCanBeStoppedAndStarted()
        {
            Server server = new TestServerConfiguration(9854).BuildServer();
            server.Configuration.PingInterval = TimeSpan.FromDays(5);
            server.StartSeparateListeningThread();

            Client client = new TestClientConfiguration().BuildClient();
            client.Configuration.PingInterval = TimeSpan.FromDays(5);
            client.StartSeparateListeningThread();

            IStringChannel ch1 = client.OpenStringChannel("localhost", "9854", 0, ChannelDeliveryRequirements.ChatLike);
            Assert.AreEqual(1, client.Connexions.Count);
            Assert.AreEqual(1, server.Connexions.Count);
            Assert.IsTrue(ch1.Connexion.Transports.Count > 0);

            int removed = 0;
            ch1.Connexion.TransportRemoved += (c,t) => removed++;

            PingBasedDisconnector pbd = PingBasedDisconnector.Install(client, TimeSpan.FromMilliseconds(500));
            pbd.Stop();
            Thread.Sleep(1000);

            Assert.AreEqual(1, client.Connexions.Count);
            Assert.AreEqual(0, removed);

            pbd.Start();
            Thread.Sleep(1000);
            Assert.AreEqual(0, client.Connexions.Count);
            Assert.IsTrue(removed > 0);
 
            client.Dispose(); 
            server.Dispose();
        }


    }


}
