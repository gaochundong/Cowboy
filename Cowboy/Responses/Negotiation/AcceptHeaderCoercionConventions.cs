using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses.Negotiation
{
    public class AcceptHeaderCoercionConventions : IEnumerable<Func<IEnumerable<Tuple<string, decimal>>, Context, IEnumerable<Tuple<string, decimal>>>>
    {
        private readonly IList<Func<IEnumerable<Tuple<string, decimal>>, Context, IEnumerable<Tuple<string, decimal>>>> conventions;

        public AcceptHeaderCoercionConventions(IList<Func<IEnumerable<Tuple<string, decimal>>, Context, IEnumerable<Tuple<string, decimal>>>> conventions)
        {
            this.conventions = conventions;
        }

        public IEnumerator<Func<IEnumerable<Tuple<string, decimal>>, Context, IEnumerable<Tuple<string, decimal>>>> GetEnumerator()
        {
            return this.conventions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
