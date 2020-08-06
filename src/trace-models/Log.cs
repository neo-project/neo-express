using MessagePack;

namespace Neo.Seattle.TraceDebug.Models
{
    [MessagePackObject]
    public struct Log : ITraceRecord
    {
        [Key(0)]
        public UInt160 ScriptHash;
        [Key(1)]
        public string Message;
    }
}
