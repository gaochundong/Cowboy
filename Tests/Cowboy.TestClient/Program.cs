using System;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            NLogLogger.Use();

            TestWithClientWebSocket.Connect().Wait();

            Console.WriteLine("Waiting...");
            Console.ReadKey();
        }
    }
}
