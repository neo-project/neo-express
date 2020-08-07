using System.Collections.Generic;
using MessagePack;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public readonly struct Notify : ITraceRecord
    {
        [Key(0)]
        public readonly UInt160 ScriptHash;
        [Key(1)]
        public readonly string EventName;
        [Key(2)]
        public readonly IReadOnlyList<StackItem> State;

        public Notify(UInt160 scriptHash, string eventName, IReadOnlyList<StackItem> state)
        {
            ScriptHash = scriptHash;
            EventName = eventName;
            State = state;
        }
    }
}
