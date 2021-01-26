using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Persistence;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface INodeManager
    {
        string GetNodePath(ExpressConsensusNode node);
        bool IsRunning(ExpressConsensusNode node);
        void Reset(ExpressConsensusNode node, bool force);
        Task RunAsync(IStore store, ExpressConsensusNode node, bool enableTrace, IConsole console, CancellationToken token);
    }
}
