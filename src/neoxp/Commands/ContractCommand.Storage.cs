using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "storage")]
        internal class Storage
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Storage(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; }

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

            internal async Task<int> OnExecuteAsync(IConsole console)
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
        }
    }
}
