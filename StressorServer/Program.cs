using System;
using System.Collections.Generic;
using System.Text;
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
            try
            {
                server.Start();
                Console.WriteLine("Server started; press a key to stop");
                Console.ReadKey();
            }
            finally
            {
                server.Stop();
                server.Dispose();
            }
            Console.WriteLine("Server shut down");
        }
    }
}
