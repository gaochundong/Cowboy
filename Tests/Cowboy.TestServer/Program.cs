using System;
using System.Diagnostics;
using Cowboy.Hosting.Self;

namespace Cowboy.TestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var bootstrapper = new Bootstrapper();
            bootstrapper.Modules.Add(new TestModule());
            var engine = bootstrapper.Boot();

            string uriString = "http://localhost:8080/";
            var host = new SelfHost(engine, new Uri(uriString));
            host.Start();
            Console.WriteLine("Server is listening.");

            //AutoNavigateTo(uriString);

            Console.ReadKey();
            Console.WriteLine("Stopped. Goodbye!");
        }

        private static void AutoNavigateTo(string uri)
        {
            Console.WriteLine("Navigating to {0}.", uri);
            try
            {
                Process.Start(uri);
            }
            catch (Exception) { }
        }
    }
}
