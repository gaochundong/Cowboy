using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cowboy.WebSockets
{
    public interface IExtensibleObject<T>
        where T : IExtensibleObject<T>
    {
        IExtensionCollection<T> Extensions { get; }
    }

    public interface IExtensionCollection<T> : ICollection<IExtension<T>>
        where T : IExtensibleObject<T>
    {
        E Find<E>();
        Collection<E> FindAll<E>();
    }

    public interface IExtension<T>
        where T : IExtensibleObject<T>
    {
        void Attach(T owner);
        void Detach(T owner);

        void EncodeHeader();
        void EncodePayload();

        void DecodeHeader();
        void DecodePayload();
    }

    public class Server : IExtensibleObject<Server>
    {
        public Server()
        {
            _extensions = new ExtensionCollection<Server>(this);
        }

        private ExtensionCollection<Server> _extensions;
        public IExtensionCollection<Server> Extensions
        {
            get
            {
                return _extensions;
            }
        }
    }

    public class Extension : IExtension<Server>
    {
        public Extension()
        {

        }

        public void Attach(Server owner)
        {
            throw new NotImplementedException();
        }

        public void DecodeHeader()
        {
            throw new NotImplementedException();
        }

        public void DecodePayload()
        {
            throw new NotImplementedException();
        }

        public void Detach(Server owner)
        {
            throw new NotImplementedException();
        }

        public void EncodeHeader()
        {
            throw new NotImplementedException();
        }

        public void EncodePayload()
        {
            throw new NotImplementedException();
        }
    }

    public class TTT
    {
        public void T()
        {
            var server = new Server();
            var extension = new Extension();
            server.Extensions.Add(extension);

            foreach (var e in server.Extensions)
            {
                //e.DecodePayload
            }
        }
    }


    






//    public class Frame
//    {

//        public boolean isFin() { .. }
//        public boolean isRsv1() { .. }
//        public boolean isRsv2() { .. }
//        public boolean isRsv3() { .. }
//        public boolean isMask() { .. }
//        public byte getOpcode() { .. }
//        public long getPayloadLength() { .. }
//        public int getMaskingKey() { .. }
//        public byte[] getPayloadData() { .. }
//        public boolean isControlFrame() { .. }

//        public static Builder builder() { .. }
//        public static Builder builder(Frame frame) { .. }

//        public final static class Builder
//        {

//            public Builder() { .. }
//            public Builder(Frame frame) { .. }
//            public Frame build() { .. }
//            public Builder fin(boolean fin) { .. }
//            public Builder rsv1(boolean rsv1) { .. }
//            public Builder rsv2(boolean rsv2) { .. }
//            public Builder rsv3(boolean rsv3) { .. }
//            public Builder mask(boolean mask) { .. }
//            public Builder opcode(byte opcode) { .. }
//            public Builder payloadLength(long payloadLength) { .. }
//            public Builder maskingKey(int maskingKey) { .. }
//            public Builder payloadData(byte[] payloadData) { .. }
//        }
//    }




//    public interface ExtendedExtension extends Extension
//    {

//        Frame processIncoming(ExtensionContext context, Frame frame);
//        Frame processOutgoing(ExtensionContext context, Frame frame);

//        List onExtensionNegotiation(ExtensionContext context, List requestedParameters);
//         void onHandshakeResponse(ExtensionContext context, List responseParameters);
 
//         void destroy(ExtensionContext context);


//         interface ExtensionContext
//            {

//                Map<String, Object> getProperties();
//            }
//        }



//public class CryptoExtension implements ExtendedExtension
//{

//    @Override
// public Frame processIncoming(ExtensionContext context, Frame frame)
//{
//    return lameCrypt(context, frame);
//}

//@Override
// public Frame processOutgoing(ExtensionContext context, Frame frame)
//{
//    return lameCrypt(context, frame);
//}

//private Frame lameCrypt(ExtensionContext context, Frame frame)
//{
//    if (!frame.isControlFrame() && (frame.getOpcode() == 0x02))
//    {
//        final byte[] payloadData = frame.getPayloadData();
//        payloadData[0] ^= (Byte)(context.getProperties().get("key"));

//        return Frame.builder(frame).payloadData(payloadData).build();
//    }
//    else {
//        return frame;
//    }
//}

//@Override
// public List onExtensionNegotiation(ExtensionContext context,
// List requestedParameters)
//{
//    init(context);
//    // no params.
//    return null;
//}

//@Override
// public void onHandshakeResponse(ExtensionContext context,
// List responseParameters)
//{
//    init(context);
//}

//private void init(ExtensionContext context)
//{
//    context.getProperties().put("key", (byte)0x55);
//}

//@Override
// public void destroy(ExtensionContext context)
//{
//    context.getProperties().clear();
//}

//@Override
// public String getName()
//{
//    return "lame-crypto-extension";
//}

//@Override
// public List getParameters()
//{
//    // no params.
//    return null;
//}
//}

}
