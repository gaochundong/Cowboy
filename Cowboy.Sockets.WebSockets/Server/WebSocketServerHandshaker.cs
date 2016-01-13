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
    internal class WebSocketServerHandshaker
    {
        private static readonly ILog _log = Logger.Get<WebSocketServerHandshaker>();

        internal static bool HandleOpenningHandshakeRequest(AsyncWebSocketSession session, byte[] buffer, int offset, int count,
            out string secWebSocketKey,
            out string path,
            out string query)
        {
            BufferValidator.ValidateBuffer(buffer, offset, count, "buffer");

            var request = Encoding.UTF8.GetString(buffer, offset, count);
#if DEBUG
            _log.DebugFormat("{0}{1}", Environment.NewLine, request);
#endif
            // GET /chat HTTP/1.1
            // Host: server.example.com
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==
            // Origin: http://example.com
            // Sec-WebSocket-Protocol: chat, superchat
            // Sec-WebSocket-Version: 13
            var headers = ParseOpenningHandshakeRequestHeaders(request);

            // An HTTP/1.1 or higher GET request, including a "Request-URI"
            // [RFC2616] that should be interpreted as a /resource name/
            // defined in Section 3 (or an absolute HTTP/HTTPS URI containing the /resource name/).
            // A |Host| header field containing the server's authority.
            if (!headers.ContainsKey(Consts.HttpGetMethodName))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of get method.", session.RemoteEndPoint));
            if (!headers.ContainsKey(HttpKnownHeaderNames.Host))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of host authority.", session.RemoteEndPoint));
            string uriString = string.Format("ws://{0}{1}", headers[HttpKnownHeaderNames.Host], headers[Consts.HttpGetMethodName]);
            Uri requestUri = null;
            if (!Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out requestUri))
            {
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid requested resource name.", session.RemoteEndPoint));
            }
            path = requestUri.AbsolutePath;
            query = requestUri.Query;

            // A |Connection| header field that includes the token "Upgrade",
            // treated as an ASCII case-insensitive value.
            if (!headers.ContainsKey(HttpKnownHeaderNames.Connection))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of connection header item.", session.RemoteEndPoint));
            if (headers[HttpKnownHeaderNames.Connection].ToLowerInvariant() != Consts.WebSocketConnectionToken.ToLowerInvariant())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid connection header item value [{1}].",
                    session.RemoteEndPoint, headers[HttpKnownHeaderNames.Connection]));

            // An |Upgrade| header field containing the value "websocket",
            // treated as an ASCII case-insensitive value.
            if (!headers.ContainsKey(HttpKnownHeaderNames.Upgrade))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of upgrade header item.", session.RemoteEndPoint));
            if (headers[HttpKnownHeaderNames.Upgrade].ToLowerInvariant() != Consts.WebSocketUpgradeToken.ToLowerInvariant())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid upgrade header item value [{1}].",
                    session.RemoteEndPoint, headers[HttpKnownHeaderNames.Upgrade]));

            // A |Sec-WebSocket-Key| header field with a base64-encoded (see
            // Section 4 of [RFC4648]) value that, when decoded, is 16 bytes in length.
            if (!headers.ContainsKey(HttpKnownHeaderNames.SecWebSocketKey))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of Sec-WebSocket-Key header item.", session.RemoteEndPoint));
            if (string.IsNullOrWhiteSpace(headers[HttpKnownHeaderNames.SecWebSocketKey]))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid Sec-WebSocket-Key header item value [{1}].",
                    session.RemoteEndPoint, headers[HttpKnownHeaderNames.SecWebSocketKey]));
            secWebSocketKey = headers[HttpKnownHeaderNames.SecWebSocketKey];

            // A |Sec-WebSocket-Version| header field, with a value of 13.
            if (!headers.ContainsKey(HttpKnownHeaderNames.SecWebSocketVersion))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of Sec-WebSocket-Version header item.", session.RemoteEndPoint));
            if (headers[HttpKnownHeaderNames.SecWebSocketVersion].ToLowerInvariant() != Consts.WebSocketVersion.ToLowerInvariant())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid Sec-WebSocket-Version header item value [{1}].",
                    session.RemoteEndPoint, headers[HttpKnownHeaderNames.SecWebSocketVersion]));

            // Optionally, a |Sec-WebSocket-Protocol| header field, with a list
            // of values indicating which protocols the client would like to
            // speak, ordered by preference.

            // Optionally, a |Sec-WebSocket-Extensions| header field, with a
            // list of values indicating which extensions the client would like
            // to speak.  The interpretation of this header field is discussed in Section 9.1.

            // Optionally, an |Origin| header field.  This header field is sent
            // by all browser clients.  A connection attempt lacking this
            // header field SHOULD NOT be interpreted as coming from a browser client.

            // Optionally, other header fields, such as those used to send
            // cookies or request authentication to a server.  Unknown header
            // fields are ignored, as per [RFC2616].

            return true;
        }

        internal static byte[] CreateOpenningHandshakeResponse(AsyncWebSocketSession session, string secWebSocketKey)
        {
            var sb = new StringBuilder();

            // A Status-Line with a 101 response code as per RFC 2616
            // [RFC2616].  Such a response could look like "HTTP/1.1 101 Switching Protocols".
            sb.AppendFormatWithCrCf("HTTP/{0} {1} {2}",
                Consts.HttpVersion,
                (int)HttpStatusCode.SwitchingProtocols,
                @"Switching Protocols");

            // An |Upgrade| header field with value "websocket" as per RFC2616 [RFC2616].
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Upgrade, Consts.WebSocketUpgradeToken);

            // A |Connection| header field with value "Upgrade".
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Connection, Consts.WebSocketConnectionToken);

            // A |Sec-WebSocket-Accept| header field.  The value of this
            // header field is constructed by concatenating /key/, defined
            // above in step 4 in Section 4.2.2, with the string "258EAFA5-
            // E914-47DA-95CA-C5AB0DC85B11", taking the SHA-1 hash of this
            // concatenated value to obtain a 20-byte value and base64-
            // encoding (see Section 4 of [RFC4648]) this 20-byte hash.
            var secWebSocketAccept = GetSecWebSocketAcceptString(secWebSocketKey);
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketAccept, secWebSocketAccept);

            /**
                Optionally, a |Sec-WebSocket-Protocol| header field, with a
                value /subprotocol/ as defined in step 4 in Section 4.2.2.

                The |Sec-WebSocket-Protocol| header field MAY appear multiple times
                in an HTTP request (which is logically the same as a single
                |Sec-WebSocket-Protocol| header field that contains all values).
                However, the |Sec-WebSocket-Protocol| header field MUST NOT appear
                more than once in an HTTP response.

                The client can request that the server use a specific subprotocol by
                including the |Sec-WebSocket-Protocol| field in its handshake.  If it
                is specified, the server needs to include the same field and one of
                the selected subprotocol values in its response for the connection to
                be established.

                These subprotocol names should be registered as per Section 11.5.  To
                avoid potential collisions, it is recommended to use names that
                contain the ASCII version of the domain name of the subprotocol's
                originator.  For example, if Example Corporation were to create a
                Chat subprotocol to be implemented by many servers around the Web,
                they could name it "chat.example.com".  If the Example Organization
                called their competing subprotocol "chat.example.org", then the two
                subprotocols could be implemented by servers simultaneously, with the
                server dynamically selecting which subprotocol to use based on the
                value sent by the client.

                Subprotocols can be versioned in backward-incompatible ways by
                changing the subprotocol name, e.g., going from
                "bookings.example.net" to "v2.bookings.example.net".  These
                subprotocols would be considered completely separate by WebSocket
                clients.  Backward-compatible versioning can be implemented by
                reusing the same subprotocol string but carefully designing the
                actual subprotocol to support this kind of extensibility.
                */

            /**
                Optionally, a |Sec-WebSocket-Extensions| header field, with a
                value /extensions/ as defined in step 4 in Section 4.2.2.  If
                multiple extensions are to be used, they can all be listed in
                a single |Sec-WebSocket-Extensions| header field or split
                between multiple instances of the |Sec-WebSocket-Extensions| header field.

                The |Sec-WebSocket-Extensions| header field MAY appear multiple times
                in an HTTP request (which is logically the same as a single
                |Sec-WebSocket-Extensions| header field that contains all values.
                However, the |Sec-WebSocket-Extensions| header field MUST NOT appear
                more than once in an HTTP response.

                WebSocket clients MAY request extensions to this specification, and
                WebSocket servers MAY accept some or all extensions requested by the
                client.  A server MUST NOT respond with any extension not requested
                by the client.  If extension parameters are included in negotiations
                between the client and the server, those parameters MUST be chosen in
                accordance with the specification of the extension to which the
                parameters apply.

                A client requests extensions by including a |Sec-WebSocket-
                Extensions| header field, which follows the normal rules for HTTP
                header fields (see [RFC2616], Section 4.2) and the value of the
                header field is defined by the following ABNF [RFC2616].  Note that
                this section is using ABNF syntax/rules from [RFC2616], including the
                "implied *LWS rule".  If a value is received by either the client or
                the server during negotiation that does not conform to the ABNF
                below, the recipient of such malformed data MUST immediately _Fail
                the WebSocket Connection_.

                Any extension-token used MUST be a registered token (see
                Section 11.4).  The parameters supplied with any given extension MUST
                be defined for that extension.  Note that the client is only offering
                to use any advertised extensions and MUST NOT use them unless the
                server indicates that it wishes to use the extension.

                Note that the order of extensions is significant.  Any interactions
                between multiple extensions MAY be defined in the documents defining
                the extensions.  In the absence of such definitions, the
                interpretation is that the header fields listed by the client in its
                request represent a preference of the header fields it wishes to use,
                with the first options listed being most preferable.  The extensions
                listed by the server in response represent the extensions actually in
                use for the connection.  Should the extensions modify the data and/or
                framing, the order of operations on the data should be assumed to be
                the same as the order in which the extensions are listed in the
                server's response in the opening handshake.

                A server accepts one or more extensions by including a
                |Sec-WebSocket-Extensions| header field containing one or more
                extensions that were requested by the client.  The interpretation of
                any extension parameters, and what constitutes a valid response by a
                server to a requested set of parameters by a client, will be defined
                by each such extension.
            */

            sb.AppendWithCrCf();

            // HTTP/1.1 101 Switching Protocols
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Accept: 1tGBmA9p0DQDgmFll6P0/UcVS/E=
            // Sec-WebSocket-Protocol: chat
            var response = sb.ToString();
#if DEBUG
            _log.DebugFormat("{0}{1}", Environment.NewLine, response);
#endif
            return Encoding.UTF8.GetBytes(response);
        }

        internal static byte[] CreateOpenningHandshakeBadRequestResponse(AsyncWebSocketSession session)
        {
            var sb = new StringBuilder();

            // HTTP/1.1 400 Bad Request
            sb.AppendFormatWithCrCf("HTTP/{0} {1} {2}",
                Consts.HttpVersion,
                (int)HttpStatusCode.BadRequest,
                @"Bad Request");

            // Upgrade: websocket
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Upgrade, Consts.WebSocketUpgradeToken);

            // Connection: Upgrade
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Connection, Consts.WebSocketConnectionToken);

            // Sec-WebSocket-Version: 13
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketVersion, Consts.WebSocketVersion);

            sb.AppendWithCrCf();

            var response = sb.ToString();
#if DEBUG
            _log.DebugFormat("{0}{1}", Environment.NewLine, response);
#endif
            return Encoding.UTF8.GetBytes(response);
        }

        private static Dictionary<string, string> ParseOpenningHandshakeRequestHeaders(string request)
        {
            var headers = new Dictionary<string, string>();

            var lines = request.Split(new char[] { '\r', '\n' }).Where(l => l.Length > 0);
            foreach (var line in lines)
            {
                // GET /chat HTTP/1.1
                if (line.StartsWith(Consts.HttpGetMethodName))
                {
                    var segements = line.Split(' ');
                    if (segements.Length > 1)
                    {
                        headers.Add(Consts.HttpGetMethodName, segements[1]);

                        if (segements.Length > 2)
                        {
                            var versions = segements[2].Split('/');
                            if (versions.Length > 1)
                            {
                                headers.Add(Consts.HttpVersionName, versions[1]);
                            }
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
