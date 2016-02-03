using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cowboy.Logging.NLogIntegration
{
    internal static class ExceptionRender
    {
        private const string _spaceLine = "--------------------------------------";
        private static List<string> _filteredProperties =
            new List<string>
            {
                "StackTrace",
                "HResult",
                "InnerException",
                "Data"
            };

        public static string Parse(Exception exception)
        {
            var builder = new StringBuilder();
            Append(builder, exception, false);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, Exception exception, bool isInnerException)
        {
            if (exception == null)
            {
                return;
            }

            builder.AppendLine();

            var type = exception.GetType();
            if (isInnerException)
            {
                builder.Append("Inner ");
            }

            builder.AppendLine("Exception Details:")
                .AppendLine(_spaceLine)
                .Append("Exception Type: ")
                .AppendLine(type.ToString());

            var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
            var properties = type.GetProperties(bindingFlags);
            foreach (var property in properties)
            {
                var propertyName = property.Name;
                var isFiltered = _filteredProperties.Any(filter => String.Equals(propertyName, filter, StringComparison.InvariantCultureIgnoreCase));
                if (isFiltered)
                {
                    continue;
                }

                var propertyValue = property.GetValue(exception, bindingFlags, null, null, null);
                var valueText = propertyValue != null ? propertyValue.ToString() : "NULL";
                builder.Append(propertyName)
                    .Append(": ")
                    .AppendLine(valueText);
            }

            AppendStackTrace(builder, exception.StackTrace, isInnerException);
            Append(builder, exception.InnerException, true);
        }

        private static void AppendStackTrace(StringBuilder builder, string stackTrace, bool isInnerException)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return;
            }

            builder.AppendLine();

            if (isInnerException)
            {
                builder.Append("Inner ");
            }

            builder.AppendLine("Exception StackTrace:")
                .AppendLine(_spaceLine)
                .AppendLine(stackTrace);
        }
    }
}
