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
using System.Windows.Forms;
using GT.Net;
using GT.Utils;
using SoftwareFX.ChartFX.Lite;

namespace StatsGraphs
{
    public partial class PingTimesForm : Form
    {
        protected readonly int MonitoredTimePeriod = 5 * 60;    // seconds

        protected Communicator _observed;
        protected CommunicationStatisticsObserver<Communicator> _statsObserver;
        protected int tickCount = 0;
        protected SequentialSet<IConnexion> _connexions = new SequentialSet<IConnexion>();

        /// <summary>
        /// Get/set the update interval
        /// </summary>
        public TimeSpan Interval { 
            get { return TimeSpan.FromMilliseconds(_timer.Interval); } 
            set { _timer.Interval = (int)value.TotalMilliseconds; } 
        }

        public PingTimesForm(Communicator comm)
        {
            InitializeComponent();
            _pingTimes.ClearData(ClearDataFlag.Values);
            _pingTimes.AxisX.Min = 0;

            Text += ": " + comm;
            _observed = comm;
            _statsObserver = CommunicationStatisticsObserver<Communicator>.On(comm);
        }

        public void UpdateGraphs()
        {
            StatisticsSnapshot stats = _statsObserver.Reset();

            // Divide by 1000 as MonitoredTimePeriod is in seconds, not milliseconds
            int numberDataPoints = MonitoredTimePeriod * 1000 / _timer.Interval;
            int index = tickCount++ % numberDataPoints;

            _pingTimes.OpenData(COD.Values, stats.ConnexionCount, numberDataPoints);
            int i = 0;
            // The order will change upon any new connexion or new transport; we just have to accept it.
            foreach(IConnexion cnx in stats.Delays.Keys)
            {
                foreach (string tn in stats.TransportNames)
                {
                    _pingTimes.Value[i, index] = stats.Delays[cnx].ContainsKey(tn) 
                        ? stats.Delays[cnx][tn] : 0;
                    _pingTimes.SerLeg[i] = cnx.Identity + ":" + tn;
                    i++;
                }
            }
            _pingTimes.CloseData(COD.Values);
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
