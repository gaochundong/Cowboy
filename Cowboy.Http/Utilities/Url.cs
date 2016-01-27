using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Cowboy.Http.Utilities
{
    public sealed class Url : ICloneable
    {
        private string basePath;
        private string query;

        public Url()
        {
            this.Scheme = Uri.UriSchemeHttp;
            this.HostName = string.Empty;
            this.Port = null;
            this.BasePath = string.Empty;
            this.Path = string.Empty;
            this.Query = string.Empty;
        }

        public Url(string url)
        {
            var uri = new Uri(url);
            this.HostName = uri.Host;
            this.Path = uri.LocalPath;
            this.Port = uri.Port;
            this.Query = uri.Query;
            this.Scheme = uri.Scheme;
        }

        public string Scheme { get; set; }

        public string HostName { get; set; }

        public int? Port { get; set; }

        public string BasePath
        {
            get { return this.basePath; }
            set { this.basePath = (value ?? string.Empty).TrimEnd('/'); }
        }

        public string Path { get; set; }

        public string Query
        {
            get { return this.query; }
            set { this.query = GetQuery(value); }
        }

        public string SiteBase
        {
            get
            {
                return new StringBuilder()
                    .Append(this.Scheme)
                    .Append(Uri.SchemeDelimiter)
                    .Append(GetHostName(this.HostName))
                    .Append(GetPort(this.Port))
                    .ToString();
            }
        }

        public bool IsSecure
        {
            get
            {
                return Uri.UriSchemeHttps.Equals(this.Scheme, StringComparison.OrdinalIgnoreCase);
            }
        }

        public override string ToString()
        {
            return new StringBuilder()
                .Append(this.Scheme)
                .Append(Uri.SchemeDelimiter)
                .Append(GetHostName(this.HostName))
                .Append(GetPort(this.Port))
                .Append(GetCorrectPath(this.BasePath))
                .Append(GetCorrectPath(this.Path))
                .Append(this.Query)
                .ToString();
        }

        object ICloneable.Clone()
        {
            return this.Clone();
        }

        public Url Clone()
        {
            return new Url
            {
                BasePath = this.BasePath,
                HostName = this.HostName,
                Port = this.Port,
                Query = this.Query,
                Path = this.Path,
                Scheme = this.Scheme
            };
        }

        public static implicit operator string(Url url)
        {
            return url.ToString();
        }

        public static implicit operator Url(string url)
        {
            return new Uri(url);
        }

        public static implicit operator Uri(Url url)
        {
            return new Uri(url.ToString(), UriKind.Absolute);
        }

        public static implicit operator Url(Uri uri)
        {
            if (uri.IsAbsoluteUri)
            {
                return new Url
                {
                    HostName = uri.Host,
                    Path = uri.LocalPath,
                    Port = uri.Port,
                    Query = uri.Query,
                    Scheme = uri.Scheme
                };
            }

            return new Url { Path = uri.OriginalString };
        }

        private static string GetQuery(string query)
        {
            return string.IsNullOrEmpty(query) ? string.Empty : (query[0] == '?' ? query : '?' + query);
        }

        private static string GetCorrectPath(string path)
        {
            return (string.IsNullOrEmpty(path) || path.Equals("/")) ? string.Empty : path;
        }

        private static string GetPort(int? port)
        {
            return port.HasValue ? string.Concat(":", port.Value) : string.Empty;
        }

        private static string GetHostName(string hostName)
        {
            IPAddress address;

            if (IPAddress.TryParse(hostName, out address))
            {
                var addressString = address.ToString();

                return address.AddressFamily == AddressFamily.InterNetworkV6
                    ? string.Format("[{0}]", addressString)
                    : addressString;
            }

            return hostName;
        }
    }
}
