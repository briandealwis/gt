using System;
using System.Threading;
using GT.Net;
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
