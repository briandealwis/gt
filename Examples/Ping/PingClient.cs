using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using GT.Net;
using GT.Utils;

namespace Ping
{
    class PingClientConfiguration : ClientConfiguration
    {
        public override IMarshaller CreateMarshaller()
        {
            return new LightweightDotNetSerializingMarshaller();
        }

        public override ICollection<IConnector> CreateConnectors()
        {
            IList<IConnector> connectors = new List<IConnector>();
            connectors.Add(new TcpConnector());
            connectors.Add(new UdpConnector(Ordering.Sequenced));
            connectors.Add(new UdpConnector(Ordering.Unordered));
            return connectors;
        }
    }


    class PingClient
    {
        /// <summary>
        /// The amount of time to wait between pings
        /// </summary>
        public TimeSpan DelayTime { get; set; }
        public uint NumberPings
        {
            get { return numPings; }
            set
            {
                if (value == 0)
                {
                    throw new ArgumentException("must be > 0");
                }
                numPings = value;
            }
        }

            private static void Usage()
        {
            Console.WriteLine("use: ping [-n npings] <host> <port>");
            Environment.Exit(-1);
        }

        static void Main(string[] args)
        {
            if (args.Length != 2) { Usage(); }
            PingClient client = new PingClient(args[0], args[1]);
            client.Run();
        }

        protected PingClientConfiguration configuration = new PingClientConfiguration();
        protected string host;
        protected string port;
        protected uint numPings = 10;
        protected IDictionary<ITransport, StatisticalMoments> statistics =
            new Dictionary<ITransport, StatisticalMoments>();
        protected IDictionary<ITransport, IList<uint>> outstandingPings =
            new Dictionary<ITransport, IList<uint>>();

        private PingClient(string host, string port)
        {
            this.host = host;
            this.port = port;
            DelayTime = TimeSpan.FromSeconds(1);
        }

        private void Run()
        {
            // set PingInterval to ridiculous value so that pings are under 
            // our manual control
            configuration.PingInterval = TimeSpan.FromDays(10);
            Client client = new Client(configuration);
            client.ConnexionAdded += _client_ConnexionAdded;
            client.Start();

            ISessionChannel channel = client.OpenSessionChannel(host, port, 0,
                ChannelDeliveryRequirements.SessionLike);

            for(int iter = 0; iter < numPings; iter++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                foreach(IConnexion cnx in client.Connexions)
                {
                    ((BaseConnexion)cnx).Ping();
                }
                while(sw.Elapsed.CompareTo(DelayTime) < 0)
                {
                    client.Update();
                    client.Sleep(TimeSpan.FromMilliseconds(1));
                }
            }
            ReportResults();
        }

        private void ReportResults()
        {
            foreach (ITransport t in statistics.Keys) {
                StringBuilder result = new StringBuilder(t.ToString());
                result.AppendLine(":");
                if (outstandingPings.ContainsKey(t) && outstandingPings[t].Count > 0)
                {
                    result.AppendFormat("  loss: {0} packets dropped ({1:%})", 
                        outstandingPings[t].Count, (float)outstandingPings[t].Count / (float)numPings);
                    result.AppendLine();
                }
                if (statistics.ContainsKey(t))
                {
                    StatisticalMoments sm = statistics[t];
                    result.AppendFormat("  avg: {0:f}ms (err: {4:f}ms), sd: {1:f}ms, skew: {2:f}, kurt: {3:f}",
                        sm.Average(), sm.StandardDeviation(), sm.Skewness(),
                        sm.Kurtosis(), sm.ErrorOnAverage());
                    result.AppendLine();
                }
                Console.WriteLine();
                Console.Write(result);
            }
        }

        private void _client_ConnexionAdded(Communicator c, IConnexion cnx)
        {
            cnx.PingRequested += _cnx_PingRequested;
            cnx.PingReceived += _cnx_PingReceived;
        }

        private void _cnx_PingReceived(ITransport transport, uint sequence, TimeSpan roundtrip)
        {
            IList<uint> outstanding;
            if (!outstandingPings.TryGetValue(transport, out outstanding))
            {
                Console.WriteLine("Warning: received ping to a ping that hadn't been sent!");
            }
            else
            {
                outstanding.Remove(sequence);
            }
            StatisticalMoments sm;
            if (!statistics.TryGetValue(transport, out sm))
            {
                Console.WriteLine("Weird: no statistics either");
                statistics[transport] = sm = new StatisticalMoments();
            }
            sm.Accumulate(roundtrip.TotalMilliseconds);
            Console.WriteLine("Ping #{1} {2:f}ms on {0}", transport,
                sequence, roundtrip.TotalMilliseconds);
        }

        private void _cnx_PingRequested(ITransport transport, uint sequence)
        {
            IList<uint> outstanding;
            if (!outstandingPings.TryGetValue(transport, out outstanding))
            {
                outstandingPings[transport] = outstanding = new List<uint>();
            }
            outstanding.Add(sequence);
            if (!statistics.ContainsKey(transport))
            {
                statistics[transport] = new StatisticalMoments();
            }
        }
    }
}
