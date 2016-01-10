using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    internal class WebSocketClientHandshaker
    {
        internal const string SecWebSocketKeyGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        internal const string WebSocketUpgradeToken = "websocket";
        internal const string WebSocketConnectionToken = "Upgrade";

        private static readonly List<string> HttpKnownHeaderNames = new List<string>()
        {
            "Upgrade",
            "Connection",
            "Sec-WebSocket-Accept",
            "Sec-WebSocket-Version",
            "Sec-WebSocket-Protocol",
            "Sec-WebSocket-Extensions",
            "Origin",
            "Date",
            "Server",
            "Cookie",
            "WWW-Authenticate",
        };

        internal static byte[] CreateOpenningHandshakeRequest(AsyncWebSocketClient client, out string secWebSocketKey)
        {
            if (client == null)
                throw new ArgumentNullException("client");

            var sb = new StringBuilder();

            sb.AppendFormatWithCrCf("GET {0} HTTP/1.1", !string.IsNullOrEmpty(client.Uri.AbsolutePath) ? client.Uri.AbsolutePath : "/");
            sb.AppendFormatWithCrCf("Host: {0}", client.Uri.Host);

            sb.AppendFormatWithCrCf("Upgrade: {0}", WebSocketUpgradeToken);
            sb.AppendFormatWithCrCf("Connection: {0}", WebSocketConnectionToken);

            // In addition to Upgrade headers, the client sends a Sec-WebSocket-Key header 
            // containing base64-encoded random bytes, and the server replies with a hash of the key 
            // in the Sec-WebSocket-Accept header. This is intended to prevent a caching proxy 
            // from re-sending a previous WebSocket conversation, and does not provide any authentication, 
            // privacy or integrity. The hashing function appends the 
            // fixed string 258EAFA5-E914-47DA-95CA-C5AB0DC85B11 (a GUID) to the value 
            // from Sec-WebSocket-Key header (which is not decoded from base64), 
            // applies the SHA-1 hashing function, and encodes the result using base64.
            secWebSocketKey = Convert.ToBase64String(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString().Substring(0, 16)));
            sb.AppendFormatWithCrCf("Sec-WebSocket-Key: {0}", secWebSocketKey);

            // The |Sec-WebSocket-Version| header field in the client's
            // handshake includes the version of the WebSocket Protocol with
            // which the client is attempting to communicate.  If this
            // version does not match a version understood by the server, the
            // server MUST abort the WebSocket handshake described in this
            // section and instead send an appropriate HTTP error code(such
            // as 426 Upgrade Required) and a |Sec-WebSocket-Version| header
            // field indicating the version(s)the server is capable of understanding.
            if (!string.IsNullOrEmpty(client.Version))
                sb.AppendFormatWithCrCf("Sec-WebSocket-Version: {0}", client.Version);
            else
                sb.AppendFormatWithCrCf("Sec-WebSocket-Version: {0}", 13);

            // Optionally
            // The |Sec-WebSocket-Protocol| request-header field can be
            // used to indicate what subprotocols(application - level protocols
            // layered over the WebSocket Protocol) are acceptable to the client.
            if (!string.IsNullOrEmpty(client.SubProtocol))
                sb.AppendFormatWithCrCf("Sec-WebSocket-Protocol: {0}", client.SubProtocol);

            // Optionally
            // A (possibly empty) list representing the protocol-level
            // extensions the server is ready to use.
            if (!string.IsNullOrEmpty(client.Extensions))
                sb.AppendFormatWithCrCf("Sec-WebSocket-Extensions: {0}", client.Extensions);

            // Optionally
            // The |Origin| header field is used to protect against
            // unauthorized cross-origin use of a WebSocket server by scripts using         
            // the WebSocket API in a web browser.
            // This header field is sent by browser clients; for non-browser clients, 
            // this header field may be sent if it makes sense in the context of those clients.
            if (!string.IsNullOrEmpty(client.Origin))
                sb.AppendFormatWithCrCf("Origin: {0}", client.Origin);

            if (client.Cookies != null && client.Cookies.Any())
            {
                string[] pairs = new string[client.Cookies.Count()];

                for (int i = 0; i < client.Cookies.Count(); i++)
                {
                    var item = client.Cookies.ElementAt(i);
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

        internal static bool VerifyOpenningHandshakeResponse(AsyncWebSocketClient client, byte[] buffer, int offset, int count, string secWebSocketKey)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (string.IsNullOrEmpty(secWebSocketKey))
                throw new ArgumentNullException("secWebSocketKey");

            var response = Encoding.UTF8.GetString(buffer, offset, count);

            // HTTP/1.1 101 Switching Protocols
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Accept: 1tGBmA9p0DQDgmFll6P0/UcVS/E=
            // Sec-WebSocket-Protocol: chat
            var headers = ParseOpenningHandshakeResponseHeaders(response);

            if (!headers.ContainsKey("HttpStatusCode"))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of status code.", client.RemoteEndPoint));
            if (!headers.ContainsKey("Connection"))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of connection header item.", client.RemoteEndPoint));
            if (!headers.ContainsKey("Upgrade"))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of upgrade header item.", client.RemoteEndPoint));
            if (!headers.ContainsKey("Sec-WebSocket-Accept"))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of Sec-WebSocket-Accept header item.", client.RemoteEndPoint));

            // If the status code received from the server is not 101, the
            // client handles the response per HTTP [RFC2616] procedures.  In
            // particular, the client might perform authentication if it
            // receives a 401 status code; the server might redirect the client
            // using a 3xx status code (but clients are not required to follow
            // them), etc.
            if (headers["HttpStatusCode"] != ((int)HttpStatusCode.SwitchingProtocols).ToString())
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to expected 101 Switching Protocols but received [{1}].",
                    client.RemoteEndPoint, headers["HttpStatusCode"]));

            // If the response lacks an |Upgrade| header field or the |Upgrade|
            // header field contains a value that is not an ASCII case-
            // insensitive match for the value "websocket", the client MUST
            // _Fail the WebSocket Connection_.
            if (headers["Connection"].ToLowerInvariant() != WebSocketConnectionToken.ToLowerInvariant())
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid connection header item value [{1}].",
                    client.RemoteEndPoint, headers["Connection"]));

            // If the response lacks a |Connection| header field or the
            // |Connection| header field doesn't contain a token that is an
            // ASCII case-insensitive match for the value "Upgrade", the client
            // MUST _Fail the WebSocket Connection_.
            if (headers["Upgrade"].ToLowerInvariant() != WebSocketUpgradeToken.ToLowerInvariant())
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid upgrade header item value [{1}].",
                    client.RemoteEndPoint, headers["Upgrade"]));

            // If the response lacks a |Sec-WebSocket-Accept| header field or
            // the |Sec-WebSocket-Accept| contains a value other than the
            // base64-encoded SHA-1 of the concatenation of the |Sec-WebSocket-
            // Key| (as a string, not base64-decoded) with the string "258EAFA5-
            // E914-47DA-95CA-C5AB0DC85B11" but ignoring any leading and
            // trailing whitespace, the client MUST _Fail the WebSocket Connection_.
            string challenge = GetSecWebSocketAcceptString(secWebSocketKey);
            if (!headers["Sec-WebSocket-Accept"].Equals(challenge, StringComparison.OrdinalIgnoreCase))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid Sec-WebSocket-Accept header item value [{1}].",
                    client.RemoteEndPoint, headers["Sec-WebSocket-Accept"]));

            // If the response includes a |Sec-WebSocket-Protocol| header field
            // and this header field indicates the use of a subprotocol that was
            // not present in the client's handshake (the server has indicated a
            // subprotocol not requested by the client), the client MUST _Fail
            // the WebSocket Connection_.
            if (headers.ContainsKey("Sec-WebSocket-Protocol"))
            {
                string subProtocol = headers["Sec-WebSocket-Protocol"];
                if (!string.IsNullOrWhiteSpace(subProtocol) && !string.IsNullOrWhiteSpace(client.SubProtocol))
                {
                    if (!WebSocketHelpers.ValidateSubprotocol(subProtocol))
                        throw new WebSocketException(string.Format(
                            "Handshake with remote [{0}] failed due to invalid char in sub-protocol [{1}] with requested [{2}].",
                            client.RemoteEndPoint, headers["Sec-WebSocket-Protocol"], client.SubProtocol));

                    var requestedSubProtocols = client.SubProtocol.Split(',').Select(p => p.Trim());

                    bool foundMatch = false;
                    foreach (string requestedSubProtocol in requestedSubProtocols)
                    {
                        if (string.Equals(requestedSubProtocol, subProtocol, StringComparison.OrdinalIgnoreCase))
                        {
                            foundMatch = true;
                            break;
                        }
                    }
                    if (!foundMatch)
                    {
                        throw new WebSocketException(string.Format(
                            "Handshake with remote [{0}] failed due to accept unsupported sub-protocol [{1}] not in requested [{2}].",
                            client.RemoteEndPoint, headers["Sec-WebSocket-Protocol"], client.SubProtocol));
                    }
                }
                else
                {
                    throw new WebSocketException(string.Format(
                        "Handshake with remote [{0}] failed due to mismatched sub-protocol [{1}] with requested [{2}].",
                        client.RemoteEndPoint, headers["Sec-WebSocket-Protocol"], client.SubProtocol));
                }
            }

            // If the response includes a |Sec-WebSocket-Extensions| header
            // field and this header field indicates the use of an extension
            // that was not present in the client's handshake (the server has
            // indicated an extension not requested by the client), the client
            // MUST _Fail the WebSocket Connection_.

            return true;
        }

        private static Dictionary<string, string> ParseOpenningHandshakeResponseHeaders(string response)
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
                else
                {
                    foreach (var item in HttpKnownHeaderNames)
                    {
                        if (line.StartsWith(item + ":"))
                        {
                            var index = line.IndexOf(':');
                            if (index != -1)
                            {
                                var value = line.Substring(index + 1);
                                headers.Add(item, value.Trim());
                            }
                        }
                    }
                }
            }

            return headers;
        }

        private static string GetSecWebSocketAcceptString(string secWebSocketKey)
        {
            string retVal;

            using (SHA1 sha1 = SHA1.Create())
            {
                string acceptString = string.Concat(secWebSocketKey, SecWebSocketKeyGuid);
                byte[] toHash = Encoding.UTF8.GetBytes(acceptString);
                retVal = Convert.ToBase64String(sha1.ComputeHash(toHash));
            }

            return retVal;
        }
    }
}
