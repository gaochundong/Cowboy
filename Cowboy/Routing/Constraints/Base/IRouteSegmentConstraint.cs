using Cowboy.Routing.Trie;

namespace Cowboy.Routing.Constraints
{
    public interface IRouteSegmentConstraint
    {
        bool Matches(string constraint);

        SegmentMatch GetMatch(string constraint, string segment, string parameterName);
    }
}