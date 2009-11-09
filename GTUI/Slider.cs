using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace GT.UI
{
    [DefaultEvent("ValueChanged")]
    public partial class Slider : UserControl
    {
        [Browsable(true),Description("Triggered when the slider value is changed."),
        Category("Action")]
        public EventHandler ValueChanged;

        protected float minimum = 0.0f;
        protected float maximum = 5.0f;
        protected float resolution = 0.1f;
        protected float tickFrequency = 0.2f;
        protected float largeChange = 1f;

        protected string textValueFormat;

        public Slider()
        {
            InitializeComponent();
            UpdateBounds();
        }

        [Browsable(true), Category("Behavior"),
        Description("The units of the value.")]
        public string Units
        {
            get { return unitsLabel.Text; }
            set { unitsLabel.Text = value ?? string.Empty; }
        }

        [Browsable(true), Category("Behavior"), 
        Description("Show the minimum and maximum values below the slider.")]
        public bool ShowMinMaxValues
        {
            get { return minLabel.Visible && maxLabel.Visible; }
            set { minLabel.Visible = maxLabel.Visible = value; }
        }

        [Browsable(true), Category("Behavior"), 
        Description("The slider value.")]
        public float Value
        {
            get
            {
                try
                {
                    return float.Parse(textValue.Text);
                } catch(FormatException)
                {
                    return FromTrackBar(trackBar.Value);
                }
            }
            set
            {
                if (value < Minimum)
                {
                    Value = Minimum;
                }
                else if (value > Maximum)
                {
                    Value = Maximum;
                }
                else
                {
                    SetTrackBar(value);
                    SetTextValue(value);
                }
                OnValueChanged();
            }
        }

        [Browsable(true), Category("Behavior"), 
        Description("The minimum slider value.")]
        public float Minimum
        {
            get { return minimum; }
            set
            {
                minimum = value;
                BoundsUpdated();
            }
        }

        [Browsable(true), Category("Behavior"), 
        Description("The maximum slider value.")]
        public float Maximum
        {
            get { return maximum; }
            set
            {
                maximum = value;
                BoundsUpdated();
            }
        }

        [Browsable(true), Category("Behavior"), 
        Description("The finest resolution of the slider area.")]
        public float Resolution
        {
            get { return resolution; }
            set
            {
                if (value > 0 && value < Maximum - Minimum)
                {
                    resolution = value;
                    BoundsUpdated();
                }
            }
        }

        [Browsable(true), Category("Behavior"), 
        Description("The tick frequency in the slider area.")]
        public float TickFrequency
        {
            get { return tickFrequency; }
            set
            {
                if (value > 0 && value < Maximum - Minimum)
                {
                    tickFrequency = value;
                    BoundsUpdated();
                }
            }
        }

        [Browsable(true), Category("Behavior"), 
        Description("The change from clicking on the slider area.")]
        public float LargeChange
        {
            get { return largeChange; }
            set {
                if (value > 0 && value < Maximum - Minimum)
                {
                    largeChange = value;
                    BoundsUpdated();
                }
            }
        }

        private void BoundsUpdated()
        {
            float value = Value;
            minLabel.Text = minimum.ToString();
            maxLabel.Text = maximum.ToString();

            trackBar.Minimum = 0;
            trackBar.Maximum = (int) (Math.Ceiling(maximum - minimum) / resolution);
            trackBar.TickFrequency = Math.Max(2, (int)Math.Ceiling(tickFrequency/resolution));
            trackBar.LargeChange = (int) Math.Ceiling(largeChange/resolution);

            uint precision = DeterminePrecision(resolution);
            if(precision > 0)
            {
                textValueFormat = "{0:0.";
                while(precision-- > 0)
                {
                    textValueFormat += "0";
                }
                textValueFormat += "}";
            } else
            {
                textValueFormat = "{0}";
            }
            if (value < Minimum) { Value = Minimum; }
            else if (value > Maximum) { Value = Maximum; }
        }

        /// <summary>
        /// Determine the number of significant digits required to represent
        /// values with the provided resolution.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint DeterminePrecision(float value)
        {
            string text = value.ToString();
            int index = text.IndexOf('.');
            if (index < 0) { return 0; }

            uint count = 0;
            // skip over possibly-unnecessary zeros (e.g., "0.000")
            while (++index < text.Length && text[index] == '0') { count++; }
            if (index == text.Length) { return 0; } // unnecessary zeros
            // FIXME: could do check for floating error, e.g., 0.00999999999 => 0.01
            while (index++ < text.Length) { count++; }
            return count;
        }

        protected int ToTrackBar(float v) {
            return (int)((v - minimum) / resolution);
        }

        protected float FromTrackBar(int tb)
        {
            return minimum + tb * resolution;
        }

        protected void OnValueChanged()
        {
            if (ValueChanged != null) { ValueChanged(this, EventArgs.Empty); }
        }

        protected void SetTrackBar(float v)
        {
            trackBar.Value = ToTrackBar(v);
        }

        protected void SetTextValue(float v)
        {
            textValue.Text = String.Format(textValueFormat, v);
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            SetTextValue(FromTrackBar(trackBar.Value));
            OnValueChanged();
        }

        private void textValue_TextChanged(object sender, EventArgs e)
        {
            try
            {
                float value = float.Parse(textValue.Text);
                if (value >= Minimum && value <= Maximum)
                {
                    SetTrackBar(value);
                    OnValueChanged();
                    warningLabel.Visible = false;
                } else
                {
                    warningLabel.Visible = true;
                }
            }
            catch (FormatException)
            {
                warningLabel.Visible = true;
            }
        }

        private void Slider_Resize(object sender, EventArgs e)
        {
            BoundsUpdated();
        }


    }
}
