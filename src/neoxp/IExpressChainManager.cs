using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Neo.Persistence;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface IExpressChainManager
    {
        ExpressChain Chain { get; }

        Task RunAsync(IStore store, ExpressConsensusNode node, uint secondsPerBlock, bool enableTrace, TextWriter writer, CancellationToken token);
        IStore GetNodeStore(ExpressConsensusNode node, bool discard);
        IStore GetCheckpointStore(string checkPointPath);

        void SaveChain(string path);
        Task<(string path, bool online)> CreateCheckpointAsync(string checkPointPath, bool force);
        void RestoreCheckpoint(string checkPointPath, bool force);
        void ResetNode(ExpressConsensusNode node, bool force);
        IExpressNode GetExpressNode(bool offlineTrace = false);

    }
}
