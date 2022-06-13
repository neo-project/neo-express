using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;
using OneOf;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "sync", Description = "Synchronize local policy values with public Neo network")]
        internal class Sync
        {
            readonly IExpressChain chain;

            public Sync(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Sync(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Source of policy values. Must be local policy settings JSON file or the URL of Neo JSON-RPC Node\nFor Node URL,\"MainNet\" or \"TestNet\" can be specified in addition to a standard HTTP URL")]
            [Required]
            internal string Source { get; } = string.Empty;

            [Argument(1, Description = "Account to pay contract invocation GAS fee")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "password to use for NEP-2/NEP-6 sender")]
            internal string Password { get; init; } = string.Empty;

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IFileSystem fileSystem, IConsole console)
            {
                PolicyValues policy;
                if (Node.NodeUtility.TryParseRpcUri(Source, out var uri))
                {
                    using var rpcClient = new RpcClient(uri);
                    policy = await Get.GetPolicyAsync(rpcClient).ConfigureAwait(false);
                }
                else
                {
                    var loadResult = await TryLoadPolicyFromFileSystemAsync(fileSystem, Source).ConfigureAwait(false);
                    if (!loadResult.TryPickT0(out policy, out _))
                    {
                        throw new ArgumentException($"Could not load policy values from \"{Source}\"");
                    }
                }

                var password = chain.ResolvePassword(Account, Password);
                using var expressNode = chain.GetExpressNode(Trace);
                var txHash = await ExecuteAsync(expressNode, policy, Account, password).ConfigureAwait(false);
                await console.Out.WriteTxHashAsync(txHash, "Policy Sync", Json).ConfigureAwait(false);
            }

            public static async Task<UInt256> ExecuteAsync(IExpressNode expressNode, PolicyValues policyValues, string account, string password)
            {
                var (wallet, accountHash) = expressNode.Chain.ResolveSigner(account, password);

                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(NativeContract.NEO.Hash, "setGasPerBlock", policyValues.GasPerBlock.Value);
                builder.EmitDynamicCall(NativeContract.ContractManagement.Hash, "setMinimumDeploymentFee", policyValues.MinimumDeploymentFee.Value);
                builder.EmitDynamicCall(NativeContract.NEO.Hash, "setRegisterPrice", policyValues.CandidateRegistrationFee.Value);
                builder.EmitDynamicCall(NativeContract.Oracle.Hash, "setPrice", policyValues.OracleRequestFee.Value);
                builder.EmitDynamicCall(NativeContract.Policy.Hash, "setFeePerByte", policyValues.NetworkFeePerByte.Value);
                builder.EmitDynamicCall(NativeContract.Policy.Hash, "setStoragePrice", policyValues.StorageFeeFactor);
                builder.EmitDynamicCall(NativeContract.Policy.Hash, "setExecFeeFactor", policyValues.ExecutionFeeFactor);

                return await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray())
                    .ConfigureAwait(false);
            }

            public static async Task<OneOf<PolicyValues, Exception>> TryLoadPolicyFromFileSystemAsync(IFileSystem fileSystem, string path)
            {
                try
                {
                    using var stream = fileSystem.File.OpenRead(path);
                    using var reader = new System.IO.StreamReader(stream);
                    var text = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var json = Neo.IO.Json.JObject.Parse(text);
                    return PolicyValues.FromJson(json);
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }
        }
    }
}
