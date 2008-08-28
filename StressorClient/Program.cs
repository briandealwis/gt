using System;
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
            try
            {
                client.Start();

                StatisticsDialog.On(client.Client);

                Console.WriteLine("Client started; press a key to stop");
                Console.ReadKey();
            }
            finally
            {
                client.Stop();
                client.Dispose();
            }
            Console.WriteLine("Client shut down");

        }
    }
}
