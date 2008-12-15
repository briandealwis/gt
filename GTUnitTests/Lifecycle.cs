using GT.Net;
using NUnit.Framework;

namespace GT.UnitTests
{
    [TestFixture]
    public class LifecycleTests
    {
        public void TestClientRestarting()
        {
            Client client = new Client();
            client.Start();
            client.Stop();
            client.ToString();  // shouldn't throw an exception
            client.Start();
            client.Stop();

            client.Dispose();
            client.ToString();
        }

        public void TestServerRestarting()
        {
            Server server = new Server(0);
            server.Start();
            server.Stop();
            server.ToString();  // shouldn't throw an exception

            server.Start();
            server.Stop();

            server.Dispose();
            server.ToString();
        }

    }
}