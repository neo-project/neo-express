using MessagePack;

namespace Neo.Seattle.TraceDebug.Models
{
    [MessagePackObject]
    public readonly struct Log : ITraceRecord
    {
        [Key(0)]
        public readonly UInt160 ScriptHash;
        [Key(1)]
        public readonly string Message;

        public Log(UInt160 scriptHash, string message)
        {
            ScriptHash = scriptHash;
            Message = message;
        }
    }
}
