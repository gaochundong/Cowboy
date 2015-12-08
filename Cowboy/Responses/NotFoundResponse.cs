using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses
{
    public class NotFoundResponse : Response
    {
        public NotFoundResponse()
        {
            this.ContentType = "text/html";
            this.StatusCode = HttpStatusCode.NotFound;
        }
    }
}
