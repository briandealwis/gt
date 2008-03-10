using System;
using GT;

namespace GT.Net
{
    public class CannotConnectToRemoteException : GTException
    {
        public CannotConnectToRemoteException(string m)
            : base(m)
        {
        }

        public CannotConnectToRemoteException(Exception e)
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
    /// </summary>
    public class NoMatchingTransport : GTException
    {
        public NoMatchingTransport(string message)
            : base(message)
        { }
    }
}