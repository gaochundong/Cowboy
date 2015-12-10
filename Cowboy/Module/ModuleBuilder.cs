namespace Cowboy
{
    public class ModuleBuilder
    {
        private readonly ResponseFormatterFactory responseFormatterFactory;

        public ModuleBuilder(ResponseFormatterFactory responseFormatterFactory)
        {
            this.responseFormatterFactory = responseFormatterFactory;
        }

        public Module BuildModule(Module module, Context context)
        {
            module.Context = context;
            module.Response = this.responseFormatterFactory.Create(context);

            return module;
        }
    }
}
