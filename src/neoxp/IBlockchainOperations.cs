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
        string CreateChain(int nodeCount, string output, bool force);
        (ExpressChain chain, string filename) LoadChain(string filename);
        void SaveChain(ExpressChain chain, string fileName);
        void ResetNode(ExpressConsensusNode node, bool force);


         
        ExpressWallet CreateWallet(ExpressChain chain, string name);
        void ExportChain(ExpressChain chain, string password, TextWriter writer);

        IExpressNode GetExpressNode(ExpressChain chain, bool offlineTrace = false);
        string GetNodePath(ExpressConsensusNode node);
        Task RunAsync(IStore store, ExpressChain chain, ExpressConsensusNode node, uint secondsPerBlock, bool enableTrace, IConsole console, CancellationToken token);
    }
}
