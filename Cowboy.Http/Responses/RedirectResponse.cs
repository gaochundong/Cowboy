using System.Net;

namespace Cowboy.Http.Responses
{
    public class RedirectResponse : Response
    {
        public RedirectResponse(string location, RedirectType type = RedirectType.SeeOther)
        {
            this.Headers.Add("Location", location);
            this.Contents = GetStringContents(string.Empty);
            this.ContentType = "text/html";
            switch (type)
            {
                case RedirectType.Permanent:
                    this.StatusCode = HttpStatusCode.MovedPermanently;
                    break;
                case RedirectType.Temporary:
                    this.StatusCode = HttpStatusCode.TemporaryRedirect;
                    break;
                default:
                    this.StatusCode = HttpStatusCode.SeeOther;
                    break;
            }
        }

        /// <summary>
        /// Which type of redirect
        /// </summary>
        public enum RedirectType
        {
            /// <summary>
            /// HTTP 301 - All future requests should be to this URL
            /// </summary>
            Permanent,
            /// <summary>
            /// HTTP 307 - Redirect this request but allow future requests to the original URL
            /// </summary>
            Temporary,
            /// <summary>
            /// HTTP 303 - Redirect this request using an HTTP GET
            /// </summary>
            SeeOther
        }
    }
}
