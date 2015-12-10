using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cowboy.Sessions;
using Cowboy.Sessions.Cryptography;
using Cowboy.Utilities;

namespace Cowboy
{
    public class CookieBasedSession : IObjectSerializerSelector
    {
        private readonly CookieBasedSessionConfiguration currentConfiguration;

        public CookieBasedSession(IEncryptionProvider encryptionProvider, IHmacProvider hmacProvider, IObjectSerializer objectSerializer)
        {
            this.currentConfiguration = new CookieBasedSessionConfiguration
            {
                Serializer = objectSerializer,
                CryptographyConfiguration = new CryptographyConfiguration(encryptionProvider, hmacProvider)
            };
        }

        public CookieBasedSession(CookieBasedSessionConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            if (!configuration.IsValid)
            {
                throw new ArgumentException("Configuration is invalid", "configuration");
            }
            this.currentConfiguration = configuration;
        }

        public string CookieName
        {
            get
            {
                return this.currentConfiguration.CookieName;
            }
        }

        public void WithSerializer(IObjectSerializer newSerializer)
        {
            this.currentConfiguration.Serializer = newSerializer;
        }

        public void Save(ISession session, Response response)
        {
            if (session == null || !session.HasChanged)
            {
                return;
            }

            var sb = new StringBuilder();
            foreach (var kvp in session)
            {
                sb.Append(HttpUtility.UrlEncode(kvp.Key));
                sb.Append("=");

                var objectString = this.currentConfiguration.Serializer.Serialize(kvp.Value);

                sb.Append(HttpUtility.UrlEncode(objectString));
                sb.Append(";");
            }

            var cryptographyConfiguration = this.currentConfiguration.CryptographyConfiguration;
            var encryptedData = cryptographyConfiguration.EncryptionProvider.Encrypt(sb.ToString());
            var hmacBytes = cryptographyConfiguration.HmacProvider.GenerateHmac(encryptedData);
            var cookieData = String.Format("{0}{1}", Convert.ToBase64String(hmacBytes), encryptedData);

            var cookie = new Cookie(this.currentConfiguration.CookieName, cookieData, true)
            {
                Domain = this.currentConfiguration.Domain,
                Path = this.currentConfiguration.Path
            };
            //response.WithCookie(cookie);
        }

        public ISession Load(Request request)
        {
            var dictionary = new Dictionary<string, object>();

            var cookieName = this.currentConfiguration.CookieName;
            var hmacProvider = this.currentConfiguration.CryptographyConfiguration.HmacProvider;
            var encryptionProvider = this.currentConfiguration.CryptographyConfiguration.EncryptionProvider;

            if (request.Cookies.ContainsKey(cookieName))
            {
                var cookieData = HttpUtility.UrlDecode(request.Cookies[cookieName]);
                var hmacLength = Base64Helpers.GetBase64Length(hmacProvider.HmacLength);
                if (cookieData.Length < hmacLength)
                {
                    return new Session(dictionary);
                }

                var hmacString = cookieData.Substring(0, hmacLength);
                var encryptedCookie = cookieData.Substring(hmacLength);

                var hmacBytes = Convert.FromBase64String(hmacString);
                var newHmac = hmacProvider.GenerateHmac(encryptedCookie);
                var hmacValid = HmacComparer.Compare(newHmac, hmacBytes, hmacProvider.HmacLength);

                var data = encryptionProvider.Decrypt(encryptedCookie);
                var parts = data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts.Select(part => part.Split('=')).Where(part => part.Length == 2))
                {
                    var valueObject = this.currentConfiguration.Serializer.Deserialize(HttpUtility.UrlDecode(part[1]));

                    dictionary[HttpUtility.UrlDecode(part[0])] = valueObject;
                }

                if (!hmacValid)
                {
                    dictionary.Clear();
                }
            }

            return new Session(dictionary);
        }

        private static void SaveSession(Context context, CookieBasedSession sessionStore)
        {
            //sessionStore.Save(context.Request.Session, context.Response);
        }

        private static Response LoadSession(Context context, CookieBasedSession sessionStore)
        {
            if (context.Request == null)
            {
                return null;
            }

            //context.Request.Session = sessionStore.Load(context.Request);

            return null;
        }
    }
}
