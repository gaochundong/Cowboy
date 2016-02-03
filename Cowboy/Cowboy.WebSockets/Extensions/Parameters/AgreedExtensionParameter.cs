using System;

namespace Cowboy.WebSockets.Extensions
{
    public abstract class AgreedExtensionParameter
    {
        public AgreedExtensionParameter(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name");

            this.Name = name;
        }

        public string Name { get; private set; }
        public abstract ExtensionParameterType ParameterType { get; }

        public override string ToString()
        {
            return string.Format("{0}", this.Name);
        }
    }
}
