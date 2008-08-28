using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using GT.Net;
using SoftwareFX.ChartFX.Lite;
using StatsGraphs;
using Message=GT.Net.Message;

namespace GT.StatsGraphs
{
    public partial class StatisticsDialog : Form
    {
        protected readonly int MonitoredTimePeriod = 5 * 60;    // seconds

        protected bool paused = false;
        protected object observed;
        protected int tickCount = 0;
        protected int connectionsCount = 0;
        protected IList<string> transportNames = new List<string>();

        protected int messagesSent = 0;
        protected int messagesReceived = 0;
        protected IDictionary<byte, IDictionary<MessageType, IDictionary<string, int>>> messagesReceivedCounts;
        protected IDictionary<byte, IDictionary<MessageType, IDictionary<string, int>>> messagesSentCounts;

        protected IDictionary<string, int> msgsRecvPerTransport;
        protected IDictionary<string, int> msgsSentPerTransport;

        protected int bytesSent = 0;
        protected int bytesReceived = 0;
        protected IDictionary<string, int> bytesRecvPerTransport;
        protected IDictionary<string, int> bytesSentPerTransport;


        #region Static Methods 
        public static void On(Client client)
        {
            Thread t = new Thread(() => OpenOn(client));
            t.IsBackground = true;
            t.Name = "StatisticsDialog on " + client;
            t.Start();
        }

        public static void On(Server server) {
            Thread t = new Thread(() => OpenOn(server));
            t.IsBackground = true;
            t.Name = "StatisticsDialog on " + server;
            t.Start();
        }

        public static void OpenOn(Client client)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            StatisticsDialog sd = new StatisticsDialog();
            sd.Observe(client);

            Application.Run(sd);
        }

        public static void OpenOn(Server server)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            StatisticsDialog sd = new StatisticsDialog();
            sd.Observe(server);

            Application.Run(sd);
        }

        #endregion

        public StatisticsDialog()
        {
            ResetLifetimeCounts();
            ResetTransientValues();

            InitializeComponent();
            UpdateTitle();

            connectionsGraph.ClearData(ClearDataFlag.Values);
            connectionsGraph.AxisX.Min = 0;

            messagesGraph.ClearData(ClearDataFlag.Values);
            messagesGraph.AxisX.Min = 0;
            messagesGraph.SerLeg[0] = "Msgs sent";
            messagesGraph.SerLeg[1] = "Msgs recvd";

            bytesGraph.ClearData(ClearDataFlag.Values);
            bytesGraph.AxisX.Min = 0;
            bytesGraph.SerLeg[0] = "Bytes sent";
            bytesGraph.SerLeg[1] = "Bytes recvd";

            msgsPerTransportGraph.ClearData(ClearDataFlag.Values);
            msgsPerTransportGraph.AxisX.Min = 0;

            bytesPerTransportGraph.ClearData(ClearDataFlag.Values);
            bytesGraph.AxisX.Min = 0;
            bytesGraph.SerLeg[0] = "Bytes sent";
            bytesGraph.SerLeg[1] = "Bytes recvd";

            messagesSentPiechart.NValues = 0;
            messagesReceivedPiechart.NValues = 0;
        }

        /// <summary>
        /// Reset those values that are accumulated over the lifetime
        /// </summary>
        public void ResetLifetimeCounts()
        {
            messagesReceivedCounts = new Dictionary<byte, IDictionary<MessageType, IDictionary<string, int>>>();
            messagesSentCounts = new Dictionary<byte, IDictionary<MessageType, IDictionary<string, int>>>();
        }

        /// <summary>
        /// Reset the values that are updated each tick
        /// </summary>
        public void ResetTransientValues()
        {
            messagesSent = 0;
            messagesReceived = 0;
            msgsSentPerTransport = new Dictionary<string, int>();
            msgsRecvPerTransport = new Dictionary<string, int>();

            bytesSent = 0;
            bytesReceived = 0;
            bytesSentPerTransport = new Dictionary<string, int>();
            bytesRecvPerTransport = new Dictionary<string, int>();
        }

        public bool Paused
        {
            get { return paused; }
            set { paused = value; }
        }
        
        private void UpdateTitle()
        {
            if (observed == null)
            {
                this.Text = "Statistics";
            }
            else
            {
                this.Text = "Statistics: " + observed;
            }
        }

        private void resetValuesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetLifetimeCounts();
        }

        private void changeIntervalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IntervalDialog id = new IntervalDialog((float)timer1.Interval / 1000f);
            if (id.ShowDialog(this) == DialogResult.OK)
            {
                timer1.Interval = (int)(id.Interval * 1000);
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            this.BeginInvoke(new MethodInvoker(UpdateGraphs));
        }

        private void UpdateGraphs()
        {
            lock (this)
            {
                UpdateTitle();

                DateTime now = DateTime.Now;
                int numberDataPoints = MonitoredTimePeriod * 1000 / timer1.Interval;
                int index = tickCount % numberDataPoints;

                // timer1.Interval is in milliseconds
                if (!Paused)
                {
                    /// the # of current connections
                    if (index == 0)
                    {
                        connectionsGraph.ClearData(ClearDataFlag.Values);
                    }
                    connectionsGraph.OpenData(COD.Values, 1, numberDataPoints);
                    //clientsGraph.AxisX.LabelsFormat.CustomFormat = "#.##";
                    connectionsGraph.Value[0, index] = connectionsCount;
                    connectionsGraph.CloseData(COD.Values);

                    /// the # messages sent & received
                    if (index == 0)
                    {
                        messagesGraph.ClearData(ClearDataFlag.Values);
                    }
                    messagesGraph.OpenData(COD.Values, 2, numberDataPoints);
                    //messagesGraph.AxisX.LabelsFormat.CustomFormat = "#.##";
                    messagesGraph.Value[0, index] = messagesSent;
                    messagesGraph.Value[1, index] = messagesReceived;
                    messagesGraph.CloseData(COD.Values);

                    /// The # bytes sent & received
                    if (index == 0)
                    {
                        bytesGraph.ClearData(ClearDataFlag.Values);
                    }
                    bytesGraph.OpenData(COD.Values, 2, numberDataPoints);
                    //bytesGraph.AxisX.LabelsFormat.CustomFormat = "#.##";
                    bytesGraph.Value[0, index] = bytesSent;
                    bytesGraph.Value[1, index] = bytesReceived;
                    bytesGraph.CloseData(COD.Values);

                    /// Ensure we use a consistent ordering of transports
                    UpdateTransportNames(msgsSentPerTransport.Keys, msgsRecvPerTransport.Keys,
                        bytesSentPerTransport.Keys, bytesRecvPerTransport.Keys);

                    /// # messages and bytes sent & received per transport
                    msgsPerTransportGraph.OpenData(COD.Values, 2 * transportNames.Count, numberDataPoints);
                    bytesPerTransportGraph.OpenData(COD.Values, 2 * transportNames.Count, numberDataPoints);
                    for (int i = 0; i < transportNames.Count; i++)
                    {
                        string xport = transportNames[i];
                        int value = 0;
                        if (!msgsSentPerTransport.TryGetValue(xport, out value)) { value = 0; }
                        msgsPerTransportGraph.Value[2 * i, index] = value;
                        if (!msgsRecvPerTransport.TryGetValue(xport, out value)) { value = 0; }
                        msgsPerTransportGraph.Value[2*i + 1, index] = value;

                        if (!bytesSentPerTransport.TryGetValue(xport, out value)) { value = 0; }
                        bytesPerTransportGraph.Value[2*i, index] = value;
                        if (!bytesRecvPerTransport.TryGetValue(xport, out value)) { value = 0; }
                        bytesPerTransportGraph.Value[2*i + 1, index] = value;
                        msgsPerTransportGraph.SerLeg[2 * i] = xport + " sent";
                        msgsPerTransportGraph.SerLeg[2 * i + 1] = xport + " recvd";
                        bytesPerTransportGraph.SerLeg[2 * i] = xport + " sent";
                        bytesPerTransportGraph.SerLeg[2 * i + 1] = xport + " recvd";
                    }
                    msgsPerTransportGraph.CloseData(COD.Values);
                    bytesPerTransportGraph.CloseData(COD.Values);


                    /// Pie charts
                    messagesSentPiechart.OpenData(COD.Values, 1, (int) COD.Unknown);
                    index = 0;
                    List<byte> IDs = new List<byte>(messagesSentCounts.Keys);
                    IDs.Sort();
                    foreach (byte id in IDs)
                    {
                        IDictionary<MessageType, IDictionary<string, int>> subdict =
                            messagesSentCounts[id];
                        foreach (MessageType t in Enum.GetValues(typeof (MessageType)))
                        {
                            if (subdict.ContainsKey(t))
                            {
                                int total = 0;
                                foreach (int protocolCount in subdict[t].Values)
                                {
                                    total += protocolCount;
                                }
                                messagesSentPiechart.Value[0, index] = total;
                                messagesSentPiechart.Legend[index] = id + " " + t;
                                index++;
                            }
                        }
                    }
                    messagesSentPiechart.CloseData(COD.Values);

                    messagesReceivedPiechart.OpenData(COD.Values, 1, (int) COD.Unknown);
                    index = 0;
                    IDs = new List<byte>(messagesReceivedCounts.Keys);
                    IDs.Sort();
                    foreach (byte id in IDs)
                    {
                        IDictionary<MessageType, IDictionary<string, int>> subdict =
                            messagesReceivedCounts[id];
                        foreach (MessageType t in Enum.GetValues(typeof (MessageType)))
                        {
                            if (subdict.ContainsKey(t))
                            {
                                int total = 0;
                                foreach (int protocolCount in subdict[t].Values)
                                {
                                    total += protocolCount;
                                }
                                messagesReceivedPiechart.Value[0, index] = total;
                                messagesReceivedPiechart.Legend[index] = id + " " + t;
                                index++;
                            }
                        }
                    }
                    /// And finally advance the tickCount
                    tickCount++;
                }   // !Paused
                ResetTransientValues();
            }
        }

        /// <summary>
        /// Update tranport names but preserving order of those seen previously
        /// </summary>
        /// <param name="args"></param>
        private void UpdateTransportNames(params ICollection<string>[] args) 
        {
            foreach (ICollection<string> names in args)
            {
                foreach (string name in names)
                {
                    if (!transportNames.Contains(name))
                    {
                        transportNames.Add(name);
                    }
                }
            }
        }

        #region Client Observations

        public void Observe(Client client)
        {
            observed = client;
            foreach (IConnexion c in client.Connexions)
            {
                client_ConnexionAdded(c);
            }
            client.ConnexionAdded += client_ConnexionAdded;
            client.ConnexionRemoved += client_ConnexionRemoved;
            if (Visible) { this.BeginInvoke(new MethodInvoker(UpdateTitle)); }
        }

        private void client_ConnexionAdded(IConnexion c)
        {
            lock (this)
            {
                connectionsCount++;
                // dup'd in server_ClientsJoined
                c.MessageReceived += connexion_MessageReceived;
                c.MessageSent += connexion_MessageSent;
                c.TransportAdded += connexion_TransportAdded;
                foreach (ITransport t in c.Transports) { connexion_TransportAdded(c, t); }
            }
        }

        private void client_ConnexionRemoved(IConnexion c)
        {
            lock (this)
            {
                connectionsCount = Math.Max(0, connectionsCount - 1);
            }
        }


        #endregion

        #region Server Observations

        public void Observe(Server server)
        {
            observed = server;

            // Servers use the same IConnexion.MessageSent event as clients rather
            // than the Server.MessageSent event.  By using the former, we count
            // each message for every destination client, even when the same message
            // is sent to those clients.
            server.ClientsJoined += server_ClientsJoined;
            server.ClientsRemoved += server_ClientsRemoved;
            server_ClientsJoined(server.Clients);
            if (Visible) { this.BeginInvoke(new MethodInvoker(UpdateTitle)); }
        }

        private void server_ClientsJoined(ICollection<IConnexion> list)
        {
            lock (this)
            {
                connectionsCount += list.Count;
                foreach (IConnexion c in list)
                {
                    // dup'd in client_ConnexionAdded
                    c.MessageReceived += connexion_MessageReceived;
                    c.MessageSent += connexion_MessageSent;
                    c.TransportAdded += connexion_TransportAdded;
                    foreach (ITransport t in c.Transports) { connexion_TransportAdded(c, t); }
                }
            }
        }

        private void server_ClientsRemoved(ICollection<IConnexion> list)
        {
            lock (this)
            {
                connectionsCount = Math.Max(0, connectionsCount - list.Count);
            }
        }

        #endregion

        #region Connexion 
        
        private void connexion_TransportAdded(IConnexion connexion, ITransport newTransport)
        {
            newTransport.PacketSentEvent += transport_PacketSent;
            newTransport.PacketReceivedEvent += transport_PacketReceived;
        }

        private void transport_PacketReceived(byte[] buffer, int offset, int count, ITransport transport)
        {
            bytesReceived += count;

            // Record the bytes per transport
            int value;
            if (!bytesRecvPerTransport.TryGetValue(transport.Name, out value)) { value = 0; }
            bytesRecvPerTransport[transport.Name] = value + count;
        }

        private void transport_PacketSent(byte[] buffer, int offset, int count, ITransport transport)
        {
            bytesSent += count;
            int value;

            // Record the bytes per transport
            if (!bytesSentPerTransport.TryGetValue(transport.Name, out value)) { value = 0; }
            bytesSentPerTransport[transport.Name] = value + count;
        }

        private void connexion_MessageReceived(Message m, IConnexion source, ITransport transport)
        {
            if (m.MessageType == MessageType.System) { return; }
            lock (this)
            {
                messagesReceived++;
                int value;

                // Record the messages per transport
                if (!msgsRecvPerTransport.TryGetValue(transport.Name, out value)) { value = 0; }
                msgsRecvPerTransport[transport.Name] = value + 1;

                // Record the messages per channel per message-type
                IDictionary<MessageType, IDictionary<string, int>> subdict;
                if (!messagesReceivedCounts.TryGetValue(m.Id, out subdict))
                {
                    subdict = messagesReceivedCounts[m.Id] =
                        new Dictionary<MessageType, IDictionary<string, int>>();
                }
                IDictionary<string, int> transDict;
                if (!subdict.TryGetValue(m.MessageType, out transDict))
                {
                    transDict = subdict[m.MessageType] = new Dictionary<string, int>();
                }
                if (!transDict.TryGetValue(transport.Name, out value))
                {
                    value = 0;
                }
                transDict[transport.Name] = ++value;
            }
        }

        private void connexion_MessageSent(Message m, IConnexion dest, ITransport transport)
        {
            if (m.MessageType == MessageType.System) { return; }
            lock (this)
            {
                messagesSent++;

                int value;

                // Record the messages per transport
                if (!msgsSentPerTransport.TryGetValue(transport.Name, out value)) { value = 0; }
                msgsSentPerTransport[transport.Name] = value + 1;
                
                // Record the messages per channel per message-type
                IDictionary<MessageType, IDictionary<string, int>> subdict;
                if (!messagesSentCounts.TryGetValue(m.Id, out subdict))
                {
                    subdict = messagesSentCounts[m.Id] =
                        new Dictionary<MessageType, IDictionary<string, int>>();
                }
                IDictionary<string, int> transDict;
                if (!subdict.TryGetValue(m.MessageType, out transDict))
                {
                    transDict = subdict[m.MessageType] = new Dictionary<string, int>();
                }
                if (!transDict.TryGetValue(transport.Name, out value))
                {
                    value = 0;
                }
                transDict[transport.Name] = ++value;
            }
        }

        #endregion

    }
}
