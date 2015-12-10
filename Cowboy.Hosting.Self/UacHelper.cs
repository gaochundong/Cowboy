using System.Diagnostics;

namespace Cowboy.Hosting.Self
{
    public static class UacHelper
    {
        public static bool RunElevated(string file, string args)
        {
            var process = CreateProcess(args, file);

            process.Start();
            process.WaitForExit();

            return process.ExitCode == 0;
        }

        private static Process CreateProcess(string args, string file)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    Verb = "runas",
                    Arguments = args,
                    FileName = file,
                }
            };
        }
    }
}
