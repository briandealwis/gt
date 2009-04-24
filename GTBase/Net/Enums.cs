namespace GT.Net
{
    #region Enumerations

    /// <summary>Internal message types for SystemMessages to have.</summary>
    public enum SystemMessageType
    {
        IdentityRequest = 1,
        IdentityResponse = 2,
        Acknowledged = 3,
        PingRequest = 4,
        PingResponse = 5,

        /// <summary>
        /// Sent when a connexion is about to be torn down.
        /// </summary>
        ConnexionClosing = 6,

        /// <summary>
        /// The remote speaks an incompatible dialect.
        /// </summary>
        IncompatibleVersion = 8,
    }

    /// <summary>
    /// Possible message types for Messages to have.
    /// These values must fall between 0 to 127.
    /// Values 128 - 255 are reserved for marshaller uses.
    /// </summary>
    public enum MessageType
    {
        /// <summary>This message is a byte array</summary>
        Binary = 1,
        /// <summary>This message is an object</summary>
        Object = 2,
        /// <summary>This message is a string</summary>
        String = 3,
        /// <summary>This message is for the system, and special</summary>
        System = 4,
        /// <summary>This message refers to a session</summary>
        Session = 5,
        /// <summary>This message refers to a streaming 1-tuple</summary>
        Tuple1D = 6,
        /// <summary>This message refers to a streaming 2-tuple</summary>
        Tuple2D = 7,
        /// <summary>This message refers to a streaming 3-tuple</summary>
        Tuple3D = 8
    }

    /// <summary>Session action performed.  We can add a lot more to this list.</summary>
    public enum SessionAction
    {
        /// <summary>This client is joining this session.</summary>
        Joined = 1,
        /// <summary>This client is part of this session.</summary>
        Lives = 2,
        /// <summary>This client is inactive.</summary>
        Inactive = 3,
        /// <summary>This client is leaving this session.</summary>
        Left = 4
    }

    #endregion

    #region String Constants
    /// <summary>
    /// Constants used within GT and its implementations.
    /// </summary>
    public struct GTCapabilities
    {
        public static readonly string CLIENT_GUID = "CLI-ID";
        public static readonly string MARSHALLER_DESCRIPTORS = "MRSHLRS";
    }
    #endregion
}
