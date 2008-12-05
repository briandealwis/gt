using System;
using System.Threading;
using GT.Net;
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
