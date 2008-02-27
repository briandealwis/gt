using System;
using System.Runtime.Serialization;

namespace GT.Common
{

    public class GTException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the System.Exception class.
        /// </summary>
        public GTException() : base() {}

        /// <summary>
        /// Initializes a new instance of the System.Exception class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</parameter>
        public GTException(string message) : base(message) {}

        /// <summary>
        /// Initializes a new instance of the System.Exception class with serialized data.
        /// </summary>
        /// <param name="context">The System.Runtime.Serialization.StreamingContext that 
        /// contains contextual information about the source or destination.</param>
        /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds 
        /// the serialized object data about the exception being thrown.</param>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The class 
        /// name is null or System.Exception.HResult is zero (0).</exception>
        /// <exception cref="System.ArgumentNullException">The info parameter is null.</exception>
        protected GTException(SerializationInfo info, StreamingContext context) : base(info, context) {}
 

        /// <summary>
        /// Initializes a new instance of the System.Exception class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</parameter>
        /// <param name="innerException">The exception that is the cause of the current exception, 
        /// or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public GTException(string message, Exception innerException) : base(message, innerException) {}
    }

    public class InvalidStateException : GTException
    {
        public object cause;

        /// <summary>
        /// Initializes a new instance of the System.Exception class.
        /// </summary>
        public InvalidStateException() : base() {}

        /// <summary>
        /// Initializes a new instance of the System.Exception class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</parameter>
        public InvalidStateException(string message) : base(message) {}

        /// <summary>
        /// Initializes a new instance of the System.Exception class with serialized data.
        /// </summary>
        /// <param name="context">The System.Runtime.Serialization.StreamingContext that 
        /// contains contextual information about the source or destination.</param>
        /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds 
        /// the serialized object data about the exception being thrown.</param>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The class 
        /// name is null or System.Exception.HResult is zero (0).</exception>
        /// <exception cref="System.ArgumentNullException">The info parameter is null.</exception>
        protected InvalidStateException(SerializationInfo info, StreamingContext context) : base(info, context) {}
 

        /// <summary>
        /// Initializes a new instance of the System.Exception class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</parameter>
        /// <param name="innerException">The exception that is the cause of the current exception, 
        /// or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public InvalidStateException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the System.Exception class with a specified error message
        /// and a reference to some object that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</parameter>
        /// <param name="cause">The object that is the cause of the current exception.</param>
        public InvalidStateException(string message, object cause) : base(message) {
            this.cause = cause;
        }
    }
}