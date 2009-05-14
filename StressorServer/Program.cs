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
using GT.StatsGraphs;
using GT.UnitTests;

namespace StressorServer
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Use: <program> port");
                return;
            }
            StressingServer server = new StressingServer(int.Parse(args[0]));
            Thread dialogThread = null;
            try
            {
                server.Start();
                server.Server.Configuration.PingInterval = TimeSpan.FromSeconds(1);

                dialogThread = StatisticsDialog.On(server.Server);

                Console.WriteLine("Server started; press a key to stop");
                Console.ReadKey();
            }
            finally
            {
                if (dialogThread != null) { dialogThread.Abort(); }
                server.Stop();
                server.Dispose();
            }
            Console.WriteLine("Server shut down");
        }
    }
}
