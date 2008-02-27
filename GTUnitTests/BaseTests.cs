using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using NUnit.Framework;
using GT.Common;
using GT.Clients;
using GT.Servers;
 
namespace GT.UnitTests.BaseTests
{
    /// <summary>Test basic converter functionality</summary>
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
    /// Test basic GT functionality
    /// </summary>
    [TestFixture]
    public class StringStreamTests {

        private Boolean errorOccurred;
        private Boolean responseReceived;
        private Client client;

        private static string EXPECTED_GREETING = "Hello!";
        private static string EXPECTED_RESPONSE = "Go Away!";
        private static int ServerSleepTime = 10;
        private static int ClientSleepTime = 10;

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

        private void ServerStringMessageReceived(byte[] b, byte id, Server.Client client, MessageProtocol protocol)
        {
            string s = Server.BytesToString(b);
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
            server.Send(EXPECTED_RESPONSE, id, clientGroup, protocol);
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
            client.ErrorEvent += new ErrorEventHandler(client_ErrorEvent);  //triggers if there is an error
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
            Assert.AreEqual(s, EXPECTED_RESPONSE);
        }

        [Test]
        public void EchoStringViaUDP()
        {
            StartExpectedResponseServer(EXPECTED_GREETING, EXPECTED_RESPONSE);

            client = new Client();  //this is a client
            client.ErrorEvent += new ErrorEventHandler(client_ErrorEvent);  //triggers if there is an error
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
            Assert.AreEqual(s, EXPECTED_RESPONSE);
        }


        void ClientStringMessageReceivedEvent(IStringStream stream)
        {
            Console.WriteLine("Client: received a response\n");
            responseReceived = true;
        }

        /// <summary>This is triggered if something goes wrong</summary>
        void client_ErrorEvent(Exception e, SocketError se, ServerStream ss, string explanation)
        {
            Console.WriteLine("Error: " + explanation + "\n" + e.ToString());
            errorOccurred = true;
        }

        #endregion
    }
}
