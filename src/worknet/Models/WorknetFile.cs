// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.BlockchainToolkit.Models;
using Neo.Wallets;
using System;

namespace NeoWorkNet.Models;

public record WorknetFile(
    Uri Uri,
    BranchInfo BranchInfo,
    Wallet ConsensusWallet);
