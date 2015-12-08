using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses
{
    public interface ISerializer
    {
        bool CanSerialize(string contentType);

        IEnumerable<string> Extensions { get; }

        void Serialize<TModel>(string contentType, TModel model, Stream outputStream);
    }
}
