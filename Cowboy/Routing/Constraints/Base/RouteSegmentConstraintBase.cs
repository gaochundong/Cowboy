using System;
using Cowboy.Routing.Trie;

namespace Cowboy.Routing.Constraints
{
    public abstract class RouteSegmentConstraintBase<T> : IRouteSegmentConstraint
    {
        public abstract string Name { get; }

        public virtual bool Matches(string constraint)
        {
            return constraint.Equals(Name, StringComparison.OrdinalIgnoreCase);
        }

        public SegmentMatch GetMatch(string constraint, string segment, string parameterName)
        {
            T value;
            if (this.TryMatch(constraint, segment, out value))
            {
                return CreateMatch(parameterName, value);
            }

            return SegmentMatch.NoMatch;
        }

        protected abstract bool TryMatch(string constraint, string segment, out T matchedValue);

        private static SegmentMatch CreateMatch(string parameterName, object matchedValue)
        {
            var match = new SegmentMatch(true);
            match.CapturedParameters.Add(parameterName, matchedValue);
            return match;
        }
    }
}