using System;
using NLog;
using TLogLevel = Cowboy.Logging.LogLevel;

namespace Cowboy.Logging.NLogIntegration
{
    public class NLogLog : ILog
    {
        readonly NLog.Logger _log;

        public NLogLog(NLog.Logger log, string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            _log = log;
        }

        public bool IsDebugEnabled
        {
            get { return _log.IsDebugEnabled; }
        }

        public bool IsInfoEnabled
        {
            get { return _log.IsInfoEnabled; }
        }

        public bool IsWarnEnabled
        {
            get { return _log.IsWarnEnabled; }
        }

        public bool IsErrorEnabled
        {
            get { return _log.IsErrorEnabled; }
        }

        public bool IsFatalEnabled
        {
            get { return _log.IsFatalEnabled; }
        }

        public void Log(TLogLevel level, object obj)
        {
            _log.Log(GetNLogLevel(level), obj);
        }

        public void Log(TLogLevel level, object obj, Exception exception)
        {
            string message = string.Format("{0}{1}{2}", obj == null ? "" : obj.ToString(), Environment.NewLine, ExceptionRender.Parse(exception));
            _log.Log(GetNLogLevel(level), exception, message);
        }

        public void Log(TLogLevel level, LogOutputProvider messageProvider)
        {
            _log.Log(GetNLogLevel(level), ToGenerator(messageProvider));
        }

        public void LogFormat(TLogLevel level, IFormatProvider formatProvider, string format, params object[] args)
        {
            _log.Log(GetNLogLevel(level), formatProvider, format, args);
        }

        public void LogFormat(TLogLevel level, string format, params object[] args)
        {
            _log.Log(GetNLogLevel(level), format, args);
        }

        public void Debug(object obj)
        {
            _log.Log(NLog.LogLevel.Debug, obj);
        }

        public void Debug(object obj, Exception exception)
        {
            string message = string.Format("{0}{1}{2}", obj == null ? "" : obj.ToString(), Environment.NewLine, ExceptionRender.Parse(exception));
            _log.Log(NLog.LogLevel.Debug, exception, message);
        }

        public void Debug(LogOutputProvider messageProvider)
        {
            _log.Debug(ToGenerator(messageProvider));
        }

        public void Info(object obj)
        {
            _log.Log(NLog.LogLevel.Info, obj);
        }

        public void Info(object obj, Exception exception)
        {
            string message = string.Format("{0}{1}{2}", obj == null ? "" : obj.ToString(), Environment.NewLine, ExceptionRender.Parse(exception));
            _log.Log(NLog.LogLevel.Info, exception, message);
        }

        public void Info(LogOutputProvider messageProvider)
        {
            _log.Info(ToGenerator(messageProvider));
        }

        public void Warn(object obj)
        {
            _log.Log(NLog.LogLevel.Warn, obj);
        }

        public void Warn(object obj, Exception exception)
        {
            string message = string.Format("{0}{1}{2}", obj == null ? "" : obj.ToString(), Environment.NewLine, ExceptionRender.Parse(exception));
            _log.Log(NLog.LogLevel.Warn, exception, message);
        }

        public void Warn(LogOutputProvider messageProvider)
        {
            _log.Warn(ToGenerator(messageProvider));
        }

        public void Error(object obj)
        {
            _log.Log(NLog.LogLevel.Error, obj);
        }

        public void Error(object obj, Exception exception)
        {
            string message = string.Format("{0}{1}{2}", obj == null ? "" : obj.ToString(), Environment.NewLine, ExceptionRender.Parse(exception));
            _log.Log(NLog.LogLevel.Error, exception, message);
        }

        public void Error(LogOutputProvider messageProvider)
        {
            _log.Error(ToGenerator(messageProvider));
        }

        public void Fatal(object obj)
        {
            _log.Log(NLog.LogLevel.Fatal, obj);
        }

        public void Fatal(object obj, Exception exception)
        {
            string message = string.Format("{0}{1}{2}", obj == null ? "" : obj.ToString(), Environment.NewLine, ExceptionRender.Parse(exception));
            _log.Log(NLog.LogLevel.Fatal, exception, message);
        }

        public void Fatal(LogOutputProvider messageProvider)
        {
            _log.Fatal(ToGenerator(messageProvider));
        }

        public void DebugFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Debug, formatProvider, format, args);
        }

        public void DebugFormat(string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Debug, format, args);
        }

        public void InfoFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Info, formatProvider, format, args);
        }

        public void InfoFormat(string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Info, format, args);
        }

        public void WarnFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Warn, formatProvider, format, args);
        }

        public void WarnFormat(string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Warn, format, args);
        }

        public void ErrorFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Error, formatProvider, format, args);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Error, format, args);
        }

        public void FatalFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Fatal, formatProvider, format, args);
        }

        public void FatalFormat(string format, params object[] args)
        {
            _log.Log(NLog.LogLevel.Fatal, format, args);
        }

        NLog.LogLevel GetNLogLevel(TLogLevel level)
        {
            if (level == TLogLevel.Fatal)
                return NLog.LogLevel.Fatal;
            if (level == TLogLevel.Error)
                return NLog.LogLevel.Error;
            if (level == TLogLevel.Warn)
                return NLog.LogLevel.Warn;
            if (level == TLogLevel.Info)
                return NLog.LogLevel.Info;
            if (level == TLogLevel.Debug)
                return NLog.LogLevel.Debug;
            if (level == TLogLevel.All)
                return NLog.LogLevel.Trace;

            return NLog.LogLevel.Off;
        }

        LogMessageGenerator ToGenerator(LogOutputProvider provider)
        {
            return () =>
            {
                object obj = provider();
                return obj == null ? "" : obj.ToString();
            };
        }
    }
}
