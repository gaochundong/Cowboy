using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Cowboy.CommandLines
{
    public static class WildcardCharacterHelper
    {
        public static bool IsContainsWildcard(string path)
        {
            bool result = false;

            if (!string.IsNullOrEmpty(path))
            {
                if (path.Contains(@"*"))
                {
                    result = true;
                }
                else if (path.Contains(@"?"))
                {
                    result = true;
                }
            }

            return result;
        }

        public static string TranslateWildcardToRegex(string pattern)
        {
            return Regex.Escape(pattern).Replace(@"\*", @".*").Replace(@"\?", @".");
        }

        public static string TranslateWildcardFilePath(string file)
        {
            string path = string.Empty;

            if (!string.IsNullOrEmpty(file))
            {
                var currentDirectory = new DirectoryInfo(Environment.CurrentDirectory);
                path = file.Replace(@"/", @"\\");
                if (path.StartsWith(@"." + Path.DirectorySeparatorChar, StringComparison.CurrentCulture))
                {
                    path = (currentDirectory.FullName
                      + Path.DirectorySeparatorChar
                      + path.TrimStart('.', Path.DirectorySeparatorChar)).Replace(@"\\", @"\");
                }
            }

            return path;
        }

        public static string TranslateWildcardDirectoryPath(string directory)
        {
            string path = string.Empty;

            if (!string.IsNullOrEmpty(directory))
            {
                var currentDirectory = new DirectoryInfo(Environment.CurrentDirectory);
                path = directory.Replace(@"/", @"\\");
                if (path == @".")
                {
                    path = currentDirectory.FullName;
                }
                else if (path.StartsWith(@"." + Path.DirectorySeparatorChar, StringComparison.CurrentCulture))
                {
                    path = (currentDirectory.FullName
                      + Path.DirectorySeparatorChar
                      + path.TrimStart('.', Path.DirectorySeparatorChar)).Replace(@"\\", @"\");
                }
            }

            return path;
        }
    }
}
