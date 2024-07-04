// Copyright (C) 2015-2024 The Neo Project.
//
// TransferRecord.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Numerics;

namespace Neo.BlockchainToolkit.Plugins
{
    public record TransferRecord(
        ulong Timestamp,
        UInt160 Asset,
        UInt160 From,
        UInt160 To,
        BigInteger Amount,
        uint BlockIndex,
        ushort TransferNotifyIndex,
        UInt256 TxHash,
        ReadOnlyMemory<byte> TokenId);
}
