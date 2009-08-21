using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using BBall.Client;
using BBall.Server;
using GT.Net;

namespace BBall.UI
{
    public partial class Form1 : Form
    {
        private BBClient client;
        private BBServer server;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            StartServer(9876);
            StartClient("localhost", 9876);
        }

        private void StartClient(string host, ushort port)
        {
            client = new BBClient(host, port);
            client.Start();
        }

        private void StartServer(ushort port)
        {
            server = new BBServer(port);
            server.PositionUpdates += _server_PositionUpdated;
            server.Start();
            server.UpdateWindowSize((uint)bouncyBall1.Width, (uint)bouncyBall1.Height, 
                bouncyBall1.BallRadius);
        }

        private void _server_PositionUpdated(double x, double y)
        {
            bouncyBall1.BallX = x;
            bouncyBall1.BallY = y;
            bouncyBall1.Repaint();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            client.Dispose();
            server.Dispose();
        }

        private void bouncyBall1_Resize(object sender, EventArgs e)
        {
            server.UpdateWindowSize((uint)bouncyBall1.Width, (uint)bouncyBall1.Height,
                bouncyBall1.BallRadius);
        }
    }
}
