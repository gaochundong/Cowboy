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

            var host = new SelfHost(engine, new Uri("http://localhost:8080/"));
            host.Start();

            var navigateTo = "http://localhost:8080/";
            Console.WriteLine("Server is listening - navigating to {0}.", navigateTo);
            try
            {
                Process.Start(navigateTo);
            }
            catch (Exception) { }

            Console.ReadKey();
            Console.WriteLine("Stopped. Goodbye!");
        }
    }
}
