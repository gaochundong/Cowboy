namespace Cowboy.WebSockets.Extensions
{
    public class AgreedSingleParameter : AgreedExtensionParameter
    {
        public AgreedSingleParameter(string name)
            : base(name)
        {
        }

        public override ExtensionParameterType ParameterType
        {
            get
            {
                return ExtensionParameterType.Single;
            }
        }
    }
}
