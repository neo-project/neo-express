using MessagePack;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [Union(0, typeof(TracePoint))]
    [Union(1, typeof(Notify))]
    [Union(2, typeof(Log))]
    [Union(3, typeof(Results))]
    [Union(4, typeof(Fault))]
    public interface ITraceRecord
    {
    }
}
