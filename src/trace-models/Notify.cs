using System.Collections.Generic;
using MessagePack;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.Seattle.TraceDebug.Models
{
    [MessagePackObject]
    public struct Notify : ITraceRecord
    {
        [Key(0)]
        public UInt160 ScriptHash;
        [Key(2)]
        public string EventName;
        [Key(2)]
        public IReadOnlyList<StackItem> State;
    }
}
