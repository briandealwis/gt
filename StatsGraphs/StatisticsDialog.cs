using System;
using System.Threading;
using System.Windows.Forms;
using GT.Net;
using SoftwareFX.ChartFX.Lite;
using StatsGraphs;

namespace GT.StatsGraphs
{
    public partial class StatisticsDialog : Form
    {
        protected readonly TimeSpan MonitoredTimePeriod = TimeSpan.FromSeconds(5 * 60);

        protected Communicator _observed;
        protected CommunicationStatisticsObserver<Communicator> _statsObserver;
        protected int tickCount = 0;

        protected PingTimesForm _pingTimes;
        protected BacklogForm _backlog;

        /// <summary>
        /// Get/set the update interval in milliseconds.
        /// </summary>
        public TimeSpan Interval
        {
            get { return TimeSpan.FromMilliseconds(_timer.Interval); }
            set { _timer.Interval = (int)value.TotalMilliseconds; }
        }

        public static Thread On(Communicator c)
        {
            Thread thread = new Thread(new ThreadStart(delegate {
                Application.Run(new StatisticsDialog(c));
            }));
            thread.IsBackground = true;
            thread.Start();
            return thread;
        }

        public StatisticsDialog(Communicator c) : this()
        {
            Observe(c);
        }

        public StatisticsDialog()
        {
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
            _statsObserver.Reset();
        }

        /// <summary>
        /// Reset the values that are updated each tick
        /// </summary>
        public void ResetTransientValues()
        {
            _statsObserver.Reset();
        }

        public bool Paused
        {
            get { return _statsObserver.Paused; }
            set { _statsObserver.Paused = value; }
        }
        
        private void UpdateTitle()
        {
            if (_observed == null)
            {
                Text = "Statistics";
            }
            else
            {
                Text = "Statistics: " + _observed;
            }
        }

        private void _resetValuesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetLifetimeCounts();
        }

        private void _changeIntervalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Divide by 1000 as IntervalDialog deals in seconds, not milliseconds
            IntervalDialog id = new IntervalDialog(Interval);   
            if (id.ShowDialog(this) == DialogResult.OK)
            {
                Interval = id.Interval;
                if (_pingTimes != null) { _pingTimes.Interval = Interval; }
                if (_backlog != null) { _backlog.Interval = Interval; }
            }
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            BeginInvoke(new MethodInvoker(UpdateGraphs));
        }

        private void UpdateGraphs()
        {
            lock (this)
            {
                UpdateTitle();
                StatisticsSnapshot stats = _statsObserver.Reset();

                // Interval is in milliseconds
                int numberDataPoints = 
                    (int)(MonitoredTimePeriod.TotalMilliseconds / Interval.TotalMilliseconds);
                int index = tickCount % numberDataPoints;

                if (!Paused)
                {
                    /// advance the tickCount for the next
                    tickCount++;

                    /// the # of current connections
                    if (index == 0)
                    {
                        connectionsGraph.ClearData(ClearDataFlag.Values);
                    }
                    connectionsGraph.OpenData(COD.Values, 1, numberDataPoints);
                    //clientsGraph.AxisX.LabelsFormat.CustomFormat = "#.##";
                    connectionsGraph.Value[0, index] = stats.ConnexionCount;
                    connectionsGraph.CloseData(COD.Values);

                    /// the # messages sent & received
                    if (index == 0)
                    {
                        messagesGraph.ClearData(ClearDataFlag.Values);
                    }
                    messagesGraph.OpenData(COD.Values, 2, numberDataPoints);
                    //messagesGraph.AxisX.LabelsFormat.CustomFormat = "#.##";
                    messagesGraph.Value[0, index] = stats.MessagesSent;
                    messagesGraph.Value[1, index] = stats.MessagesReceived;
                    messagesGraph.CloseData(COD.Values);

                    /// The # bytes sent & received
                    if (index == 0)
                    {
                        bytesGraph.ClearData(ClearDataFlag.Values);
                    }
                    bytesGraph.OpenData(COD.Values, 2, numberDataPoints);
                    //bytesGraph.AxisX.LabelsFormat.CustomFormat = "#.##";
                    bytesGraph.Value[0, index] = stats.BytesSent;
                    bytesGraph.Value[1, index] = stats.BytesReceived;
                    bytesGraph.CloseData(COD.Values);

                    /// # messages and bytes sent & received per transport
                    msgsPerTransportGraph.OpenData(COD.Values, 2 * stats.TransportNames.Count, 
                        numberDataPoints);
                    bytesPerTransportGraph.OpenData(COD.Values, 2 * stats.TransportNames.Count, 
                        numberDataPoints);

                    int i = 0;
                    foreach(string tn in stats.TransportNames)
                    {
                        msgsPerTransportGraph.Value[2 * i, index] = stats.ComputeMessagesSentByTransport(tn);
                        msgsPerTransportGraph.SerLeg[2 * i] = tn + " sent";
                        msgsPerTransportGraph.Value[2 * i + 1, index] = stats.ComputeMessagesReceivedByTransport(tn);
                        msgsPerTransportGraph.SerLeg[2 * i + 1] = tn + " recvd";

                        bytesPerTransportGraph.Value[2*i, index] = stats.ComputeBytesSentByTransport(tn);
                        bytesPerTransportGraph.SerLeg[2*i] = tn + " sent";
                        bytesPerTransportGraph.Value[2*i + 1, index] = stats.ComputeBytesReceivedByTransport(tn);
                        bytesPerTransportGraph.SerLeg[2*i + 1] = tn + " recvd";
                        i++;
                    }
                    msgsPerTransportGraph.CloseData(COD.Values);
                    bytesPerTransportGraph.CloseData(COD.Values);

                    /// Pie charts
                    messagesSentPiechart.OpenData(COD.Values, 1, (int) COD.Unknown);
                    index = 0;
                    foreach (byte channelId in stats.SentChannelIds)
                    {
                        foreach (MessageType t in Enum.GetValues(typeof(MessageType)))
                        {
                            int count = stats.ComputeMessagesSent(channelId, t);
                            if (count == 0) { continue; }
                            messagesSentPiechart.Value[0, index] = count;
                            messagesSentPiechart.Legend[index] = channelId + " " + t;
                            index++;
                        }
                    }
                    messagesSentPiechart.CloseData(COD.Values);

                    messagesReceivedPiechart.OpenData(COD.Values, 1, (int) COD.Unknown);
                    index = 0;
                    foreach (byte channelId in stats.ReceivedChannelIds)
                    {
                        foreach (MessageType t in Enum.GetValues(typeof(MessageType)))
                        {
                            int count = stats.ComputeMessagesReceived(channelId, t);
                            if (count == 0) { continue; }
                            messagesReceivedPiechart.Value[0, index] = count;
                            messagesReceivedPiechart.Legend[index] = channelId + " " + t;
                            index++;
                        }
                    }
                    messagesReceivedPiechart.CloseData(COD.Values);
                }   // !Paused
                ResetTransientValues();
            }
        }

        #region Client Observations

        public void Observe(Communicator comm)
        {
            _observed = comm;
            _statsObserver = CommunicationStatisticsObserver<Communicator>.On(comm);
            if (Visible) { BeginInvoke(new MethodInvoker(UpdateTitle)); }
        }

        #endregion

        private void _btnPingTimes_Click(object sender, EventArgs e)
        {
            _pingTimes = new PingTimesForm(_observed);
            _pingTimes.Interval = Interval;
            _pingTimes.Show();
        }

        private void _btnBacklog_Click(object sender, EventArgs e)
        {
            _backlog = new BacklogForm(_observed);
            _backlog.Interval = Interval;
            _backlog.Show();
        }


    }
}
