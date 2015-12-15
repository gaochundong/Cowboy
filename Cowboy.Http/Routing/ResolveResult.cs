namespace Cowboy.Http.Routing
{
    public class ResolveResult
    {
        public ResolveResult()
        {
        }

        public ResolveResult(Route route, DynamicDictionary parameters)
        {
            this.Route = route;
            this.Parameters = parameters;
        }

        public Route Route { get; set; }
        public DynamicDictionary Parameters { get; set; }
    }
}
