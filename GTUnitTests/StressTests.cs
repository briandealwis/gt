using System;
using System.Collections.Generic;
using System.Threading;
using GT.Net;
using NUnit.Framework;

namespace GT.UnitTests
{
    public class StressingServer : EchoingServer
    {
        public int minSleep = 0, maxSleep = 2000;  // each client should sleep between 0 to 2000 ms
        public int numberSenders = 10;

        private bool errorOccurred = false;
        private bool running = false;
        private IList<Thread> threads = new List<Thread>();
        private Random random = new Random();

        public StressingServer(int port)
            : base(port) {}

        public bool ErrorOccurred { get { return errorOccurred; } }

        public override void Start()
        {
            running = true;
            base.Start();
            Server.MessageReceived += server_MessageReceived;
            Server.ErrorEvent += server_ErrorEvent;

            for (int i = 0; i < 10 * numberSenders; i++)
            {
                Thread t = new Thread(server_StartThread);
                t.IsBackground = true;
                t.Name = "Server thread #" + i;
                t.Start(i);
                threads.Add(t);
            }

        }

        public override void Stop()
        {
            running = false;

            foreach (Thread t in threads)
            {
                t.Abort();
            }

            base.Stop();
        }

        private void server_ErrorEvent(ErrorSummary summary)
        {
            Console.WriteLine("Server: {0}", summary);
            errorOccurred = summary.Severity >= Severity.Warning;
        }

        private void server_StartThread(object obj)
        {
            try
            {
                while (running)
                {
                    while (Server.Clients.Count == 0)
                    {
                        Console.Write("No connected clients; sleeping...\r");
                        Thread.Sleep(10000 + random.Next(-5000, 5000));
                    }
                    int index = random.Next(0, Server.Clients.Count - 1);
                    switch (random.Next(0, 2))
                    {
                        case 0:
                            Console.Write('O');
                            server_SendMessage(new ObjectMessage(0, new object()), index);
                            break;

                        case 1:
                            Console.Write('S');
                            server_SendMessage(new StringMessage(1, "its fleece was white as snow"),
                                index);
                            break;

                        case 2:
                            Console.Write('B');
                            server_SendMessage(new BinaryMessage(2, new byte[] { 0, 1, 2, 3, 4, 5 }),
                                index);
                            break;

                    }
                    Thread.Sleep(random.Next(minSleep, maxSleep));
                }
            }
            catch (ThreadAbortException) { }
        }


        private void server_MessageReceived(Message m, IConnexion client, ITransport transport)
        {
            // Randomly send a message elsewhere 10% of the time;
            // this has multiple senders from outside of the server's listening loop
            if (random.Next(0, 100) < 10)
            {
                return;
            }

            int clientNumber = random.Next(0, Server.Clients.Count - 1);
            server_SendMessage(m, clientNumber);
        }

        private void server_SendMessage(Message m, int clientNumber)
        {
            foreach (IConnexion c in Server.Clients)
            {
                if (--clientNumber == 0)
                {
                    c.Send(m, MessageDeliveryRequirements.LeastStrict, null);
                }
            }
        }


    }

    public class StressingClient : IDisposable
    {
        public int minSleep = 0, maxSleep = 2000;  // each client should sleep between 0 to 2000 ms
        public int numberSenders = 10;

        private string host;
        private string port;
        private Client client;

        private bool errorOccurred = false;
        private bool running = false;
        private IList<Thread> threads = new List<Thread>();
        private Random random = new Random();

        private IObjectStream objectStream;
        private IBinaryStream binaryStream;
        private IStringStream stringStream;

        public StressingClient(string host, string port)
        {
            this.host = host;
            this.port = port;
            client = new Client();
        }

        public bool ErrorOccurred { get { return errorOccurred; } }

        virtual public void Start()
        {
            running = true;
            client.Start();
            objectStream = client.GetObjectStream(host, port,
                0, ChannelDeliveryRequirements.MostStrict);
            objectStream.ObjectNewMessageEvent += client_ReceivedObjectMessage;
            stringStream = client.GetStringStream(host, port,
                1, ChannelDeliveryRequirements.MostStrict);
            stringStream.StringNewMessageEvent += client_ReceivedStringMessage;
            binaryStream = client.GetBinaryStream(host, port,
                2, ChannelDeliveryRequirements.MostStrict);
            binaryStream.BinaryNewMessageEvent += client_ReceivedBinaryMessage;

            client.StartSeparateListeningThread();
            for (int i = 0; i < numberSenders; i++)
            {
                Thread t = new Thread(client_StartThread);
                t.IsBackground = true;
                t.Name = "Client thread #" + i;
                t.Start(i);
                threads.Add(t);
            }

        }

        virtual public void Stop()
        {
            foreach (Thread t in threads)
            {
                t.Abort();
            }

        }

        virtual public void Dispose()
        {
            client.Stop();
        }

        private void client_StartThread(object obj)
        {
            try
            {
                while (running)
                {
                    switch (random.Next(0, 2))
                    {
                        case 0:
                            Console.Write('o');
                            objectStream.Send(new object());
                            break;

                        case 1:
                            Console.Write('s');
                            stringStream.Send("mary had a little lamb");
                            break;

                        case 2:
                            Console.Write('b');
                            binaryStream.Send(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                            break;

                    }
                    Thread.Sleep(random.Next(minSleep, maxSleep));
                }
            }
            catch (ThreadAbortException) { }
        }

        private void client_ReceivedBinaryMessage(IBinaryStream stream)
        {
            byte[] message;
            while ((message = stream.DequeueMessage(0)) != null) {
                if (random.Next(0, 100) < 10)
                {
                    binaryStream.Send(message);
                }
            }
        }

        private void client_ReceivedStringMessage(IStringStream stream)
        {
            string message;
            while ((message = stream.DequeueMessage(0)) != null)
            {
                if (random.Next(0, 100) < 10)
                {
                    stringStream.Send(message);
                }
            }
        }

        private void client_ReceivedObjectMessage(IObjectStream stream)
        {
            object message;
            while ((message = stream.DequeueMessage(0)) != null)
            {
                if (random.Next(0, 100) < 10)
                {
                    objectStream.Send(message);
                }
            }
        }


    }

    /// <summary>
    /// Test GT transports functionality
    /// </summary>
    [TestFixture]
    public class ZZZZStressTests
    {
        int serverPort = 9876;
        private EchoingServer server;
        bool running = false;

        private IList<StressingClient> clients = new List<StressingClient>();
        
        [Test]
        public void StressTest() {
            int totalSeconds = 30;
            Console.WriteLine("Starting Stress Test ({0} seconds)", totalSeconds);
            running = true;
            server = new EchoingServer(serverPort);
            server.Start();

            clients = new List<StressingClient>();
            for (int i = 0; i < 5; i++)
            {
                StressingClient c = new StressingClient("127.0.0.1", serverPort.ToString());
                c.Start();
                clients.Add(c);
            }

            Thread.Sleep(30000);
            running = false;
            Assert.IsFalse(server.ErrorOccurred);
            foreach (StressingClient c in clients)
            {
                Assert.IsFalse(c.ErrorOccurred);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (server != null)
            {
                try
                {
                    server.Stop();
                    server.Dispose();
                }
                catch (Exception) { }
            }

            if (clients != null)
            {
                foreach (StressingClient client in clients)
                {
                    try
                    {
                        client.Stop();
                        client.Dispose();
                    }
                    catch (Exception) {}
                }
            }
        }

    }
}