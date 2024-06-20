// Copyright (C) 2015-2024 The Neo Project.
//
// ITraceDebugRecord.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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
