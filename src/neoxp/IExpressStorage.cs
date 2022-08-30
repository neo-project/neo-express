using Neo.Persistence;

namespace NeoExpress
{
    interface IExpressStorage : IDisposable
    {
        string Name { get; }
        IStore GetStore(string? path);
    }
}
