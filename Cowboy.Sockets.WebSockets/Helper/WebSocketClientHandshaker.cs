using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cowboy.Buffer;

namespace Cowboy.Sockets.WebSockets
{
    internal class WebSocketClientHandshaker
    {
        internal static byte[] CreateOpenningHandshakeRequest(AsyncWebSocketClient client, out string secWebSocketKey)
        {
            var sb = new StringBuilder();

            sb.AppendFormatWithCrCf("GET {0} HTTP/{1}",
                !string.IsNullOrEmpty(client.Uri.AbsolutePath) ? client.Uri.AbsolutePath : "/",
                @"1.1");
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Host, client.Uri.Host);

            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Upgrade, Consts.WebSocketUpgradeToken);
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Connection, Consts.WebSocketConnectionToken);

            // In addition to Upgrade headers, the client sends a Sec-WebSocket-Key header 
            // containing base64-encoded random bytes, and the server replies with a hash of the key 
            // in the Sec-WebSocket-Accept header. This is intended to prevent a caching proxy 
            // from re-sending a previous WebSocket conversation, and does not provide any authentication, 
            // privacy or integrity. The hashing function appends the 
            // fixed string 258EAFA5-E914-47DA-95CA-C5AB0DC85B11 (a GUID) to the value 
            // from Sec-WebSocket-Key header (which is not decoded from base64), 
            // applies the SHA-1 hashing function, and encodes the result using base64.
            secWebSocketKey = Convert.ToBase64String(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString().Substring(0, 16)));
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketKey, secWebSocketKey);

            // The |Sec-WebSocket-Version| header field in the client's
            // handshake includes the version of the WebSocket Protocol with
            // which the client is attempting to communicate.  If this
            // version does not match a version understood by the server, the
            // server MUST abort the WebSocket handshake described in this
            // section and instead send an appropriate HTTP error code(such
            // as 426 Upgrade Required) and a |Sec-WebSocket-Version| header
            // field indicating the version(s)the server is capable of understanding.
            if (!string.IsNullOrEmpty(client.Version))
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketVersion, client.Version);
            else
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketVersion, 13);

            // Optionally
            // The |Sec-WebSocket-Protocol| request-header field can be
            // used to indicate what subprotocols(application - level protocols
            // layered over the WebSocket Protocol) are acceptable to the client.
            if (!string.IsNullOrEmpty(client.SubProtocol))
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketProtocol, client.SubProtocol);

            // Optionally
            // A (possibly empty) list representing the protocol-level
            // extensions the server is ready to use.
            if (!string.IsNullOrEmpty(client.Extensions))
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketExtensions, client.Extensions);

            // Optionally
            // The |Origin| header field is used to protect against
            // unauthorized cross-origin use of a WebSocket server by scripts using         
            // the WebSocket API in a web browser.
            // This header field is sent by browser clients; for non-browser clients, 
            // this header field may be sent if it makes sense in the context of those clients.
            if (!string.IsNullOrEmpty(client.Origin))
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Origin, client.Origin);

            if (client.Cookies != null && client.Cookies.Any())
            {
                string[] pairs = new string[client.Cookies.Count()];

                for (int i = 0; i < client.Cookies.Count(); i++)
                {
                    var item = client.Cookies.ElementAt(i);
                    pairs[i] = item.Key + "=" + Uri.EscapeUriString(item.Value);
                }

                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Cookie, string.Join(";", pairs));
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
            BufferValidator.ValidateBuffer(buffer, offset, count, "buffer");
            if (string.IsNullOrEmpty(secWebSocketKey))
                throw new ArgumentNullException("secWebSocketKey");

            var response = Encoding.UTF8.GetString(buffer, offset, count);

            // HTTP/1.1 101 Switching Protocols
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Accept: 1tGBmA9p0DQDgmFll6P0/UcVS/E=
            // Sec-WebSocket-Protocol: chat
            var headers = ParseOpenningHandshakeResponseHeaders(response);

            if (!headers.ContainsKey(Consts.HttpStatusCodeName))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of status code.", client.RemoteEndPoint));
            if (!headers.ContainsKey(Consts.HttpStatusCodeDescription))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of status description.", client.RemoteEndPoint));
            if (!headers.ContainsKey(HttpKnownHeaderNames.Connection))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of connection header item.", client.RemoteEndPoint));
            if (!headers.ContainsKey(HttpKnownHeaderNames.Upgrade))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of upgrade header item.", client.RemoteEndPoint));
            if (!headers.ContainsKey(HttpKnownHeaderNames.SecWebSocketAccept))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of Sec-WebSocket-Accept header item.", client.RemoteEndPoint));

            // If the status code received from the server is not 101, the
            // client handles the response per HTTP [RFC2616] procedures.  In
            // particular, the client might perform authentication if it
            // receives a 401 status code; the server might redirect the client
            // using a 3xx status code (but clients are not required to follow
            // them), etc.
            if (headers[Consts.HttpStatusCodeName] == ((int)HttpStatusCode.BadRequest).ToString())
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to bad request [{1}].",
                    client.RemoteEndPoint, headers[Consts.HttpStatusCodeName]));
            if (headers[Consts.HttpStatusCodeName] != ((int)HttpStatusCode.SwitchingProtocols).ToString())
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to expected 101 Switching Protocols but received [{1}].",
                    client.RemoteEndPoint, headers[Consts.HttpStatusCodeName]));

            // If the response lacks an |Upgrade| header field or the |Upgrade|
            // header field contains a value that is not an ASCII case-
            // insensitive match for the value "websocket", the client MUST
            // _Fail the WebSocket Connection_.
            if (headers[HttpKnownHeaderNames.Connection].ToLowerInvariant() != Consts.WebSocketConnectionToken.ToLowerInvariant())
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid connection header item value [{1}].",
                    client.RemoteEndPoint, headers[HttpKnownHeaderNames.Connection]));

            // If the response lacks a |Connection| header field or the
            // |Connection| header field doesn't contain a token that is an
            // ASCII case-insensitive match for the value "Upgrade", the client
            // MUST _Fail the WebSocket Connection_.
            if (headers[HttpKnownHeaderNames.Upgrade].ToLowerInvariant() != Consts.WebSocketUpgradeToken.ToLowerInvariant())
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid upgrade header item value [{1}].",
                    client.RemoteEndPoint, headers[HttpKnownHeaderNames.Upgrade]));

            // If the response lacks a |Sec-WebSocket-Accept| header field or
            // the |Sec-WebSocket-Accept| contains a value other than the
            // base64-encoded SHA-1 of the concatenation of the |Sec-WebSocket-
            // Key| (as a string, not base64-decoded) with the string "258EAFA5-
            // E914-47DA-95CA-C5AB0DC85B11" but ignoring any leading and
            // trailing whitespace, the client MUST _Fail the WebSocket Connection_.
            string challenge = GetSecWebSocketAcceptString(secWebSocketKey);
            if (!headers[HttpKnownHeaderNames.SecWebSocketAccept].Equals(challenge, StringComparison.OrdinalIgnoreCase))
                throw new WebSocketException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid Sec-WebSocket-Accept header item value [{1}].",
                    client.RemoteEndPoint, headers[HttpKnownHeaderNames.SecWebSocketAccept]));

            // If the response includes a |Sec-WebSocket-Protocol| header field
            // and this header field indicates the use of a subprotocol that was
            // not present in the client's handshake (the server has indicated a
            // subprotocol not requested by the client), the client MUST _Fail
            // the WebSocket Connection_.
            if (headers.ContainsKey(HttpKnownHeaderNames.SecWebSocketProtocol))
            {
                string subProtocol = headers[HttpKnownHeaderNames.SecWebSocketProtocol];
                if (!string.IsNullOrWhiteSpace(subProtocol) && !string.IsNullOrWhiteSpace(client.SubProtocol))
                {
                    if (!WebSocketHelpers.ValidateSubprotocol(subProtocol))
                        throw new WebSocketException(string.Format(
                            "Handshake with remote [{0}] failed due to invalid char in sub-protocol [{1}] with requested [{2}].",
                            client.RemoteEndPoint, headers[HttpKnownHeaderNames.SecWebSocketProtocol], client.SubProtocol));

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
                            client.RemoteEndPoint, headers[HttpKnownHeaderNames.SecWebSocketProtocol], client.SubProtocol));
                    }
                }
                else
                {
                    throw new WebSocketException(string.Format(
                        "Handshake with remote [{0}] failed due to mismatched sub-protocol [{1}] with requested [{2}].",
                        client.RemoteEndPoint, headers[HttpKnownHeaderNames.SecWebSocketProtocol], client.SubProtocol));
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
                    foreach (var item in HttpKnownHeaderNames.All)
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
                string acceptString = string.Concat(secWebSocketKey, Consts.SecWebSocketKeyGuid);
                byte[] toHash = Encoding.UTF8.GetBytes(acceptString);
                retVal = Convert.ToBase64String(sha1.ComputeHash(toHash));
            }

            return retVal;
        }
    }
}
