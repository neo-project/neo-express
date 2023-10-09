// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using MessagePack;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [Union(TraceRecord.RecordKey, typeof(TraceRecord))]
    [Union(NotifyRecord.RecordKey, typeof(NotifyRecord))]
    [Union(LogRecord.RecordKey, typeof(LogRecord))]
    [Union(ResultsRecord.RecordKey, typeof(ResultsRecord))]
    [Union(FaultRecord.RecordKey, typeof(FaultRecord))]
    [Union(ScriptRecord.RecordKey, typeof(ScriptRecord))]
    [Union(StorageRecord.RecordKey, typeof(StorageRecord))]
    [Union(ProtocolSettingsRecord.RecordKey, typeof(ProtocolSettingsRecord))]
    public interface ITraceDebugRecord
    {
    }
}
