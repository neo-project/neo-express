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

        Task<(string path, IExpressNode.CheckpointMode checkpointMode)> CreateCheckpointAsync(IExpressNode expressNode, string checkPointPath, bool force, System.IO.TextWriter? writer = null);
        IDisposableStorageProvider GetCheckpointStorageProvider(string checkPointPath);
        IExpressNode GetExpressNode(bool offlineTrace = false);
        IDisposableStorageProvider GetNodeStorageProvider(ExpressConsensusNode node, bool discard);
        bool IsRunning(ExpressConsensusNode? node = null);
        void ResetNode(ExpressConsensusNode node, bool force);
        void RestoreCheckpoint(string checkPointPath, bool force);
        Task RunAsync(IStorageProvider store, ExpressConsensusNode node, bool enableTrace, TextWriter writer, CancellationToken token);
        void SaveChain(string path);
        Task<bool> StopNodeAsync(ExpressConsensusNode node);
    }
}
