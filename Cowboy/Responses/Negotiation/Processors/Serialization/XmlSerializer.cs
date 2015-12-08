using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses
{
    public class XmlSerializer : ISerializer
    {
        public bool CanSerialize(string contentType)
        {
            return IsXmlType(contentType);
        }

        public IEnumerable<string> Extensions
        {
            get { yield return "xml"; }
        }

        public void Serialize<TModel>(string contentType, TModel model, Stream outputStream)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(TModel));

            serializer.Serialize(new StreamWriter(outputStream, Encoding.UTF8), model);
        }

        private static bool IsXmlType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return false;
            }

            var contentMimeType = contentType.Split(';')[0];

            return contentMimeType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
                   contentMimeType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ||
                  (contentMimeType.StartsWith("application/vnd", StringComparison.OrdinalIgnoreCase) &&
                   contentMimeType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase));
        }
    }
}
