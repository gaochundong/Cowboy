using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Extensions
{
    public abstract class ExtensionParameter
    {
        public ExtensionParameter(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name");

            this.Name = name;
        }

        public string Name { get; private set; }
        public abstract ExtensionParameterType ParameterType { get; }
    }
}
