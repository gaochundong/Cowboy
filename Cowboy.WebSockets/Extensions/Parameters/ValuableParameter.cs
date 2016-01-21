using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Extensions
{
    public class ValuableParameter<T> : ExtensionParameter
    {
        public ValuableParameter(string name, Func<string, bool> valueValidator)
            : base(name)
        {
            if (valueValidator == null)
                throw new ArgumentNullException("valueValidator");

            this.ValueValidator = valueValidator;
        }

        public override ExtensionParameterType ParameterType
        {
            get
            {
                return ExtensionParameterType.Valuable;
            }
        }

        public Func<string, bool> ValueValidator { get; private set; }
    }
}
