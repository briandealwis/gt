using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Common.Logging;
using GT.Millipede;
using GT.Utils;

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
        /// <summary>
        /// Set the maximum packet size to be configured for all transports.
        /// If 0, then do not change the maximum packet size from the transport's
        /// default.
        /// </summary>
        public uint MaximumPacketSize { get; set; }

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

        public override ITransport ConfigureTransport(ITransport t)
        {
            if (MaximumPacketSize > 0) { t.MaximumPacketSize = MaximumPacketSize; }
            return base.ConfigureTransport(t);
        }
    }

    /// <summary>
    /// ClientRepeater: a simple server that simply resends all incoming
    /// messages to all the clients that have connected to it.  The
    /// ClientRepeater also sends Joined and Left session messages too.
    /// </summary>
    public class ClientRepeater : IStartable
    {
        public static uint DefaultPort = 9999;
        public static byte DefaultSessionChannel = 0;

        protected ILog log;
        protected ServerConfiguration config;
        protected Server server;
        protected Thread serverThread;
        protected MessageDeliveryRequirements sessionMDR =
            new MessageDeliveryRequirements(Reliability.Reliable,
                MessageAggregation.Immediate, Ordering.Unordered);

        /// <summary>
        /// The channel for automatically broadcasting session changes to client members.  
        /// If &lt; 0, then not sent.
        /// </summary>
        public int SessionChangesChannel { get; set; }

        public uint Verbose { get; set; }
        
        public Server Server { get { return server; } }

        static void Usage()
        {
            Console.WriteLine("Use: <ClientRepeater.exe> [-v] [-m pktsize] [-s channel] [-M mpede] [port]");
            Console.WriteLine("  -v   be more verbose");
            Console.WriteLine("  -s   cause session announcements to be sent on specified channel");
            Console.WriteLine("       (use -1 to disable session announcements)");
            Console.WriteLine("  -m   set the maximum packet size to <pktsize>");
            Console.WriteLine("  -M   set the GT-Millipede configuration string");
            Console.WriteLine("[port] defaults to {0} if not specified", DefaultPort);
            Console.WriteLine("[channel] defaults to {0} if not specified", DefaultSessionChannel);
        }

        static void Main(string[] args)
        {
            int port = (int)DefaultPort;
            uint verbose = 0;
            uint maxPacketSize = 0;
            int sessionChannel = DefaultSessionChannel;

            GetOpt options = new GetOpt(args, "vm:s:M:");
            try
            {
                Option opt;
                while ((opt = options.NextOption()) != null)
                {
                    switch (opt.Character)
                    {
                        case 'v':
                            verbose++;
                            break;
                        case 'm':
                            maxPacketSize = uint.Parse(opt.Argument);
                            break;
                        case 'b':
                            sessionChannel = int.Parse(opt.Argument);
                            break;

                        case 'M':
                            Environment.SetEnvironmentVariable(MillipedeRecorder.ConfigurationEnvironmentVariableName, opt.Argument);
                            break;
                    }
                }
            }
            catch (GetOptException e)
            {
                Console.WriteLine(e.Message);
                Usage();
                return;
            }
            args = options.RemainingArguments();
            if (args.Length > 1)
            {
                Usage();
                return;
            }
            if (args.Length == 1)
            {
                port = Int32.Parse(args[0]);
                if (port <= 0)
                {
                    Console.WriteLine("error: port must be greater than 0");
                    return;
                }
            }

            if (verbose > 0) { Console.WriteLine("Starting server on port {0}", port); }
            RepeaterConfiguration config = new RepeaterConfiguration(port);
            config.MaximumPacketSize = maxPacketSize;
            ClientRepeater cr = new ClientRepeater(config);
            cr.SessionChangesChannel = sessionChannel;
            cr.Verbose = verbose;
            cr.StartListening();
            if (verbose > 0) { Console.WriteLine("Server stopped"); }
        }

        public ClientRepeater(int port) : this(new RepeaterConfiguration(port)) { }

        public ClientRepeater(ServerConfiguration sc) {
            log = LogManager.GetLogger(GetType());
            config = sc;
        }

        public bool Active { get { return server != null && server.Active; } }

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
            string message = es.ToString();
            switch(es.Severity)
            {
            case Severity.Error:
                log.Error(message, es.Context);
                break;
            case Severity.Warning:
                log.Warn(message, es.Context);
                break;
            case Severity.Fatal:
                log.Fatal(message, es.Context);
                break;
            case Severity.Information:
                log.Info(message, es.Context);
                break;
            }
        }

        private void s_ClientsJoined(ICollection<IConnexion> list)
        {
            if (log.IsInfoEnabled)
            {
                foreach(ConnexionToClient client in list)
                {
                    StringBuilder builder = new StringBuilder("Client joined: ");
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
                    log.Info(builder.ToString());
                }
            }
            if (SessionChangesChannel < 0) { return; }

            foreach (IConnexion client in list)
            {
                client.TransportAdded += _client_TransportAdded;
                client.TransportRemoved += _client_TransportRemoved;
                server.Send(new SessionMessage((byte)SessionChangesChannel, client.Identity,
                    SessionAction.Joined), null, sessionMDR);
            }
        }

        private void s_ClientsRemoved(ICollection<IConnexion> list)
        {
            if (log.IsInfoEnabled)
            {
                foreach(ConnexionToClient client in list)
                {
                    StringBuilder builder = new StringBuilder("Client left: ");
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
                    log.Info(builder.ToString());
                }
            }
            if (SessionChangesChannel < 0) { return; }

            foreach (IConnexion client in list)
            {
                //inform others client is gone
                server.Send(new SessionMessage((byte)SessionChangesChannel, client.Identity,
                    SessionAction.Left), null, sessionMDR);
            }
        }

        private void _client_TransportAdded(IConnexion client, ITransport newTransport)
        {
            if (Verbose > 0)
            {
                Console.WriteLine(String.Format("{0}: Client {1}: transport added: {2}",
                    DateTime.Now, client.Identity, newTransport));
            }
        }

        private void _client_TransportRemoved(IConnexion client, ITransport newTransport)
        {
            if (Verbose > 0)
            {
                Console.WriteLine(String.Format("{0}: Client {1}: transport removed: {2}",
                    DateTime.Now, client.Identity, newTransport));
            }
        }

        private void s_MessageReceived(Message m, IConnexion client, ITransport transport)
        {
            if (Verbose > 1)
            {
                Console.WriteLine("{0}: received message: {1} from Client {2} via {3}", 
                    DateTime.Now, m, client.Identity, transport);
            }
            //repeat whatever we receive to everyone else
            server.Send(m, null, new MessageDeliveryRequirements(transport.Reliability, 
                MessageAggregation.Immediate, transport.Ordering));
        }

        public override string ToString()
        {
            return String.Format("{0}({1})", GetType().Name, server);
        }

    }
}
