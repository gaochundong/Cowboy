using System;

namespace Cowboy.Sockets
{
    public class FrameBuilder : IFrameBuilder
    {
        public FrameBuilder(IFrameEncoder encoder, IFrameDecoder decoder)
        {
            if (encoder == null)
                throw new ArgumentNullException("encoder");
            if (decoder == null)
                throw new ArgumentNullException("decoder");

            this.Encoder = encoder;
            this.Decoder = decoder;
        }

        public IFrameEncoder Encoder { get; private set; }
        public IFrameDecoder Decoder { get; private set; }
    }
}
