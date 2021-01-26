using System.IO;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Persistence;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface IBlockchainOperations
    {
        ExpressChain CreateChain(int nodeCount);
        ExpressWallet CreateWallet(ExpressChain chain, string name);
        void ExportChain(ExpressChain chain, string password, TextWriter writer);
        (ExpressChain chain, string filename) LoadChain(string filename);
        string ResolveChainFileName(string filename);
        void SaveChain(ExpressChain chain, string fileName);

        IExpressNode GetExpressNode(ExpressChain chain, bool offlineTrace = false);
        string GetNodePath(ExpressConsensusNode node);
        void ResetChain(ExpressConsensusNode node, bool force);
        Task RunAsync(IStore store, ExpressChain chain, ExpressConsensusNode node, uint secondsPerBlock, bool enableTrace, IConsole console, CancellationToken token);

    }

}
