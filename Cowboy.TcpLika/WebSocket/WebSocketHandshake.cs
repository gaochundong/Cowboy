using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Cowboy.TcpLika
{
    internal class WebSocketHandshake
    {
        public const string MagicHandeshakeAcceptedKey = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

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

            sb.AppendFormatWithCrCf("Sec-WebSocket-Key: {0}", key);

            if (!string.IsNullOrEmpty(protocol))
                sb.AppendFormatWithCrCf("Sec-WebSocket-Protocol: {0}", protocol);

            if (!string.IsNullOrEmpty(version))
                sb.AppendFormatWithCrCf("Sec-WebSocket-Version: {0}", version);
            else
                sb.AppendFormatWithCrCf("Sec-WebSocket-Version: {0}", 13);

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
            var headers = ParseWebSocketResponseHeaders(response);

            if (!headers.ContainsKey("Sec-WebSocket-Accept"))
                return false;

            string challenge =
                Convert.ToBase64String(
                    SHA1.Create().ComputeHash(
                        Encoding.ASCII.GetBytes(
                            context.SecWebSocketKey + MagicHandeshakeAcceptedKey)));

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
                    var segements = line.Split(':');
                    if (segements.Length > 1)
                    {
                        headers.Add("Upgrade", segements[1].Trim());
                    }
                }
                else if (line.StartsWith(@"Connection:"))
                {
                    var segements = line.Split(':');
                    if (segements.Length > 1)
                    {
                        headers.Add("Connection", segements[1].Trim());
                    }
                }
                else if (line.StartsWith(@"Sec-WebSocket-Accept:"))
                {
                    var segements = line.Split(':');
                    if (segements.Length > 1)
                    {
                        headers.Add("Sec-WebSocket-Accept", segements[1].Trim());
                    }
                }
                else if (line.StartsWith(@"Sec-WebSocket-Protocol:"))
                {
                    var segements = line.Split(':');
                    if (segements.Length > 1)
                    {
                        headers.Add("Sec-WebSocket-Protocol", segements[1].Trim());
                    }
                }
            }

            return headers;
        }
    }
}
