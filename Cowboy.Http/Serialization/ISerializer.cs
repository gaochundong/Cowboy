using System.Collections.Generic;
using System.IO;

namespace Cowboy.Http.Serialization
{
    public interface ISerializer
    {
        bool CanSerialize(string contentType);

        IEnumerable<string> Extensions { get; }

        void Serialize<TModel>(string contentType, TModel model, Stream outputStream);
    }
}
