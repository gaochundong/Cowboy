using System;
using System.Globalization;

namespace Cowboy.CommandLines
{
    public class CommandLineUsageEventArgs : EventArgs
    {
        public string Usage { get; set; }

        public CommandLineUsageEventArgs(string usage)
        {
            Usage = usage;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, "Usage : {0}", Usage);
        }
    }
}
