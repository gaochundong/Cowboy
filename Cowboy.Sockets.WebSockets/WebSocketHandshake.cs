using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    internal class WebSocketHandshake
    {
        public const string MagicHandeshakeAcceptedKey = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static byte[] CreateHandshakeRequest(
            string host,
            string path,
            out string key,
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
            key = Convert.ToBase64String(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString().Substring(0, 16)));
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
            return Encoding.UTF8.GetBytes(message);
        }

        public static bool VerifyHandshake(byte[] buffer, int offset, int count, string secWebSocketKey)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (string.IsNullOrEmpty(secWebSocketKey))
                throw new ArgumentNullException("context.SecWebSocketKey");

            var response = Encoding.UTF8.GetString(buffer, offset, count);

            // HTTP/1.1 101 Switching Protocols
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Accept: 1tGBmA9p0DQDgmFll6P0/UcVS/E=
            // Sec-WebSocket-Protocol: chat
            var headers = ParseWebSocketResponseHeaders(response);

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
                            secWebSocketKey + MagicHandeshakeAcceptedKey)));

            return headers["Sec-WebSocket-Accept"].Equals(challenge, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> ParseWebSocketResponseHeaders(string response)
        {
            var headers = new Dictionary<string, string>();

            var lines = response.Split(new char[] { '\r', '\n' }).Where(l => l.Length > 0);
            foreach (var line in lines)
            {
                if (line.StartsWith(@"HTTP/"))
                {
                    var segements = line.Split(' ');
                    if (segements.Length > 1)
                    {
                        headers.Add("HttpStatusCode", segements[1]);
                    }
                }
                else if (line.StartsWith(@"Upgrade:"))
                {
                    var index = line.IndexOf(':');
                    if (index != -1)
                    {
                        var value = line.Substring(index + 1);
                        headers.Add("Upgrade", value.Trim());
                    }
                }
                else if (line.StartsWith(@"Connection:"))
                {
                    var index = line.IndexOf(':');
                    if (index != -1)
                    {
                        var value = line.Substring(index + 1);
                        headers.Add("Connection", value.Trim());
                    }
                }
                else if (line.StartsWith(@"Sec-WebSocket-Accept:"))
                {
                    var index = line.IndexOf(':');
                    if (index != -1)
                    {
                        var value = line.Substring(index + 1);
                        headers.Add("Sec-WebSocket-Accept", value.Trim());
                    }
                }
                else if (line.StartsWith(@"Sec-WebSocket-Version:"))
                {
                    var index = line.IndexOf(':');
                    if (index != -1)
                    {
                        var value = line.Substring(index + 1);
                        headers.Add("Sec-WebSocket-Version", value.Trim());
                    }
                }
                else if (line.StartsWith(@"Sec-WebSocket-Protocol:"))
                {
                    var index = line.IndexOf(':');
                    if (index != -1)
                    {
                        var value = line.Substring(index + 1);
                        headers.Add("Sec-WebSocket-Protocol", value.Trim());
                    }
                }
                else if (line.StartsWith(@"Sec-WebSocket-Extensions:"))
                {
                    var index = line.IndexOf(':');
                    if (index != -1)
                    {
                        var value = line.Substring(index + 1);
                        headers.Add("Sec-WebSocket-Extensions", value.Trim());
                    }
                }
                else if (line.StartsWith(@"Origin:"))
                {
                    var index = line.IndexOf(':');
                    if (index != -1)
                    {
                        var value = line.Substring(index + 1);
                        headers.Add("Origin", value.Trim());
                    }
                }
                else if (line.StartsWith(@"Cookie:"))
                {
                    var index = line.IndexOf(':');
                    if (index != -1)
                    {
                        var value = line.Substring(index + 1);
                        headers.Add("Cookie", value.Trim());
                    }
                }
            }

            return headers;
        }
    }
}
