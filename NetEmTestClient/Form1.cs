using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace NetEmTestClient
{
    public partial class Form1 : Form
    {
        Stopwatch sw = new Stopwatch();
        private double velX = 50;  // 5 pixels / second
        private double velY = 50;  // 5 pixels / second

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            sw.Start();
            StartServer(9876);
            StartClient("localhost", 9876);
        }

        private void StartClient(string p, int p_2)
        {
        }

        private void StartServer(int p)
        {
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            double x = bouncyBall1.BallX;
            double y = bouncyBall1.BallY;
            double elapsed = sw.Elapsed.TotalSeconds;
            x += velX*elapsed;
            y += velY*elapsed;
            if(x < bouncyBall1.BallRadius)
            {
                velX = Math.Abs(velX);
                x = bouncyBall1.BallRadius;
            } else if(x > bouncyBall1.Width - bouncyBall1.BallRadius)
            {
                velX = -Math.Abs(velX);
                x = bouncyBall1.Width - bouncyBall1.BallRadius;
            }

            if (y < bouncyBall1.BallRadius)
            {
                velY = Math.Abs(velY);
                y = bouncyBall1.BallRadius;
            }
            else if (y > bouncyBall1.Height - bouncyBall1.BallRadius)
            {
                velY = -Math.Abs(velY);
                y = bouncyBall1.Height - bouncyBall1.BallRadius;
            }
            bouncyBall1.BallX = x;
            bouncyBall1.BallY = y;
            bouncyBall1.Repaint();
            sw.Reset();
            sw.Start();
        }


    }
}
