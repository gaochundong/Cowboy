using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Hosting.Self
{
    public class SelfHost
    {
        private IList<Uri> _baseUriList;
        private HttpListener _listener;
        private bool _keepProcessing = false;
        private Engine _engine;
        private SemaphoreSlim _concurrencyController;

        public SelfHost(Engine engine, params Uri[] baseUris)
            : this(engine, Environment.ProcessorCount * 2, baseUris)
        {
        }

        public SelfHost(Engine engine, int concurrentCount, params Uri[] baseUris)
        {
            if (engine == null)
                throw new ArgumentNullException("engine");

            _engine = engine;
            _baseUriList = baseUris;
            _concurrencyController = new SemaphoreSlim(concurrentCount, concurrentCount);
        }

        public void Start()
        {
            StartListener();

            _keepProcessing = true;
            Task.Run(async () =>
                {
                    await StartProcess();
                })
                .Forget();
        }

        public void Stop()
        {
            _keepProcessing = false;
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
            }
        }

        private async Task StartProcess()
        {
            while (_keepProcessing)
            {
                await _concurrencyController.WaitAsync();

                var context = await _listener.GetContextAsync();
                Task.Run(async () =>
                    {
                        try
                        {
                            await Process(context);
                        }
                        finally
                        {
                            _concurrencyController.Release();
                        }
                    })
                    .Forget();
            }
        }

        private async Task Process(HttpListenerContext httpContext)
        {
            try
            {
                var cancellationToken = new CancellationToken();

                if (httpContext.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await httpContext.AcceptWebSocketAsync(null);
                    await _engine.HandleWebSocket(webSocketContext, cancellationToken);
                }
                else
                {
                    var baseUri = GetBaseUri(httpContext.Request);
                    await _engine.HandleHttp(httpContext, baseUri, cancellationToken);
                }
            }
            catch
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                httpContext.Response.Close();
            }
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
    }
}
