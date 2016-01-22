namespace Cowboy.WebSockets.Extensions
{
    public class AgreedValuableParameter<T> : AgreedExtensionParameter
    {
        public AgreedValuableParameter(string name, T @value)
            : base(name)
        {
            this.Value = @value;
        }

        public override ExtensionParameterType ParameterType
        {
            get
            {
                return ExtensionParameterType.Valuable;
            }
        }

        public T Value { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}={1}", this.Name, this.Value);
        }
    }
}
