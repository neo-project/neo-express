using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

using System.Text;
using System;
using System.Text.Json;
using System.IO;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "storage")]
        private class Storage
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Storage(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            string Contract { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            bool Json { get; }

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var expressNode = chainManager.GetExpressNode();
                var parser = await expressNode.GetContractParameterParserAsync(chainManager).ConfigureAwait(false);
                var scriptHash = parser.ParseScriptHash(Contract);
                var storages = await expressNode.GetStoragesAsync(scriptHash).ConfigureAwait(false);

                if (Json)
                {
                    using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(writer);
                    await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
                    foreach (var storage in storages)
                    {
                        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                        await jsonWriter.WritePropertyNameAsync("key").ConfigureAwait(false);
                        await jsonWriter.WriteValueAsync(storage.Key).ConfigureAwait(false);
                        await jsonWriter.WritePropertyNameAsync("value").ConfigureAwait(false);
                        await jsonWriter.WriteValueAsync(storage.Value).ConfigureAwait(false);
                        await jsonWriter.WritePropertyNameAsync("constant").ConfigureAwait(false);
                        await jsonWriter.WriteValueAsync(storage.Constant).ConfigureAwait(false);
                        await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    }
                    await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
                }
                else
                {
                    foreach (var storage in storages)
                    {
                        await writer.WriteLineAsync($"key:        0x{storage.Key}").ConfigureAwait(false);
                        await writer.WriteLineAsync($"  value:    0x{storage.Value}").ConfigureAwait(false);
                        await writer.WriteLineAsync($"  constant: {storage.Constant}").ConfigureAwait(false);
                    }
                }
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    // var (chain, _) = Program.LoadExpressChain(Input);

                    // var blockchainOperations = new BlockchainOperations();
                    // var storages = await blockchainOperations.GetStoragesAsync(chain, Contract);

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
