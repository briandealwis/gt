using System;
using System.Collections.Generic;
using System.Text;
using Lobby;
using GT.Net;

namespace LobbyServerTest
{
    class Test
    {
        string lobbyAddress, lobbyPort;
        SimpleSharedDictionary lobbySharedDictionary;
        string ourAddress, ourPort, ourName;
        

        public Test()
        {
            Client client = new Client();
            client.StartListeningOnSeperateThread(10);

            lobbyAddress = "127.0.0.1";
            lobbyPort = "9999";
            lobbySharedDictionary = 
                new SimpleSharedDictionary(client.GetBinaryStream(lobbyAddress, lobbyPort, 0));

            ourAddress = "127.0.0.1";
            ourPort = "9997";

            Console.Write("My name: ");
            ourName = Prompt.Show("What is the name of the server?");
            if (ourName == null)
                return;

            Console.WriteLine("Lobby started.");
            LobbyServer ls = new LobbyServer(ourName, ourAddress, ourPort, lobbySharedDictionary);

            Console.ReadKey();

            Console.WriteLine("Now in Progress.");
            ls.InProgress = true;

            Console.ReadKey();
        }

        public static void Main()
        {
            new Test();
        }
    }
}
