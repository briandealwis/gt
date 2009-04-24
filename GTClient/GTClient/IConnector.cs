using System.Collections.Generic;

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
