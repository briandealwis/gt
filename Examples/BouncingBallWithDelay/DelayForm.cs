using System;
using System.Windows.Forms;

namespace BBall.UI
{
    public partial class DelayForm : Form
    {
        public event Action<TimeSpan> DelayChanged;
        public event Action<TimeSpan> UpdateIntervalChanged;
        public event MethodInvoker Reset;

        public DelayForm()
        {
            InitializeComponent();
            delaySlider.ValueChanged += _delaySlider_ValueChanged;
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

        private void _delaySlider_ValueChanged(object sender, EventArgs e)
        {
            if (DelayChanged != null)
            {
                DelayChanged(TimeSpan.FromMilliseconds(delaySlider.Value));
            }
        }

        private void _updatetimeSlider_ValueChanged(object sender, EventArgs e)
        {
            if (UpdateIntervalChanged != null)
            {
                UpdateIntervalChanged(TimeSpan.FromMilliseconds(updatetimeSlider.Value));
            }
        }


        private void resetButton_Click(object sender, EventArgs e)
        {
            if (Reset != null) { Reset(); }
        }
    }
}
