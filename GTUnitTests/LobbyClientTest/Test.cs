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
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using GT.Net;
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

            LobbyClient lobby = new LobbyClient(name,
                new SimpleSharedDictionary(
                    client.OpenBinaryChannel("127.0.0.1","9999", 0)));
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
