using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

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

            sb.AppendWithCrCf("Upgrade: WebSocket");
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

            string challenge =
                Convert.ToBase64String(
                    SHA1.Create().ComputeHash(
                        Encoding.ASCII.GetBytes(
                            context.SecWebSocketKey + MagicHandeshakeAcceptedKey)));

            var response = Encoding.UTF8.GetString(context.ResponseBuffer, context.ResponseBufferOffset, context.ResponseBufferCount);

            return true;
        }
    }
}
