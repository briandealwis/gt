using System;
using System.Windows.Forms;
using BBall.Client;
using BBall.Server;
using GT.Net;

namespace BBall.UI
{
    public partial class Form1 : Form
    {
        private BBClient client;
        private BBServer server;
        private DelayForm clientDelayForm;

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
            clientDelayForm = new DelayForm();

            clientDelayForm.Delay = client.Delay;
            clientDelayForm.PacketLoss = (float)client.PacketLoss;
            clientDelayForm.PacketLossCorrelation = (float)client.PacketLossCorrelation;
            clientDelayForm.PacketReordering = (float)client.PacketReordering;
            clientDelayForm.UpdateInterval = client.UpdateInterval;
            clientDelayForm.SendUnreliably = false;

            clientDelayForm.DelayChanged += (value) => client.Delay = value;
            clientDelayForm.PacketLossChanged += (value) => client.PacketLoss = value;
            clientDelayForm.PacketLossCorrelationChanged += (value) => client.PacketLossCorrelation = value;
            clientDelayForm.PacketReorderingChanged += (value) => client.PacketReordering = value;
            clientDelayForm.UpdateIntervalChanged += (value) => client.UpdateInterval = value;
            clientDelayForm.SendUnreliablyChanged +=
                (value) =>
                    client.MDR = value ? MessageDeliveryRequirements.LeastStrict 
                        : MessageDeliveryRequirements.MostStrict;

            clientDelayForm.Reset += client.Reset;
            client.PacketDisposition += clientDelayForm.ReportDisposition;
            clientDelayForm.Show();
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
            if(clientDelayForm != null) { clientDelayForm.Dispose(); }
        }

        private void bouncyBall1_Resize(object sender, EventArgs e)
        {
            server.UpdateWindowSize((uint)bouncyBall1.Width, (uint)bouncyBall1.Height,
                bouncyBall1.BallRadius);
        }
    }
}
