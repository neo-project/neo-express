// Copyright (C) 2015-2024 The Neo Project.
//
// DiagnosticRecords.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.BlockchainToolkit.Models
{
    public record GetStorageStart(
        UInt160 ContractHash,
        string ContractName,
        ReadOnlyMemory<byte> Key);

    public record GetStorageStop(TimeSpan Elapsed);

    public record DownloadStatesStart(
        UInt160 ContractHash,
        string ContractName,
        byte? Prefix);

    public record DownloadStatesFound(
        int Total,
        int Count);

    public record DownloadStatesStop(
        int Count,
        TimeSpan Elapsed);
}
