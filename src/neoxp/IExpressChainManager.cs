using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface IExpressChainManager
    {
        ExpressChain Chain { get; }

        Task RunAsync(IExpressStore store, ExpressConsensusNode node, uint secondsPerBlock, bool enableTrace, TextWriter writer, CancellationToken token);
        IExpressStore GetNodeStore(ExpressConsensusNode node, bool discard);
        IExpressStore GetCheckpointStore(string checkPointPath);

        void SaveChain(string path);
        Task<(string path, bool online)> CreateCheckpointAsync(IExpressNode expressNode, string checkPointPath, bool force);
        void RestoreCheckpoint(string checkPointPath, bool force);
        void ResetNode(ExpressConsensusNode node, bool force);
        IExpressNode GetExpressNode(bool offlineTrace = false);
    }
}
