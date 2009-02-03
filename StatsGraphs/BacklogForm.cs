using System;
using System.Windows.Forms;
using GT.Net;
using SoftwareFX.ChartFX.Lite;

namespace StatsGraphs
{
    public partial class BacklogForm : Form
    {
        protected readonly int MonitoredTimePeriod = 5 * 60;    // seconds

        protected Communicator _observed;
        protected CommunicationStatisticsObserver<Communicator> _statsObserver;
        protected int tickCount = 0;

        /// <summary>
        /// Get/set the update interval in milliseconds.
        /// </summary>
        public int Interval { 
            get { return _timer.Interval; } 
            set { _timer.Interval = value; } 
        }

        public BacklogForm(Communicator comm)
        {
            InitializeComponent();
            _backlog.ClearData(ClearDataFlag.Values);
            _backlog.AxisX.Min = 0;

            Text += ": " + comm.ToString();
            _observed = comm;
            _statsObserver = CommunicationStatisticsObserver<Communicator>.On(comm);
        }

        public void UpdateGraphs()
        {
            StatisticsSnapshot stats = _statsObserver.Reset();

            // Divide by 1000 as MonitoredTimePeriod is in seconds, not milliseconds
            int numberDataPoints = MonitoredTimePeriod * 1000 / _timer.Interval;
            int index = tickCount++ % numberDataPoints;

            _backlog.OpenData(COD.Values, stats.ConnexionCount * stats.TransportNames.Count, numberDataPoints);
            int i = 0;
            // The order will change upon any new connexion or new transport; we just have to accept it.
            foreach (IConnexion cnx in stats.Backlogs.Keys)
            {
                foreach (string tn in stats.TransportNames)
                {
                    _backlog.Value[i, index] = stats.Backlogs[cnx].ContainsKey(tn) 
                        ? stats.Backlogs[cnx][tn] : 0;
                    _backlog.SerLeg[i] = cnx.Identity + ":" + tn;
                    i++;
                }
            }
            _backlog.CloseData(COD.Values);
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            BeginInvoke(new MethodInvoker(UpdateGraphs));
        }

        private void _Form_Load(object sender, EventArgs e)
        {
            _timer.Enabled = true;
        }

        private void _Form_Closed(object sender, FormClosedEventArgs e)
        {
            _timer.Enabled = false;
            _statsObserver.Dispose();
        }
    }
}
