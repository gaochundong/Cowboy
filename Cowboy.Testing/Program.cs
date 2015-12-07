using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cowboy;
using Cowboy.Hosting.Self;
using Cowboy.Routing;

namespace Cowboy.Testing
{
    class Program
    {
        static void Main(string[] args)
        {
            ModuleCatalog._modules.Add(new TestModule());
            var host = new SelfHost(new Uri("http://localhost:8888/greeting/"));
            host.Start();

            var navigateTo = "http://localhost:8888/greeting/";
            Console.WriteLine("Server now listening - navigating to {0}.", navigateTo);
            try
            {
                Process.Start(navigateTo);
            }
            catch (Exception)
            {
            }

            Console.ReadKey();
            Console.WriteLine("Stopped. Goodbye!");
        }
    }
}
