using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using GTClient;

namespace ClientChat
{
    public partial class Form1 : Form
    {
        private Client c;
        private StringStream s;
        private SessionStream session;

        public Form1()
        {
            c = new Client();
            s = c.GetStringStream("127.0.0.1","9999",0);
            session = c.GetSessionStream("127.0.0.1","9999",0);

            InitializeComponent();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            c.Update();
            String str;
            SessionMessage mes;

            while((str = s.DequeueMessage(0)) != null)
                richTextBox.Text += str + "\n";

            while ((mes = session.DequeueMessage(0)) != null)
                richTextBox.Text += "Client " + mes.ClientId + " " + mes.Action + "\n";
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.KeyCode == Keys.Enter)
            {
                s.Send(textBox1.Text);
                textBox1.Text = "";
            }
        }
    }
}