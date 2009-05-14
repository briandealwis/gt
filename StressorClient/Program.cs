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
using System.Threading;
using GT.UnitTests;
using GT.StatsGraphs;

namespace StressorClient
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Use: <program> host port");
                return;
            }
            StressingClient client = new StressingClient(args[0], args[1]);
            Thread dialogThread = null;
            try
            {
                client.Start();
                client.Client.Configuration.PingInterval = TimeSpan.FromSeconds(1);
                dialogThread = StatisticsDialog.On(client.Client);

                Console.WriteLine("Client started; press a key to stop");
                Console.ReadKey();
            }
            finally
            {
                if (dialogThread != null) { dialogThread.Abort(); }
                client.Stop();
                client.Dispose();
            }
            Console.WriteLine("Client shut down");

        }
    }
}
