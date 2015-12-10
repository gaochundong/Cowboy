using System;
using System.Linq;
using System.Net;

namespace Cowboy.Responses.Negotiation
{
    public class Negotiator : IHideObjectMembers
    {
        public Negotiator(Context context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            this.NegotiationContext = context.NegotiationContext;
        }

        public NegotiationContext NegotiationContext { get; private set; }

        public Negotiator WithModel(dynamic model)
        {
            this.NegotiationContext.DefaultModel = model;
            return this;
        }

        public Negotiator WithHeaders(params Tuple<string, string>[] headers)
        {
            foreach (var keyValuePair in headers)
            {
                this.NegotiationContext.Headers[keyValuePair.Item1] = keyValuePair.Item2;
            }

            return this;
        }

        public Negotiator WithHeaders(params object[] headers)
        {
            return this.WithHeaders(headers.Select(GetTuple).ToArray());
        }

        public Negotiator WithContentType(string contentType)
        {
            return this.WithHeaders(new { Header = "Content-Type", Value = contentType });
        }

        public Negotiator WithStatusCode(int statusCode)
        {
            this.NegotiationContext.StatusCode = (HttpStatusCode)statusCode;
            return this;
        }

        public Negotiator WithReasonPhrase(string reasonPhrase)
        {
            this.NegotiationContext.ReasonPhrase = reasonPhrase;
            return this;
        }

        public Negotiator WithStatusCode(HttpStatusCode statusCode)
        {
            this.NegotiationContext.StatusCode = statusCode;
            return this;
        }

        private static Tuple<string, string> GetTuple(object header)
        {
            var properties = header.GetType()
                                   .GetProperties()
                                   .Where(prop => prop.CanRead && prop.PropertyType == typeof(string))
                                   .ToArray();

            var headerProperty = properties
                                    .Where(p => string.Equals(p.Name, "Header", StringComparison.OrdinalIgnoreCase))
                                    .FirstOrDefault();

            var valueProperty = properties
                                    .Where(p => string.Equals(p.Name, "Value", StringComparison.OrdinalIgnoreCase))
                                    .FirstOrDefault();

            if (headerProperty == null || valueProperty == null)
            {
                throw new ArgumentException("Unable to extract 'Header' or 'Value' properties from anonymous type.");
            }

            return Tuple.Create(
                (string)headerProperty.GetValue(header, null),
                (string)valueProperty.GetValue(header, null));
        }
    }
}
