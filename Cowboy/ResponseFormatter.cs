using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy
{
    public class ResponseFormatter
    {
        private readonly RootPathProvider rootPathProvider;
        private readonly IEnumerable<ISerializer> serializers;
        private readonly Context context;

        public ResponseFormatter(RootPathProvider rootPathProvider, Context context, IEnumerable<ISerializer> serializers)
        {
            this.serializers = serializers.ToArray();
            this.rootPathProvider = rootPathProvider;
            this.context = context;
        }

        public IEnumerable<ISerializer> Serializers
        {
            get
            {
                return this.serializers;
            }
        }

        public Context Context
        {
            get { return this.context; }
        }

        public string RootPath
        {
            get { return this.rootPathProvider.GetRootPath(); }
        }
    }
}
