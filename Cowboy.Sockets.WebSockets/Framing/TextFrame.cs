using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    public sealed class TextFrame : DataFrame
    {
        public TextFrame(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException("text");

            this.Text = text;
        }

        public string Text { get; private set; }

        public byte[] ToArray()
        {
            var data = Encoding.UTF8.GetBytes(Text);
            return Encode(OpCode.Text, data, 0, data.Length);
        }
    }
}
