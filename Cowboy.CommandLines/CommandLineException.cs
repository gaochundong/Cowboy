using System;
using System.Runtime.Serialization;

namespace Cowboy.CommandLines
{
    [Serializable]
    public class CommandLineException : Exception
    {
        public CommandLineException()
          : this("Exception occurred when processing command line.")
        {
        }

        public CommandLineException(string message)
          : this(message, null)
        {
        }

        public CommandLineException(string message, Exception innerException)
          : base(message, innerException)
        {
        }

        protected CommandLineException(SerializationInfo info, StreamingContext context)
          : base(info, context)
        {
        }
    }
}
