namespace Cowboy.Responses.Negotiation.Processors
{
    public interface IResponseProcessor
    {
        ProcessorMatch CanProcess(dynamic model, Context context);

        Response Process(dynamic model, Context context);
    }
}
