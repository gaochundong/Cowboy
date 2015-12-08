using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy
{
    public class RootPathProvider
    {
        public string GetRootPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
