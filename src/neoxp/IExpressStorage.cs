using System;
using Neo.Persistence;

namespace NeoExpress
{
    interface IExpressStorage : IDisposable
    {
        string Name { get; }
        IStore ChainStore { get; }
        IStore AppLogsStore { get; }
        IStore NotificationsStore { get; }
    }
}
