using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Cowboy.CommandLines
{
    public abstract class CommandLine : ICommandLine
    {
        #region Ctors

        protected CommandLine(string[] args)
        {
            this.Arguments = new ReadOnlyCollection<string>(args);
        }

        #endregion

        #region Properties

        public ReadOnlyCollection<string> Arguments { get; private set; }

        #endregion

        #region ICommandLine Members

        public virtual void Execute()
        {
            IsExecuting = true;
        }

        public virtual void Terminate()
        {
            IsExecuting = false;
        }

        public bool IsExecuting { get; protected set; }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        protected virtual void Dispose(bool disposing)
        {
        }

        #endregion

        #region Events

        public event EventHandler<CommandLineUsageEventArgs> CommandLineUsage;

        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate")]
        protected virtual void RaiseCommandLineUsage(object sender, string usage)
        {
            EventHandler<CommandLineUsageEventArgs> handler = CommandLineUsage;
            if (handler != null)
            {
                handler(sender, new CommandLineUsageEventArgs(usage));
            }
        }

        public event EventHandler<CommandLineDataChangedEventArgs> CommandLineDataChanged;

        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate")]
        protected virtual void RaiseCommandLineDataChanged(object sender, string data)
        {
            EventHandler<CommandLineDataChangedEventArgs> handler = CommandLineDataChanged;
            if (handler != null)
            {
                handler(sender, new CommandLineDataChangedEventArgs(data));
            }
        }

        public event EventHandler<CommandLineExceptionEventArgs> CommandLineException;

        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate")]
        protected virtual void RaiseCommandLineException(object sender, CommandLineException exception)
        {
            EventHandler<CommandLineExceptionEventArgs> handler = CommandLineException;
            if (handler != null)
            {
                handler(sender, new CommandLineExceptionEventArgs(exception));
            }
        }

        #endregion

        #region Output

        protected virtual void OutputText(string text)
        {
            RaiseCommandLineDataChanged(this, string.Format(CultureInfo.CurrentCulture,
              "{0}{1}", text, Environment.NewLine));
        }

        protected void OutputFormatText(string format, params object[] args)
        {
            RaiseCommandLineDataChanged(this, string.Format(CultureInfo.CurrentCulture, format, args));
        }

        #endregion

        #region Version

        public virtual string Version
        {
            get
            {
                var sb = new StringBuilder();

                sb.AppendLine();

                sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "{0} {1}",
                  VersionHelper.GetExecutingAssemblyName(),
                  VersionHelper.GetExecutingAssemblyVersion()));

                sb.AppendLine();

                if (!string.IsNullOrEmpty(VersionHelper.GetExecutingAssemblyCompanyName()))
                    sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "Company  : {0}",
                      VersionHelper.GetExecutingAssemblyCompanyName()));

                if (!string.IsNullOrEmpty(VersionHelper.GetExecutingAssemblyProductName()))
                    sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "Product  : {0}",
                        VersionHelper.GetExecutingAssemblyProductName()));

                if (!string.IsNullOrEmpty(VersionHelper.GetExecutingAssemblyCopyright()))
                    sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "Copyright: {0}",
                        VersionHelper.GetExecutingAssemblyCopyright()));

                return sb.ToString();
            }
        }

        #endregion
    }
}
