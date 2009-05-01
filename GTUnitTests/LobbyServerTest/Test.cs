//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

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
