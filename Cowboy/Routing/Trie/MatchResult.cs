using System;
using System.Collections.Generic;

namespace Cowboy.Routing.Trie
{
    public class MatchResult : NodeData, IComparable<MatchResult>
    {
        private static readonly MatchResult noMatch = new MatchResult();
        private static readonly MatchResult[] noMatches = new MatchResult[] { };

        public IDictionary<string, object> Parameters { get; set; }

        public static MatchResult NoMatch
        {
            get
            {
                return noMatch;
            }
        }

        public static MatchResult[] NoMatches
        {
            get
            {
                return noMatches;
            }
        }

        public MatchResult(IDictionary<string, object> parameters)
        {
            this.Parameters = parameters;
        }

        public MatchResult()
            : this(new Dictionary<string, object>())
        {
        }

        public int CompareTo(MatchResult other)
        {
            // Length of the route takes precedence over score
            if (this.RouteLength < other.RouteLength)
            {
                return -1;
            }

            if (this.RouteLength > other.RouteLength)
            {
                return 1;
            }

            if (this.Score < other.Score)
            {
                return -1;
            }

            if (this.Score > other.Score)
            {
                return 1;
            }

            if (string.Equals(this.ModuleType, other.ModuleType))
            {
                if (this.RouteIndex < other.RouteIndex)
                {
                    return -1;
                }

                if (this.RouteIndex > other.RouteIndex)
                {
                    return 1;
                }
            }

            return 0;
        }
    }
}