using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cowboy.Http;
using Cowboy.Http.Responses;
using Cowboy.Http.Utilities;

namespace Cowboy.StaticContent
{
    public class StaticContentConventionBuilder
    {
        private static readonly ConcurrentDictionary<ResponseFactoryCacheKey, Func<Context, Response>> ResponseFactoryCache;
        private static readonly Regex PathReplaceRegex = new Regex(@"[/\\]", RegexOptions.Compiled);

        static StaticContentConventionBuilder()
        {
            ResponseFactoryCache = new ConcurrentDictionary<ResponseFactoryCacheKey, Func<Context, Response>>();
        }

        public static Func<Context, string, Response> AddDirectory(string requestedPath, string contentPath = null, params string[] allowedExtensions)
        {
            if (!requestedPath.StartsWith("/"))
            {
                requestedPath = string.Concat("/", requestedPath);
            }

            return (ctx, root) =>
            {
                var path = HttpUtility.UrlDecode(ctx.Request.Path);

                var fileName = GetSafeFileName(path);

                if (string.IsNullOrEmpty(fileName))
                {
                    return null;
                }

                var pathWithoutFilename = GetPathWithoutFilename(fileName, path);

                if (!pathWithoutFilename.StartsWith(requestedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                contentPath = GetContentPath(requestedPath, contentPath);

                if (contentPath.Equals("/"))
                {
                    throw new ArgumentException("This is not the security vulnerability you are looking for. Mapping static content to the root of your application is not a good idea.");
                }

                var responseFactory = ResponseFactoryCache.GetOrAdd(
                    new ResponseFactoryCacheKey(path, root),
                    BuildContentDelegate(ctx, root, requestedPath, contentPath, allowedExtensions));

                return responseFactory.Invoke(ctx);
            };
        }

        public static Func<Context, string, Response> AddFile(string requestedFile, string contentFile)
        {
            return (ctx, root) =>
            {
                var path = ctx.Request.Path;

                if (!path.Equals(requestedFile, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var responseFactory = ResponseFactoryCache.GetOrAdd(
                    new ResponseFactoryCacheKey(path, root),
                    BuildContentDelegate(ctx, root, requestedFile, contentFile, new string[] { }));

                return responseFactory.Invoke(ctx);
            };
        }

        private static string GetSafeFileName(string path)
        {
            try
            {
                return Path.GetFileName(path);
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static string GetContentPath(string requestedPath, string contentPath)
        {
            contentPath = contentPath ?? requestedPath;

            if (!contentPath.StartsWith("/"))
            {
                contentPath = string.Concat("/", contentPath);
            }

            return contentPath;
        }

        private static Func<ResponseFactoryCacheKey, Func<Context, Response>> BuildContentDelegate(Context context, string applicationRootPath, string requestedPath, string contentPath, string[] allowedExtensions)
        {
            return pathAndRootPair =>
            {
                var extension = Path.GetExtension(pathAndRootPair.Path);

                if (!string.IsNullOrEmpty(extension))
                {
                    extension = extension.Substring(1);
                }

                if (allowedExtensions.Length != 0 && !allowedExtensions.Any(e => string.Equals(e.TrimStart(new[] { '.' }), extension, StringComparison.OrdinalIgnoreCase)))
                {
                    return ctx => null;
                }

                var transformedRequestPath = GetSafeRequestPath(pathAndRootPair.Path, requestedPath, contentPath);

                transformedRequestPath = GetEncodedPath(transformedRequestPath);

                var fileName = Path.GetFullPath(Path.Combine(applicationRootPath, transformedRequestPath));

                var contentRootPath = Path.GetFullPath(Path.Combine(applicationRootPath, GetEncodedPath(contentPath)));

                if (!IsWithinContentFolder(contentRootPath, fileName))
                {
                    return ctx => null;
                }

                if (!File.Exists(fileName))
                {
                    return ctx => null;
                }

                return ctx => new FileResponse(fileName, ctx);
            };
        }

        private static string GetEncodedPath(string path)
        {
            return PathReplaceRegex.Replace(path.TrimStart(new[] { '/' }), Path.DirectorySeparatorChar.ToString());
        }

        private static string GetPathWithoutFilename(string fileName, string path)
        {
            var pathWithoutFileName = path.Replace(fileName, string.Empty);

            return (pathWithoutFileName.Equals("/")) ?
                pathWithoutFileName :
                pathWithoutFileName.TrimEnd(new[] { '/' });
        }

        private static string GetSafeRequestPath(string requestPath, string requestedPath, string contentPath)
        {
            var actualContentPath = (contentPath.Equals("/") ? string.Empty : contentPath);

            if (requestedPath.Equals("/"))
            {
                return string.Concat(actualContentPath, requestPath);
            }

            var expression = new Regex(Regex.Escape(requestedPath), RegexOptions.IgnoreCase);

            return expression.Replace(requestPath, actualContentPath, 1);
        }

        private static bool IsWithinContentFolder(string contentRootPath, string fileName)
        {
            return fileName.StartsWith(contentRootPath, StringComparison.Ordinal);
        }

        private class ResponseFactoryCacheKey : IEquatable<ResponseFactoryCacheKey>
        {
            private readonly string path;
            private readonly string rootPath;

            public ResponseFactoryCacheKey(string path, string rootPath)
            {
                this.path = path;
                this.rootPath = rootPath;
            }

            public string Path
            {
                get { return this.path; }
            }

            public string RootPath
            {
                get { return this.rootPath; }
            }

            public bool Equals(ResponseFactoryCacheKey other)
            {
                if (ReferenceEquals(null, other))
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return string.Equals(this.path, other.path) && string.Equals(this.rootPath, other.rootPath);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                if (obj.GetType() != this.GetType())
                {
                    return false;
                }

                return Equals((ResponseFactoryCacheKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((this.path != null ? this.path.GetHashCode() : 0) * 397) ^ (this.rootPath != null ? this.rootPath.GetHashCode() : 0);
                }
            }
        }
    }
}
