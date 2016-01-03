using System;

namespace Cowboy.CommandLines
{
    public static class CommandLineBootstrap
    {
        public static void Start(CommandLine command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            try
            {
                command.CommandLineException += new EventHandler<CommandLineExceptionEventArgs>(CommandLineConsole.OnCommandLineException);
                command.CommandLineUsage += new EventHandler<CommandLineUsageEventArgs>(CommandLineConsole.OnCommandLineUsage);
                command.CommandLineDataChanged += new EventHandler<CommandLineDataChangedEventArgs>(CommandLineConsole.OnCommandLineDataChanged);
                command.Execute();

                while (command.IsExecuting) ;
            }
            catch (CommandLineException ex)
            {
                CommandLineConsole.OnCommandLineException(null, new CommandLineExceptionEventArgs(ex));
            }
        }
    }
}
