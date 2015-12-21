using System.Collections.Concurrent;
using System.Diagnostics;

namespace Cowboy.Logging
{
    public class TraceLogger : ILogger
    {
        private readonly ConcurrentDictionary<string, TraceLog> _logs;
        private readonly ConcurrentDictionary<string, TraceSource> _sources;

        public TraceLogger()
        {
            _logs = new ConcurrentDictionary<string, TraceLog>();
            _sources = new ConcurrentDictionary<string, TraceSource>();
        }

        public ILog Get(string name)
        {
            if (!_logs.ContainsKey(name))
                _logs.AddOrUpdate(name, CreateTraceLog(name), (k, v) => { return v; });

            return _logs[name];
        }

        private TraceLog CreateTraceLog(string name)
        {
            if (!_sources.ContainsKey(name))
                _sources.AddOrUpdate(name, CreateTraceSource(name), (k, v) => { return v; });

            return new TraceLog(_sources[name]);
        }

        private TraceSource CreateTraceSource(string name)
        {
            LogLevel logLevel = LogLevel.None;
            SourceLevels sourceLevel = logLevel.SourceLevel;
            var source = new TraceSource(name, sourceLevel);
            if (IsSourceConfigured(source))
            {
                return source;
            }

            ConfigureTraceSource(source, name, sourceLevel);

            return source;
        }

        private static void ConfigureTraceSource(TraceSource source, string name, SourceLevels sourceLevel)
        {
            var defaultSource = new TraceSource("Default", sourceLevel);
            for (string parentName = ShortenName(name);
                 !string.IsNullOrEmpty(parentName);
                 parentName = ShortenName(parentName))
            {
                var parentSource = new TraceSource(parentName, sourceLevel);
                if (IsSourceConfigured(parentSource))
                {
                    defaultSource = parentSource;
                    break;
                }
            }

            source.Switch = defaultSource.Switch;
            source.Listeners.Clear();
            foreach (TraceListener listener in defaultSource.Listeners)
                source.Listeners.Add(listener);
        }

        private static bool IsSourceConfigured(TraceSource source)
        {
            return source.Listeners.Count != 1
                   || !(source.Listeners[0] is DefaultTraceListener)
                   || source.Listeners[0].Name != "Default";
        }

        private static string ShortenName(string name)
        {
            int length = name.LastIndexOf('.');

            return length != -1
                       ? name.Substring(0, length)
                       : null;
        }
    }
}
