using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Hosting.Self
{
    public static class NetSh
    {
        private const string NetshCommand = "netsh";

        public static bool AddUrlAcl(string url, string user)
        {
            try
            {
                var arguments = GetParameters(url, user);

                return UacHelper.RunElevated(NetshCommand, arguments);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static string GetParameters(string url, string user)
        {
            return string.Format("http add urlacl url=\"{0}\" user=\"{1}\"", url, user);
        }
    }
}
