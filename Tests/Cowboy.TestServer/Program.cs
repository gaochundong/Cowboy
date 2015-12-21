using System;
using System.Diagnostics;
using Cowboy.Hosting.Self;
using Cowboy.Logging;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.TestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            NLogLogger.Use();
            var log = Logger.Get("Cowboy.TestServer");

            var bootstrapper = new Bootstrapper();
            bootstrapper.Modules.Add(new TestModule());
            bootstrapper.WebSocketModules.Add(new TestWebSocketModule());

            var engine = bootstrapper.Boot();

            string uriString = "http://localhost:3202/";
            var host = new SelfHost(engine, new Uri(uriString));
            host.Start();
            log.DebugFormat("Server is listening.");

            //AutoNavigateTo(uriString);

            Console.ReadKey();
            log.DebugFormat("Stopped. Goodbye!");
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
