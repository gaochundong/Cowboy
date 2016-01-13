using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets.WebSockets
{
    internal class WebSocketClientHandshaker
    {
        private static readonly ILog _log = Logger.Get<WebSocketClientHandshaker>();

        // Once a connection to the server has been established (including a
        // connection via a proxy or over a TLS-encrypted tunnel), the client
        // MUST send an opening handshake to the server.  The handshake consists
        // of an HTTP Upgrade request, along with a list of required and
        // optional header fields.  The requirements for this handshake are as follows.
        internal static byte[] CreateOpenningHandshakeRequest(AsyncWebSocketClient client, out string secWebSocketKey)
        {
            var sb = new StringBuilder();

            // The handshake MUST be a valid HTTP request as specified by [RFC2616].
            // The method of the request MUST be GET, and the HTTP version MUST be at least 1.1.
            // For example, if the WebSocket URI is "ws://example.com/chat",
            // the first line sent should be "GET /chat HTTP/1.1".
            sb.AppendFormatWithCrCf("GET {0} HTTP/{1}",
                !string.IsNullOrEmpty(client.Uri.PathAndQuery) ? client.Uri.PathAndQuery : "/",
                Consts.HttpVersion);

            // The request MUST contain a |Host| header field whose value
            // contains /host/ plus optionally ":" followed by /port/ (when not
            // using the default port).
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Host, client.Uri.Host);

            // The request MUST contain an |Upgrade| header field whose value
            // MUST include the "websocket" keyword.
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Upgrade, Consts.WebSocketUpgradeToken);

            // The request MUST contain a |Connection| header field whose value
            // MUST include the "Upgrade" token.
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Connection, Consts.WebSocketConnectionToken);

            // The request MUST include a header field with the name
            // |Sec-WebSocket-Key|.  The value of this header field MUST be a
            // nonce consisting of a randomly selected 16-byte value that has
            // been base64-encoded (see Section 4 of [RFC4648]).  The nonce
            // MUST be selected randomly for each connection.
            secWebSocketKey = Convert.ToBase64String(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString().Substring(0, 16)));
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketKey, secWebSocketKey);

            // The request MUST include a header field with the name |Origin|
            // [RFC6454] if the request is coming from a browser client.  If
            // the connection is from a non-browser client, the request MAY
            // include this header field if the semantics of that client match
            // the use-case described here for browser clients.  The value of
            // this header field is the ASCII serialization of origin of the
            // context in which the code establishing the connection is
            // running.  See [RFC6454] for the details of how this header field
            // value is constructed.
            if (!string.IsNullOrEmpty(client.Origin))
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Origin, client.Origin);

            // The request MUST include a header field with the name
            // |Sec-WebSocket-Version|.  The value of this header field MUST be 13.
            if (!string.IsNullOrEmpty(client.Version))
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketVersion, client.Version);
            else
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketVersion, Consts.WebSocketVersion);

            // The request MAY include a header field with the name
            // |Sec-WebSocket-Protocol|.  If present, this value indicates one
            // or more comma-separated subprotocol the client wishes to speak,
            // ordered by preference.  The elements that comprise this value
            // MUST be non-empty strings with characters in the range U+0021 to
            // U+007E not including separator characters as defined in
            // [RFC2616] and MUST all be unique strings.  The ABNF for the
            // value of this header field is 1#token, where the definitions of
            // constructs and rules are as given in [RFC2616].
            if (!string.IsNullOrEmpty(client.SubProtocol))
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketProtocol, client.SubProtocol);

            // The request MAY include a header field with the name
            // |Sec-WebSocket-Extensions|.  If present, this value indicates
            // the protocol-level extension(s) the client wishes to speak.  The
            // interpretation and format of this header field is described in Section 9.1.
            if (!string.IsNullOrEmpty(client.Extensions))
                sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketExtensions, client.Extensions);

            // The request MAY include any other header fields, for example,
            // cookies [RFC6265] and/or authentication-related header fields
            // such as the |Authorization| header field [RFC2616], which are
            // processed according to documents that define them.
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
            var request = sb.ToString();
#if DEBUG
            _log.DebugFormat("{0}{1}", Environment.NewLine, request);
#endif
            return Encoding.UTF8.GetBytes(request);
        }

        internal static bool VerifyOpenningHandshakeResponse(AsyncWebSocketClient client, byte[] buffer, int offset, int count, string secWebSocketKey)
        {
            BufferValidator.ValidateBuffer(buffer, offset, count, "buffer");
            if (string.IsNullOrEmpty(secWebSocketKey))
                throw new ArgumentNullException("secWebSocketKey");

            var response = Encoding.UTF8.GetString(buffer, offset, count);
#if DEBUG
            _log.DebugFormat("{0}{1}", Environment.NewLine, response);
#endif
            // HTTP/1.1 101 Switching Protocols
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Accept: 1tGBmA9p0DQDgmFll6P0/UcVS/E=
            // Sec-WebSocket-Protocol: chat
            var headers = ParseOpenningHandshakeResponseHeaders(response);

            // If the status code received from the server is not 101, the
            // client handles the response per HTTP [RFC2616] procedures.  In
            // particular, the client might perform authentication if it
            // receives a 401 status code; the server might redirect the client
            // using a 3xx status code (but clients are not required to follow them), etc.
            if (!headers.ContainsKey(Consts.HttpStatusCodeName))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of status code.", client.RemoteEndPoint));
            if (!headers.ContainsKey(Consts.HttpStatusCodeDescription))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of status description.", client.RemoteEndPoint));
            if (headers[Consts.HttpStatusCodeName] == ((int)HttpStatusCode.BadRequest).ToString())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to bad request [{1}].",
                    client.RemoteEndPoint, headers[Consts.HttpStatusCodeName]));
            if (headers[Consts.HttpStatusCodeName] != ((int)HttpStatusCode.SwitchingProtocols).ToString())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to expected 101 Switching Protocols but received [{1}].",
                    client.RemoteEndPoint, headers[Consts.HttpStatusCodeName]));

            // If the response lacks an |Upgrade| header field or the |Upgrade|
            // header field contains a value that is not an ASCII case-
            // insensitive match for the value "websocket", the client MUST
            // _Fail the WebSocket Connection_.
            if (!headers.ContainsKey(HttpKnownHeaderNames.Connection))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of connection header item.", client.RemoteEndPoint));
            if (headers[HttpKnownHeaderNames.Connection].ToLowerInvariant() != Consts.WebSocketConnectionToken.ToLowerInvariant())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid connection header item value [{1}].",
                    client.RemoteEndPoint, headers[HttpKnownHeaderNames.Connection]));

            // If the response lacks a |Connection| header field or the
            // |Connection| header field doesn't contain a token that is an
            // ASCII case-insensitive match for the value "Upgrade", the client
            // MUST _Fail the WebSocket Connection_.
            if (!headers.ContainsKey(HttpKnownHeaderNames.Upgrade))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of upgrade header item.", client.RemoteEndPoint));
            if (headers[HttpKnownHeaderNames.Upgrade].ToLowerInvariant() != Consts.WebSocketUpgradeToken.ToLowerInvariant())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid upgrade header item value [{1}].",
                    client.RemoteEndPoint, headers[HttpKnownHeaderNames.Upgrade]));

            // If the response lacks a |Sec-WebSocket-Accept| header field or
            // the |Sec-WebSocket-Accept| contains a value other than the
            // base64-encoded SHA-1 of the concatenation of the |Sec-WebSocket-
            // Key| (as a string, not base64-decoded) with the string "258EAFA5-
            // E914-47DA-95CA-C5AB0DC85B11" but ignoring any leading and
            // trailing whitespace, the client MUST _Fail the WebSocket Connection_.
            if (!headers.ContainsKey(HttpKnownHeaderNames.SecWebSocketAccept))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of Sec-WebSocket-Accept header item.", client.RemoteEndPoint));
            string challenge = GetSecWebSocketAcceptString(secWebSocketKey);
            if (!headers[HttpKnownHeaderNames.SecWebSocketAccept].Equals(challenge, StringComparison.OrdinalIgnoreCase))
                throw new WebSocketHandshakeException(string.Format(
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
                        throw new WebSocketHandshakeException(string.Format(
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
                        throw new WebSocketHandshakeException(string.Format(
                            "Handshake with remote [{0}] failed due to accept unsupported sub-protocol [{1}] not in requested [{2}].",
                            client.RemoteEndPoint, headers[HttpKnownHeaderNames.SecWebSocketProtocol], client.SubProtocol));
                    }
                }
                else
                {
                    throw new WebSocketHandshakeException(string.Format(
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
