using System.Diagnostics;
using System.Reflection;

namespace Cowboy.CommandLines
{
    public static class VersionHelper
    {
        public static string GetExecutingAssemblyName()
        {
            Process p = Process.GetCurrentProcess();
            return p.ProcessName;
        }

        public static string GetExecutingAssemblyVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            return fvi.ProductVersion;
        }

        public static string GetExecutingAssemblyCompanyName()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            return fvi.CompanyName;
        }

        public static string GetExecutingAssemblyProductName()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            return fvi.ProductName;
        }

        public static string GetExecutingAssemblyCopyright()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            return fvi.LegalCopyright;
        }
    }
}
