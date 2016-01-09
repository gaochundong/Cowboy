using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    public sealed class BinaryFragmentation
    {
        public BinaryFragmentation(List<ArraySegment<byte>> fragments)
        {
            if (fragments == null)
                throw new ArgumentNullException("fragments");
            this.Fragments = fragments;
        }

        public List<ArraySegment<byte>> Fragments { get; private set; }

        public IEnumerable<byte[]> ToArrayList()
        {
            for (int i = 0; i < Fragments.Count; i++)
            {
                yield return Frame.Encode(
                    i == 0 ? FrameOpCode.Binary : FrameOpCode.Continuation,
                    Fragments[i].Array,
                    Fragments[i].Offset,
                    Fragments[i].Count);
            }
        }
    }
}
