using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy
{
    public static class CollectionExtensions
    {
        public static IDictionary<string, IEnumerable<string>> ToDictionary(this NameValueCollection source)
        {
            return source.AllKeys.ToDictionary<string, string, IEnumerable<string>>(key => key, source.GetValues);
        }
    }
}
