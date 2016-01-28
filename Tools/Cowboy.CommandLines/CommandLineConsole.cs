using System;

namespace Cowboy.CommandLines
{
    public static class CommandLineConsole
    {
        public static void OnCommandLineException(object sender, CommandLineExceptionEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException("e");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e.Exception.Message);
            Console.ResetColor();

            ICommandLine commandLine = sender as ICommandLine;
            if (commandLine != null)
            {
                commandLine.Terminate();
            }

            Environment.Exit(0);
        }

        public static void OnCommandLineUsage(object sender, CommandLineUsageEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException("e");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(e.Usage);
            Console.ResetColor();
        }

        public static void OnCommandLineDataChanged(object sender, CommandLineDataChangedEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException("e");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(e.Data);
            Console.ResetColor();
        }
    }
}
