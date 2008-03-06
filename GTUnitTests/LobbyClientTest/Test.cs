using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using GT;
using Lobby;
using System.Threading;

namespace LobbyClientTest
{
    class Test
    {
        public Test()
        {
            Client client = new Client();
            Thread t = client.StartListeningOnSeperateThread(10);

            string name = Prompt.Show("What is your name?");

            if (name == null)
                return;

            LobbyClient lobby = new LobbyClient(name, new SimpleSharedDictionary(client.GetBinaryStream("127.0.0.1","9999", 0)));
            Console.WriteLine("Lobby started.");
            lobby.ShowDialog();

            t.Abort();

            if (lobby.JoinedServer != null)
            {
                Console.WriteLine("We should connect to " + lobby.JoinedServer.IPAddress + ":" + lobby.JoinedServer.Port);
            }
        }

        [STAThread]
        static public void Main(string[] args)
        {
            new Test();
        }
    }
}
