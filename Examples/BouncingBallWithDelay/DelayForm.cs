using System;
using System.Text;
using System.Windows.Forms;
using GT.Net;
using GT.Net.Utils;

namespace BBall.UI
{
    public partial class DelayForm : Form
    {
        public event Action<TimeSpan> DelayChanged;
        public event Action<bool> SendUnreliablyChanged;
        public event Action<float> PacketLossChanged;
        public event Action<float> PacketLossCorrelationChanged;
        public event Action<float> PacketReorderingChanged;
        public event Action<TimeSpan> UpdateIntervalChanged;
        public event MethodInvoker Reset;

        public DelayForm()
        {
            InitializeComponent();
            delaySlider.ValueChanged += _delaySlider_ValueChanged;
            packetLossSlider.ValueChanged += _packetLossSlider_ValueChanged;
            packetLossCorrelationSlider.ValueChanged += _packetLossCorrelationSlider_ValueChanged;
            packetReorderingSlider.ValueChanged += _packetReorderingSlider_ValueChanged;
            updatetimeSlider.ValueChanged += _updatetimeSlider_ValueChanged;
        }

        public TimeSpan Delay
        {
            get { return TimeSpan.FromMilliseconds(delaySlider.Value); }
            set
            {
                if (value.CompareTo(TimeSpan.Zero) < 0) { throw new ArgumentException("value must be >= 0"); }
                delaySlider.Value = (float)value.TotalMilliseconds;
            }
        }

        public TimeSpan UpdateInterval
        {
            get { return TimeSpan.FromMilliseconds(updatetimeSlider.Value); }
            set
            {
                if (value.CompareTo(TimeSpan.Zero) < 0) { throw new ArgumentException("value must be >= 0"); }
                updatetimeSlider.Value = (float)value.TotalMilliseconds;
            }
        }

        /// <summary>
        /// Probability of a packet being lost on [0,1].
        /// </summary>
        public float PacketLoss
        {
            get { return packetLossSlider.Value / 100f; }
            set
            {
                if (value < 0 || value > 1) { throw new ArgumentException("value must be [0,1]"); }
                packetLossSlider.Value = (float)value * 100f;
            }
        }

        /// <summary>
        /// Probability of a packet loss being correlated on [0,1]
        /// </summary>
        public float PacketLossCorrelation
        {
            get { return packetLossCorrelationSlider.Value / 100f; }
            set
            {
                if (value < 0 || value > 100) { throw new ArgumentException("value must be [0,1]"); }
                packetLossCorrelationSlider.Value = (float)value * 100f;
            }
        }

        /// <summary>
        /// Probability of a packet being reordered on [0,1]
        /// </summary>
        public float PacketReordering
        {
            get { return packetReorderingSlider.Value / 100f; }
            set
            {
                if (value < 0 || value > 1) { throw new ArgumentException("value must be [0,1]"); }
                packetReorderingSlider.Value = (float)value * 100f;
            }
        }

        public bool SendUnreliably
        {
            get { return sendUnreliablyCheckbox.Checked; }
            set { sendUnreliablyCheckbox.Checked = value; }
        }

        public void ReportDisposition(NetworkEmulatorTransport.PacketMode mode, 
            NetworkEmulatorTransport.PacketEffect effect, TransportPacket packet)
        {
            StringBuilder report = new StringBuilder(60);
            switch(effect)
            {
                case NetworkEmulatorTransport.PacketEffect.Delayed:
                    report.Append("D");
                    break;
                case NetworkEmulatorTransport.PacketEffect.Reordered:
                    report.Append("R");
                    break;
                case NetworkEmulatorTransport.PacketEffect.Dropped:
                    report.Append("_");
                    break;
                case NetworkEmulatorTransport.PacketEffect.None:
                    report.Append(" ");
                    break;
            }
            report.Append(Text.Length > 60 ? Text.Substring(0, 58) : Text);
            Text = report.ToString();
        }

        private void _delaySlider_ValueChanged(object sender, EventArgs e)
        {
            if (DelayChanged != null)
            {
                DelayChanged(Delay);
            }
        }

        private void _packetLossSlider_ValueChanged(object sender, EventArgs e)
        {
            if (PacketLossChanged != null)
            {
                PacketLossChanged(PacketLoss);
            }
        }

        private void _packetLossCorrelationSlider_ValueChanged(object sender, EventArgs e)
        {
            if (PacketLossCorrelationChanged != null)
            {
                PacketLossCorrelationChanged(PacketLossCorrelation);
            }
        }

        private void _packetReorderingSlider_ValueChanged(object sender, EventArgs e)
        {
            if (PacketReorderingChanged != null)
            {
                PacketReorderingChanged(PacketReordering);
            }
        }

        private void _updatetimeSlider_ValueChanged(object sender, EventArgs e)
        {
            if (UpdateIntervalChanged != null)
            {
                UpdateIntervalChanged(UpdateInterval);
            }
        }


        private void resetButton_Click(object sender, EventArgs e)
        {
            if (Reset != null) { Reset(); }
        }

        private void sendUnreliablyCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            groupBox1.Enabled = sendUnreliablyCheckbox.Checked;
            packetLossSlider.Enabled = sendUnreliablyCheckbox.Checked;
            packetLossCorrelationSlider.Enabled = sendUnreliablyCheckbox.Checked;
            packetReorderingSlider.Enabled = sendUnreliablyCheckbox.Checked;
            SendUnreliablyChanged(sendUnreliablyCheckbox.Checked);
        }
    }
}
