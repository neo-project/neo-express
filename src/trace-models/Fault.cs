using MessagePack;

namespace Neo.Seattle.TraceDebug.Models
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
