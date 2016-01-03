using System;

namespace Cowboy.CommandLines
{
    public interface ICommandLine : IDisposable
    {
        void Execute();

        void Terminate();

        bool IsExecuting { get; }
    }
}
