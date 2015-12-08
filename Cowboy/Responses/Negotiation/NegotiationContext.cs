using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses.Negotiation
{
    public class NegotiationContext
    {
        public NegotiationContext()
        {
            //this.Cookies = new List<INancyCookie>();
            this.PermissableMediaRanges = new List<MediaRange>(new[] { (MediaRange)"*/*" });
            this.MediaRangeModelMappings = new Dictionary<MediaRange, Func<dynamic>>();
            this.Headers = new Dictionary<string, string>();
        }

        //public IList<INancyCookie> Cookies { get; set; }

        public dynamic DefaultModel { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public IDictionary<MediaRange, Func<dynamic>> MediaRangeModelMappings { get; set; }

        public string ModuleName { get; set; }

        public string ModulePath { get; set; }

        public IList<MediaRange> PermissableMediaRanges { get; set; }

        public HttpStatusCode? StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public string ViewName { get; set; }

        public dynamic GetModelForMediaRange(MediaRange mediaRange)
        {
            var matching = this.MediaRangeModelMappings.Any(m => mediaRange.Matches(m.Key));

            return matching ?
                this.MediaRangeModelMappings.First(m => mediaRange.Matches(m.Key)).Value.Invoke() :
                this.DefaultModel;
        }

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
