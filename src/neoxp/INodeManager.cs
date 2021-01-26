using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Persistence;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface INodeManager
    {
        IExpressNode GetExpressNode(ExpressChain chain, bool offlineTrace = false);
        string GetNodePath(ExpressConsensusNode node);
        void Reset(ExpressConsensusNode node, bool force);
        Task RunAsync(IStore store, ExpressChain chain, ExpressConsensusNode node, uint secondsPerBlock, bool enableTrace, IConsole console, CancellationToken token);
    }
}
