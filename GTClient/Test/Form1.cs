using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using GT.Clients;

namespace Test
{
    public partial class Form1 : Form
    {
        Client client = new Client();
        StringStream stream;

        int sent;
        int received;

        public Form1()
        {
            InitializeComponent();
            stream = client.GetStringStream("127.0.0.1", "9999", 0);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            while (true)
            {
                string test = "";
                for (int i = 0; i < 9999; i++)
                    test += "test";
                stream.Send(test);
                sent++;
                client.Update();
                while (stream.DequeueMessage(0) != null)
                    received++;
            }
        }
    }
}
