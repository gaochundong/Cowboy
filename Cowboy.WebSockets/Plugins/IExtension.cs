using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Plugins
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
    }





    //public class BaseObject : IExtensibleObject<BaseObject>
    //{
    //    private DateTime _startDate;
    //    private ExtensionCollection<BaseObject> _extensions;

    //    public DateTime StartDate
    //    {
    //        get { return _startDate; }
    //        set { _startDate = value; }
    //    }

    //    public BaseObject()
    //    {
    //        StartDate = DateTime.Now;
    //        _extensions = new ExtensionCollection<BaseObject>(this);
    //    }

    //    #region IExtensibleObject<BaseObject> Members

    //    public IExtensionCollection<BaseObject> Extensions
    //    {
    //        get
    //        {
    //            return _extensions;
    //        }
    //    }

    //    #endregion
    //}

    //public class DateTimeConverterExtension : IExtension<BaseObject>
    //{
    //    private BaseObject _owner;

    //    #region IExtension<BaseObject> Members

    //    public void Attach(BaseObject owner)
    //    {
    //        _owner = owner;
    //        _owner.StartDate = owner.StartDate.ToUniversalTime();
    //    }

    //    public void Detach(BaseObject owner)
    //    {
    //        _owner.StartDate = _owner.StartDate.ToLocalTime();
    //    }

    //    #endregion
    //}






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
