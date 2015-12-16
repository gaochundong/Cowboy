using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;

namespace Cowboy.Http
{
    public class RequestHeaders : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
    {
        private readonly IDictionary<string, IEnumerable<string>> headers;
        private readonly ConcurrentDictionary<string, IEnumerable<Tuple<string, decimal>>> cache;

        public RequestHeaders(IDictionary<string, IEnumerable<string>> headers)
        {
            this.headers = new Dictionary<string, IEnumerable<string>>(headers, StringComparer.OrdinalIgnoreCase);
            this.cache = new ConcurrentDictionary<string, IEnumerable<Tuple<string, decimal>>>(StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<Tuple<string, decimal>> Accept
        {
            get { return this.GetWeightedValues("Accept").ToList(); }
            set { this.SetHeaderValues("Accept", value, GetWeightedValuesAsStrings); }
        }

        public IEnumerable<Tuple<string, decimal>> AcceptCharset
        {
            get { return this.GetWeightedValues("Accept-Charset"); }
            set { this.SetHeaderValues("Accept-Charset", value, GetWeightedValuesAsStrings); }
        }

        public IEnumerable<string> AcceptEncoding
        {
            get { return this.GetSplitValues("Accept-Encoding"); }
            set { this.SetHeaderValues("Accept-Encoding", value, x => x); }
        }

        public IEnumerable<Tuple<string, decimal>> AcceptLanguage
        {
            get { return this.GetWeightedValues("Accept-Language"); }
            set { this.SetHeaderValues("Accept-Language", value, GetWeightedValuesAsStrings); }
        }

        public string Authorization
        {
            get { return this.GetValue("Authorization", x => x.First()); }
            set { this.SetHeaderValues("Authorization", value, x => new[] { x }); }
        }

        public IEnumerable<string> CacheControl
        {
            get { return this.GetValue("Cache-Control"); }
            set { this.SetHeaderValues("Cache-Control", value, x => x); }
        }

        public string Connection
        {
            get { return this.GetValue("Connection", x => x.First()); }
            set { this.SetHeaderValues("Connection", value, x => new[] { x }); }
        }

        public long ContentLength
        {
            get { return this.GetValue("Content-Length", x => Convert.ToInt64(x.First())); }
            set { this.SetHeaderValues("Content-Length", value, x => new[] { x.ToString(CultureInfo.InvariantCulture) }); }
        }

        public string ContentType
        {
            get { return this.GetValue("Content-Type", x => x.First()); }
            set { this.SetHeaderValues("Content-Type", value, x => new[] { x }); }
        }

        public DateTime? Date
        {
            get { return this.GetValue("Date", x => ParseDateTime(x.First())); }
            set { this.SetHeaderValues("Date", value, x => new[] { GetDateAsString(value) }); }
        }

        public string Host
        {
            get { return this.GetValue("Host", x => x.First()); }
            set { this.SetHeaderValues("Host", value, x => new[] { x }); }
        }

        public IEnumerable<string> IfMatch
        {
            get { return this.GetValue("If-Match"); }
            set { this.SetHeaderValues("If-Match", value, x => x); }
        }

        public DateTime? IfModifiedSince
        {
            get { return this.GetValue("If-Modified-Since", x => ParseDateTime(x.First())); }
            set { this.SetHeaderValues("If-Modified-Since", value, x => new[] { GetDateAsString(value) }); }
        }

        public IEnumerable<string> IfNoneMatch
        {
            get { return this.GetValue("If-None-Match"); }
            set { this.SetHeaderValues("If-None-Match", value, x => x); }
        }

        public string IfRange
        {
            get { return this.GetValue("If-Range", x => x.First()); }
            set { this.SetHeaderValues("If-Range", value, x => new[] { x }); }
        }

        public DateTime? IfUnmodifiedSince
        {
            get { return this.GetValue("If-Unmodified-Since", x => ParseDateTime(x.First())); }
            set { this.SetHeaderValues("If-Unmodified-Since", value, x => new[] { GetDateAsString(value) }); }

        }

        public IEnumerable<string> Keys
        {
            get { return this.headers.Keys; }
        }

        public int MaxForwards
        {
            get { return this.GetValue("Max-Forwards", x => Convert.ToInt32(x.First())); }
            set { this.SetHeaderValues("Max-Forwards", value, x => new[] { x.ToString(CultureInfo.InvariantCulture) }); }
        }

        public string Referrer
        {
            get { return this.GetValue("Referer", x => x.First()); }
            set { this.SetHeaderValues("Referer", value, x => new[] { x }); }
        }

        public string UserAgent
        {
            get { return this.GetValue("User-Agent", x => x.First()); }
            set { this.SetHeaderValues("User-Agent", value, x => new[] { x }); }
        }

        public IEnumerable<IEnumerable<string>> Values
        {
            get { return this.headers.Values; }
        }

        public IEnumerator<KeyValuePair<string, IEnumerable<string>>> GetEnumerator()
        {
            return this.headers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerable<string> this[string name]
        {
            get
            {
                return (this.headers.ContainsKey(name)) ?
                    this.headers[name] :
                    Enumerable.Empty<string>();
            }
        }

        private static string GetDateAsString(DateTime? value)
        {
            return !value.HasValue ? null : value.Value.ToString("R", CultureInfo.InvariantCulture);
        }

        private IEnumerable<string> GetSplitValues(string header)
        {
            var values = this.GetValue(header);

            return values
                .SelectMany(x => x.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(x => x.Trim())
                .ToList();
        }

        private IEnumerable<Tuple<string, decimal>> GetWeightedValues(string headerName)
        {
            return this.cache.GetOrAdd(headerName, header =>
            {

                var values = this.GetSplitValues(header);

                var parsed = values.Select(x =>
                {
                    var sections = x.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                    var mediaRange = sections[0].Trim();
                    var quality = 1m;

                    for (var index = 1; index < sections.Length; index++)
                    {
                        var trimmedValue = sections[index].Trim();
                        if (trimmedValue.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
                        {
                            decimal temp;
                            var stringValue = trimmedValue.Substring(2);
                            if (decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out temp))
                            {
                                quality = temp;
                            }
                        }
                        else
                        {
                            mediaRange += ";" + trimmedValue;
                        }
                    }

                    return new Tuple<string, decimal>(mediaRange, quality);
                });

                return parsed.OrderByDescending(x => x.Item2).ToArray();
            });
        }

        private static object GetDefaultValue(Type T)
        {
            if (IsGenericEnumerable(T))
            {
                var enumerableType = T.GetGenericArguments().First();
                var x = typeof(List<>).MakeGenericType(new[] { enumerableType });
                return Activator.CreateInstance(x);
            }

            if (T == typeof(DateTime))
            {
                return null;
            }

            return T == typeof(string) ?
                string.Empty :
                null;
        }

        private IEnumerable<string> GetValue(string name)
        {
            return this.GetValue(name, x => x);
        }

        private T GetValue<T>(string name, Func<IEnumerable<string>, T> converter)
        {
            if (!this.headers.ContainsKey(name))
            {
                return (T)(GetDefaultValue(typeof(T)) ?? default(T));
            }

            return converter.Invoke(this.headers[name]);
        }

        private static IEnumerable<string> GetWeightedValuesAsStrings(IEnumerable<Tuple<string, decimal>> values)
        {
            return values.Select(x => string.Concat(x.Item1, ";q=", x.Item2.ToString(CultureInfo.InvariantCulture)));
        }

        private static bool IsGenericEnumerable(Type T)
        {
            return !(T == typeof(string)) && T.IsGenericType && T.GetGenericTypeDefinition() == typeof(IEnumerable<>);
        }

        private static DateTime? ParseDateTime(string value)
        {
            DateTime result;
            // note CultureInfo.InvariantCulture is ignored
            if (DateTime.TryParseExact(value, "R", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return result;
            }
            return null;
        }

        private void SetHeaderValues<T>(string header, T value, Func<T, IEnumerable<string>> valueTransformer)
        {
            this.InvalidateCacheEntry(header);

            if (EqualityComparer<T>.Default.Equals(value, default(T)))
            {
                if (this.headers.ContainsKey(header))
                {
                    this.headers.Remove(header);
                }
            }
            else
            {
                this.headers[header] = valueTransformer.Invoke(value);
            }
        }

        private void InvalidateCacheEntry(string header)
        {
            IEnumerable<Tuple<string, decimal>> values;
            this.cache.TryRemove(header, out values);
        }

        public IEnumerable<Cookie> Cookie
        {
            get { return this.GetValue("Cookie", GetCookies); }
        }

        private static IEnumerable<Cookie> GetCookies(IEnumerable<string> cookies)
        {
            if (cookies == null)
            {
                yield break;
            }

            foreach (var cookie in cookies)
            {
                var cookieStrings = cookie.Split(';');
                foreach (var cookieString in cookieStrings)
                {
                    var equalPos = cookieString.IndexOf('=');
                    if (equalPos >= 0)
                    {
                        yield return new Cookie(cookieString.Substring(0, equalPos).TrimStart(), cookieString.Substring(equalPos + 1).TrimEnd());
                    }
                }
            }
        }
    }
}
