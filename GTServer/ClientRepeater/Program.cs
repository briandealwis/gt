using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace GT.Net
{

    /// <summary>
    /// The server configuration used for the ClientRepeater.
    /// </summary>
    /// <remarks>
    /// This configuration specifies the
    /// <see cref="LightweightDotNetSerializingMarshaller"/> as the
    /// marshaller to be used.  This is a lightweight marshaller 
    /// that unmarshals only system messages and session messages, and leaves
    /// all other messages as uninterpreted bytes, passed as instances of 
    /// <c>RawMessage</c>.  ClientRepeaters uses this marshaller to avoid
    /// unnecessary unmarshalling and remarshalling of messages
    /// that it will not use, and thus reduce message processing latency.
    /// Servers that need to use the contents of messages should
    /// change the <c>CreateMarshaller()</c> method to use a
    /// <c>DotNetSerializingMarshaller</c> marshaller instead.
    /// </remarks>
    public class RepeaterConfiguration : DefaultServerConfiguration
    {
        public RepeaterConfiguration(int port)
            : base(port)
        {
            // Sleep at most 1 ms between updates
            this.TickInterval = TimeSpan.FromMilliseconds(1);
        }

        override public Server BuildServer()
        {
            return new Server(this);
        }

        override public IMarshaller CreateMarshaller()
        {
            return new LightweightDotNetSerializingMarshaller();
        }
    }

    /// <summary>
    /// ClientRepeater: a simple server that simply resends all incoming
    /// messages to all the clients that have connected to it.  The
    /// ClientRepeater also sends Joined and Left session messages too.
    /// </summary>
    public class ClientRepeater : IStartable
    {
        protected ServerConfiguration config;
        protected Server server;
        protected Thread serverThread;
        protected int sessionChangesChannel = 0;
        protected bool verbose = false;

        static void Main(string[] args)
        {
            int port = 9999;
            int optind;
            bool verbose = false;

            // poor man's getopt
            for (optind = 0; optind < args.Length && args[optind].StartsWith("-"); optind++)
            {
                Debug.Assert(args[optind][0] == '-');
                if (args[optind][1] == '-') { optind++; break; }
                else if (args[optind][1] == 'v') { verbose = true; }
                else
                {
                    Console.WriteLine("Invalid option: '{0}'", args[optind]);
                    Console.WriteLine("Use: <ClientRepeater.exe> [-v] [port]");
                    Console.WriteLine("[port] defaults to {0} if not specified", port);
                    return;
                }
            }

            if (args.Length - optind > 1)
            {
                Console.WriteLine("Use: <ClientRepeater.exe> [-v] [port]");
                Console.WriteLine("[port] defaults to {0} if not specified", port);
                return;
            }
            else if (args.Length - optind == 1)
            {
                port = Int32.Parse(args[optind]);
                if (port <= 0)
                {
                    Console.WriteLine("error: port must be greater than 0");
                    return;
                }
            }

            if (verbose) { Console.WriteLine("Starting server on port {0}", port); }
            ClientRepeater cr = new ClientRepeater(port);
            cr.Verbose = verbose;
            cr.StartListening();
            if (verbose) { Console.WriteLine("Server stopped"); }
        }

        public ClientRepeater(int port) : this(new RepeaterConfiguration(port)) {}

        public ClientRepeater(ServerConfiguration sc) {
            config = sc;
        }

        public bool Active { get { return server != null && server.Active; } }
        public bool Verbose
        {
            get { return verbose; }
            set { verbose = value; }
        }

        /// <summary>
        /// The channel id for automatically broadcasting session changes to client members.  
        /// If &lt; 0, then not sent.
        /// </summary>
        public int SessionChangesChannel
        {
            get { return sessionChangesChannel; }
            set { sessionChangesChannel = value; }
        }

        public void Start()
        {
            serverThread = new Thread(StartListening);
            serverThread.Name = this.ToString();
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        public void StartListening()
        {
            server = config.BuildServer();
            server.MessageReceived += s_MessageReceived;
            server.ClientsJoined += s_ClientsJoined;
            server.ClientsRemoved += s_ClientsRemoved;
            server.ErrorEvent += s_ErrorEvent;
            server.StartListening();
        }


        public void Stop()
        {
            if (server != null) { server.Stop(); }
            if (serverThread != null) { serverThread.Abort(); }
        }

        public void Dispose()
        {
            Stop();
            if (server != null) { server.Dispose(); }
            server = null;
            serverThread = null;
        }

        private void s_ErrorEvent(ErrorSummary es)
        {
            Console.WriteLine("{0}: {1}[{2}]: {3}: {4}", DateTime.Now, es.Severity, es.ErrorCode,
                es.Message, es.Context);
        }

        private void s_ClientsJoined(ICollection<IConnexion> list)
        {
            Console.WriteLine("{0}: clients joined: {1}", DateTime.Now, ToString(list));
            if (sessionChangesChannel < 0) { return; }
           
            foreach (ClientConnexion client in list)
            {
                int clientId = client.UniqueIdentity;

                foreach (ClientConnexion c in server.Clients)
                {
                    try
                    {
                        c.Send(clientId, SessionAction.Joined, (byte)0,
                            new MessageDeliveryRequirements(Reliability.Reliable, MessageAggregation.Immediate,
                                Ordering.Unordered), null);
                    }
                    catch (GTException e)
                    {
                        Console.WriteLine("{0}: EXCEPTION: when sending: {1}", DateTime.Now, e);
                    }
                }
            }
        }

        private void s_ClientsRemoved(ICollection<IConnexion> list)
        {
            Console.WriteLine("{0}: clients left: {1}", DateTime.Now, ToString(list));
            if (sessionChangesChannel < 0) { return; }

            foreach (ClientConnexion client in list)
            {
                //kill client
                int clientId = client.UniqueIdentity;
                try
                {
                    client.Dispose();
                } catch(Exception e) {
                    Console.WriteLine("{0} EXCEPTION: when stopping client {1} id#{2}: {3}",
                        DateTime.Now, clientId, client, e);
                }

                //inform others client is gone
                foreach (ClientConnexion c in server.Clients)
                {
                    try {
                        c.Send(clientId, SessionAction.Left, (byte)0,
                            new MessageDeliveryRequirements(Reliability.Reliable, MessageAggregation.Immediate,
                                Ordering.Unordered), null);
                    }
                    catch (GTException e)
                    {
                        Console.WriteLine("{0}: EXCEPTION: when sending: {1}", DateTime.Now, e);
                    }
                }
            }
        }

        private void s_MessageReceived(Message m, IConnexion client, ITransport transport)
        {
            if (verbose)
            {
                Console.WriteLine("{0}: received message: {1} from {2} via {3}", 
                    DateTime.Now, m, client, transport);
            }
            //repeat whatever we receive to everyone else
            server.Send(m, server.Clients, new MessageDeliveryRequirements(transport.Reliability, 
                MessageAggregation.Immediate, transport.Ordering));
        }

        public override string ToString()
        {
            return String.Format("{0}({1})", GetType().Name, server);
        }

        private string ToString<T>(ICollection<T> c)
        {
            StringBuilder b = new StringBuilder();
            IEnumerator<T> it = c.GetEnumerator();
            if (!it.MoveNext()) { return ""; }
            while (true)
            { 
                b.Append(it.Current.ToString());
                if(!it.MoveNext()) { break; }
                b.Append(", ");
            }
            return b.ToString();
        }
    }
}
