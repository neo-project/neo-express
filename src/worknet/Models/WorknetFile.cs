// Copyright (C) 2015-2026 The Neo Project.
//
// WorknetFile.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Models;
using Neo.Wallets;

namespace NeoWorkNet.Models;

public record WorknetFile(
    Uri Uri,
    BranchInfo BranchInfo,
    Wallet ConsensusWallet)
{
    // RPC port of the running instance as recorded in the worknet file's
    // consensus-nodes entry. RunCommand refreshes it on startup so that
    // StopCommand can reach the node when a non-default --rpc-port was used.
    public ushort RpcPort { get; init; } = Commands.RunCommand.DEFAULT_RPC_PORT;
}
