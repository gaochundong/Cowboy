using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Cowboy.Routing
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    public sealed class RouteDescription
    {
        public RouteDescription(string name, string method, string path, Func<Context, bool> condition)
        {
            if (String.IsNullOrEmpty(method))
            {
                throw new ArgumentException("Method must be specified", "method");
            }

            if (String.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path must be specified", "path");
            }

            this.Name = name ?? string.Empty;
            this.Method = method;
            this.Path = path;
            this.Condition = condition;
        }

        public string Name { get; set; }

        public Func<Context, bool> Condition { get; private set; }

        public string Description { get; set; }

        public string Method { get; private set; }

        public string Path { get; private set; }

        public IEnumerable<string> Segments { get; set; }

        private string DebuggerDisplay
        {
            get
            {
                var builder = new StringBuilder();

                if (!string.IsNullOrEmpty(this.Name))
                {
                    builder.AppendFormat("{0} - ", this.Name);
                }

                builder.AppendFormat("{0} {1}", this.Method, this.Path);

                return builder.ToString();
            }
        }
    }
}
