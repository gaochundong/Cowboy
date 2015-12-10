using Cowboy.Sessions.Cryptography;

namespace Cowboy.Sessions
{
    public class CookieBasedSessionConfiguration
    {
        internal const string DefaultCookieName = "_cb";

        public CookieBasedSessionConfiguration() : this(CryptographyConfiguration.Default)
        {
        }

        public CookieBasedSessionConfiguration(CryptographyConfiguration cryptographyConfiguration)
        {
            CryptographyConfiguration = cryptographyConfiguration;
            CookieName = DefaultCookieName;
        }

        public CryptographyConfiguration CryptographyConfiguration { get; set; }

        public IObjectSerializer Serializer { get; set; }

        public string CookieName { get; set; }

        public string Domain { get; set; }

        public string Path { get; set; }

        public virtual bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(this.CookieName))
                {
                    return false;
                }

                if (this.Serializer == null)
                {
                    return false;
                }

                if (this.CryptographyConfiguration == null)
                {
                    return false;
                }

                if (this.CryptographyConfiguration.EncryptionProvider == null)
                {
                    return false;
                }

                if (this.CryptographyConfiguration.HmacProvider == null)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
