using System;
using System.Windows.Forms;

namespace StatsGraphs
{
    /// <summary>
    /// A dialog for prompting for a particular interval in seconds (as a float). 
    /// Expected use:
    /// <pre>
    /// IntervalDialog id = new IntervalDialog(0.5);
    /// if(id.ShowDialog(otherForm) == DialogResult.OK) {
    ///     interval = id.Interval;
    /// }
    /// </pre>
    /// </summary>
    public partial class IntervalDialog : Form
    {
        protected const float maxResolution = 0.5f;
        protected const float minResolution = 0.2f;

        protected float minimum = 0.0f;
        protected float maximum = 1.0f;
        protected float resolution = 1.0f;

        public IntervalDialog(float value)
        {
            InitializeComponent();
            Interval = value;
        }

        public float Interval
        {
            get { return float.Parse(textValue.Text); }
            set
            {
                if (value < 0) { throw new ArgumentException("value must be >= 0"); }
                SetTrackBar(value);
                SetTextValue(value);
            }
        }

        protected int ToTrackBar(float v) {
            return (int)((v - minimum) / minResolution);
        }

        protected float FromTrackBar(int tb)
        {
            return minimum + tb * minResolution;
        }

        protected void SetTrackBar(float v)
        {
            Accomodate(0, v);
            trackBar1.Value = ToTrackBar(v);
        }

        protected void SetTextValue(float v)
        {
            textValue.Text = String.Format("{0:0.0}", v);
        }

        protected void Accomodate(float min, float max)
        {
            if (min < minimum) { minimum = min; }
            if (max > maximum) { maximum = max; }
            trackBarMin.Text = minimum.ToString();
            trackBarMax.Text = maximum.ToString();

            trackBar1.Minimum = 0;
            trackBar1.Maximum = (int)Math.Ceiling((maximum - minimum) / minResolution);

            if (trackBar1.Maximum < trackBar1.Width) {
                trackBar1.TickFrequency = (int)(60f / minResolution);   // every minute
            }
            else
            {
                trackBar1.TickFrequency = 1;    // every second
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            SetTextValue(FromTrackBar(trackBar1.Value));
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
        }

    }
}
