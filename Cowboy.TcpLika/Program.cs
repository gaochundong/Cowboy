using Cowboy.CommandLines;

namespace Cowboy.TcpLika
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var command = new TcpLikaCommandLine(args))
            {
                CommandLineBootstrap.Start(command);
            }
        }
    }
}
