namespace Cowboy.Sessions
{
    public interface IObjectSerializerSelector : IHideObjectMembers
    {
        void WithSerializer(IObjectSerializer newSerializer);
    }

    public interface IObjectSerializer
    {
        string Serialize(object sourceObject);

        object Deserialize(string sourceString);
    }
}
