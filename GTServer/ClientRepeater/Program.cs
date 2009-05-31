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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using Common.Logging;
using Common.Logging.Simple;
using GT.Millipede;
using GT.Net.Utils;
using GT.Utils;

namespace GT.Net
{

    /// <summary>
    /// The server configuration used for the <see cref="ClientRepeater"/>.
    /// </summary>
    /// <remarks>
    /// This particular configuration specifies the
    /// <see cref="BaseLWMCFMarshaller"/> as the
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
    /// <remarks>
    /// This ClientRepeater serves as an example of creating a server using
    /// GT.  ClientRepeater can act as a threaded server, where a separate
    /// thread is launched to handle incoming messages, or as a polled server,
    /// where <see cref="Update"/> is called from some other event loop
    /// (e.g., from a timer loop).
    /// </remarks>
    public class ClientRepeater : IStartable
    {
        /// <summary>
        /// The default port to be used by ClientRepeater instances.
        /// </summary>
        public static uint DefaultPort = 9999;

        /// <summary>
        /// The default session channel to be used by ClientRepeater instances
        /// for broadcasting session events, such as when clients join or leave.
        /// </summary>
        public static byte DefaultSessionChannelId = 0;

        /// <summary>
        /// The default timeout period for inactive connections (meaning
        /// those that do not respond to the GT-level pings); this timeout 
        /// should be less than the ping interval.  Zero disables
        /// the behaviour.
        /// </summary>
        public static TimeSpan DefaultInactiveTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The default verbosity of ClientRepeater instances.
        /// </summary>
        public static uint DefaultVerbosity = 1;

        /// <summary>
        /// Triggered on the occurrence of underlying GT errors.
        /// </summary>
        public event ErrorEventNotication ErrorEvent;

        public TimeSpan InactiveTransportTimeout { get; set; }

        protected ILog log;
        protected ServerConfiguration config;
        protected Server server;
        protected PingBasedDisconnector pbd;
        protected MessageDeliveryRequirements sessionMDR =
            new MessageDeliveryRequirements(Reliability.Reliable,
                MessageAggregation.Immediate, Ordering.Unordered);

        /// <summary>
        /// The channel for automatically broadcasting session changes to client members.  
        /// If &lt; 0, then not sent.
        /// </summary>
        public int SessionChangesChannelId { get; set; }

        /// <summary>
        /// Sets the verbosity of the ClientRepeater's logging detail.
        /// </summary>
        public uint Verbose { get; set; }

        public Server Server { get { return server; } }

        static void Usage()
        {
            Console.WriteLine("Use: <ClientRepeater.exe> [-q] [-v] [-l level] [-m pktsize] [-s channelId] [-M mpede] [port]");
            Console.WriteLine("  -q   be very very quiet (use NoOpLogger)");
            Console.WriteLine("  -l   use a console logger at the specified level");
            Console.Write("       levels: ");
            foreach (string value in Enum.GetNames(typeof(LogLevel)))
            {
                Console.Write(value + ", ");
            }
            Console.WriteLine();
            Console.WriteLine("  -v   increase the verbosity (default: {0})", DefaultVerbosity);
            Console.WriteLine("         0: warnings/errors only");
            Console.WriteLine("         1: clients joining and leaving");
            Console.WriteLine("         2: transport details");
            Console.WriteLine("         3: incoming messages");
            Console.WriteLine("  -s   cause session announcements to be sent on specified channel");
            Console.WriteLine("       (use -1 to disable session announcements)");
            Console.WriteLine("  -m   set the maximum packet size to <pktsize>");
            Console.WriteLine("  -M   set the GT-Millipede configuration string");
            Console.WriteLine("  -T   timeout inactive connections (in seconds; use 0 to deactivate)");
            Console.WriteLine("[port] defaults to {0} if not specified", DefaultPort);
            Console.WriteLine("[channelId] defaults to {0} if not specified", DefaultSessionChannelId);
        }

        static void Main(string[] args)
        {
            int port = (int)DefaultPort;
            uint verbose = DefaultVerbosity;
            uint maxPacketSize = 0;
            int sessionChannelId = DefaultSessionChannelId;
            TimeSpan timeout = DefaultInactiveTimeout;

            GetOpt options = new GetOpt(args, "ql:vm:s:M:T:");
            try
            {
                Option opt;
                while ((opt = options.NextOption()) != null)
                {
                    switch (opt.Character)
                    {
                        case 'q':
                            LogManager.Adapter = new NoOpLoggerFactoryAdapter();
                            break;
                        case 'v':
                            verbose++;
                            break;
                        case 'l':
                            NameValueCollection prop = new NameValueCollection();
                            prop["level"] = opt.Argument;
                            LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(prop);
                            break;
                        case 'm':
                            maxPacketSize = uint.Parse(opt.Argument);
                            break;
                        case 'b':
                            sessionChannelId = int.Parse(opt.Argument);
                            break;

                        case 'M':
                            Environment.SetEnvironmentVariable(MillipedeRecorder.ConfigurationEnvironmentVariableName, opt.Argument);
                            break;

                        case 'T':
                            int t = int.Parse(opt.Argument);
                            timeout = t <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(t);
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

            if (verbose > 0)
            {
                LogManager.GetLogger(typeof (ClientRepeater)).Info(String.Format("Starting server on port {0}", port));
            }
            RepeaterConfiguration config = new RepeaterConfiguration(port);
            config.MaximumPacketSize = maxPacketSize;
            ClientRepeater cr = new ClientRepeater(config);
            cr.SessionChangesChannelId = sessionChannelId;
            cr.Verbose = verbose;
            cr.InactiveTransportTimeout = timeout;
            cr.StartListening();
            if (verbose > 0)
            {
                LogManager.GetLogger(typeof (ClientRepeater)).Info("Server stopped");
            }
        }

        public ClientRepeater(int port) : this(new RepeaterConfiguration(port)) { }

        public ClientRepeater(ServerConfiguration sc) {
            log = LogManager.GetLogger(GetType());
            config = sc;
            InactiveTransportTimeout = DefaultInactiveTimeout;
        }

        public bool Active { get { return server != null && server.Active; } }

        /// <summary>
        /// Start the client-repeater instance.
        /// </summary>
        /// <remarks>
        /// Note: the behaviour of this method was changed with GT 3.0
        /// such that a new thread is no longer launched for handling incoming
        /// messages.  Callers desiring this behaviour should call <see cref="StartSeparateListeningThread"/>
        /// instead.
        /// </remarks>
        public void Start()
        {
            if (server == null)
            {
                server = config.BuildServer();
                server.MessageReceived += s_MessageReceived;
                server.ClientsJoined += s_ClientsJoined;
                server.ClientsRemoved += s_ClientsRemoved;
                server.ErrorEvent += s_ErrorEvent;

                // Some transports are unreliable, meaning that we cannot tell whether
                // a remote has stopped communicating because they have shutdown ungracefully
                // (e.g., crashed), because the network is down, or because they haven't
                // sent a reply.  The PingBasedDisconnector uses the ping response time
                // to automatically drop inactive connections.
                if (InactiveTransportTimeout.TotalSeconds > 0)
                {
                    pbd = new PingBasedDisconnector(server, InactiveTransportTimeout);
                    pbd.ErrorEvent += s_ErrorEvent;
                }
            }
            if(!server.Active)
            {
                server.Start();
                if (pbd != null) { pbd.Start(); }
            }
        }

        /// <summary>
        /// Starts a new thread that listens to periodically call 
        /// <see cref="Update"/>.  This thread instance will be stopped
        /// on <see cref="Stop"/> or <see cref="Dispose"/>.
        /// The frequency between calls to <see cref="Update"/> is controlled
        /// by the configuration's <see cref="BaseConfiguration.TickInterval"/>.
        /// </summary>
        public void StartSeparateListeningThread()
        {
            if (!Active) { Start(); }
            server.StartSeparateListeningThread();
        }

        /// <summary>
        /// Start the client-repeater instance, running on the calling thread.
        /// Execution will not return to the caller until the server has been
        /// stopped.
        /// </summary>
        public void StartListening()
        {
            if (!Active) { Start(); }
            server.StartListening();
        }

        /// <summary>
        /// Run a cycle to process any pending events for the connexions or
        /// other related objects for this instance.  This method is <strong>not</strong> 
        /// re-entrant and should not be called from GT callbacks.
        /// </summary>
        public void Update()
        {
            server.Update();
        }

        public void Stop()
        {
            if (server != null) { server.Stop(); }
            if (pbd != null) { pbd.Stop(); }
        }

        public void Dispose()
        {
            Stop();
            if (server != null) { server.Dispose(); }
            server = null;
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
            if (ErrorEvent != null) { ErrorEvent(es); }
        }

        private void s_ClientsJoined(ICollection<IConnexion> list)
        {
            if (Verbose > 0 && log.IsInfoEnabled)
            {
                foreach(IConnexion client in list)
                {
                    StringBuilder builder = new StringBuilder("Client joined: ");
                    builder.Append(client.Identity);
                    builder.Append(':');
                    if (client is BaseConnexion)
                    {
                        builder.Append(((BaseConnexion)client).ClientGuid);
                        builder.Append(":");
                    }
                    foreach(ITransport t in client.Transports)
                    {
                        builder.Append(" {");
                        builder.Append(t.ToString());
                        builder.Append('}');
                    }
                    log.Info(builder.ToString());
                }
            }
            if (SessionChangesChannelId < 0) { return; }

            // Update all clients with the new clients
            foreach (IConnexion client in list)
            {
                // First update the new clients with the currently connected set
                foreach (IConnexion other in server.Connexions)
                {
                    if (!list.Contains(other))
                    {
                        client.Send(new SessionMessage((byte)SessionChangesChannelId, other.Identity,
                            SessionAction.Lives), sessionMDR, null);
                    }
                }

                client.TransportAdded += _client_TransportAdded;
                client.TransportRemoved += _client_TransportRemoved;
                server.Send(new SessionMessage((byte)SessionChangesChannelId, client.Identity,
                    SessionAction.Joined), null, sessionMDR);
            }
        }

        private void s_ClientsRemoved(ICollection<IConnexion> list)
        {
            if (Verbose > 0 && log.IsInfoEnabled)
            {
                foreach(IConnexion client in list)
                {
                    StringBuilder builder = new StringBuilder("Client left: ");
                    builder.Append(client.Identity);
                    builder.Append(':');
                    if (client is BaseConnexion)
                    {
                        builder.Append(((BaseConnexion)client).ClientGuid);
                        builder.Append(":");
                    }
                    foreach(ITransport t in client.Transports)
                    {
                        builder.Append(" {");
                        builder.Append(t.ToString());
                        builder.Append('}');
                    }
                    log.Info(builder.ToString());
                }
            }
            if (SessionChangesChannelId < 0) { return; }

            foreach (IConnexion client in list)
            {
                //inform others client is gone
                server.Send(new SessionMessage((byte)SessionChangesChannelId, client.Identity,
                    SessionAction.Left), null, sessionMDR);
            }
        }

        private void _client_TransportAdded(IConnexion client, ITransport newTransport)
        {
            if (Verbose > 1 && log.IsInfoEnabled)
            {
                log.Info(String.Format("Client {0}: transport added: {1}",
                                       client.Identity, newTransport));
            }
        }

        private void _client_TransportRemoved(IConnexion client, ITransport newTransport)
        {
            if (Verbose > 1 && log.IsInfoEnabled)
            {
                log.Info(String.Format("Client {0}: transport removed: {1}",
                                       client.Identity, newTransport));
            }
        }

        private void s_MessageReceived(Message m, IConnexion client, ITransport transport)
        {
            if (Verbose > 2 && log.IsInfoEnabled)
            {
                log.Info(String.Format("received message: {0} from Client {1} via {2}",
                                        m, client.Identity, transport));
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
