using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;

namespace NeoExpress
{
    interface IExpressFile
    {
        ExpressChain Chain { get; }

        IExpressNode GetExpressNode(bool offlineTrace = false);
        void SaveChain();
        bool TryResolveSigner(string name, string password, [MaybeNullWhen(false)] out Neo.Wallets.Wallet wallet, out UInt160 accountHash);
    }
}
