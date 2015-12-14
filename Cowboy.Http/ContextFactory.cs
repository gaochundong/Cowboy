namespace Cowboy.Http
{
    public class ContextFactory
    {
        public ContextFactory()
        {
        }

        public Context Create(Request request)
        {
            var context = new Context();

            context.Request = request;

            return context;
        }
    }
}
