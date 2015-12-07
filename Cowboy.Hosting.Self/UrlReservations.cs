using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Hosting.Self
{
    public class UrlReservations
    {
        private const string EveryoneAccountName = "Everyone";

        private static readonly IdentityReference EveryoneReference =
            new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        public UrlReservations()
        {
            this.CreateAutomatically = false;
            this.User = GetEveryoneAccountName();
        }

        public bool CreateAutomatically { get; set; }

        public string User { get; set; }

        private static string GetEveryoneAccountName()
        {
            try
            {
                var account = EveryoneReference.Translate(typeof(NTAccount)) as NTAccount;
                if (account != null)
                {
                    return account.Value;
                }

                return EveryoneAccountName;
            }
            catch (Exception)
            {
                return EveryoneAccountName;
            }
        }
    }
}
