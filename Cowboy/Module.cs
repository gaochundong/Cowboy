using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Routing;

namespace Cowboy
{
    public abstract class Module : IHideObjectMembers
    {
        private readonly List<Route> routes;

        protected Module()
            : this(string.Empty)
        {
        }

        protected Module(string modulePath)
        {
            //this.After = new AfterPipeline();
            //this.Before = new BeforePipeline();
            //this.OnError = new ErrorPipeline();

            this.ModulePath = modulePath;
            this.routes = new List<Route>();
        }

        //public dynamic ViewBag
        //{
        //    get
        //    {
        //        return this.Context == null ? null : this.Context.ViewBag;
        //    }
        //}

        //public dynamic Text
        //{
        //    get { return this.Context.Text; }
        //}

        public RouteBuilder Delete
        {
            get { return new RouteBuilder("DELETE", this); }
        }

        public RouteBuilder Get
        {
            get { return new RouteBuilder("GET", this); }
        }

        public RouteBuilder Head
        {
            get
            {
                //if (!StaticConfiguration.EnableHeadRouting)
                //{
                //    throw new InvalidOperationException("Explicit HEAD routing is disabled. Set StaticConfiguration.EnableHeadRouting to enable.");
                //}

                return new RouteBuilder("HEAD", this);
            }
        }

        public RouteBuilder Options
        {
            get { return new RouteBuilder("OPTIONS", this); }
        }

        public RouteBuilder Patch
        {
            get { return new RouteBuilder("PATCH", this); }
        }

        public RouteBuilder Post
        {
            get { return new RouteBuilder("POST", this); }
        }

        public RouteBuilder Put
        {
            get { return new RouteBuilder("PUT", this); }
        }

        public string ModulePath { get; protected set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual IEnumerable<Route> Routes
        {
            get { return this.routes.AsReadOnly(); }
        }

        //public ISession Session
        //{
        //    get { return this.Request.Session; }
        //}

        //public ViewRenderer View
        //{
        //    get { return new ViewRenderer(this); }
        //}

        //public Negotiator Negotiate
        //{
        //    get { return new Negotiator(this.Context); }
        //}

        //[EditorBrowsable(EditorBrowsableState.Never)]
        //public IModelValidatorLocator ValidatorLocator { get; set; }

        public virtual Request Request
        {
            get { return this.Context.Request; }
            set { this.Context.Request = value; }
        }

        //[EditorBrowsable(EditorBrowsableState.Never)]
        //public IViewFactory ViewFactory { get; set; }

        //public AfterPipeline After { get; set; }
        //public BeforePipeline Before { get; set; }
        //public ErrorPipeline OnError { get; set; }

        public Context Context { get; set; }

        //public IResponseFormatter Response { get; set; }

        //[EditorBrowsable(EditorBrowsableState.Never)]
        //public IModelBinderLocator ModelBinderLocator { get; set; }
        //public virtual ModelValidationResult ModelValidationResult
        //{
        //    get { return this.Context == null ? null : this.Context.ModelValidationResult; }
        //    set
        //    {
        //        if (this.Context != null)
        //        {
        //            this.Context.ModelValidationResult = value;
        //        }
        //    }
        //}

        public class RouteBuilder : IHideObjectMembers
        {
            private readonly string method;
            private readonly Module parentModule;

            public RouteBuilder(string method, Module parentModule)
            {
                this.method = method;
                this.parentModule = parentModule;
            }

            public Func<dynamic, dynamic> this[string path]
            {
                set { this.AddRoute(string.Empty, path, null, value); }
            }

            public Func<dynamic, dynamic> this[string path, Func<Context, bool> condition]
            {
                set { this.AddRoute(string.Empty, path, condition, value); }
            }

            public Func<dynamic, CancellationToken, Task<dynamic>> this[string path, bool runAsync]
            {
                set { this.AddRoute(string.Empty, path, null, value); }
            }

            public Func<dynamic, CancellationToken, Task<dynamic>> this[string path, Func<Context, bool> condition, bool runAsync]
            {
                set { this.AddRoute(string.Empty, path, condition, value); }
            }

            public Func<dynamic, dynamic> this[string name, string path]
            {
                set { this.AddRoute(name, path, null, value); }
            }

            public Func<dynamic, dynamic> this[string name, string path, Func<Context, bool> condition]
            {
                set { this.AddRoute(name, path, condition, value); }
            }

            public Func<dynamic, CancellationToken, Task<dynamic>> this[string name, string path, bool runAsync]
            {
                set { this.AddRoute(name, path, null, value); }
            }

            public Func<dynamic, CancellationToken, Task<dynamic>> this[string name, string path, Func<Context, bool> condition, bool runAsync]
            {
                set { this.AddRoute(name, path, condition, value); }
            }

            protected void AddRoute(string name, string path, Func<Context, bool> condition, Func<dynamic, dynamic> value)
            {
                var fullPath = GetFullPath(path);

                this.parentModule.routes.Add(Route.FromSync(name, this.method, fullPath, condition, value));
            }

            protected void AddRoute(string name, string path, Func<Context, bool> condition, Func<dynamic, CancellationToken, Task<dynamic>> value)
            {
                var fullPath = GetFullPath(path);

                this.parentModule.routes.Add(new Route(name, this.method, fullPath, condition, value));
            }

            private string GetFullPath(string path)
            {
                var relativePath = (path ?? string.Empty).Trim('/');
                var parentPath = (this.parentModule.ModulePath ?? string.Empty).Trim('/');

                if (string.IsNullOrEmpty(parentPath))
                {
                    return string.Concat("/", relativePath);
                }

                if (string.IsNullOrEmpty(relativePath))
                {
                    return string.Concat("/", parentPath);
                }

                return string.Concat("/", parentPath, "/", relativePath);
            }
        }
    }
}
