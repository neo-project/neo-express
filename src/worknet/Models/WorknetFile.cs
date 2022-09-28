using Neo.BlockchainToolkit.Models;
using Neo.Wallets;

namespace NeoWorkNet.Models;

public record WorknetFile(
    Uri Uri,
    BranchInfo BranchInfo,
    Wallet ConsensusWallet);