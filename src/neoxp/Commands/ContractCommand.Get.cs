using System;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.SmartContract.Manifest;
using Newtonsoft.Json;
using Neo.VM;
using Neo.SmartContract.Native;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "get", Description = "Get information for a deployed contract")]
        internal class Get
        {
            readonly IExpressChain chain;

            public Get(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Get(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                using var _ = writer.WriteArray();

                if (UInt160.TryParse(Contract, out var contractHash))
                {
                    using var builder = new ScriptBuilder();
                    builder.EmitDynamicCall(NativeContract.ContractManagement.Hash, "getContract", contractHash);
                    var result = await expressNode.GetResultAsync(builder.ToArray()).ConfigureAwait(false);
                    var manifest = new ContractManifest();
                    ((Neo.SmartContract.IInteroperable)manifest).FromStackItem(result.Stack[0]);
                    WriteContract(writer, contractHash, manifest);
                }
                else
                {
                    var contracts = await ContractCommand.ListByNameAsync(expressNode, Contract).ConfigureAwait(false);
                    foreach (var (hash, manifest) in contracts)
                    {
                        WriteContract(writer, hash, manifest);
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
