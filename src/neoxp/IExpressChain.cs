using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;

namespace NeoExpress
{
    interface IExpressChain
    {
        uint Network { get; }
        byte AddressVersion { get; }
        IReadOnlyList<ExpressConsensusNode> ConsensusNodes { get; }
        IReadOnlyList<ExpressWallet> Wallets { get; }
        IReadOnlyDictionary<string, string> Settings { get; }

        IExpressNode GetExpressNode(bool offlineTrace = false);
        void SaveChain();
        bool TryResolveSigner(string name, string password, [MaybeNullWhen(false)] out Neo.Wallets.Wallet wallet, out UInt160 accountHash);
    }
}
