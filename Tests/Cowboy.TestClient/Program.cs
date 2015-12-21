using System;
using Cowboy.Logging;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            NLogLogger.Use();
            var log = Logger.Get("Cowboy.TestServer");

            TestWithWebSocketClient.Connect().Wait();
            //TestWithClientWebSocket.Connect().Wait();

            log.DebugFormat("Waiting...");
            Console.ReadKey();
        }
    }
}
