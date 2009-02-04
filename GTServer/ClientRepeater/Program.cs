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
    /// The server configuration used for the <see cref="ClientRepeater"/>.
    /// </summary>
    /// <remarks>
    /// This particular configuration specifies the
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
        protected uint verbose = 0;

        public Server Server { get { return server; } }

        static void Main(string[] args)
        {
            int port = 9999;
            int optind;
            uint verbose = 0;

            // poor man's getopt
            for (optind = 0; optind < args.Length && args[optind].StartsWith("-"); optind++)
            {
                Debug.Assert(args[optind][0] == '-');
                if (args[optind][1] == '-') { optind++; break; }
                else if (args[optind][1] == 'v') { verbose++; }
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

            if (verbose > 0) { Console.WriteLine("Starting server on port {0}", port); }
            ClientRepeater cr = new ClientRepeater(port);
            cr.Verbose = verbose;
            cr.StartListening();
            if (verbose > 0) { Console.WriteLine("Server stopped"); }
        }

        public ClientRepeater(int port) : this(new RepeaterConfiguration(port)) {}

        public ClientRepeater(ServerConfiguration sc) {
            config = sc;
        }

        public bool Active { get { return server != null && server.Active; } }
        public uint Verbose
        {
            get { return verbose; }
            set { verbose = value; }
        }

        /// <summary>
        /// The channel for automatically broadcasting session changes to client members.  
        /// If &lt; 0, then not sent.
        /// </summary>
        public int SessionChangesChannel
        {
            get { return sessionChangesChannel; }
            set { sessionChangesChannel = value; }
        }

        /// <summary>
        /// Start an independently running client-repeater instance.
        /// Note that the new thread may not have started before the
        /// calling thread has returned: this instance may not yet
        /// be functional on return.
        /// </summary>
        public void Start()
        {
            serverThread = new Thread(StartListening);
            serverThread.Name = this.ToString();
            serverThread.IsBackground = true;
            serverThread.Start();
            Thread.Sleep(0);  // try to give the listening thread a chance to run
        }

        /// <summary>
        /// Start the client-repeater instance, running on the calling thread.
        /// Execution will not return to the caller until the server has been
        /// stopped.
        /// </summary>
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
            if (verbose == 0 && es.Severity < Severity.Error)
            {
                return;
            }
            if (es.Context != null)
            {
                Console.WriteLine("{0}: {1}[{2}]: {3} [{4}: {5}]", DateTime.Now, es.Severity,
                    es.ErrorCode, es.Message, es.Context.GetType().Name, es.Context.Message);
            }
            else
            {
                Console.WriteLine("{0}: {1}[{2}]: {3}", DateTime.Now, es.Severity,
                    es.ErrorCode, es.Message);
            }
        }

        private void s_ClientsJoined(ICollection<IConnexion> list)
        {
            if (verbose > 0)
            {
                DateTime now = DateTime.Now;
                foreach(ConnexionToClient client in list)
                {
                    client.TransportAdded += _client_TransportAdded;
                    client.TransportRemoved += _client_TransportRemoved;

                    StringBuilder builder = new StringBuilder("Client ");
                    builder.Append(client.Identity);
                    builder.Append(':');
                    builder.Append(client.ClientGuid);
                    builder.Append(":");
                    foreach(ITransport t in client.Transports)
                    {
                        builder.Append(" {");
                        builder.Append(t.ToString());
                        builder.Append('}');
                    }
                    Console.WriteLine("{0}: client joined: {1}", now, builder.ToString());
                }
            }
            if (sessionChangesChannel < 0) { return; }
           
            foreach (ConnexionToClient cnx in list)
            {
                int clientId = cnx.Identity;

                foreach (ConnexionToClient c in server.Clients)
                {
                    try
                    {
                        c.Send(clientId, SessionAction.Joined, (byte)sessionChangesChannel,
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
            if (verbose > 0)
            {
                DateTime now = DateTime.Now;
                foreach(ConnexionToClient client in list)
                {
                    StringBuilder builder = new StringBuilder("Client ");
                    builder.Append(client.Identity);
                    builder.Append(':');
                    builder.Append(client.ClientGuid);
                    builder.Append(":");
                    foreach(ITransport t in client.Transports)
                    {
                        builder.Append(" {");
                        builder.Append(t.ToString());
                        builder.Append('}');
                    }
                    Console.WriteLine("{0}: client left: {1}", now, builder.ToString());
                }
            }
            if (sessionChangesChannel < 0) { return; }

            foreach (ConnexionToClient client in list)
            {
                client.TransportAdded += _client_TransportAdded;
                client.TransportRemoved += _client_TransportRemoved;
                //kill client
                int clientId = client.Identity;
                try
                {
                    client.Dispose();
                } catch(Exception e) {
                    Console.WriteLine("{0} EXCEPTION: when stopping Client {1}: {2}",
                        DateTime.Now, clientId, e);
                }

                //inform others client is gone
                foreach (ConnexionToClient c in server.Clients)
                {
                    try {
                        c.Send(clientId, SessionAction.Left, (byte)sessionChangesChannel,
                            new MessageDeliveryRequirements(Reliability.Reliable, MessageAggregation.Immediate,
                                Ordering.Unordered), null);
                    }
                    catch (GTException e)
                    {
                        Console.WriteLine("{0} EXCEPTION: when sending: {1}", DateTime.Now, e);
                    }
                }
            }
        }

        private void _client_TransportAdded(IConnexion connexion, ITransport newTransport)
        {
            if (verbose > 0)
            {
                Console.WriteLine("{0} Client {1}: transport added: {2}", DateTime.Now,
                    connexion.Identity, newTransport);
            }
        }

        private void _client_TransportRemoved(IConnexion connexion, ITransport newTransport)
        {
            if (verbose > 0)
            {
                Console.WriteLine("{0} Client {1}: transport removed: {2}", DateTime.Now,
                    connexion.Identity, newTransport);
            }
        }

        private void s_MessageReceived(Message m, IConnexion client, ITransport transport)
        {
            if (verbose > 1)
            {
                Console.WriteLine("{0}: received message: {1} from Client {2} via {3}", 
                    DateTime.Now, m, client.Identity, transport);
            }
            //repeat whatever we receive to everyone else
            server.Send(m, server.Clients, new MessageDeliveryRequirements(transport.Reliability, 
                MessageAggregation.Immediate, transport.Ordering));
        }

        public override string ToString()
        {
            return String.Format("{0}({1})", GetType().Name, server);
        }

    }
}
