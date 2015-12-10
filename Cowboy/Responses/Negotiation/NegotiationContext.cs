using System;
using System.Collections.Generic;
using System.Net;

namespace Cowboy.Responses.Negotiation
{
    public class NegotiationContext
    {
        public NegotiationContext()
        {
            //this.Cookies = new List<INancyCookie>();
            this.Headers = new Dictionary<string, string>();
        }

        //public IList<INancyCookie> Cookies { get; set; }

        public dynamic DefaultModel { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public string ModuleName { get; set; }

        public string ModulePath { get; set; }

        public HttpStatusCode? StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        //public string ViewName { get; set; }

        internal void SetModule(Module module)
        {
            if (module == null)
            {
                throw new ArgumentNullException("module");
            }

            this.ModuleName = module.GetModuleName();
            this.ModulePath = module.ModulePath;
        }
    }
}
