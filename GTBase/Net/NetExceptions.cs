using System;
using GT;
using System.Collections.Generic;

namespace GT.Net
{
    public class CannotConnectException : GTException
    {
        public CannotConnectException(string m)
            : base(m)
        {
        }

        public CannotConnectException(Exception e)
            : base("unable to connect", e)
        {
        }

        //public CannotConnectToRemoteException()
        //{
        //}

        // "There was a problem connecting to the server you specified. " +
        //"The address or port you provided may be improper, the receiving server may be down, " +
        //"full, or unavailable, or your system's host file may be corrupted. " +
        //"See inner exception for details."
    }

    /// <summary>
    /// No transport could be found that supports the required quality-of-service specifications.
    /// Unsendable may contain the list of messages.
    /// </summary>
    public class NoMatchingTransport : GTException
    {
        protected ICollection<PendingMessage> unsendable;

        public NoMatchingTransport(string message)
            : base(message)
        { }

        public NoMatchingTransport(string message, ICollection<PendingMessage> unsendableMessages)
            : base(message)
        {
            unsendable = unsendableMessages;
        }

        /// <summary>
        /// Return the list of messages that were unsendable (if recorded)
        /// </summary>
        public ICollection<PendingMessage> Unsendable { get { return unsendable; } }
    }

    /// <summary>
    /// An internal exception indicating that the connexion has been closed by the remote side.
    /// </summary>
    public class ConnexionClosedException : GTException
    {
        public ConnexionClosedException() { }
    }

    /// <summary>
    /// Indicates some kind of fatal error that cannot be handled by the underlying
    /// system object.  The underlying system object is not in a useable state.
    /// Catchers have the option of restarting / reinitializing the
    /// underlying system object.
    /// </summary>
    public class TransportError : GTException
    {
        protected object transportObject;
        protected object transportError;

        public TransportError(object o, string message) : base(message) {
            transportObject = o;
        }
        public TransportError(object t, string message, object error)
            : this(t, message)
        {
            transportError = error;
        }

        public object TransportObject { get { return transportObject; } }
        public object ErrorObject { get { return transportError; } }
    }

    /// <summary>
    /// This transport object has been cleanly invalidated somehow
    /// (e.g., remote has closed the socket) and should be decomissioned.
    /// </summary>
    public class TransportDecomissionedException : GTException
    {
        protected object transportObject;

        public TransportDecomissionedException(object t)
        {
            transportObject = t;
        }

        public object TransportObject { get { return transportObject; } }
    }
}
