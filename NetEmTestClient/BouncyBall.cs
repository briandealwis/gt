using System;
using System.Drawing;
using System.Windows.Forms;

namespace BBall.UI
{
    public partial class BouncyBall : UserControl
    {
        private double x, y;

        public double BallX 
        { 
            get { return x; } 
            set { x = Math.Min(Width, Math.Max(0, value)); }
        }

        public double BallY
        {
            get { return y; }
            set { y = Math.Min(Height, Math.Max(0, value)); }
        }

        public uint BallRadius { get; set; }

        public BouncyBall()
        {
            BallRadius = 6;
            BallX = BallRadius;
            BallY = BallRadius;

            DoubleBuffered = true;

            InitializeComponent();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Brush backgroundBrush = new SolidBrush(BackColor);
            Brush ballBrush = new SolidBrush(ForeColor);

            e.Graphics.FillRectangle(backgroundBrush, 0, 0, Width, Height);
            e.Graphics.FillEllipse(ballBrush, 
                (uint)Math.Max(0, BallX - BallRadius), 
                (uint)Math.Max(0, BallY - BallRadius), 
                BallRadius * 2, BallRadius * 2);

            ballBrush.Dispose();
            backgroundBrush.Dispose();
        }

        public void Repaint()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            BeginInvoke(new MethodInvoker(Invalidate));
        }

    }
}
