using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net;
using GT.Net;
using System.IO;

namespace GT.Net
{
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
        /// <exception cref="CannotConnectToRemoteException">thrown if the connector
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
