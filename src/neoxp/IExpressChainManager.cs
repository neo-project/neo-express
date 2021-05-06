using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Plugins;

namespace NeoExpress
{
    internal interface IExpressChainManager
    {
        ExpressChain Chain { get; }
        ProtocolSettings ProtocolSettings { get; }

        Task RunAsync(IStorageProvider store, ExpressConsensusNode node, bool enableTrace, TextWriter writer, CancellationToken token);
        IDisposableStorageProvider GetNodeStorageProvider(ExpressConsensusNode node, bool discard);
        IDisposableStorageProvider GetCheckpointStorageProvider(string checkPointPath);

        void SaveChain(string path);
        Task<(string path, IExpressNode.CheckpointMode checkpointMode)> CreateCheckpointAsync(IExpressNode expressNode, string checkPointPath, bool force);
        void RestoreCheckpoint(string checkPointPath, bool force);
        void ResetNode(ExpressConsensusNode node, bool force);
        IExpressNode GetExpressNode(bool offlineTrace = false);
    }
}
