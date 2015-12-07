using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Responses.Negotiation;

namespace Cowboy.Routing
{
    public class RouteInvoker
    {
        private readonly ResponseNegotiator negotiator;

        public RouteInvoker(ResponseNegotiator negotiator)
        {
            this.negotiator = negotiator;
        }

        public async Task<Response> Invoke(Route route, CancellationToken cancellationToken, DynamicDictionary parameters, Context context)
        {
            //var tcs = new TaskCompletionSource<Response>();

            var result = await route.Invoke(parameters, cancellationToken);

            //result.WhenCompleted(
            //    completedTask =>
            //    {
            //        var returnResult = completedTask.Result;
            //        if (!(returnResult is ValueType) && returnResult == null)
            //        {
            //            context.WriteTraceLog(
            //                sb => sb.AppendLine("[DefaultRouteInvoker] Invocation of route returned null"));

            //            returnResult = new Response();
            //        }

            //        try
            //        {
            //            var response = this.negotiator.NegotiateResponse(returnResult, context);

            //            tcs.SetResult(response);
            //        }
            //        catch (Exception e)
            //        {
            //            tcs.SetException(e);
            //        }
            //    },
            //    faultedTask =>
            //    {
            //        var earlyExitException = GetEarlyExitException(faultedTask);

            //        if (earlyExitException != null)
            //        {
            //            context.WriteTraceLog(
            //                sb =>
            //                sb.AppendFormat(
            //                    "[DefaultRouteInvoker] Caught RouteExecutionEarlyExitException - reason {0}",
            //                    earlyExitException.Reason));
            //            tcs.SetResult(earlyExitException.Response);
            //        }
            //        else
            //        {
            //            tcs.SetException(faultedTask.Exception);
            //        }
            //    });

            return null;
        }

        //private static RouteExecutionEarlyExitException GetEarlyExitException(Task<dynamic> faultedTask)
        //{
        //    var taskExceptions = faultedTask.Exception;

        //    if (taskExceptions == null)
        //    {
        //        return null;
        //    }

        //    if (taskExceptions.InnerExceptions.Count > 1)
        //    {
        //        return null;
        //    }

        //    return taskExceptions.InnerException as RouteExecutionEarlyExitException;
        //}
    }
}
