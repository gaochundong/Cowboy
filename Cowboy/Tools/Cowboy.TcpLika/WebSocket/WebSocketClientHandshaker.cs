using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Cowboy.TcpLika
{
    internal class WebSocketClientHandshaker
    {
        private static readonly char[] _headerLineSplitter = new char[] { '\r', '\n' };

        public class HandshakeContext
        {
            public byte[] RequestBuffer { get; set; }
            public int RequestBufferOffset { get; set; }
            public int RequestBufferCount { get; set; }

            public byte[] ResponseBuffer { get; set; }
            public int ResponseBufferOffset { get; set; }
            public int ResponseBufferCount { get; set; }

            public string SecWebSocketKey { get; set; }
        }

        public static HandshakeContext BuildHandeshakeContext(
            string host,
            string path,
            string key = null,
            string protocol = null,
            string version = null,
            string extensions = null,
            string origin = null,
            IEnumerable<KeyValuePair<string, string>> cookies = null)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException("host");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            var sb = new StringBuilder();

            if (string.IsNullOrEmpty(key))
                key = Convert.ToBase64String(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString().Substring(0, 16)));

            sb.AppendFormatWithCrCf("GET {0} HTTP/1.1", path);
            sb.AppendFormatWithCrCf("Host: {0}", host);

            sb.AppendWithCrCf("Upgrade: websocket");
            sb.AppendWithCrCf("Connection: Upgrade");

            // In addition to Upgrade headers, the client sends a Sec-WebSocket-Key header 
            // containing base64-encoded random bytes, and the server replies with a hash of the key 
            // in the Sec-WebSocket-Accept header. This is intended to prevent a caching proxy 
            // from re-sending a previous WebSocket conversation, and does not provide any authentication, 
            // privacy or integrity. The hashing function appends the 
            // fixed string 258EAFA5-E914-47DA-95CA-C5AB0DC85B11 (a GUID) to the value 
            // from Sec-WebSocket-Key header (which is not decoded from base64), 
            // applies the SHA-1 hashing function, and encodes the result using base64.
            sb.AppendFormatWithCrCf("Sec-WebSocket-Key: {0}", key);

            // The |Sec-WebSocket-Version| header field in the client's
            // handshake includes the version of the WebSocket Protocol with
            // which the client is attempting to communicate.  If this
            // version does not match a version understood by the server, the
            // server MUST abort the WebSocket handshake described in this
            // section and instead send an appropriate HTTP error code(such
            // as 426 Upgrade Required) and a |Sec-WebSocket-Version| header
            // field indicating the version(s)the server is capable of understanding.
            if (!string.IsNullOrEmpty(version))
                sb.AppendFormatWithCrCf("Sec-WebSocket-Version: {0}", version);
            else
                sb.AppendFormatWithCrCf("Sec-WebSocket-Version: {0}", 13);

            // Optionally
            // The |Sec-WebSocket-Protocol| request-header field can be
            // used to indicate what subprotocols(application - level protocols
            // layered over the WebSocket Protocol) are acceptable to the client.
            if (!string.IsNullOrEmpty(protocol))
                sb.AppendFormatWithCrCf("Sec-WebSocket-Protocol: {0}", protocol);

            // Optionally
            // A (possibly empty) list representing the protocol-level
            // extensions the server is ready to use.
            if (!string.IsNullOrEmpty(extensions))
                sb.AppendFormatWithCrCf("Sec-WebSocket-Extensions: {0}", extensions);

            // Optionally
            // The |Origin| header field is used to protect against
            // unauthorized cross-origin use of a WebSocket server by scripts using         
            // the WebSocket API in a web browser.
            // This header field is sent by browser clients; for non-browser clients, 
            // this header field may be sent if it makes sense in the context of those clients.
            if (!string.IsNullOrEmpty(origin))
                sb.AppendFormatWithCrCf("Origin: {0}", origin);

            if (cookies != null && cookies.Any())
            {
                string[] pairs = new string[cookies.Count()];

                for (int i = 0; i < cookies.Count(); i++)
                {
                    var item = cookies.ElementAt(i);
                    pairs[i] = item.Key + "=" + Uri.EscapeUriString(item.Value);
                }

                sb.AppendFormatWithCrCf("Cookie: {0}", string.Join(";", pairs));
            }

            sb.AppendWithCrCf();

            // GET /chat HTTP/1.1
            // Host: server.example.com
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==
            // Sec-WebSocket-Protocol: chat, superchat
            // Sec-WebSocket-Version: 13
            // Origin: http://example.com
            var message = sb.ToString();

            var requestBuffer = Encoding.UTF8.GetBytes(message);
            var context = new HandshakeContext()
            {
                RequestBuffer = requestBuffer,
                RequestBufferOffset = 0,
                RequestBufferCount = requestBuffer.Length,
                SecWebSocketKey = key,
            };
            return context;
        }

        public static bool VerifyHandshake(HandshakeContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (context.ResponseBuffer == null)
                throw new ArgumentNullException("context.ResponseBuffer");
            if (string.IsNullOrEmpty(context.SecWebSocketKey))
                throw new ArgumentNullException("context.SecWebSocketKey");

            var response = Encoding.UTF8.GetString(context.ResponseBuffer, context.ResponseBufferOffset, context.ResponseBufferCount);

            // HTTP/1.1 101 Switching Protocols
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Accept: 1tGBmA9p0DQDgmFll6P0/UcVS/E=
            // Sec-WebSocket-Protocol: chat
            Dictionary<string, string> headers;
            List<string> extensions;
            List<string> protocols;
            ParseOpenningHandshakeResponseHeaders(response, out headers, out extensions, out protocols);

            if (!headers.ContainsKey("HttpStatusCode"))
                return false;

            // Any status code other than 101 indicates that the WebSocket handshake
            // has not completed and that the semantics of HTTP still apply.
            if (headers["HttpStatusCode"] != "101")
                return false;

            if (!headers.ContainsKey("Sec-WebSocket-Accept"))
                return false;

            string challenge =
                Convert.ToBase64String(
                    SHA1.Create().ComputeHash(
                        Encoding.ASCII.GetBytes(
                            context.SecWebSocketKey + Consts.SecWebSocketKeyGuid)));

            return headers["Sec-WebSocket-Accept"].Equals(challenge, StringComparison.OrdinalIgnoreCase);
        }

        private static void ParseOpenningHandshakeResponseHeaders(string response,
            out Dictionary<string, string> headers,
            out List<string> extensions,
            out List<string> protocols)
        {
            headers = new Dictionary<string, string>();

            // The |Sec-WebSocket-Extensions| header field MAY appear multiple times
            // in an HTTP request (which is logically the same as a single
            // |Sec-WebSocket-Extensions| header field that contains all values.
            // However, the |Sec-WebSocket-Extensions| header field MUST NOT appear
            // more than once in an HTTP response.
            extensions = null;
            // The |Sec-WebSocket-Protocol| header field MAY appear multiple times
            // in an HTTP request (which is logically the same as a single
            // |Sec-WebSocket-Protocol| header field that contains all values).
            // However, the |Sec-WebSocket-Protocol| header field MUST NOT appear
            // more than once in an HTTP response.
            protocols = null;

            var lines = response.Split(_headerLineSplitter).Where(l => l.Length > 0);
            foreach (var line in lines)
            {
                // HTTP/1.1 101 Switching Protocols
                // HTTP/1.1 400 Bad Request
                if (line.StartsWith(@"HTTP/"))
                {
                    var segements = line.Split(' ');
                    if (segements.Length > 1)
                    {
                        headers.Add(Consts.HttpStatusCodeName, segements[1]);

                        if (segements.Length > 2)
                        {
                            headers.Add(Consts.HttpStatusCodeDescription, segements[2]);
                        }
                    }
                }
                else
                {
                    foreach (var key in HttpKnownHeaderNames.All)
                    {
                        if (line.StartsWith(key + ":"))
                        {
                            var index = line.IndexOf(':');
                            if (index != -1)
                            {
                                var value = line.Substring(index + 1);

                                if (key == HttpKnownHeaderNames.SecWebSocketExtensions)
                                {
                                    if (extensions == null)
                                        extensions = new List<string>();
                                    extensions.Add(value.Trim());
                                }
                                else if (key == HttpKnownHeaderNames.SecWebSocketProtocol)
                                {
                                    if (protocols == null)
                                        protocols = new List<string>();
                                    protocols.Add(value.Trim());
                                }
                                else
                                {
                                    if (headers.ContainsKey(key))
                                    {
                                        headers[key] = string.Join(",", headers[key], value.Trim());
                                    }
                                    else
                                    {
                                        headers.Add(key, value.Trim());
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
