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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace GT.Net
{
    public delegate void NewClientHandler(ITransport transport, Dictionary<string,string> capabilities);

    /// <summary>
    /// An object responsible for negotiating and accepting incoming connections.
    /// The remote service is often implemented using an <c>IConnector</c>.
    /// See
    ///    DC Schmidt (1997). Acceptor and connector: A family of object 
    ///    creational patterns for initializing communication services. 
    ///    In R Martin, F Buschmann, D Riehle (Eds.), Pattern Languages of 
    ///    Program Design 3. Addison-Wesley
    ///    http://www.cs.wustl.edu/~schmidt/PDF/Acc-Con.pdf
    /// </summary>
    public interface IAcceptor : IStartable
    {
        event NewClientHandler NewClientEvent;

        void Update();
    }

    public abstract class BaseAcceptor : IAcceptor
    {
        public event NewClientHandler NewClientEvent;
        protected IPAddress address;
        protected int port;

        public BaseAcceptor(IPAddress address, int port)
        {
            this.address = address;
            this.port = port;
        }

        public abstract void Update();

        public abstract bool Active { get; }
        public abstract void Start();
        public abstract void Stop();
        public abstract void Dispose();

        public void NotifyNewClient(ITransport t, Dictionary<string,string> capabilities)
        {
            NewClientEvent(t, capabilities);
        }
    }
}
