namespace BBall.UI
{
    partial class Form1
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
            this.bouncyBall1 = new BBall.UI.BouncyBall();
            this.SuspendLayout();
            // 
            // bouncyBall1
            // 
            this.bouncyBall1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.bouncyBall1.BackColor = System.Drawing.Color.White;
            this.bouncyBall1.BallRadius = ((uint)(6u));
            this.bouncyBall1.BallX = 6;
            this.bouncyBall1.BallY = 6;
            this.bouncyBall1.ForeColor = System.Drawing.Color.Red;
            this.bouncyBall1.Location = new System.Drawing.Point(12, 12);
            this.bouncyBall1.Name = "bouncyBall1";
            this.bouncyBall1.Size = new System.Drawing.Size(272, 231);
            this.bouncyBall1.TabIndex = 0;
            this.bouncyBall1.TabStop = false;
            this.bouncyBall1.Resize += new System.EventHandler(this.bouncyBall1_Resize);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(296, 255);
            this.Controls.Add(this.bouncyBall1);
            this.DoubleBuffered = true;
            this.Name = "Form1";
            this.Text = "Bouncing Ball";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.ResumeLayout(false);

        }

        #endregion

        private BouncyBall bouncyBall1;
    }
}

