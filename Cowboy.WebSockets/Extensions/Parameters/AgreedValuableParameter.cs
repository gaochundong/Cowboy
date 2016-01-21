using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Extensions
{
    public class AgreedValuableParameter<T> : AgreedExtensionParameter
    {
        public AgreedValuableParameter(string name, T @value)
            : base(name)
        {
            this.Value = @value;
        }

        public override ExtensionParameterType ParameterType
        {
            get
            {
                return ExtensionParameterType.Valuable;
            }
        }

        public T Value { get; private set; }
    }
}
