using System;
using System.Globalization;

namespace Cowboy.CommandLines
{
    public class CommandLineExceptionEventArgs : EventArgs
    {
        public CommandLineException Exception { get; set; }

        public CommandLineExceptionEventArgs(CommandLineException exception)
        {
            Exception = exception;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, "Exception : {0}", Exception.Message);
        }
    }
}
