using System;
using System.Collections.Generic;
using System.Linq;

namespace Cowboy.Responses.Negotiation
{
    public static class BuiltInAcceptHeaderCoercions
    {
        private const string HtmlContentType = "text/html";

        private static readonly IEnumerable<Tuple<string, decimal>> DefaultAccept = new[] { Tuple.Create(HtmlContentType, 1.0m), Tuple.Create("*/*", 0.9m) };

        private static readonly string[] BrokenBrowsers = new[] { "MSIE 8", "MSIE 7", "MSIE 6", "AppleWebKit" };

        public static IEnumerable<Tuple<string, decimal>> CoerceBlankAcceptHeader(IEnumerable<Tuple<string, decimal>> currentAcceptHeaders, Context context)
        {
            var current = currentAcceptHeaders as Tuple<string, decimal>[] ?? currentAcceptHeaders.ToArray();

            return !current.Any() ? DefaultAccept : current;
        }

        public static IEnumerable<Tuple<string, decimal>> CoerceStupidBrowsers(IEnumerable<Tuple<string, decimal>> currentAcceptHeaders, Context context)
        {
            var current = currentAcceptHeaders as Tuple<string, decimal>[] ?? currentAcceptHeaders.ToArray();

            return IsStupidBrowser(current, context) ? DefaultAccept : current;
        }

        public static IEnumerable<Tuple<string, decimal>> BoostHtml(IEnumerable<Tuple<string, decimal>> currentAcceptHeaders, Context context)
        {
            var current = currentAcceptHeaders as Tuple<string, decimal>[] ?? currentAcceptHeaders.ToArray();

            var html = current.FirstOrDefault(h => string.Equals(h.Item1, HtmlContentType, StringComparison.OrdinalIgnoreCase) && h.Item2 < 1.0m);

            if (html == null)
            {
                return current;
            }

            var index = Array.IndexOf(current, html);
            if (index == -1)
            {
                return current;
            }

            current[index] = Tuple.Create(HtmlContentType, html.Item2 + 0.2m);

            return current.OrderByDescending(x => x.Item2).ToArray();
        }

        private static bool IsStupidBrowser(Tuple<string, decimal>[] current, Context context)
        {
            // If there's one or less accept headers then we can't be a stupid
            // browser so just bail out early
            if (current.Length <= 1)
            {
                return false;
            }

            var maxScore = current.First().Item2;

            if (IsPotentiallyBrokenBrowser(context.Request.Headers.UserAgent)
                && !current.Any(h => h.Item2 == maxScore && string.Equals(HtmlContentType, h.Item1, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private static bool IsPotentiallyBrokenBrowser(string userAgent)
        {
            return BrokenBrowsers.Any(userAgent.Contains);
        }
    }
}
