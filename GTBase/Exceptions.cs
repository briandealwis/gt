using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using GT.Net;

namespace GT
{
    /// <summary>
    /// Describes the impact of an error or exception.
    /// </summary>
    public enum Severity
    {
        /// <summary>
        /// Fatal: an error has occurred such that GT cannot continue in its operation.
        /// </summary>
        Fatal,

        /// <summary>
        /// Error: an error has occurred; the application will likely be able to continue, but GT functionality 
        /// may be significantly limited.
        /// </summary>
        Error,

        /// <summary>
        /// Warning: an error has occurred such that GT is able to continue, but the application's 
        /// functionality may be compromised.
        /// </summary>
        Warning,

        /// <summary>
        /// Information: a problem has occurred but has been dealt with; the error is being 
        /// reported purely for informational purposes.
        /// </summary>
        Information
    }

    public class GTException : Exception
    {
        protected object component;
        protected Severity severity;

        public Severity Severity { 
            get { return severity; }
            set { severity = value; }
        }

        public object SourceComponent
        {
            get { return component; }
            set
            {
                component = value;
                if (value != null) { Source = value.ToString(); }
            }
        }

        /// <summary>
        /// Initializes a new instance of the System.Exception class.
        /// </summary>
        public GTException(Severity sev) 
        {
            this.severity = sev;
        }

        /// <summary>
        /// Initializes a new instance of the System.Exception class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</parameter>
        public GTException(Severity sev, string message)
            : base(message)
        {
            this.severity = sev;
        }

        /// <summary>
        /// Initializes a new instance of the System.Exception class with a specified error message
        /// and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</parameter>
        public GTException(Severity sev, string message, Exception inner)
            : base(message, inner)
        {
            this.severity = sev;
        }
    }

    public abstract class GTCompositeException : GTException
    {
        public GTCompositeException(Severity sev) : base(sev) { }

        public abstract ICollection<Exception> SubExceptions { get; }
    }

    /// <summary>
    /// Captures situations where there is a violation of a documented contraint or
    /// contract.
    /// </summary>
    public class ContractViolation : GTException
    {
        public ContractViolation(Severity sev, string message)
            : base(sev, message)
        { }
        
        /// <summary>
        /// If <c>condition</c> is false, create and throw an instance of this exception type.
        /// This method serves as syntactic sugar to save coding space.
        /// </summary>
        /// <param name="condition">the result of a test</param>
        /// <param name="text">descriptive text if the test fails</param>
        public static void Assert(bool condition, string text)
        {
            if (!condition) { throw new ContractViolation(Severity.Warning, text); }
        }
    }

    /// <summary>
    /// Denotes an problem.
    /// </summary>
    public class InvalidStateException : GTException
    {
        /// <summary>
        /// Initializes a new instance of the System.Exception class with a specified error message
        /// and a reference to some object that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</parameter>
        /// <param name="source">The object that is the cause of the current exception.</param>
        public InvalidStateException(string message, object source)
            : base(Severity.Warning, message)
        {
            SourceComponent = component;
        }

        /// <summary>
        /// If <c>condition</c> is false, create and throw an instance of this exception type.
        /// This method serves as syntactic sugar to save coding space.
        /// </summary>
        /// <param name="condition">the result of a test</param>
        /// <param name="text">descriptive text if the test fails</param>

        public static void Assert(bool condition, string text, object source)
        {
            if (!condition) { throw new InvalidStateException(text, source); }
        }
    }
}
