using System;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.SmartContract.Manifest;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "get", Description = "Get information for a deployed contract")]
        internal class Get
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Get(ExpressChainManagerFactory chainManagerFactory)
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

                using var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
                jsonWriter.WriteStartArray();

                if (UInt160.TryParse(Contract, out var contractHash))
                {
                    var manifest = await expressNode.GetContractAsync(contractHash).ConfigureAwait(false);
                    WriteContract(jsonWriter, contractHash, manifest);
                }
                else
                {
                    var contracts = await expressNode.ListContractsAsync(Contract).ConfigureAwait(false);

                    foreach (var (hash, manifest) in contracts)
                    {
                        WriteContract(jsonWriter, hash, manifest);
                    }
                }

                static void WriteContract(JsonWriter writer, UInt160 hash, ContractManifest manifest)
                {
                    var json = manifest.ToJson();
                    json["hash"] = hash.ToString();
                    writer.WriteJson(json);
                }
            }
        }
    }
}
