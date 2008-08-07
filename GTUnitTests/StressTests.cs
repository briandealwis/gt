using System;
using System.Collections.Generic;
using System.Threading;
using GT.Net;
using NUnit.Framework;

namespace GT.UnitTests
{

    /// <summary>
    /// Test GT transports functionality
    /// </summary>
    [TestFixture]
    public class ZZZZStressTests
    {
        int serverPort = 9876;
        bool running = false;
        bool errorOccurred = false;
        private EchoingServer server;
        private IList<Client> clients = new List<Client>();
        private IList<Thread> threads = new List<Thread>();
        private IList<IObjectStream> objectStreams = new List<IObjectStream>();
        private IList<IBinaryStream> binaryStreams = new List<IBinaryStream>();
        private IList<IStringStream> stringStreams = new List<IStringStream>();
        private Random random = new Random();

        int minSleep = 0, maxSleep = 2000;  // each client should sleep between 0 to 2000 ms
        
        [Test]
        public void StressTest() {
            running = true;
            server = new EchoingServer(serverPort);
            server.Start();
            server.Server.MessageReceived += server_MessageReceived;
            server.Server.ErrorEvent += server_ErrorEvent;

            clients = new List<Client>();
            for (int i = 0; i < 5; i++)
            {
                Client c = new Client();
                c.Start();
                IObjectStream os = c.GetObjectStream("127.0.0.1", serverPort.ToString(),
                    0, ChannelDeliveryRequirements.MostStrict);
                os.ObjectNewMessageEvent += client_ReceivedObjectMessage;
                IStringStream ss = c.GetStringStream("127.0.0.1", serverPort.ToString(),
                    1, ChannelDeliveryRequirements.MostStrict);
                ss.StringNewMessageEvent += client_ReceivedStringMessage;
                IBinaryStream bs = c.GetBinaryStream("127.0.0.1", serverPort.ToString(),
                    2, ChannelDeliveryRequirements.MostStrict);
                bs.BinaryNewMessageEvent += client_ReceivedBinaryMessage;

                clients.Add(c);
                objectStreams.Add(os);
                binaryStreams.Add(bs);
                stringStreams.Add(ss);
                c.StartSeparateListeningThread();
            }
            for (int i = 0; i < 10 * clients.Count; i++)
            {
                Thread t = new Thread(client_StartThread);
                t.IsBackground = true;
                t.Name = "Client thread #" + i;
                t.Start(i);
                threads.Add(t);
            }
            for (int i = 0; i < 10 * clients.Count; i++)
            {
                Thread t = new Thread(server_StartThread);
                t.IsBackground = true;
                t.Name = "Server thread #" + i;
                t.Start(i);
                threads.Add(t);
            }

            Thread.Sleep(30000);
            running = false;
            Assert.IsFalse(errorOccurred);
        }

        private void server_ErrorEvent(ErrorSummary summary)
        {
            Console.WriteLine("Server: {0}", summary);
            errorOccurred = summary.Severity >= Severity.Error;
        }

        private void client_StartThread(object obj)
        {
            try
            {
                int index = (int)random.Next(0, clients.Count - 1);
                while (running)
                {
                    switch (random.Next(0, 2))
                    {
                        case 0:
                            objectStreams[index].Send(new object());
                            break;

                        case 1:
                            stringStreams[index].Send("mary had a little lamb " + index);
                            break;

                        case 2:
                            binaryStreams[index].Send(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, (byte)index });
                            break;

                    }
                    Thread.Sleep(random.Next(minSleep, maxSleep));
                }
            }
            catch (ThreadAbortException) {}
        }

        private void client_ReceivedBinaryMessage(IBinaryStream stream)
        {
            while (stream.DequeueMessage(0) != null) {}
        }

        private void client_ReceivedStringMessage(IStringStream stream)
        {
            while (stream.DequeueMessage(0) != null) { }
        }

        private void client_ReceivedObjectMessage(IObjectStream stream)
        {
            while (stream.DequeueMessage(0) != null) { }
        }

        private void server_StartThread(object obj)
        {
            try
            {
                int index = (int)random.Next(0, server.Server.Clients.Count - 1);
                while (running)
                {
                    switch (random.Next(0, 2))
                    {
                        case 0:
                            server_SendMessage(new ObjectMessage(0, new object()), index);
                            break;

                        case 1:
                            server_SendMessage(new StringMessage(1, "its fleece was white as snow"),
                                index);
                            break;

                        case 2:
                            server_SendMessage(new BinaryMessage(2, new byte[] { 0, 1, 2, 3, 4, 5 }),
                                index);
                            break;

                    }
                    Thread.Sleep(random.Next(minSleep, maxSleep));
                }
            }
            catch (ThreadAbortException) {}
        }


        private void server_MessageReceived(Message m, IConnexion client, ITransport transport)
        {
            // Randomly send a message elsewhere 10% of the time;
            // this has multiple senders from outside of the server's listening loop
            if (random.Next(0, 100) < 10)
            {
                return;
            }

            int clientNumber = random.Next(0, server.Server.Clients.Count - 1);
            server_SendMessage(m, clientNumber);
        }

        private void server_SendMessage(Message m, int clientNumber)
        {
            foreach (IConnexion c in server.Server.Clients)
            {
                if (--clientNumber == 0)
                {
                    c.Send(m, MessageDeliveryRequirements.LeastStrict, null);
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach(Thread t in threads)
            {
                t.Abort();
            }

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
                foreach (Client client in clients)
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