using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Windows.Forms;
using GT.Net;
using SoftwareFX.ChartFX.Lite;
using StatsGraphs;
using Message=System.Windows.Forms.Message;

namespace GT.StatsGraphs
{
    public partial class StatisticsDialog : Form
    {
        protected readonly int MonitoredTimePeriod = 5 * 60;    // seconds

        protected Communicator _observed;
        protected CommunicationStatisticsObserver<Communicator> _statsObserver;
        protected int tickCount = 0;

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

        private void resetValuesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetLifetimeCounts();
        }

        private void changeIntervalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IntervalDialog id = new IntervalDialog((float)_timer.Interval / 1000f);
            if (id.ShowDialog(this) == DialogResult.OK)
            {
                _timer.Interval = (int)(id.Interval * 1000);
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
                StatisticsSnapshot _stats = _statsObserver.Reset();

                DateTime now = DateTime.Now;
                int numberDataPoints = MonitoredTimePeriod * 1000 / _timer.Interval;
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
                    connectionsGraph.Value[0, index] = _stats.ConnexionCount;
                    connectionsGraph.CloseData(COD.Values);

                    /// the # messages sent & received
                    if (index == 0)
                    {
                        messagesGraph.ClearData(ClearDataFlag.Values);
                    }
                    messagesGraph.OpenData(COD.Values, 2, numberDataPoints);
                    //messagesGraph.AxisX.LabelsFormat.CustomFormat = "#.##";
                    messagesGraph.Value[0, index] = _stats.MessagesSent;
                    messagesGraph.Value[1, index] = _stats.MessagesReceived;
                    messagesGraph.CloseData(COD.Values);

                    /// The # bytes sent & received
                    if (index == 0)
                    {
                        bytesGraph.ClearData(ClearDataFlag.Values);
                    }
                    bytesGraph.OpenData(COD.Values, 2, numberDataPoints);
                    //bytesGraph.AxisX.LabelsFormat.CustomFormat = "#.##";
                    bytesGraph.Value[0, index] = _stats.BytesSent;
                    bytesGraph.Value[1, index] = _stats.BytesReceived;
                    bytesGraph.CloseData(COD.Values);

                    /// # messages and bytes sent & received per transport
                    msgsPerTransportGraph.OpenData(COD.Values, 2 * _stats.TransportNames.Count, 
                        numberDataPoints);
                    bytesPerTransportGraph.OpenData(COD.Values, 2 * _stats.TransportNames.Count, 
                        numberDataPoints);

                    int i = 0;
                    foreach(string tn in _stats.TransportNames)
                    {
                        msgsPerTransportGraph.Value[2 * i, index] = _stats.ComputeMessagesSentByTransport(tn);
                        msgsPerTransportGraph.SerLeg[2 * i] = tn + " sent";
                        msgsPerTransportGraph.Value[2 * i + 1, index] = _stats.ComputeMessagesReceivedByTransport(tn);
                        msgsPerTransportGraph.SerLeg[2 * i + 1] = tn + " recvd";

                        bytesPerTransportGraph.Value[2*i, index] = _stats.ComputeBytesSentByTransport(tn);
                        bytesPerTransportGraph.SerLeg[2*i] = tn + " sent";
                        bytesPerTransportGraph.Value[2*i + 1, index] = _stats.ComputeBytesReceivedByTransport(tn);
                        bytesPerTransportGraph.SerLeg[2*i + 1] = tn + " recvd";
                        i++;
                    }
                    msgsPerTransportGraph.CloseData(COD.Values);
                    bytesPerTransportGraph.CloseData(COD.Values);


                    /// Pie charts
                    messagesSentPiechart.OpenData(COD.Values, 1, (int) COD.Unknown);
                    index = 0;
                    foreach (byte channel in _stats.SentChannels)
                    {
                        foreach (MessageType t in Enum.GetValues(typeof(MessageType)))
                        {
                            int count = _stats.ComputeMessagesSent(channel, t);
                            if (count == 0) { continue; }
                            messagesSentPiechart.Value[0, index] = count;
                            messagesSentPiechart.Legend[index] = channel + " " + t;
                            index++;
                        }
                    }
                    messagesSentPiechart.CloseData(COD.Values);

                    messagesReceivedPiechart.OpenData(COD.Values, 1, (int) COD.Unknown);
                    index = 0;
                    foreach (byte channel in _stats.ReceivedChannels)
                    {
                        foreach (MessageType t in Enum.GetValues(typeof(MessageType)))
                        {
                            int count = _stats.ComputeMessagesReceived(channel, t);
                            if (count == 0) { continue; }
                            messagesReceivedPiechart.Value[0, index] = count;
                            messagesReceivedPiechart.Legend[index] = channel + " " + t;
                            index++;
                        }
                    }
                    messagesReceivedPiechart.CloseData(COD.Values);
                    /// And finally advance the tickCount
                    tickCount++;
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


    }
}
