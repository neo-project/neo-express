using MessagePack;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct Fault : ITraceRecord
    {
        [Key(0)]
        public readonly string Exception;

        public Fault(string exception)
        {
            Exception = exception;
        }
    }
}
