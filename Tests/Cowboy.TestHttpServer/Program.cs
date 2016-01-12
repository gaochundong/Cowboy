using System;
using System.Diagnostics;
using Cowboy.Hosting.Self;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.TestHttpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            NLogLogger.Use();

            var bootstrapper = new Bootstrapper();
            bootstrapper.Modules.Add(new TestModule());
            bootstrapper.WebSocketModules.Add(new TestWebSocketModule());

            var engine = bootstrapper.Boot();

            string uri = "http://localhost:3202/";
            var host = new SelfHost(engine, new Uri(uri));
            host.Start();
            Console.WriteLine("Server is listening on [{0}].", uri);

            //AutoNavigateTo(uri);

            Console.ReadKey();
            Console.WriteLine("Stopped. Goodbye!");
        }

        private static void AutoNavigateTo(string uri)
        {
            try
            {
                Process.Start(uri);
            }
            catch (Exception) { }
        }
    }
}
