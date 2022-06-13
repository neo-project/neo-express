using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Neo;
using Neo.BlockchainToolkit.Models;

namespace NeoExpress
{
    interface IExpressChain
    {
        public const string GENESIS = "genesis";

        uint Network { get; }
        byte AddressVersion { get; }
        IReadOnlyList<ExpressConsensusNode> ConsensusNodes { get; }
        IReadOnlyList<ExpressWallet> Wallets { get; }
        IReadOnlyDictionary<string, string> Settings { get; }

        IExpressNode GetExpressNode(bool offlineTrace = false);
        string GetNodePath(ExpressConsensusNode node);
        void AddWallet(ExpressWallet wallet);
        void RemoveWallet(ExpressWallet wallet);
        void SaveChain();
        bool TryResolveSigner(string name, string password, [MaybeNullWhen(false)] out Neo.Wallets.Wallet wallet, out UInt160 accountHash);
    }
}
