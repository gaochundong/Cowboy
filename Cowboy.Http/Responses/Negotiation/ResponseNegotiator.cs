using System;

namespace Cowboy.Http.Responses
{
    public class ResponseNegotiator
    {
        public ResponseNegotiator()
        {
        }

        public Response NegotiateResponse(dynamic routeResult, Context context)
        {
            Response response;
            if (TryCastResultToResponse(routeResult, out response))
            {
                return response;
            }

            throw new NotSupportedException("Negotiate response failed.");
        }

        private static bool TryCastResultToResponse(dynamic routeResult, out Response response)
        {
            // This code has to be designed this way in order for the cast operator overloads
            // to be called in the correct way. It cannot be replaced by the as-operator.
            try
            {
                response = (Response)routeResult;
                return true;
            }
            catch
            {
                response = null;
                return false;
            }
        }
    }
}
