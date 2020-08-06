using MessagePack;

namespace Neo.Seattle.TraceDebug.Models
{
    [MessagePackObject]
    public struct Fault : ITraceRecord
    {
        [Key(0)]
        public string Exception;
    }
}
