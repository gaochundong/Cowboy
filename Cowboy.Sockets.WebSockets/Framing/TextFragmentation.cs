using System;
using System.Collections.Generic;
using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    internal sealed class TextFragmentation
    {
        public TextFragmentation(List<string> fragments, bool isMasked = true)
        {
            if (fragments == null)
                throw new ArgumentNullException("fragments");
            this.Fragments = fragments;
            this.IsMasked = isMasked;
        }

        public List<string> Fragments { get; private set; }
        public bool IsMasked { get; private set; }

        public IEnumerable<byte[]> ToArrayList()
        {
            for (int i = 0; i < Fragments.Count; i++)
            {
                var data = Encoding.UTF8.GetBytes(Fragments[i]);
                yield return Frame.Encode(
                    i == 0 ? OpCode.Text : OpCode.Continuation,
                    data,
                    0,
                    data.Length,
                    i + 1 == Fragments.Count,
                    IsMasked);
            }
        }
    }
}
