using System.Net;

namespace Cowboy.Http.Responses
{
    /// <summary>
    /// Response with status code 404 (Not Found).
    /// </summary>
    public class NotFoundResponse : Response
    {
        public NotFoundResponse()
        {
            this.ContentType = "text/html";
            this.StatusCode = HttpStatusCode.NotFound;
        }
    }
}
