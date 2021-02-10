using System;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "get", Description = "Get information for a deployed contract")]
        internal class Get
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Get(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    await ExecuteAsync(console.Out).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var expressNode = chainManager.GetExpressNode();
                var parser = await expressNode.GetContractParameterParserAsync(chainManager).ConfigureAwait(false);
                var scriptHash = parser.ParseScriptHash(Contract);
                var manifest = await expressNode.GetContractAsync(scriptHash).ConfigureAwait(false);
                await writer.WriteLineAsync(manifest.ToJson().ToString(true)).ConfigureAwait(false);
            }
        }
    }
}
