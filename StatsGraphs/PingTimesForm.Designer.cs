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

namespace StatsGraphs
{
    partial class PingTimesForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._pingTimes = new SoftwareFX.ChartFX.Lite.Chart();
            this._timer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // _pingTimes
            // 
            this._pingTimes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this._pingTimes.AxisX.ForceZero = false;
            this._pingTimes.AxisX.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this._pingTimes.AxisX.Max = 60;
            this._pingTimes.AxisX.Min = 0;
            this._pingTimes.AxisY.Gridlines = true;
            this._pingTimes.AxisY.LabelsFormat.Decimals = 0;
            this._pingTimes.AxisY.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this._pingTimes.AxisY.Title.Text = "Pings (ms)";
            this._pingTimes.BorderObject = new SoftwareFX.ChartFX.Lite.DefaultBorder(SoftwareFX.ChartFX.Lite.BorderType.None, System.Drawing.SystemColors.ControlDarkDark);
            this._pingTimes.BottomGap = 5;
            this._pingTimes.Gallery = SoftwareFX.ChartFX.Lite.Gallery.Lines;
            this._pingTimes.InsideColor = System.Drawing.Color.Black;
            this._pingTimes.LeftGap = 5;
            this._pingTimes.Location = new System.Drawing.Point(12, 12);
            this._pingTimes.MarkerShape = SoftwareFX.ChartFX.Lite.MarkerShape.None;
            this._pingTimes.Name = "_pingTimes";
            this._pingTimes.NSeries = 1;
            this._pingTimes.NValues = 60;
            this._pingTimes.RightGap = 10;
            this._pingTimes.SerLegBox = true;
            this._pingTimes.Size = new System.Drawing.Size(525, 187);
            this._pingTimes.Stacked = SoftwareFX.ChartFX.Lite.Stacked.Normal;
            this._pingTimes.TabIndex = 13;
            this._pingTimes.TopGap = 5;
            // 
            // _timer
            // 
            this._timer.Interval = 5000;
            this._timer.Tick += new System.EventHandler(this._timer_Tick);
            // 
            // PingTimesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(549, 211);
            this.Controls.Add(this._pingTimes);
            this.Name = "PingTimesForm";
            this.Text = "Ping Times";
            this.Load += new System.EventHandler(this._Form_Load);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this._Form_Closed);
            this.ResumeLayout(false);

        }

        #endregion

        private SoftwareFX.ChartFX.Lite.Chart _pingTimes;
        private System.Windows.Forms.Timer _timer;
    }
}
