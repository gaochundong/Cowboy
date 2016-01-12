using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Http;
using Cowboy.StaticContent;
using Cowboy.Utilities;
using Cowboy.Http.WebSockets;

namespace Cowboy
{
    public class Engine
    {
        private StaticContentProvider _staticContentProvider;
        private RequestDispatcher _requestDispatcher;
        private WebSocketDispatcher _webSocketDispatcher;

        public Engine(StaticContentProvider staticContentProvider, RequestDispatcher requestDispatcher, WebSocketDispatcher webSocketDispatcher)
        {
            if (staticContentProvider == null)
                throw new ArgumentNullException("staticContentProvider");
            if (requestDispatcher == null)
                throw new ArgumentNullException("requestDispatcher");
            if (webSocketDispatcher == null)
                throw new ArgumentNullException("webSocketDispatcher");

            _staticContentProvider = staticContentProvider;
            _requestDispatcher = requestDispatcher;
            _webSocketDispatcher = webSocketDispatcher;
        }

        public async Task HandleHttp(HttpListenerContext httpContext, Uri baseUri, CancellationToken cancellationToken)
        {
            if (httpContext == null)
                throw new ArgumentNullException("httpContext");

            var request = ConvertRequest(baseUri, httpContext.Request);

            var context = new Context()
            {
                Request = request,
            };

            var staticContentResponse = _staticContentProvider.GetContent(context);
            if (staticContentResponse != null)
            {
                context.Response = staticContentResponse;
            }
            else
            {
                context.Response = await _requestDispatcher.Dispatch(context, cancellationToken);
            }

            ConvertResponse(context.Response, httpContext.Response);
        }

        public async Task HandleWebSocket(HttpListenerContext httpContext, HttpListenerWebSocketContext webSocketContext, CancellationToken cancellationToken)
        {
            if (httpContext == null)
                throw new ArgumentNullException("httpContext");
            if (webSocketContext == null)
                throw new ArgumentNullException("webSocketContext");

            await _webSocketDispatcher.Dispatch(httpContext, webSocketContext, cancellationToken);
        }

        private Request ConvertRequest(Uri baseUri, HttpListenerRequest httpRequest)
        {
            var expectedRequestLength = GetExpectedRequestLength(httpRequest.Headers.ToDictionary());

            var relativeUrl = baseUri.MakeAppLocalPath(httpRequest.Url);

            var url = new Url
            {
                Scheme = httpRequest.Url.Scheme,
                HostName = httpRequest.Url.Host,
                Port = httpRequest.Url.IsDefaultPort ? null : (int?)httpRequest.Url.Port,
                BasePath = baseUri.AbsolutePath.TrimEnd('/'),
                Path = HttpUtility.UrlDecode(relativeUrl),
                Query = httpRequest.Url.Query,
            };

            var fieldCount = httpRequest.ProtocolVersion.Major == 2 ? 1 : 2;
            var protocolVersion = string.Format("HTTP/{0}", httpRequest.ProtocolVersion.ToString(fieldCount));

            byte[] certificate = null;
            if (httpRequest.IsSecureConnection)
            {
                var x509Certificate = httpRequest.GetClientCertificate();
                if (x509Certificate != null)
                {
                    certificate = x509Certificate.RawData;
                }
            }

            return new Request(
                httpRequest.HttpMethod,
                url,
                RequestStream.FromStream(httpRequest.InputStream, expectedRequestLength, false),
                httpRequest.Headers.ToDictionary(),
                (httpRequest.RemoteEndPoint != null) ? httpRequest.RemoteEndPoint.Address.ToString() : null,
                protocolVersion,
                certificate);
        }

        private void ConvertResponse(Response response, HttpListenerResponse httpResponse)
        {
            foreach (var header in response.Headers)
            {
                if (!IgnoredHeaders.IsIgnored(header.Key))
                {
                    httpResponse.AddHeader(header.Key, header.Value);
                }
            }

            foreach (var cookie in response.Cookies)
            {
                httpResponse.Headers.Add(HttpResponseHeader.SetCookie, cookie.ToString());
            }

            if (response.ReasonPhrase != null)
            {
                httpResponse.StatusDescription = response.ReasonPhrase;
            }

            if (response.ContentType != null)
            {
                httpResponse.ContentType = response.ContentType;
            }

            httpResponse.StatusCode = (int)response.StatusCode;

            using (var output = httpResponse.OutputStream)
            {
                response.Contents.Invoke(output);
            }
        }

        private static long GetExpectedRequestLength(IDictionary<string, IEnumerable<string>> incomingHeaders)
        {
            if (incomingHeaders == null)
            {
                return 0;
            }

            if (!incomingHeaders.ContainsKey("Content-Length"))
            {
                return 0;
            }

            var headerValue = incomingHeaders["Content-Length"].SingleOrDefault();

            if (headerValue == null)
            {
                return 0;
            }

            long contentLength;

            return !long.TryParse(headerValue, NumberStyles.Any, CultureInfo.InvariantCulture, out contentLength) ?
                0 : contentLength;
        }
    }
}
