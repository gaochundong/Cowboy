using System;

namespace Cowboy.WebSockets
{
    public sealed class TextFrame : DataFrame
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

        public byte[] ToArray(IFrameBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException("builder");
            return builder.EncodeFrame(this);
        }
    }
}
