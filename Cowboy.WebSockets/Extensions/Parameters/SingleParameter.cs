using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Extensions
{
    public class SingleParameter : ExtensionParameter
    {
        public SingleParameter(string name)
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
