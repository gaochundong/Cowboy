using System;
using System.Linq;
using Cowboy.Utilities;

namespace Cowboy
{
    public static class StringExtensions
    {
        public static DynamicDictionary AsQueryDictionary(this string queryString)
        {
            var coll = HttpUtility.ParseQueryString(queryString);
            var ret = new DynamicDictionary();
            var requestQueryFormMultipartLimit = 1000;

            var found = 0;
            foreach (var key in coll.AllKeys.Where(key => key != null))
            {
                ret[key] = coll[key];

                found++;

                if (found >= requestQueryFormMultipartLimit)
                {
                    break;
                }
            }

            return ret;
        }

        public static string ToCamelCase(this string value)
        {
            return value.ConvertFirstCharacter(x => x.ToLowerInvariant());
        }

        public static string ToPascalCase(this string value)
        {
            return value.ConvertFirstCharacter(x => x.ToUpperInvariant());
        }

        private static string ConvertFirstCharacter(this string value, Func<string, string> converter)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return string.Concat(converter(value.Substring(0, 1)), value.Substring(1));
        }
    }
}
