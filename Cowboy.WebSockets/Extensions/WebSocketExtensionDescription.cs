namespace Cowboy.WebSockets.Extensions
{
    public sealed class WebSocketExtensionDescription
    {
        public WebSocketExtensionDescription()
        {
        }

        public WebSocketExtensionDescription(string extensionString)
        {
            ExtensionString = extensionString;
        }

        public string ExtensionString { get; set; }
    }
}
