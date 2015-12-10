using System;

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

            Get["/text"] = x =>
            {
                return "text";
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

            //Get["/session"] = x => 
            //{
            //    var value = Session["moo"] ?? "";

            //    var output = "Current session value is: " + value;

            //    if (String.IsNullOrEmpty(value.ToString()))
            //    {
            //        Session["moo"] = "I've created a session!";
            //    }

            //    return output;
            //};

            //Get["/sessionObject"] = x => 
            //{
            //    var value = Session["baa"] ?? "null";

            //    var output = "Current session value is: " + value;

            //    if (value.ToString() == "null")
            //    {
            //        Session["baa"] = new Payload(27, true, "some random string value");
            //    }

            //    return output;
            //};

            Get["/error"] = x =>
            {
                throw new NotSupportedException("This is an exception thrown in a route.");
            };
        }
    }
}
