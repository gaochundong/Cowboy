using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses
{
    /// <summary>
    /// Response with status code 406 (Not Acceptable).
    /// </summary>
    public class NotAcceptableResponse : Response
    {
        public NotAcceptableResponse()
        {
            this.StatusCode = HttpStatusCode.NotAcceptable;
        }
    }
}
