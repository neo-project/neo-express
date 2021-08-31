using System;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;

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
                    app.WriteException(ex);
                    return 1;
                }
            }

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var expressNode = chainManager.GetExpressNode();

                if (UInt160.TryParse(Contract, out var hash))
                {
                    var manifest = await expressNode.GetContractAsync(hash).ConfigureAwait(false);
                    await writer.WriteLineAsync(manifest.ToJson().ToString(true)).ConfigureAwait(false);
                }
                else
                {
                    var contracts = await expressNode.ListContractsAsync(Contract).ConfigureAwait(false);
                    if (contracts.Count == 0)
                    {
                        await writer.WriteLineAsync($"No contracts named {Contract} found").ConfigureAwait(false);
                    }
                    else if (contracts.Count == 1)
                    {
                        await writer.WriteLineAsync(contracts[0].manifest.ToJson().ToString(true)).ConfigureAwait(false);
                    }
                    else
                    {
                        await writer.WriteLineAsync("[").ConfigureAwait(false);
                        var first = true;
                        for (int i = 0; i < contracts.Count; i++)
                        {
                            if (!contracts[i].manifest.Name.Equals(Contract)) continue;

                            await writer.WriteLineAsync(contracts[i].manifest.ToJson().ToString(true)).ConfigureAwait(false);
                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                await writer.WriteLineAsync(",").ConfigureAwait(false);
                            }
                        }
                        await writer.WriteLineAsync("]").ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
