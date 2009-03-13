﻿using System;
using System.Collections.Generic;
using System.Threading;
using GT.Net;
using NUnit.Framework;
using GT.Utils;
using System.Collections;

namespace GT.UnitTests
{
    internal class StandardObjects
    {
        internal static readonly byte[] ByteMessage = new byte[] { 0, 1, 2, 3, 4, 5 };
        internal static readonly string StringMessage = "This is a test of faith";
        internal static readonly object ObjectMessage = new List<string>(new[] { "hello", "world" });

        public static bool Equivalent(object a, object b) {
            if (a.GetType() != b.GetType()) { return false; }
            if (a is IList) { return Equivalent((IList)a, (IList)b); }
            return false;
        }

        public static bool Equivalent(IList a, IList b)
        {
            if (a.Count != b.Count) { return false; }
            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i])) { return false; }
            }
            return true;
        }
    }

    public class StressingServer : EchoingServer
    {
        public int minSleep = 0, maxSleep = 2000;  // each client should sleep between 0 to 2000 ms
        public int numberSenders = 10;

        private bool running = false;
        private readonly IList<Thread> threads = new List<Thread>();
        private readonly Random random = new Random();

        public StressingServer(int port)
            : base(port) {}

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
                    switch (random.Next(3))     // integer >= 0 but < 3
                    {
                        case 0:
                            Console.Write('O');
                            server_SendMessage(new ObjectMessage(0, StandardObjects.ObjectMessage), index);
                            break;

                        case 1:
                            Console.Write('S');
                            server_SendMessage(new StringMessage(1, StandardObjects.StringMessage),
                                index);
                            break;

                        case 2:
                            Console.Write('B');
                            server_SendMessage(new BinaryMessage(2, StandardObjects.ByteMessage),
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
            switch (m.MessageType)
            {
            case MessageType.Binary:
                if (!ByteUtils.Compare(((BinaryMessage) m).Bytes, StandardObjects.ByteMessage))
                {
                    Console.WriteLine("Invalid byte message: {0}",
                        ByteUtils.DumpBytes(((BinaryMessage) m).Bytes));
                    errorOccurred = true;
                }
                break;
            case MessageType.String:
                if (!StandardObjects.StringMessage.Equals(((StringMessage) m).Text))
                {
                    Console.WriteLine("Invalid strings message: {0}",
                        ((StringMessage) m).Text);
                    errorOccurred = true;
                }
                break;

            case MessageType.Object:
                if (!StandardObjects.Equivalent(StandardObjects.ObjectMessage, ((ObjectMessage) m).Object))
                {
                    Console.WriteLine("Invalid object message: {0}",
                        ((ObjectMessage) m).Object);
                    errorOccurred = true;
                }
                break;
            }

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
        private readonly IList<Thread> threads = new List<Thread>();
        private readonly Random random = new Random();

        private IObjectStream objectStream;
        private IBinaryStream binaryStream;
        private IStringStream stringStream;

        public Client Client { get { return client; } }

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
            objectStream.MessagesReceived += client_ReceivedObjectMessage;
            stringStream = client.GetStringStream(host, port,
                1, ChannelDeliveryRequirements.MostStrict);
            stringStream.MessagesReceived += client_ReceivedStringMessage;
            binaryStream = client.GetBinaryStream(host, port,
                2, ChannelDeliveryRequirements.MostStrict);
            binaryStream.MessagesReceived += client_ReceivedBinaryMessage;

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
            running = false;
            foreach (Thread t in threads)
            {
                t.Abort();
            }
            threads.Clear();
            client.Stop();
        }

        virtual public void Dispose()
        {
            Stop();
        }

        private void client_StartThread(object obj)
        {
            try
            {
                while (running)
                {
                    switch (random.Next(3)) // integer >= 0 but < 3
                    {
                        case 0:
                            Console.Write('o');
                            objectStream.Send(StandardObjects.ObjectMessage);
                            break;

                        case 1:
                            Console.Write('s');
                            stringStream.Send(StandardObjects.StringMessage);
                            break;

                        case 2:
                            Console.Write('b');
                            binaryStream.Send(StandardObjects.ByteMessage);
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
            while ((message = stream.DequeueMessage(0)) != null)
            {
                if (!ByteUtils.Compare(message, StandardObjects.ByteMessage))
                {
                    Console.WriteLine("Invalid byte message: {0}",
                        ByteUtils.DumpBytes(message));
                    errorOccurred = true;
                }

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
                if (!StandardObjects.StringMessage.Equals(message))
                {
                    Console.WriteLine("Invalid strings message: {0}",
                        message);
                    errorOccurred = true;
                }

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
                if (!StandardObjects.Equivalent(StandardObjects.ObjectMessage, message))
                {
                    Console.WriteLine("Invalid object message: {0}",
                        message);
                    errorOccurred = true;
                }

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
        private const int serverPort = 9876;
        private EchoingServer server;

        private IList<StressingClient> clients = new List<StressingClient>();
        
        [Test]
        public void StressTest() {
            TimeSpan duration = TimeSpan.FromSeconds(30);
            Console.WriteLine("Starting Stress Test ({0} seconds)", duration.TotalSeconds);
            server = new EchoingServer(serverPort);
            server.Start();

            clients = new List<StressingClient>();
            for (int i = 0; i < 5; i++)
            {
                StressingClient c = new StressingClient("127.0.0.1", serverPort.ToString());
                c.Start();
                clients.Add(c);
            }

            Thread.Sleep(duration);
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