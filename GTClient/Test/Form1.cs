using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using GT.Net;

namespace Test
{
    public partial class Form1 : Form
    {
        Client client = new Client();
        IStringChannel channel;

        int sent;
        int received;

        public Form1()
        {
            InitializeComponent();
            channel = client.OpenStringChannel("127.0.0.1", "9999", 0);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            while (true)
            {
                string test = "";
                for (int i = 0; i < 9999; i++)
                    test += "test";
                channel.Send(test);
                sent++;
                client.Update();
                while (channel.DequeueMessage(0) != null)
                    received++;
            }
        }
    }
}
