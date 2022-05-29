using System;
using Neo.Persistence;

namespace NeoExpress
{
    interface IExpressStorage : IDisposable
    {
        string Name { get; }
        IStore Chain { get; }
        IStore AppLogs { get; }
        IStore Notifications { get; }
    }
}
