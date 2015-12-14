using System;

namespace Cowboy.Http
{
    public class RootPathProvider
    {
        public string GetRootPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
