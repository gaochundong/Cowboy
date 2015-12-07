using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Testing
{
    public class TestModule : Module
    {
        public TestModule()
        {
            Get["/"] = parameters =>
            {
                return "hello world";
            };

            Get["/greeting"] = parameters =>
            {
                return "greeting";
            };
        }
    }
}
