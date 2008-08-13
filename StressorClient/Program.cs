using System;
using System.Collections.Generic;
using System.Text;
using GT.UnitTests;

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
