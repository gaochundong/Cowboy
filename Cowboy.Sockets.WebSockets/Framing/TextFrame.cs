using System;
using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    internal sealed class TextFrame : DataFrame
    {
        public TextFrame(string text, bool isMasked = true)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException("text");

            this.Text = text;
            this.IsMasked = isMasked;
        }

        public string Text { get; private set; }
        public bool IsMasked { get; private set; }

        public override OpCode OpCode
        {
            get { return OpCode.Text; }
        }

        public byte[] ToArray()
        {
            var data = Encoding.UTF8.GetBytes(Text);
            return Encode(OpCode, data, 0, data.Length, true, IsMasked);
        }
    }
}
