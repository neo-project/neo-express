using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "get", Description = "Retrieve current value of a blockchain policy")]
        internal class Get
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Get(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Policy to set")]
            [Required]
            internal PolicyName Policy { get; init; }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();

                    var value = await expressNode.GetPolicyAsync(Policy).ConfigureAwait(false);
                    await console.Out.WriteLineAsync($"{Policy}: {value}");

                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }
        }
    }
}
