using System.Text;

namespace Cowboy.WebSockets
{
    internal static class StringBuilderExtensions
    {
        private static readonly char[] _crcf = new char[] { '\r', '\n' };

        public static void AppendFormatWithCrCf(this StringBuilder builder, string format, object arg)
        {
            builder.AppendFormat(format, arg);
            builder.Append(_crcf);
        }

        public static void AppendFormatWithCrCf(this StringBuilder builder, string format, params object[] args)
        {
            builder.AppendFormat(format, args);
            builder.Append(_crcf);
        }

        public static void AppendWithCrCf(this StringBuilder builder, string text)
        {
            builder.Append(text);
            builder.Append(_crcf);
        }

        public static void AppendWithCrCf(this StringBuilder builder)
        {
            builder.Append(_crcf);
        }
    }
}
