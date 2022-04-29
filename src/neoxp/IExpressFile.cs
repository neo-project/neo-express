using Neo.BlockchainToolkit.Models;

namespace NeoExpress
{
    interface IExpressFile
    {
        ExpressChain Chain { get; }
        IExpressNode GetExpressNode(bool offlineTrace = false);
        void Save();
    }
}
