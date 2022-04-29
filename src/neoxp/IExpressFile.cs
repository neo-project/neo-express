using Neo.BlockchainToolkit.Models;

namespace NeoExpress
{
    interface IExpressFile
    {
        ExpressChain Chain { get; }
        void Save();
    }
}
