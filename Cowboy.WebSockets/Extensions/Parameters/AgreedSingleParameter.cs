using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Extensions
{
    public class AgreedSingleParameter : AgreedExtensionParameter
    {
        public AgreedSingleParameter(string name)
            : base(name)
        {
        }

        public override ExtensionParameterType ParameterType
        {
            get
            {
                return ExtensionParameterType.Single;
            }
        }
    }
}
