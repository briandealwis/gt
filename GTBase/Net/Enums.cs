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
using System.Text;

namespace GT.Net
{
    #region Enumerations

    /// <summary>Internal message types for SystemMessages to have.</summary>
    public enum SystemMessageType
    {
        UniqueIDRequest = 1,
        // ids 2 and 3 are reserved (they have been deprecated and removed)
        PingRequest = 4,
        PingResponse = 5,

        /// <summary>
        /// Sent when a connexion is about to be torn down.
        /// </summary>
        ConnexionClosing = 6,

        /// <summary>
        /// Intended for unreliable transports where the initial connection handshake
        /// may never have been received.
        /// </summary>
        UnknownConnexion = 7,

        /// <summary>
        /// The remote speaks an incompatible dialect.
        /// </summary>
        IncompatibleVersion = 8
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
        public static readonly string CLIENT_ID = "CLI-ID";
        public static readonly string MARSHALLER_DESCRIPTORS = "MRSHLRS";
    }
    #endregion
}
