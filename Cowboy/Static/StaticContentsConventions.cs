using System;
using System.Collections;
using System.Collections.Generic;

namespace Cowboy
{
    public class StaticContentsConventions : IEnumerable<Func<Context, string, Response>>
    {
        private readonly IEnumerable<Func<Context, string, Response>> conventions;

        public StaticContentsConventions(IEnumerable<Func<Context, string, Response>> conventions)
        {
            this.conventions = conventions;
        }

        public IEnumerator<Func<Context, string, Response>> GetEnumerator()
        {
            return conventions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
