using Cowboy.Routing;

namespace Cowboy
{
    public class Context
    {
        public Context()
        {
        }

        public Request Request { get; set; }

        public Response Response { get; set; }

        public Route ResolvedRoute { get; set; }

        public dynamic Parameters { get; set; }

        public string ToFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (this.Request == null)
            {
                return path.TrimStart('~');
            }

            if (string.IsNullOrEmpty(this.Request.Url.BasePath))
            {
                return path.TrimStart('~');
            }

            if (!path.StartsWith("~/"))
            {
                return path;
            }

            return string.Format("{0}{1}", this.Request.Url.BasePath, path.TrimStart('~'));
        }
    }
}
