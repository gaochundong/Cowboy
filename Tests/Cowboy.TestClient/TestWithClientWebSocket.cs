using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.TestClient
{
    public class TestWithClientWebSocket
    {
        private static object _consoleLock = new object();

        public static async Task Connect()
        {
            string uri = "ws://localhost:3202/test";

            ClientWebSocket webSocket = null;

            try
            {
                webSocket = new ClientWebSocket();
                await webSocket.ConnectAsync(new Uri(uri), CancellationToken.None);
                Console.WriteLine("Connected to {0}", uri);
                Console.WriteLine("Type something...");
                await Task.WhenAll(Receive(webSocket), Send(webSocket));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex);
            }
            finally
            {
                if (webSocket != null)
                    webSocket.Dispose();
                Console.WriteLine();

                lock (_consoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("WebSocket closed.");
                    Console.ResetColor();
                }
            }
        }

        private static async Task Send(ClientWebSocket webSocket)
        {
            while (webSocket.State == WebSocketState.Open)
            {
                string text = Console.ReadLine();
                var buffer = Encoding.UTF8.GetBytes(text);

                await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, CancellationToken.None);
                Log(false, buffer, buffer.Length);

                await Task.Delay(TimeSpan.FromMilliseconds(1000));
            }
        }

        private static async Task Receive(ClientWebSocket webSocket)
        {
            byte[] buffer = new byte[64];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else
                {
                    Log(true, buffer, result.Count);
                }
            }
        }

        private static void Log(bool receiving, byte[] buffer, int length)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = receiving ? ConsoleColor.Green : ConsoleColor.Gray;
                Console.WriteLine("{0} {1} bytes... ", receiving ? "Received" : "Sent", length);

                Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, length));

                Console.ResetColor();
            }
        }
    }
}
