using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command("deploy", Description = "Deploy contract to a neo-express instance")]
        internal class Deploy
        {
            readonly IExpressFile expressFile;

            public Deploy(IExpressFile expressFile)
            {
                this.expressFile = expressFile;
            }

            public Deploy(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Argument(0, Description = "Path to contract .nef file")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "Account to pay contract deployment GAS fee")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
            [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
            internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;

            [Option(Description = "Optional data parameter to pass to _deploy operation")]
            internal string Data { get; init; } = string.Empty;

            [Option(Description = "Password to use for NEP-2/NEP-6 account")]
            internal string Password { get; init; } = string.Empty;

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            [Option(Description = "Deploy contract regardless of name conflict")]
            internal bool Force { get; }

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = expressFile.GetExpressNode(Trace);
                var password = expressFile.ResolvePassword(Account, Password);
                var (txHash, contractHash, manifest) = await ExecuteAsync(expressNode, Contract, Account, password, WitnessScope, Data, Force)
                    .ConfigureAwait(false);

                if (Json)
                {
                    using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                    using var _ = writer.WriteObject();
                    writer.WriteProperty("contract-name", manifest.Name);
                    writer.WriteProperty("contract-hash", $"{contractHash}");
                    writer.WriteProperty("tx-hash", $"{txHash}");
                }
                else
                {
                    await console.Out.WriteLineAsync($"Deployment of {manifest.Name} ({contractHash}) Transaction {txHash} submitted").ConfigureAwait(false);
                }
            }

            public static async Task<(UInt256 txHash, UInt160 contractHash, ContractManifest manifest)>
                ExecuteAsync(IExpressNode expressNode, string contract, string accountName, string password, WitnessScope witnessScope, string data, bool force)
            {
                var (wallet, accountHash) = expressNode.ExpressFile.ResolveSigner(accountName, password);

                var (nefFile, manifest) = await expressNode.ExpressFile.LoadContractAsync(contract).ConfigureAwait(false);

                // check for bad opcodes (logic borrowed from neo-cli LoadDeploymentScript)
                Neo.VM.Script script = nefFile.Script;
                for (var i = 0; i < script.Length;)
                {
                    var instruction = script.GetInstruction(i);
                    if (instruction == null)
                    {
                        throw new FormatException($"null opcode found at {i}");
                    }
                    else
                    {
                        if (!Enum.IsDefined(typeof(Neo.VM.OpCode), instruction.OpCode))
                        {
                            throw new FormatException($"Invalid opcode found at {i}-{((byte)instruction.OpCode).ToString("x2")}");
                        }
                        i += instruction.Size;
                    }
                }

                if (!force)
                {
                    var contracts = await expressNode.ListContractsAsync(manifest.Name).ConfigureAwait(false);
                    if (contracts.Count > 0)
                    {
                        throw new Exception($"Contract named {manifest.Name} already deployed. Use --force to deploy contract with conflicting name.");
                    }

                    var nep11 = false; var nep17 = false;
                    var standards = manifest.SupportedStandards;
                    for (var i = 0; i < standards.Length; i++)
                    {
                        if (standards[i] == "NEP-11") nep11 = true;
                        if (standards[i] == "NEP-17") nep17 = true;
                    }
                    if (nep11 && nep17)
                    {
                        throw new Exception($"{manifest.Name} Contract declares support for both NEP-11 and NEP-17 standards. Use --force to deploy contract with invalid supported standards declarations.");
                    }
                }

                ContractParameter? dataParam = null;
                if (!string.IsNullOrEmpty(data))
                {
                    dataParam = new ContractParameter(ContractParameterType.Any);
                }
                else
                {
                    var parser = await expressNode.GetContractParameterParserAsync().ConfigureAwait(false);
                    dataParam = parser.ParseParameter(data);
                }

                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(NativeContract.ContractManagement.Hash,
                    "deploy", Neo.IO.Helper.ToArray(nefFile), manifest.ToJson().ToString(), data);
                var txHash = await expressNode.ExecuteAsync(wallet, accountHash, witnessScope, builder.ToArray())
                    .ConfigureAwait(false);

                var contractHash = Neo.SmartContract.Helper.GetContractHash(accountHash, nefFile.CheckSum, manifest.Name);
                return (txHash, contractHash, manifest);
            }
        }
    }
}