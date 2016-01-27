using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cowboy.Serialization
{
    public class JsonSerializer : ISerializer
    {
        private Newtonsoft.Json.JsonSerializer _serializer = Newtonsoft.Json.JsonSerializer.CreateDefault();

        public bool CanSerialize(string contentType)
        {
            return IsJsonType(contentType);
        }

        public IEnumerable<string> Extensions
        {
            get { yield return "json"; }
        }


        public void Serialize<TModel>(string contentType, TModel model, Stream outputStream)
        {
            using (var writer = new StreamWriter(new UnclosableStreamWrapper(outputStream), Encoding.UTF8))
            {
                _serializer.Serialize(writer, model);
            }
        }

        private static bool IsJsonType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return false;
            }

            var contentMimeType = contentType.Split(';')[0];

            return contentMimeType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                   contentMimeType.StartsWith("application/json-", StringComparison.OrdinalIgnoreCase) ||
                   contentMimeType.Equals("text/json", StringComparison.OrdinalIgnoreCase) ||
                  (contentMimeType.StartsWith("application/vnd", StringComparison.OrdinalIgnoreCase) &&
                   contentMimeType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
        }
    }
}
