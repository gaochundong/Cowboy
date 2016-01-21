using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Extensions
{
    public class AbsentableValueParameter<T> : ExtensionParameter
    {
        public AbsentableValueParameter(string name, Func<string, bool> valueValidator, T defaultValue)
            : base(name)
        {
            if (valueValidator == null)
                throw new ArgumentNullException("valueValidator");

            this.ValueValidator = valueValidator;
            this.DefaultValue = defaultValue;
        }

        public override ExtensionParameterType ParameterType
        {
            get
            {
                return ExtensionParameterType.Single | ExtensionParameterType.Valuable;
            }
        }

        public Func<string, bool> ValueValidator { get; private set; }

        public T DefaultValue { get; private set; }
    }
}
