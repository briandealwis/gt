using System;
using System.Windows.Forms;

namespace BBall.UI
{
    public partial class DelayForm : Form
    {
        public DelayForm()
        {
            InitializeComponent();
        }

        public TimeSpan Delay
        {
            get { return TimeSpan.FromMilliseconds(float.Parse(textValue.Text)); }
            set
            {
                if (value.CompareTo(TimeSpan.Zero) < 0) { throw new ArgumentException("value must be >= 0"); }
                SetTrackBar(value.TotalMilliseconds);
                SetTextValue(value.TotalMilliseconds);
            }
        }

        protected int ToTrackBar(double v)
        {
            return (int)Math.Min(trackBar1.Maximum, Math.Max(trackBar1.Minimum, v));
        }

        protected double FromTrackBar(int tb)
        {
            return tb;
        }

        protected void SetTrackBar(double v)
        {
            trackBar1.Value = ToTrackBar(v);
        }

        protected void SetTextValue(double v)
        {
            textValue.Text = String.Format("{0:0.0}", v);
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            SetTextValue(FromTrackBar(trackBar1.Value));
            if (Changed != null) { Changed((uint)trackBar1.Value); }
        }

        private void textValue_TextChanged(object sender, EventArgs e)
        {
            try
            {
                SetTrackBar(float.Parse(textValue.Text));
            }
            catch (FormatException)
            {
                SetTextValue(FromTrackBar(trackBar1.Value));
            }
            if (Changed != null) { Changed((uint)trackBar1.Value); }
        }


        public event Action<uint> Changed;
    }
}
