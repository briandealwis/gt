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

        protected double minimum = 0.0f;
        protected double maximum = 1.0f;
        protected double resolution = 1.0f;

        public IntervalDialog(TimeSpan value)
        {
            InitializeComponent();
            Interval = value;
        }

        public TimeSpan Interval
        {
            get { return TimeSpan.FromSeconds(float.Parse(textValue.Text)); }
            set
            {
                if (value.CompareTo(TimeSpan.Zero) < 0) { throw new ArgumentException("value must be >= 0"); }
                SetTrackBar(value.TotalSeconds);
                SetTextValue(value.TotalSeconds);
            }
        }

        protected int ToTrackBar(double v) {
            return (int)((v - minimum) / minResolution);
        }

        protected double FromTrackBar(int tb)
        {
            return minimum + tb * minResolution;
        }

        protected void SetTrackBar(double v)
        {
            Accomodate(0, v);
            trackBar1.Value = ToTrackBar(v);
        }

        protected void SetTextValue(double v)
        {
            textValue.Text = String.Format("{0:0.0}", v);
        }

        protected void Accomodate(double min, double max)
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
