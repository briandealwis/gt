using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using GTClient;

namespace EmptyStringTest
{
    /// <summary>
    /// A very simple program.
    /// </summary>
    public partial class Form1 : Form
    {
        Client client = null;  //controls the network
        StringStream stream = null;  //one stream of strings

        public Form1()
        {
            InitializeComponent();
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            client = new Client();  //this is a client
            client.ErrorEvent += new ErrorEventHandler(client_ErrorEvent);  //triggers if there is an error
            stream = client.GetStringStream("127.0.0.1", "9999", 0);  //connect here
            stream.Send("Hello!");  //send a string
        }


        /// <summary>This triggers if something goes wrong</summary>
        void client_ErrorEvent(Exception e, System.Net.Sockets.SocketError se, ServerStream ss, string explanation)
        {
            Console.WriteLine("Error: " + explanation + "\n" + e.ToString());
        }


        /// <summary>This is a windows form timer event</summary>
        private void timer1_Tick(object sender, EventArgs e)
        {

            if (client == null || stream == null)  //a guard
                return;


            client.Update();  //let the client check the network


            string s;  //read any new messages
            while ((s = stream.DequeueMessage(0)) != null)
            {
                Console.WriteLine("String received: " + s);
            }
        }
    }
}