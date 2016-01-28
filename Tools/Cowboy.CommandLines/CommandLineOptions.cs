using System.Collections.Generic;

namespace Cowboy.CommandLines
{
    /// <summary>
    /// Contains the parsed command line options. This consists of two
    /// lists, one of argument pairs, and one of stand-alone parameters.
    /// </summary>
    public class CommandLineOptions
    {
        private List<string> parameters = new List<string>();
        private Dictionary<string, string> arguments = new Dictionary<string, string>();

        /// <summary>
        /// Returns the list of stand-alone parameters.
        /// </summary>
        public ICollection<string> Parameters
        {
            get { return parameters; }
        }

        /// <summary>
        /// Returns the dictionary of argument/value pairs.
        /// </summary>
        public IDictionary<string, string> Arguments
        {
            get { return arguments; }
        }
    }
}
