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
    partial class BacklogForm
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
            this._backlog = new SoftwareFX.ChartFX.Lite.Chart();
            this._timer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // _backlog
            // 
            this._backlog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this._backlog.AxisX.ForceZero = false;
            this._backlog.AxisX.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this._backlog.AxisX.Max = 60;
            this._backlog.AxisX.Min = 0;
            this._backlog.AxisY.Gridlines = true;
            this._backlog.AxisY.LabelsFormat.Decimals = 0;
            this._backlog.AxisY.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this._backlog.AxisY.Title.Text = "Message Backlog";
            this._backlog.BorderObject = new SoftwareFX.ChartFX.Lite.DefaultBorder(SoftwareFX.ChartFX.Lite.BorderType.None, System.Drawing.SystemColors.ControlDarkDark);
            this._backlog.BottomGap = 5;
            this._backlog.Gallery = SoftwareFX.ChartFX.Lite.Gallery.Lines;
            this._backlog.InsideColor = System.Drawing.Color.Black;
            this._backlog.LeftGap = 5;
            this._backlog.Location = new System.Drawing.Point(12, 12);
            this._backlog.MarkerShape = SoftwareFX.ChartFX.Lite.MarkerShape.None;
            this._backlog.Name = "_backlog";
            this._backlog.NSeries = 1;
            this._backlog.NValues = 60;
            this._backlog.RightGap = 10;
            this._backlog.SerLegBox = true;
            this._backlog.Size = new System.Drawing.Size(525, 187);
            this._backlog.Stacked = SoftwareFX.ChartFX.Lite.Stacked.Normal;
            this._backlog.TabIndex = 13;
            this._backlog.TopGap = 5;
            // 
            // _timer
            // 
            this._timer.Interval = 5000;
            this._timer.Tick += new System.EventHandler(this._timer_Tick);
            // 
            // BacklogForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(549, 211);
            this.Controls.Add(this._backlog);
            this.Name = "BacklogForm";
            this.Text = "Message Backlogs";
            this.Load += new System.EventHandler(this._Form_Load);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this._Form_Closed);
            this.ResumeLayout(false);

        }

        #endregion

        private SoftwareFX.ChartFX.Lite.Chart _backlog;
        private System.Windows.Forms.Timer _timer;
    }
}
