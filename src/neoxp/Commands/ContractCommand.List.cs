using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "list", Description = "List deployed contracts")]
        internal class List
        {
            readonly IFileSystem fileSystem;

            public List(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal async Task ExecuteAsync(System.IO.TextWriter writer)
            {
                var (chain, _) = fileSystem.LoadExpressChainInfo(Input);
                using var expressNode = chain.GetExpressNode(fileSystem);

                var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);

                if (Json)
                {
                    using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(writer);
                    await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
                    foreach (var (hash, manifest) in contracts)
                    {

                        await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                        await jsonWriter.WritePropertyNameAsync("name").ConfigureAwait(false);
                        await jsonWriter.WriteValueAsync(manifest.Name).ConfigureAwait(false);
                        await jsonWriter.WritePropertyNameAsync("hash").ConfigureAwait(false);
                        await jsonWriter.WriteValueAsync(hash.ToString()).ConfigureAwait(false);
                        await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    }
                    await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
                }
                else
                {
                    foreach (var (hash, manifest) in contracts)
                    {
                        await writer.WriteLineAsync($"{manifest.Name} ({hash})").ConfigureAwait(false);
                    }
                }
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    await ExecuteAsync(Console.Out).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
