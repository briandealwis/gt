using System;
using System.Collections.Generic;
using System.Text;

namespace GT.Common
{
    #region Enumerations

    /// <summary>Internal message types for SystemMessages to have.</summary>
    public enum SystemMessageType
    {
        UniqueIDRequest = 1,
        PingRequest = 4,
        PingResponse = 5
    }

    /// <summary>Possible message types for Messages to have.</summary>
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

    /// <summary>Possible ways messages can be sent.</summary>
    public enum MessageProtocol
    {
        /// <summary>This message will be sent via TCP, and is very reliable.</summary>
        Tcp = 1,
        /// <summary>This message will be sent via UDP, and is not reliable at all.</summary>
        Udp = 2
    }

    /// <summary>Should this message be aggregated?</summary>
    public enum MessageAggregation
    {
        /// <summary>This message will be saved, and sent dependant on the specified message ordering</summary>
        Yes,
        /// <summary>This message will be sent immediately</summary>
        No
    }

    /// <summary>Which messages should be sent before this one?</summary>
    public enum MessageOrder
    {
        /// <summary>This message will flush all other saved-to-be-aggregated messages out beforehand</summary>
        All,
        /// <summary>This message will flush all other saved-to-be-aggregated messages on this channel out beforehand</summary>
        AllChannel,
        /// <summary>This message will be sent immediately, without worrying about any saved-to-be-aggregated messages</summary>
        None
    }

    /// <summary>Should receiving clients keep old messages?</summary>
    /// FIXME: Is this the same as freshness?
    public enum MessageTimeliness
    {
        /// <summary>Throw away old messages</summary>
        RealTime,
        /// <summary>Keep old messages</summary>
        NonRealTime
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
}
