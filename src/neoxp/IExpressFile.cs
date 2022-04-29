using System.Diagnostics.CodeAnalysis;
using Neo;
using Neo.BlockchainToolkit.Models;

namespace NeoExpress
{
    interface IExpressFile
    {
        ExpressChain Chain { get; }

        IExpressNode GetExpressNode(bool offlineTrace = false);
        void Save();
        bool TryResolveSigner(string name, string password, [MaybeNullWhen(false)] out Neo.Wallets.Wallet wallet, out UInt160 accountHash);
    }
}
