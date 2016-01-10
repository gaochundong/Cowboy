using System;
using System.Collections.Generic;
using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    public sealed class TextFragmentation
    {
        public TextFragmentation(List<string> fragments)
        {
            if (fragments == null)
                throw new ArgumentNullException("fragments");
            this.Fragments = fragments;
        }

        public List<string> Fragments { get; private set; }

        public IEnumerable<byte[]> ToArrayList()
        {
            for (int i = 0; i < Fragments.Count; i++)
            {
                var data = Encoding.UTF8.GetBytes(Fragments[i]);
                yield return Frame.Encode(
                    i == 0 ? FrameOpCode.Text : FrameOpCode.Continuation,
                    data,
                    0,
                    data.Length);
            }
        }
    }
}
