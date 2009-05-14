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
