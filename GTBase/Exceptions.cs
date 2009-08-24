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

// Fundamental classes and interfaces used throughout GT, including
// for reporting errors.
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
        Fatal = 3,

        /// <summary>
        /// Error: an error has occurred; the application will likely be able to continue, but GT functionality 
        /// may be significantly limited.
        /// </summary>
        Error = 2,

        /// <summary>
        /// Warning: an error has occurred such that GT is able to continue, but the application's 
        /// functionality may be compromised.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Information: a problem has occurred but has been dealt with; the error is being 
        /// reported purely for informational purposes.
        /// </summary>
        Information = 0
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
        /// <param name="sev">the severity of the exception</param>
        /// <param name="message">The message that describes the error.</param>
        public GTException(Severity sev, string message)
            : base(message)
        {
            this.severity = sev;
        }

        /// <summary>
        /// Initializes a new instance of the System.Exception class with a specified error message
        /// and inner exception.
        /// </summary>
        /// <param name="sev">the severity of the exception</param>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">the cause of the exception, if any</param>
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
        /// <summary>
        /// Create an instance documenting a contract violation
        /// </summary>
        /// <param name="sev">the associate severity of the violation</param>
        /// <param name="message">text describing the violation</param>
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

        /// <summary>
        /// If <c>condition</c> is false, create and throw an instance of this exception type.
        /// This method serves as syntactic sugar to save coding space.  This method
        /// invokes <see cref="string.Format(string,object[])"/> on the text argument.
        /// </summary>
        /// <param name="condition">the result of a test</param>
        /// <param name="text">descriptive text if the test fails</param>
        public static void Assert(bool condition, string text, params string[] parms)
        {
            if (condition) { return; }

            if(parms.Length == 0 ) { throw new ContractViolation(Severity.Warning, text); }
            throw new ContractViolation(Severity.Warning, String.Format(text, parms));
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
        /// <param name="message">The message that describes the error.</param>
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
        /// <param name="source">the object whose state is invalid</param>
        public static void Assert(bool condition, string text, object source)
        {
            if (!condition) { throw new InvalidStateException(text, source); }
        }
    }
}
