using System;
using System.Collections.Generic;
using System.Text;

namespace GT.Utils
{
    /// <summary>
    /// A simple command-line parser equivalent to the C getopt(3).
    /// This parser supports the use of stacked options (e.g., '-sxm'
    /// is equivalent to '-s -x -m'); it supports the use of '--'
    /// to indicate the end of options; and finally it detects a
    /// missing argument.  Typical use is as follows:
    /// <code>
    /// GetOpt getopt = new GetOpt(args, "abc:");
    /// Option opt;
    /// while((opt = getopt.NextOption()) != null) {
    /// }
    /// args = getopt.RemainingArguments();
    /// </code>
    /// </summary>
    public class GetOpt
    {
        protected string[] argv;
        protected int optind = 0;
        protected int optcharind = 0;
        protected IDictionary<char, bool> options = new Dictionary<char, bool>();

        public GetOpt(string[] argv, string optdesc)
        {
            this.argv = argv;

            for (int index = 0; index < optdesc.Length; index++)
            {
                if (index + 1 < optdesc.Length && optdesc[index + 1] == ':')
                {
                    options[optdesc[index]] = true;
                    index++;
                }
                else
                {
                    options[optdesc[index]] = false;
                }
            }
        }

        /// <summary>
        /// Return the next option.  Return null if there are no more options;
        /// use <see cref="RemainingArguments"/> to retrieve the remaining arguments.
        /// </summary>
        /// <returns>the next option, or null if there are no further options</returns>
        public Option NextOption()
        {
            do
            {
                if (optind >= argv.Length)
                {
                    return null;
                }
                if (optcharind >= argv[optind].Length)
                {
                    if (++optind == argv.Length) { return null; }
                    optcharind = 0;
                }
                if (optcharind == 0)
                {
                    if (argv[optind].Length <= 0 || argv[optind][optcharind] != '-')
                    {
                        return null;
                    }
                    if (argv[optind].Equals("--"))
                    {
                        optind++;
                        return null;
                    }
                    optcharind++;
                }
            }
            while (optcharind >= argv[optind].Length);

            char c = argv[optind][optcharind++];
            bool hasArg;
            if (!options.TryGetValue(c, out hasArg))
            {
                throw new UnknownOptionException(c);
            }
            if (hasArg)
            {
                string arg;
                if (optcharind < argv[optind].Length)
                {
                    arg = argv[optind].Substring(optcharind);
                    optind++;
                    optcharind = 0;
                }
                else if (optind + 1 == argv.Length)
                {
                    throw new MissingOptionException(c);
                }
                else
                {
                    arg = argv[++optind];
                    optind++;   // would have been cool to use ++optind++ instead!
                    optcharind = 0;
                }
                return new Option(c, arg);
            }
            return new Option(c);
        }

        public string[] RemainingArguments()
        {
            string[] remainder = new string[argv.Length - optind];
            Array.Copy(argv, optind, remainder, 0, argv.Length - optind);
            return remainder;
        }
    }

    /// <summary>
    /// Represents an option found on the command line.
    /// <see cref="Character"/> is the character for the option.
    /// <see cref="Argument"/> will be the argument, if expected.
    /// </summary>
    public class Option
    {
        internal Option(char c) : this(c, null) { }

        internal Option(char c, string arg)
        {
            Character = c;
            Argument = arg;
        }

        public char Character { get; private set; }

        public string Argument { get; private set; }

        public override string ToString()
        {
            StringBuilder result = new StringBuilder(GetType().Name);
            result.Append("('-");
            result.Append(Character);
            if (Argument != null)
            {
                result.Append(' ');
                result.Append(Argument);
            }
            result.Append("')");
            return result.ToString();
        }
    }

    /// <summary>
    /// An exception thrown by <see cref="GetOpt.NextOption"/> upon
    /// encountering an unspecified option character.
    /// </summary>
    public class UnknownOptionException : GetOptException
    {
        internal UnknownOptionException(char c) 
            : base(String.Format("Unknown option: '{0}'", c), c) { }
    }

    /// <summary>
    /// An exception thrown by <see cref="GetOpt.NextOption"/> should
    /// an option not include a specified argument.
    /// </summary>
    public class MissingOptionException : GetOptException
    {
        internal MissingOptionException(char c)
            : base(String.Format("Missing option: '{0}'", c), c) { }
    }

    /// <summary>
    /// A common superclass for exceptions thrown by <see cref="GetOpt"/> parsing.
    /// </summary>
    public class GetOptException : Exception
    {
        public char Character { get; protected set; }

        protected GetOptException(string message, char c)
            : base(message)
        {
            Character = c;
        }
    }

}