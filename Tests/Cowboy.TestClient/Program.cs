using System;

namespace Cowboy.TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            TestWithWebSocketClient.Connect().Wait();
            //TestWithClientWebSocket.Connect().Wait();

            Console.WriteLine("Waiting...");
            Console.ReadKey();
        }
    }
}
