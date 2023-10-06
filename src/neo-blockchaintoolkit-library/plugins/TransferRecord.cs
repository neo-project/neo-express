// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using System;
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
