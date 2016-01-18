using System;
using System.Collections.Generic;

namespace Cowboy.WebSockets
{
    public sealed class BinaryFragmentation
    {
        public BinaryFragmentation(List<ArraySegment<byte>> fragments, bool isMasked = true)
        {
            if (fragments == null)
                throw new ArgumentNullException("fragments");
            this.Fragments = fragments;
            this.IsMasked = isMasked;
        }

        public List<ArraySegment<byte>> Fragments { get; private set; }
        public bool IsMasked { get; private set; }

        public IEnumerable<byte[]> ToArrayList()
        {
            for (int i = 0; i < Fragments.Count; i++)
            {
                yield return Frame.Encode(
                    i == 0 ? OpCode.Binary : OpCode.Continuation,
                    Fragments[i].Array,
                    Fragments[i].Offset,
                    Fragments[i].Count,
                    i + 1 == Fragments.Count,
                    IsMasked);
            }
        }
    }
}
