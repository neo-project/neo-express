using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Neo.Persistence;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface IBlockchainOperations
    {
        string CreateChain(int nodeCount, string output, bool force);
        (ExpressChain chain, string filename) LoadChain(string filename);
        void SaveChain(ExpressChain chain, string fileName);
        void ResetNode(ExpressConsensusNode node, bool force);
        ExpressWallet CreateWallet(ExpressChain chain, string name);
        void ExportChain(ExpressChain chain, string password, TextWriter writer);

        IExpressNode GetExpressNode(ExpressChain chain, bool offlineTrace = false);
        Func<IStore, ExpressConsensusNode, bool, TextWriter, CancellationToken, Task> GetNodeRunner(ExpressChain chain, uint secondsPerBlock);
        IStore GetNodeStore(ExpressConsensusNode node, bool discard);

        Task CreateCheckpointAsync(ExpressChain chain, string checkPointFileName, bool force, TextWriter writer);
        void RestoreCheckpoint(ExpressChain chain, string checkPointArchive, bool force);

    }
}
