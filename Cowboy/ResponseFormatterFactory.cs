using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Responses;
using Cowboy.Responses.Serialization;

namespace Cowboy
{
    public class ResponseFormatterFactory
    {
        private readonly IEnumerable<ISerializer> serializers;

        public ResponseFormatterFactory(IEnumerable<ISerializer> serializers)
        {
            this.serializers = serializers.ToArray();
        }

        public ResponseFormatter Create(Context context)
        {
            return new ResponseFormatter(context, this.serializers);
        }
    }
}
