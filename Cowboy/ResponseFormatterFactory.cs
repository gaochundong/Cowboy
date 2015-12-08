using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy
{
    public class ResponseFormatterFactory
    {
        private readonly RootPathProvider rootPathProvider;
        private readonly IEnumerable<ISerializer> serializers;

        public ResponseFormatterFactory(RootPathProvider rootPathProvider, IEnumerable<ISerializer> serializers)
        {
            this.rootPathProvider = rootPathProvider;
            this.serializers = serializers.ToArray();
        }

        public ResponseFormatter Create(Context context)
        {
            return new ResponseFormatter(this.rootPathProvider, context, this.serializers);
        }
    }
}
