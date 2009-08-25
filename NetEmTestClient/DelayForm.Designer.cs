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
            this.delaySlider = new GT.UI.Slider();
            this.updatetimeSlider = new GT.UI.Slider();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(34, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Delay";
            // 
            // resetButton
            // 
            this.resetButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.resetButton.Location = new System.Drawing.Point(69, 107);
            this.resetButton.Name = "resetButton";
            this.resetButton.Size = new System.Drawing.Size(224, 27);
            this.resetButton.TabIndex = 5;
            this.resetButton.Text = "Reset Position";
            this.resetButton.UseVisualStyleBackColor = true;
            this.resetButton.Click += new System.EventHandler(this.resetButton_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(0, 60);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(47, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Updates";
            // 
            // delaySlider
            // 
            this.delaySlider.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.delaySlider.LargeChange = 1F;
            this.delaySlider.Location = new System.Drawing.Point(46, -2);
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
            this.updatetimeSlider.Location = new System.Drawing.Point(46, 49);
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
            this.ClientSize = new System.Drawing.Size(359, 146);
            this.Controls.Add(this.delaySlider);
            this.Controls.Add(this.updatetimeSlider);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.resetButton);
            this.Controls.Add(this.label1);
            this.Name = "DelayForm";
            this.Text = "Client Settings";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.Label label3;
        private GT.UI.Slider updatetimeSlider;
        private GT.UI.Slider delaySlider;
    }
}