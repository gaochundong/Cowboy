using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses.Negotiation
{
    public class ResponseNegotiator
    {
        private readonly IEnumerable<IResponseProcessor> processors;
        private readonly AcceptHeaderCoercionConventions coercionConventions;

        public ResponseNegotiator(IEnumerable<IResponseProcessor> processors, AcceptHeaderCoercionConventions coercionConventions)
        {
            this.processors = processors;
            this.coercionConventions = coercionConventions;
        }

        public Response NegotiateResponse(dynamic routeResult, Context context)
        {
            Response response;
            if (TryCastResultToResponse(routeResult, out response))
            {
                return response;
            }

            NegotiationContext negotiationContext = GetNegotiationContext(routeResult, context);

            var coercedAcceptHeaders = this.GetCoercedAcceptHeaders(context).ToArray();

            var compatibleHeaders = this.GetCompatibleHeaders(coercedAcceptHeaders, negotiationContext, context).ToArray();

            if (!compatibleHeaders.Any())
            {
                return new NotAcceptableResponse();
            }

            return CreateResponse(compatibleHeaders, negotiationContext, context);
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

        private static NegotiationContext GetNegotiationContext(object routeResult, Context context)
        {
            var negotiator = routeResult as Negotiator;

            if (negotiator == null)
            {
                negotiator = new Negotiator(context).WithModel(routeResult);
            }

            return negotiator.NegotiationContext;
        }

        private IEnumerable<Tuple<string, decimal>> GetCoercedAcceptHeaders(Context context)
        {
            return this.coercionConventions.Aggregate(context.Request.Headers.Accept, (current, coercion) => coercion.Invoke(current, context));
        }

        private static void GetAccepHeaderTraceLog(
            Context context,
            NegotiationContext negotiationContext,
            Tuple<string, decimal>[] coercedAcceptHeaders,
            StringBuilder sb)
        {
            var allowableFormats = negotiationContext.PermissableMediaRanges
                .Select(mr => mr.ToString())
                .Aggregate((t1, t2) => t1 + ", " + t2);

            var originalAccept = context.Request.Headers["accept"].Any()
                ? string.Join(", ", context.Request.Headers["accept"])
                : "None";

            var coercedAccept = coercedAcceptHeaders.Any()
                ? coercedAcceptHeaders.Select(h => h.Item1).Aggregate((t1, t2) => t1 + ", " + t2)
                : "None";

            sb.AppendFormat("[DefaultResponseNegotiator] Original accept header: {0}\n", originalAccept);
            sb.AppendFormat("[DefaultResponseNegotiator] Coerced accept header: {0}\n", coercedAccept);
            sb.AppendFormat("[DefaultResponseNegotiator] Acceptable media ranges: {0}\n", allowableFormats);
        }

        private IEnumerable<CompatibleHeader> GetCompatibleHeaders(
            IEnumerable<Tuple<string, decimal>> coercedAcceptHeaders,
            NegotiationContext negotiationContext,
            Context context)
        {
            var acceptHeaders = GetCompatibleHeaders(coercedAcceptHeaders, negotiationContext);

            foreach (var header in acceptHeaders)
            {
                var mediaRangeModel = negotiationContext.GetModelForMediaRange(header.Item1);

                IEnumerable<Tuple<IResponseProcessor, ProcessorMatch>> compatibleProcessors =
                    this.GetCompatibleProcessorsByHeader(header.Item1, mediaRangeModel, context);

                if (compatibleProcessors.Any())
                {
                    yield return new CompatibleHeader(header.Item1, compatibleProcessors);
                }
            }
        }

        private static IEnumerable<Tuple<string, decimal>> GetCompatibleHeaders(
            IEnumerable<Tuple<string, decimal>> coercedAcceptHeaders,
            NegotiationContext negotiationContext)
        {
            var permissableMediaRanges = negotiationContext.PermissableMediaRanges;
            if (permissableMediaRanges.Any(mr => mr.IsWildcard))
            {
                return coercedAcceptHeaders.Where(header => header.Item2 > 0m);
            }

            return coercedAcceptHeaders
                .Where(header => header.Item2 > 0m)
                .SelectMany(header => permissableMediaRanges
                    .Where(mr => mr.Matches(header.Item1))
                    .Select(mr => Tuple.Create(mr.ToString(), header.Item2)));
        }

        private IEnumerable<Tuple<IResponseProcessor, ProcessorMatch>> GetCompatibleProcessorsByHeader(
            string acceptHeader, dynamic model, Context context)
        {
            foreach (var processor in this.processors)
            {
                ProcessorMatch match = processor.CanProcess(acceptHeader, model, context);

                if (match.ModelResult != MatchResult.NoMatch && match.RequestedContentTypeResult != MatchResult.NoMatch)
                {
                    yield return new Tuple<IResponseProcessor, ProcessorMatch>(processor, match);
                }
            }
        }

        private static Response CreateResponse(
            IList<CompatibleHeader> compatibleHeaders,
            NegotiationContext negotiationContext,
            Context context)
        {
            var response = NegotiateResponse(compatibleHeaders, negotiationContext, context);

            if (response == null)
            {
                response = new NotAcceptableResponse();
            }

            response.WithHeader("Vary", "Accept");

            AddLinkHeader(compatibleHeaders, response, context.Request.Url);
            SetStatusCode(negotiationContext, response);
            SetReasonPhrase(negotiationContext, response);
            //AddCookies(negotiationContext, response);

            if (response is NotAcceptableResponse)
            {
                return response;
            }

            AddContentTypeHeader(negotiationContext, response);
            AddNegotiatedHeaders(negotiationContext, response);

            return response;
        }

        private static Response NegotiateResponse(
            IEnumerable<CompatibleHeader> compatibleHeaders,
            NegotiationContext negotiationContext,
            Context context)
        {
            foreach (var compatibleHeader in compatibleHeaders)
            {
                var prioritizedProcessors = compatibleHeader.Processors
                    .OrderByDescending(x => x.Item2.ModelResult)
                    .ThenByDescending(x => x.Item2.RequestedContentTypeResult);

                foreach (var prioritizedProcessor in prioritizedProcessors)
                {
                    var processorType = prioritizedProcessor.Item1.GetType();

                    var mediaRangeModel = negotiationContext.GetModelForMediaRange(compatibleHeader.MediaRange);

                    var response = prioritizedProcessor.Item1.Process(compatibleHeader.MediaRange, mediaRangeModel, context);
                    if (response != null)
                    {
                        return response;
                    }
                }
            }

            return null;
        }

        private static void AddLinkHeader(
            IEnumerable<CompatibleHeader> compatibleHeaders,
            Response response,
            Url requestUrl)
        {
            var linkProcessors = GetLinkProcessors(compatibleHeaders, response.ContentType);
            if (linkProcessors.Any())
            {
                response.Headers["Link"] = CreateLinkHeader(requestUrl, linkProcessors);
            }
        }

        private static IDictionary<string, MediaRange> GetLinkProcessors(
            IEnumerable<CompatibleHeader> compatibleHeaders,
            string contentType)
        {
            var linkProcessors = new Dictionary<string, MediaRange>();

            var compatibleHeaderMappings = compatibleHeaders
                .SelectMany(header => header.Processors)
                .SelectMany(processor => processor.Item1.ExtensionMappings)
                .Where(mapping => !mapping.Item2.Matches(contentType));

            foreach (var compatibleHeaderMapping in compatibleHeaderMappings)
            {
                linkProcessors[compatibleHeaderMapping.Item1] = compatibleHeaderMapping.Item2;
            }

            return linkProcessors;
        }

        private static string CreateLinkHeader(Url requestUrl, IEnumerable<KeyValuePair<string, MediaRange>> linkProcessors)
        {
            var fileName = Path.GetFileNameWithoutExtension(requestUrl.Path);
            var baseUrl = string.Concat(requestUrl.BasePath, "/", fileName);

            var links = linkProcessors
                .Select(lp => string.Format("<{0}.{1}>; rel=\"{2}\"", baseUrl, lp.Key, lp.Value));

            return string.Join(",", links);
        }

        private static void AddContentTypeHeader(NegotiationContext negotiationContext, Response response)
        {
            if (negotiationContext.Headers.ContainsKey("Content-Type"))
            {
                response.ContentType = negotiationContext.Headers["Content-Type"];
                negotiationContext.Headers.Remove("Content-Type");
            }
        }

        private static void AddNegotiatedHeaders(NegotiationContext negotiationContext, Response response)
        {
            foreach (var header in negotiationContext.Headers)
            {
                response.Headers[header.Key] = header.Value;
            }
        }

        private static void SetStatusCode(NegotiationContext negotiationContext, Response response)
        {
            if (negotiationContext.StatusCode.HasValue)
            {
                response.StatusCode = negotiationContext.StatusCode.Value;
            }
        }

        private static void SetReasonPhrase(NegotiationContext negotiationContext, Response response)
        {
            if (negotiationContext.ReasonPhrase != null)
            {
                response.ReasonPhrase = negotiationContext.ReasonPhrase;
            }
        }

        //private static void AddCookies(NegotiationContext negotiationContext, Response response)
        //{
        //    foreach (var cookie in negotiationContext.Cookies)
        //    {
        //        response.Cookies.Add(cookie);
        //    }
        //}

        private class CompatibleHeader
        {
            public CompatibleHeader(
                string mediaRange,
                IEnumerable<Tuple<IResponseProcessor, ProcessorMatch>> processors)
            {
                this.MediaRange = mediaRange;
                this.Processors = processors;
            }

            public string MediaRange { get; private set; }

            public IEnumerable<Tuple<IResponseProcessor, ProcessorMatch>> Processors { get; private set; }
        }
    }
}
