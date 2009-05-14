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

namespace GT.StatsGraphs
{
    partial class StatisticsDialog
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
            this.connectionsGraph = new SoftwareFX.ChartFX.Lite.Chart();
            this._timer = new System.Windows.Forms.Timer(this.components);
            this.messagesGraph = new SoftwareFX.ChartFX.Lite.Chart();
            this.messagesReceivedPiechart = new SoftwareFX.ChartFX.Lite.Chart();
            this.messagesSentPiechart = new SoftwareFX.ChartFX.Lite.Chart();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.bytesGraph = new SoftwareFX.ChartFX.Lite.Chart();
            this.bytesPerTransportGraph = new SoftwareFX.ChartFX.Lite.Chart();
            this.msgsPerTransportGraph = new SoftwareFX.ChartFX.Lite.Chart();
            this._btnChangeInterval = new System.Windows.Forms.Button();
            this.resetValuesButton = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this._btnPingTimes = new System.Windows.Forms.Button();
            this._btnBacklog = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // connectionsGraph
            // 
            this.connectionsGraph.AxisX.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this.connectionsGraph.AxisX.Max = 60;
            this.connectionsGraph.AxisX.Min = 0;
            this.connectionsGraph.AxisY.Gridlines = true;
            this.connectionsGraph.AxisY.LabelsFormat.Decimals = 0;
            this.connectionsGraph.AxisY.Title.Text = "Connections";
            this.connectionsGraph.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.connectionsGraph.Border = false;
            this.connectionsGraph.BorderObject = new SoftwareFX.ChartFX.Lite.DefaultBorder(SoftwareFX.ChartFX.Lite.BorderType.None, System.Drawing.SystemColors.ControlDarkDark);
            this.connectionsGraph.BottomGap = 5;
            this.connectionsGraph.Cursor = System.Windows.Forms.Cursors.Cross;
            this.connectionsGraph.Gallery = SoftwareFX.ChartFX.Lite.Gallery.Lines;
            this.connectionsGraph.InsideColor = System.Drawing.Color.Black;
            this.connectionsGraph.LeftGap = 5;
            this.connectionsGraph.Location = new System.Drawing.Point(360, 20);
            this.connectionsGraph.MarkerShape = SoftwareFX.ChartFX.Lite.MarkerShape.None;
            this.connectionsGraph.Name = "connectionsGraph";
            this.connectionsGraph.NSeries = 1;
            this.connectionsGraph.NValues = 60;
            this.connectionsGraph.RightGap = 10;
            this.connectionsGraph.Size = new System.Drawing.Size(365, 90);
            this.connectionsGraph.TabIndex = 0;
            this.connectionsGraph.TopGap = 5;
            // 
            // _timer
            // 
            this._timer.Enabled = true;
            this._timer.Interval = 1000;
            this._timer.Tick += new System.EventHandler(this._timer_Tick);
            // 
            // messagesGraph
            // 
            this.messagesGraph.AxisX.ForceZero = false;
            this.messagesGraph.AxisX.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this.messagesGraph.AxisX.Max = 60;
            this.messagesGraph.AxisX.Min = 0;
            this.messagesGraph.AxisY.Gridlines = true;
            this.messagesGraph.AxisY.LabelsFormat.Decimals = 0;
            this.messagesGraph.AxisY.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this.messagesGraph.AxisY.Title.Text = "Messages";
            this.messagesGraph.BorderObject = new SoftwareFX.ChartFX.Lite.DefaultBorder(SoftwareFX.ChartFX.Lite.BorderType.None, System.Drawing.SystemColors.ControlDarkDark);
            this.messagesGraph.BottomGap = 5;
            this.messagesGraph.Gallery = SoftwareFX.ChartFX.Lite.Gallery.Lines;
            this.messagesGraph.InsideColor = System.Drawing.Color.Black;
            this.messagesGraph.LeftGap = 5;
            this.messagesGraph.Location = new System.Drawing.Point(360, 140);
            this.messagesGraph.MarkerShape = SoftwareFX.ChartFX.Lite.MarkerShape.None;
            this.messagesGraph.Name = "messagesGraph";
            this.messagesGraph.NSeries = 1;
            this.messagesGraph.NValues = 60;
            this.messagesGraph.RightGap = 10;
            this.messagesGraph.SerLegBox = true;
            this.messagesGraph.Size = new System.Drawing.Size(450, 90);
            this.messagesGraph.Stacked = SoftwareFX.ChartFX.Lite.Stacked.Normal;
            this.messagesGraph.TabIndex = 1;
            this.messagesGraph.TopGap = 5;
            // 
            // messagesReceivedPiechart
            // 
            this.messagesReceivedPiechart.BorderObject = new SoftwareFX.ChartFX.Lite.DefaultBorder(SoftwareFX.ChartFX.Lite.BorderType.None, System.Drawing.SystemColors.ControlDarkDark);
            this.messagesReceivedPiechart.Gallery = SoftwareFX.ChartFX.Lite.Gallery.Pie;
            this.messagesReceivedPiechart.LegendBox = true;
            this.messagesReceivedPiechart.Location = new System.Drawing.Point(6, 283);
            this.messagesReceivedPiechart.Name = "messagesReceivedPiechart";
            this.messagesReceivedPiechart.NSeries = 1;
            this.messagesReceivedPiechart.Size = new System.Drawing.Size(339, 220);
            this.messagesReceivedPiechart.TabIndex = 2;
            // 
            // messagesSentPiechart
            // 
            this.messagesSentPiechart.BorderObject = new SoftwareFX.ChartFX.Lite.DefaultBorder(SoftwareFX.ChartFX.Lite.BorderType.None, System.Drawing.SystemColors.ControlDarkDark);
            this.messagesSentPiechart.Gallery = SoftwareFX.ChartFX.Lite.Gallery.Pie;
            this.messagesSentPiechart.LegendBox = true;
            this.messagesSentPiechart.Location = new System.Drawing.Point(2, 40);
            this.messagesSentPiechart.Name = "messagesSentPiechart";
            this.messagesSentPiechart.NSeries = 1;
            this.messagesSentPiechart.Size = new System.Drawing.Size(341, 215);
            this.messagesSentPiechart.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(233, 53);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(104, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Messages Received";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(253, 286);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Messages Sent";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // bytesGraph
            // 
            this.bytesGraph.AxisX.ForceZero = false;
            this.bytesGraph.AxisX.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this.bytesGraph.AxisX.Max = 60;
            this.bytesGraph.AxisX.Min = 0;
            this.bytesGraph.AxisY.Gridlines = true;
            this.bytesGraph.AxisY.LabelsFormat.Decimals = 0;
            this.bytesGraph.AxisY.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this.bytesGraph.AxisY.Title.Text = "Bytes";
            this.bytesGraph.BorderObject = new SoftwareFX.ChartFX.Lite.DefaultBorder(SoftwareFX.ChartFX.Lite.BorderType.None, System.Drawing.SystemColors.ControlDarkDark);
            this.bytesGraph.BottomGap = 5;
            this.bytesGraph.Gallery = SoftwareFX.ChartFX.Lite.Gallery.Lines;
            this.bytesGraph.InsideColor = System.Drawing.Color.Black;
            this.bytesGraph.LeftGap = 5;
            this.bytesGraph.Location = new System.Drawing.Point(360, 335);
            this.bytesGraph.MarkerShape = SoftwareFX.ChartFX.Lite.MarkerShape.None;
            this.bytesGraph.Name = "bytesGraph";
            this.bytesGraph.NSeries = 1;
            this.bytesGraph.RightGap = 10;
            this.bytesGraph.SerLegBox = true;
            this.bytesGraph.Size = new System.Drawing.Size(450, 90);
            this.bytesGraph.TabIndex = 9;
            this.bytesGraph.TopGap = 5;
            // 
            // bytesPerTransportGraph
            // 
            this.bytesPerTransportGraph.AxisX.ForceZero = false;
            this.bytesPerTransportGraph.AxisX.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this.bytesPerTransportGraph.AxisX.Max = 60;
            this.bytesPerTransportGraph.AxisX.Min = 0;
            this.bytesPerTransportGraph.AxisY.Gridlines = true;
            this.bytesPerTransportGraph.AxisY.LabelsFormat.Decimals = 0;
            this.bytesPerTransportGraph.AxisY.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this.bytesPerTransportGraph.AxisY.Title.Text = "Bytes";
            this.bytesPerTransportGraph.BorderObject = new SoftwareFX.ChartFX.Lite.DefaultBorder(SoftwareFX.ChartFX.Lite.BorderType.None, System.Drawing.SystemColors.ControlDarkDark);
            this.bytesPerTransportGraph.BottomGap = 5;
            this.bytesPerTransportGraph.Gallery = SoftwareFX.ChartFX.Lite.Gallery.Lines;
            this.bytesPerTransportGraph.InsideColor = System.Drawing.Color.Black;
            this.bytesPerTransportGraph.LeftGap = 5;
            this.bytesPerTransportGraph.Location = new System.Drawing.Point(360, 431);
            this.bytesPerTransportGraph.MarkerShape = SoftwareFX.ChartFX.Lite.MarkerShape.None;
            this.bytesPerTransportGraph.Name = "bytesPerTransportGraph";
            this.bytesPerTransportGraph.NSeries = 1;
            this.bytesPerTransportGraph.NValues = 60;
            this.bytesPerTransportGraph.RightGap = 10;
            this.bytesPerTransportGraph.SerLegBox = true;
            this.bytesPerTransportGraph.Size = new System.Drawing.Size(450, 90);
            this.bytesPerTransportGraph.Stacked = SoftwareFX.ChartFX.Lite.Stacked.Normal;
            this.bytesPerTransportGraph.TabIndex = 12;
            this.bytesPerTransportGraph.TopGap = 5;
            // 
            // msgsPerTransportGraph
            // 
            this.msgsPerTransportGraph.AxisX.ForceZero = false;
            this.msgsPerTransportGraph.AxisX.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this.msgsPerTransportGraph.AxisX.Max = 60;
            this.msgsPerTransportGraph.AxisX.Min = 0;
            this.msgsPerTransportGraph.AxisY.Gridlines = true;
            this.msgsPerTransportGraph.AxisY.LabelsFormat.Decimals = 0;
            this.msgsPerTransportGraph.AxisY.LabelsFormat.Format = SoftwareFX.ChartFX.Lite.AxisFormat.Number;
            this.msgsPerTransportGraph.AxisY.Title.Text = "Messages";
            this.msgsPerTransportGraph.BorderObject = new SoftwareFX.ChartFX.Lite.DefaultBorder(SoftwareFX.ChartFX.Lite.BorderType.None, System.Drawing.SystemColors.ControlDarkDark);
            this.msgsPerTransportGraph.BottomGap = 5;
            this.msgsPerTransportGraph.Gallery = SoftwareFX.ChartFX.Lite.Gallery.Lines;
            this.msgsPerTransportGraph.InsideColor = System.Drawing.Color.Black;
            this.msgsPerTransportGraph.LeftGap = 5;
            this.msgsPerTransportGraph.Location = new System.Drawing.Point(360, 237);
            this.msgsPerTransportGraph.MarkerShape = SoftwareFX.ChartFX.Lite.MarkerShape.None;
            this.msgsPerTransportGraph.Name = "msgsPerTransportGraph";
            this.msgsPerTransportGraph.NSeries = 1;
            this.msgsPerTransportGraph.NValues = 60;
            this.msgsPerTransportGraph.RightGap = 10;
            this.msgsPerTransportGraph.SerLegBox = true;
            this.msgsPerTransportGraph.Size = new System.Drawing.Size(450, 90);
            this.msgsPerTransportGraph.Stacked = SoftwareFX.ChartFX.Lite.Stacked.Normal;
            this.msgsPerTransportGraph.TabIndex = 11;
            this.msgsPerTransportGraph.TopGap = 5;
            // 
            // _btnChangeInterval
            // 
            this._btnChangeInterval.Location = new System.Drawing.Point(122, 556);
            this._btnChangeInterval.Name = "_btnChangeInterval";
            this._btnChangeInterval.Size = new System.Drawing.Size(151, 26);
            this._btnChangeInterval.TabIndex = 13;
            this._btnChangeInterval.Text = "Change Update Interval";
            this._btnChangeInterval.UseVisualStyleBackColor = true;
            this._btnChangeInterval.Click += new System.EventHandler(this._changeIntervalToolStripMenuItem_Click);
            // 
            // resetValuesButton
            // 
            this.resetValuesButton.Location = new System.Drawing.Point(226, 13);
            this.resetValuesButton.Name = "resetValuesButton";
            this.resetValuesButton.Size = new System.Drawing.Size(115, 26);
            this.resetValuesButton.TabIndex = 14;
            this.resetValuesButton.Text = "Reset Values";
            this.resetValuesButton.UseVisualStyleBackColor = true;
            this.resetValuesButton.Click += new System.EventHandler(this._resetValuesToolStripMenuItem_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.resetValuesButton);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.messagesSentPiechart);
            this.groupBox1.Controls.Add(this.messagesReceivedPiechart);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(347, 509);
            this.groupBox1.TabIndex = 17;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Channels x Message Types";
            // 
            // _btnPingTimes
            // 
            this._btnPingTimes.Location = new System.Drawing.Point(331, 556);
            this._btnPingTimes.Name = "_btnPingTimes";
            this._btnPingTimes.Size = new System.Drawing.Size(151, 26);
            this._btnPingTimes.TabIndex = 18;
            this._btnPingTimes.Text = "Ping Times";
            this._btnPingTimes.UseVisualStyleBackColor = true;
            this._btnPingTimes.Click += new System.EventHandler(this._btnPingTimes_Click);
            // 
            // _btnBacklog
            // 
            this._btnBacklog.Location = new System.Drawing.Point(548, 556);
            this._btnBacklog.Name = "_btnBacklog";
            this._btnBacklog.Size = new System.Drawing.Size(151, 26);
            this._btnBacklog.TabIndex = 19;
            this._btnBacklog.Text = "Message Backlogs";
            this._btnBacklog.UseVisualStyleBackColor = true;
            this._btnBacklog.Click += new System.EventHandler(this._btnBacklog_Click);
            // 
            // StatisticsDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(813, 602);
            this.Controls.Add(this._btnBacklog);
            this.Controls.Add(this._btnPingTimes);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this._btnChangeInterval);
            this.Controls.Add(this.bytesPerTransportGraph);
            this.Controls.Add(this.bytesGraph);
            this.Controls.Add(this.msgsPerTransportGraph);
            this.Controls.Add(this.messagesGraph);
            this.Controls.Add(this.connectionsGraph);
            this.Name = "StatisticsDialog";
            this.Text = "Statistics";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private SoftwareFX.ChartFX.Lite.Chart connectionsGraph;
        private System.Windows.Forms.Timer _timer;
        private SoftwareFX.ChartFX.Lite.Chart messagesGraph;
        private SoftwareFX.ChartFX.Lite.Chart messagesReceivedPiechart;
        private SoftwareFX.ChartFX.Lite.Chart messagesSentPiechart;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private SoftwareFX.ChartFX.Lite.Chart bytesGraph;
        private SoftwareFX.ChartFX.Lite.Chart bytesPerTransportGraph;
        private SoftwareFX.ChartFX.Lite.Chart msgsPerTransportGraph;
        private System.Windows.Forms.Button _btnChangeInterval;
        private System.Windows.Forms.Button resetValuesButton;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button _btnPingTimes;
        private System.Windows.Forms.Button _btnBacklog;
    }
}
