using System.Linq;
using Cowboy.Routing.Trie.Nodes;

namespace Cowboy.Routing.Trie
{
    public class TrieNodeFactory : ITrieNodeFactory
    {
        public TrieNodeFactory()
        {
        }

        public virtual TrieNode GetNodeForSegment(TrieNode parent, string segment)
        {
            if (parent == null)
            {
                return new RootNode(this);
            }

            var chars = segment.ToCharArray();
            var start = chars[0];
            var end = chars[chars.Length - 1];

            if (start == '(' && end == ')')
            {
                return new RegExNode(parent, segment, this);
            }

            if (start == '{' && end == '}' && chars.Count(c => c == '{' || c == '}') == 2)
            {
                return this.GetCaptureNode(parent, segment);
            }

            if (segment.StartsWith("^(") && (segment.EndsWith(")") || segment.EndsWith(")$")))
            {
                return new GreedyRegExCaptureNode(parent, segment, this);
            }

            return new LiteralNode(parent, segment, this);
        }

        private TrieNode GetCaptureNode(TrieNode parent, string segment)
        {
            if (segment.EndsWith("?}"))
            {
                return new OptionalCaptureNode(parent, segment, this);
            }

            if (segment.EndsWith("*}"))
            {
                return new GreedyCaptureNode(parent, segment, this);
            }

            if (segment.Contains("?"))
            {
                return new CaptureNodeWithDefaultValue(parent, segment, this);
            }

            return new CaptureNode(parent, segment, this);
        }
    }
}
