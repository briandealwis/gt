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
        /// <returns></returns>
        ITransport Connect(string address, string port, Dictionary<string, string> capabilities);
    }

}
