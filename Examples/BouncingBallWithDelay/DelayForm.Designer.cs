namespace BBall.UI
{
    partial class DelayForm
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
            this.label1 = new System.Windows.Forms.Label();
            this.resetButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.sendUnreliablyCheckbox = new System.Windows.Forms.CheckBox();
            this.packetReorderingSlider = new GT.UI.Slider();
            this.packetLossCorrelationSlider = new GT.UI.Slider();
            this.packetLossSlider = new GT.UI.Slider();
            this.delaySlider = new GT.UI.Slider();
            this.updatetimeSlider = new GT.UI.Slider();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(16, 80);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(44, 49);
            this.label1.TabIndex = 3;
            this.label1.Text = "Packet Delay (\'D\')";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // resetButton
            // 
            this.resetButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.resetButton.Location = new System.Drawing.Point(65, 400);
            this.resetButton.Name = "resetButton";
            this.resetButton.Size = new System.Drawing.Size(252, 27);
            this.resetButton.TabIndex = 5;
            this.resetButton.Text = "Reset Position";
            this.resetButton.UseVisualStyleBackColor = true;
            this.resetButton.Click += new System.EventHandler(this.resetButton_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Updates";
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(1, 32);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 48);
            this.label2.TabIndex = 10;
            this.label2.Text = "Packet Loss (\'_\')";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(1, 97);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(47, 48);
            this.label4.TabIndex = 12;
            this.label4.Text = "Packet Loss Correl";
            this.label4.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(1, 162);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(47, 48);
            this.label5.TabIndex = 14;
            this.label5.Text = "Packet Reorder (\'R\')";
            this.label5.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.packetReorderingSlider);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.packetLossCorrelationSlider);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.packetLossSlider);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Location = new System.Drawing.Point(13, 147);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(362, 214);
            this.groupBox1.TabIndex = 16;
            this.groupBox1.TabStop = false;
            // 
            // sendUnreliablyCheckbox
            // 
            this.sendUnreliablyCheckbox.AutoSize = true;
            this.sendUnreliablyCheckbox.Location = new System.Drawing.Point(25, 145);
            this.sendUnreliablyCheckbox.Name = "sendUnreliablyCheckbox";
            this.sendUnreliablyCheckbox.Size = new System.Drawing.Size(165, 17);
            this.sendUnreliablyCheckbox.TabIndex = 17;
            this.sendUnreliablyCheckbox.Text = "Send via unreliable transports";
            this.sendUnreliablyCheckbox.UseVisualStyleBackColor = true;
            this.sendUnreliablyCheckbox.CheckedChanged += new System.EventHandler(this.sendUnreliablyCheckbox_CheckedChanged);
            // 
            // packetReorderingSlider
            // 
            this.packetReorderingSlider.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.packetReorderingSlider.LargeChange = 1F;
            this.packetReorderingSlider.Location = new System.Drawing.Point(47, 151);
            this.packetReorderingSlider.Maximum = 100F;
            this.packetReorderingSlider.Minimum = 0F;
            this.packetReorderingSlider.Name = "packetReorderingSlider";
            this.packetReorderingSlider.Resolution = 0.5F;
            this.packetReorderingSlider.ShowMinMaxValues = true;
            this.packetReorderingSlider.Size = new System.Drawing.Size(312, 59);
            this.packetReorderingSlider.TabIndex = 15;
            this.packetReorderingSlider.TickFrequency = 5F;
            this.packetReorderingSlider.Units = "%";
            this.packetReorderingSlider.Value = 0F;
            // 
            // packetLossCorrelationSlider
            // 
            this.packetLossCorrelationSlider.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.packetLossCorrelationSlider.LargeChange = 1F;
            this.packetLossCorrelationSlider.Location = new System.Drawing.Point(47, 86);
            this.packetLossCorrelationSlider.Maximum = 100F;
            this.packetLossCorrelationSlider.Minimum = 0F;
            this.packetLossCorrelationSlider.Name = "packetLossCorrelationSlider";
            this.packetLossCorrelationSlider.Resolution = 0.5F;
            this.packetLossCorrelationSlider.ShowMinMaxValues = true;
            this.packetLossCorrelationSlider.Size = new System.Drawing.Size(312, 59);
            this.packetLossCorrelationSlider.TabIndex = 13;
            this.packetLossCorrelationSlider.TickFrequency = 5F;
            this.packetLossCorrelationSlider.Units = "%";
            this.packetLossCorrelationSlider.Value = 0F;
            // 
            // packetLossSlider
            // 
            this.packetLossSlider.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.packetLossSlider.LargeChange = 1F;
            this.packetLossSlider.Location = new System.Drawing.Point(47, 21);
            this.packetLossSlider.Maximum = 100F;
            this.packetLossSlider.Minimum = 0F;
            this.packetLossSlider.Name = "packetLossSlider";
            this.packetLossSlider.Resolution = 0.5F;
            this.packetLossSlider.ShowMinMaxValues = true;
            this.packetLossSlider.Size = new System.Drawing.Size(312, 59);
            this.packetLossSlider.TabIndex = 11;
            this.packetLossSlider.TickFrequency = 5F;
            this.packetLossSlider.Units = "%";
            this.packetLossSlider.Value = 0F;
            // 
            // delaySlider
            // 
            this.delaySlider.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.delaySlider.LargeChange = 1F;
            this.delaySlider.Location = new System.Drawing.Point(59, 70);
            this.delaySlider.Maximum = 5000F;
            this.delaySlider.Minimum = 0F;
            this.delaySlider.Name = "delaySlider";
            this.delaySlider.Resolution = 1F;
            this.delaySlider.ShowMinMaxValues = true;
            this.delaySlider.Size = new System.Drawing.Size(312, 59);
            this.delaySlider.TabIndex = 9;
            this.delaySlider.TickFrequency = 1000F;
            this.delaySlider.Units = "ms";
            this.delaySlider.Value = 0F;
            // 
            // updatetimeSlider
            // 
            this.updatetimeSlider.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.updatetimeSlider.AutoSize = true;
            this.updatetimeSlider.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.updatetimeSlider.LargeChange = 1F;
            this.updatetimeSlider.Location = new System.Drawing.Point(59, 14);
            this.updatetimeSlider.Maximum = 1000F;
            this.updatetimeSlider.Minimum = 1F;
            this.updatetimeSlider.Name = "updatetimeSlider";
            this.updatetimeSlider.Resolution = 1F;
            this.updatetimeSlider.ShowMinMaxValues = true;
            this.updatetimeSlider.Size = new System.Drawing.Size(312, 51);
            this.updatetimeSlider.TabIndex = 8;
            this.updatetimeSlider.TickFrequency = 100F;
            this.updatetimeSlider.Units = "ms";
            this.updatetimeSlider.Value = 10F;
            // 
            // DelayForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(387, 439);
            this.Controls.Add(this.sendUnreliablyCheckbox);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.delaySlider);
            this.Controls.Add(this.updatetimeSlider);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.resetButton);
            this.Controls.Add(this.label1);
            this.Name = "DelayForm";
            this.Text = "Client Settings";
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.Label label3;
        private GT.UI.Slider updatetimeSlider;
        private GT.UI.Slider delaySlider;
        private GT.UI.Slider packetLossSlider;
        private System.Windows.Forms.Label label2;
        private GT.UI.Slider packetLossCorrelationSlider;
        private System.Windows.Forms.Label label4;
        private GT.UI.Slider packetReorderingSlider;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox sendUnreliablyCheckbox;
    }
}