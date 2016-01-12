using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cowboy.Buffer;

namespace Cowboy.Sockets.WebSockets
{
    internal class WebSocketServerHandshaker
    {
        internal static bool HandleOpenningHandshakeRequest(AsyncWebSocketSession session, byte[] buffer, int offset, int count, out string secWebSocketKey)
        {
            BufferValidator.ValidateBuffer(buffer, offset, count, "buffer");

            var request = Encoding.UTF8.GetString(buffer, offset, count);

            // GET /chat HTTP/1.1
            // Host: server.example.com
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==
            // Origin: http://example.com
            // Sec-WebSocket-Protocol: chat, superchat
            // Sec-WebSocket-Version: 13
            var headers = ParseOpenningHandshakeRequestHeaders(request);

            secWebSocketKey = headers[HttpKnownHeaderNames.SecWebSocketKey];

            return true;
        }

        internal static byte[] CreateOpenningHandshakeResponse(AsyncWebSocketSession session, string secWebSocketKey)
        {
            var sb = new StringBuilder();

            sb.AppendFormatWithCrCf("HTTP/{0} {1} {2}",
                @"1.1",
                (int)HttpStatusCode.SwitchingProtocols,
                @"Switching Protocols");

            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Upgrade, Consts.WebSocketUpgradeToken);
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Connection, Consts.WebSocketConnectionToken);

            var secWebSocketAccept = GetSecWebSocketAcceptString(secWebSocketKey);
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketAccept, secWebSocketAccept);

            sb.AppendWithCrCf();

            // HTTP/1.1 101 Switching Protocols
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Accept: 1tGBmA9p0DQDgmFll6P0/UcVS/E=
            // Sec-WebSocket-Protocol: chat
            var message = sb.ToString();
            return Encoding.UTF8.GetBytes(message);
        }

        private static Dictionary<string, string> ParseOpenningHandshakeRequestHeaders(string request)
        {
            var headers = new Dictionary<string, string>();

            var lines = request.Split(new char[] { '\r', '\n' }).Where(l => l.Length > 0);
            foreach (var line in lines)
            {
                // GET /chat HTTP/1.1
                if (line.StartsWith(Consts.HttpGetMethod))
                {
                    var segements = line.Split(' ');
                    if (segements.Length > 1)
                    {
                        headers.Add(Consts.HttpGetMethod, segements[1]);

                        if (segements.Length > 2)
                        {
                            var versions = segements[2].Split('/');
                            if (versions.Length > 1)
                            {
                                headers.Add(Consts.HttpVersion, versions[1]);
                            }
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
