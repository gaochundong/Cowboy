using System;
using NLog;

namespace Cowboy.Logging.NLogIntegration
{
    public class NLogLogger : ILogger
    {
        readonly Func<string, NLog.Logger> _logFactory;

        public NLogLogger(LogFactory factory)
        {
            _logFactory = factory.GetLogger;
        }

        public NLogLogger()
        {
            _logFactory = NLog.LogManager.GetLogger;
        }

        public ILog Get(string name)
        {
            return new NLogLog(_logFactory(name), name);
        }

        public static void Use()
        {
            Logger.UseLogger(new NLogLogger());
        }
    }
}
