using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Utilities;

namespace Cowboy.Hosting.Self
{
    public class SelfHost
    {
        private IList<Uri> _baseUriList;
        private HttpListener _listener;
        private bool _keepGoing = false;
        private Engine _engine;
        private Semaphore _counter = new Semaphore(8, 8);

        public SelfHost(Engine engine, params Uri[] baseUris)
        {
            if (engine == null)
                throw new ArgumentNullException("engine");

            _engine = engine;
            _baseUriList = baseUris;
        }

        public void Start()
        {
            StartListener();

            _keepGoing = true;
            Task.Run(async () =>
                {
                    await StartProcess();
                })
                .Forget();
        }

        public void Stop()
        {
            _keepGoing = false;
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
            }
        }

        private async Task StartProcess()
        {
            while (_keepGoing)
            {
                _counter.WaitOne();

                var context = await _listener.GetContextAsync();
                Task.Run(async () =>
                    {
                        try
                        {
                            await Process(context);
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    })
                    .Forget();
            }
        }

        private async Task Process(HttpListenerContext httpContext)
        {
            var cancellationToken = new CancellationToken();
            var request = ConvertRequest(httpContext.Request);
            var context = await _engine.HandleRequest(request, cancellationToken);
            ConvertResponse(context.Response, httpContext.Response);
            _counter.Release();
        }

        private void StartListener()
        {
            if (TryStartListener())
            {
                return;
            }

            if (!TryAddUrlReservations())
            {
                throw new InvalidOperationException("Unable to configure namespace reservation.");
            }

            if (!TryStartListener())
            {
                throw new InvalidOperationException("Unable to start listener.");
            }
        }

        private bool TryStartListener()
        {
            try
            {
                // if the listener fails to start, it gets disposed;
                // so we need a new one, each time.
                _listener = new HttpListener();
                foreach (var prefix in GetPrefixes())
                {
                    _listener.Prefixes.Add(prefix);
                }

                _listener.Start();

                return true;
            }
            catch (HttpListenerException e)
            {
                int ACCESS_DENIED = 5;
                if (e.ErrorCode == ACCESS_DENIED)
                {
                    return false;
                }

                throw;
            }
        }

        private bool TryAddUrlReservations()
        {
            var user = WindowsIdentity.GetCurrent().Name;

            foreach (var prefix in GetPrefixes())
            {
                if (!NetSh.AddUrlAcl(prefix, user))
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerable<string> GetPrefixes()
        {
            foreach (var baseUri in _baseUriList)
            {
                var prefix = new UriBuilder(baseUri).ToString();

                bool rewriteLocalhost = true;
                if (rewriteLocalhost && !baseUri.Host.Contains("."))
                {
                    prefix = prefix.Replace("localhost", "+");
                }

                yield return prefix;
            }
        }

        private Uri GetBaseUri(HttpListenerRequest request)
        {
            var result = _baseUriList.FirstOrDefault(uri => uri.IsCaseInsensitiveBaseOf(request.Url));

            if (result != null)
            {
                return result;
            }

            return new Uri(request.Url.GetLeftPart(UriPartial.Authority));
        }

        private Request ConvertRequest(HttpListenerRequest httpRequest)
        {
            var baseUri = GetBaseUri(httpRequest);

            if (baseUri == null)
            {
                throw new InvalidOperationException(string.Format("Unable to locate base URI for request: {0}", httpRequest.Url));
            }

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

            // NOTE: For HTTP/2 we want fieldCount = 1,
            // otherwise (HTTP/1.0 and HTTP/1.1) we want fieldCount = 2
            var fieldCount = httpRequest.ProtocolVersion.Major == 2 ? 1 : 2;

            var protocolVersion = string.Format("HTTP/{0}", httpRequest.ProtocolVersion.ToString(fieldCount));

            return new Request(
                httpRequest.HttpMethod,
                url,
                RequestStream.FromStream(httpRequest.InputStream, expectedRequestLength, false),
                httpRequest.Headers.ToDictionary(),
                (httpRequest.RemoteEndPoint != null) ? httpRequest.RemoteEndPoint.Address.ToString() : null,
                protocolVersion);
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
