using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Cowboy.Utilities;

namespace Cowboy.Responses
{
    public class GenericFileResponse : Response
    {
        public static IList<string> SafePaths { get; set; }

        public static int BufferSize = 4 * 1024 * 1024;

        static GenericFileResponse()
        {
            SafePaths = new List<string>();
        }

        public GenericFileResponse(string filePath) :
            this(filePath, MimeTypes.GetMimeType(filePath))
        {
        }

        public GenericFileResponse(string filePath, Context context)
            : this(filePath, MimeTypes.GetMimeType(filePath), context)
        {
        }

        public GenericFileResponse(string filePath, string contentType, Context context = null)
        {
            InitializeGenericFileResponse(filePath, contentType, context);
        }

        public string Filename { get; protected set; }

        private static Action<Stream> GetFileContent(string filePath, long length)
        {
            return stream =>
            {
                using (var file = File.OpenRead(filePath))
                {
                    file.CopyTo(stream, (int)(length < BufferSize ? length : BufferSize));
                }
            };
        }

        static bool IsSafeFilePath(string rootPath, string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(filePath);

            return fullPath.StartsWith(Path.GetFullPath(rootPath), StringComparison.OrdinalIgnoreCase);
        }

        private void InitializeGenericFileResponse(string filePath, string contentType, Context context)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                StatusCode = HttpStatusCode.NotFound;
                return;
            }
            if (SafePaths == null || SafePaths.Count == 0)
            {
                throw new InvalidOperationException("No SafePaths defined.");
            }
            foreach (var rootPath in SafePaths)
            {
                string fullPath;
                if (Path.IsPathRooted(filePath))
                {
                    fullPath = filePath;
                }
                else
                {
                    fullPath = Path.Combine(rootPath, filePath);
                }

                if (IsSafeFilePath(rootPath, fullPath))
                {
                    this.Filename = Path.GetFileName(fullPath);

                    this.SetResponseValues(contentType, fullPath, context);

                    return;
                }
            }

            StatusCode = HttpStatusCode.NotFound;
        }

        private void SetResponseValues(string contentType, string fullPath, Context context)
        {
            // TODO - set a standard caching time and/or public?
            var fi = new FileInfo(fullPath);

            var lastWriteTimeUtc = fi.LastWriteTimeUtc;
            var etag = string.Concat("\"", lastWriteTimeUtc.Ticks.ToString("x"), "\"");
            var lastModified = lastWriteTimeUtc.ToString("R");
            var length = fi.Length;

            //if (CacheHelpers.ReturnNotModified(etag, lastWriteTimeUtc, context))
            //{
            //    this.StatusCode = HttpStatusCode.NotModified;
            //    this.ContentType = null;
            //    this.Contents = Response.NoBody;

            //    return;
            //}

            this.Headers["ETag"] = etag;
            this.Headers["Last-Modified"] = lastModified;
            this.Headers["Content-Length"] = length.ToString();

            if (length > 0)
            {
                this.Contents = GetFileContent(fullPath, length);
            }

            this.ContentType = contentType;
            this.StatusCode = HttpStatusCode.OK;
        }
    }
}
