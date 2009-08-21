namespace NetEmTestClient
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
            this.components = new System.ComponentModel.Container();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.bouncyBall1 = new NetEmTestClient.BouncyBall();
            this.SuspendLayout();
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // bouncyBall1
            // 
            this.bouncyBall1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.bouncyBall1.BackColor = System.Drawing.Color.White;
            this.bouncyBall1.BallRadius = ((uint)(6u));
            this.bouncyBall1.BallX = ((uint)(6u));
            this.bouncyBall1.BallY = ((uint)(6u));
            this.bouncyBall1.ForeColor = System.Drawing.Color.Red;
            this.bouncyBall1.Location = new System.Drawing.Point(12, 12);
            this.bouncyBall1.Name = "bouncyBall1";
            this.bouncyBall1.Size = new System.Drawing.Size(197, 161);
            this.bouncyBall1.TabIndex = 0;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(221, 185);
            this.Controls.Add(this.bouncyBall1);
            this.DoubleBuffered = true;
            this.Name = "Form1";
            this.Text = "Bouncing Ball";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer timer1;
        private BouncyBall bouncyBall1;
    }
}

