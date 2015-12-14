using System.IO;
using Cowboy.Http;

namespace Cowboy.TestServer
{
    public class TestModule : Module
    {
        public TestModule()
        {
            Get["/"] = x =>
            {
                return "hello world";
            };

            Get["/redirect"] = _ => this.Response.AsRedirect("~/text");

            Get["/text"] = x =>
            {
                return "text";
            };

            Post["/post"] = x =>
            {
                return "";
            };

            Post["/post-something"] = x =>
            {
                var body = new StreamReader(this.Request.Body).ReadToEnd();
                return body;
            };

            Get["/json"] = x =>
            {
                var model = new TestModel { Description = "Hello Json" };
                return this.Response.AsJson(model);
            };

            Get["/xml"] = x =>
            {
                var model = new TestModel { Description = "Hello Xml" };
                return this.Response.AsXml(model);
            };

            Get["/user/{name}"] = parameters =>
            {
                return (string)parameters.name;
            };

            Get["/html"] = x =>
            {
                string html =
                    @"
                    <html>
                    <head>
                      <title>Hi there</title>
                    </head>
                    <body>
                        This is a page, a simple page.
                    </body>
                    </html>
                    ";
                return this.Response.AsHtml(html);
            };
        }
    }
}
