using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Persistence;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface INodeManager
    {
        Task RunAsync(IStore store, ExpressConsensusNode node, bool enableTrace, IConsole console, CancellationToken token);
    }
}
