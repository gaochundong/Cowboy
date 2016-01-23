using System;

namespace Cowboy.StaticContent
{
    public class RootPathProvider
    {
        public string GetRootPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
