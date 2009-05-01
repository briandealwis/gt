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
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net;
using GT.Net;
using System.IO;

namespace GT.Net
{
    /// <summary>
    /// An object responsible for initiating connections to some remote service.
    /// The remote service is often implemented using an <c>IAcceptor</c>.
    /// See
    ///    DC Schmidt (1997). Acceptor and connector: A family of object 
    ///    creational patterns for initializing communication services. 
    ///    In R Martin, F Buschmann, D Riehle (Eds.), Pattern Languages of 
    ///    Program Design 3. Addison-Wesley
    ///    http://www.cs.wustl.edu/~schmidt/PDF/Acc-Con.pdf
    /// FIXME: this interface currently blocks until completion.
    /// Perhaps we should be providing an event on connection, with
    /// <c>Connect()</c> initiating a connection, and requiring periodic
    /// calls to an Update() method.
    /// </summary>
    public interface IConnector : IStartable
    {
        /// <summary>
        /// Establish a connexion to the provided address and port.  The call is
        /// assumed to be blocking.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="capabilities"></param>
        /// <returns>the newly connected transport</returns>
        /// <exception cref="CannotConnectException">thrown if the connector
        ///     cannot connect to the other side.</exception>
        ITransport Connect(string address, string port, IDictionary<string, string> capabilities);

        /// <summary>
        /// Return true if this connector was responsible for connecting the provided transport.
        /// </summary>
        /// <param name="transport">a (presumably, but not necessarily) disconnected) transport</param>
        /// <returns>Returns true if this instances was responsible for connecting the provided transport.</returns>
        bool Responsible(ITransport transport);
    }

}
