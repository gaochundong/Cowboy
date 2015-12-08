using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.Responses.Negotiation.Processors
{
    public interface IResponseProcessor
    {
        ProcessorMatch CanProcess(dynamic model, Context context);

        Response Process(dynamic model, Context context);
    }
}
