using System;
using System.Diagnostics;
using Cowboy.Hosting.Self;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.TestServer
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

            string uriString = "http://localhost:3202/";
            var host = new SelfHost(engine, new Uri(uriString));
            host.Start();
            Console.WriteLine("Server is listening.");

            //AutoNavigateTo(uriString);

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
