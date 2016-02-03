namespace Cowboy.WebSockets.Extensions
{
    public class SingleParameter : ExtensionParameter
    {
        public SingleParameter(string name)
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
