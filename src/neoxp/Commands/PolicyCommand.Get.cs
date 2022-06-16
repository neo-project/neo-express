using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "get", Description = "Retrieve current value of a blockchain policy")]
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

            [Option(Description = "URL of Neo JSON-RPC Node\nSpecify MainNet (default), TestNet or JSON-RPC URL")]
            internal string RpcUri { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                PolicyValues policy;
                if (string.IsNullOrEmpty(RpcUri))
                {
                    using var expressNode = chain.GetExpressNode();
                    policy = await GetPolicyAsync(script => expressNode.InvokeAsync(script)).ConfigureAwait(false);
                }
                else
                {
                    if (!Program.TryParseRpcUri(RpcUri, out var uri))
                        throw new ArgumentException($"Invalid RpcUri value \"{RpcUri}\"");
                    using var rpcClient = new RpcClient(uri);
                    policy = await GetPolicyAsync(rpcClient).ConfigureAwait(false);
                }

                if (Json)
                {
                    await console.Out.WriteLineAsync(policy.ToJson().ToString(true));
                }
                else
                {
                    await WritePolicyDecimalAsync(console, $"             {nameof(PolicyValues.GasPerBlock)}", policy.GasPerBlock);
                    await WritePolicyDecimalAsync(console, $"    {nameof(PolicyValues.MinimumDeploymentFee)}", policy.MinimumDeploymentFee);
                    await WritePolicyDecimalAsync(console, $"{nameof(PolicyValues.CandidateRegistrationFee)}", policy.CandidateRegistrationFee);
                    await WritePolicyDecimalAsync(console, $"        {nameof(PolicyValues.OracleRequestFee)}", policy.OracleRequestFee);
                    await WritePolicyDecimalAsync(console, $"       {nameof(PolicyValues.NetworkFeePerByte)}", policy.NetworkFeePerByte);
                    await    WritePolicyUIntAsync(console, $"        {nameof(PolicyValues.StorageFeeFactor)}", policy.StorageFeeFactor);
                    await    WritePolicyUIntAsync(console, $"      {nameof(PolicyValues.ExecutionFeeFactor)}", policy.ExecutionFeeFactor);
                }

                static Task WritePolicyDecimalAsync(IConsole console, string name, Neo.BigDecimal value)
                    => console.Out.WriteLineAsync($"{name}: {value.Value} ({value} GAS)");

                static Task WritePolicyUIntAsync(IConsole console, string name, uint value)
                    => console.Out.WriteLineAsync($"{name}: {value}");
            }

            public static ValueTask<PolicyValues> GetPolicyAsync(RpcClient rpcClient)
                => GetPolicyAsync(async script => await rpcClient.InvokeScriptAsync(script).ConfigureAwait(false));

            internal static async ValueTask<PolicyValues> GetPolicyAsync(Func<Script, ValueTask<RpcInvokeResult>> invokeAsync)
            {
                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(NativeContract.NEO.Hash, "getGasPerBlock");
                builder.EmitDynamicCall(NativeContract.ContractManagement.Hash, "getMinimumDeploymentFee");
                builder.EmitDynamicCall(NativeContract.NEO.Hash, "getRegisterPrice");
                builder.EmitDynamicCall(NativeContract.Oracle.Hash, "getPrice");
                builder.EmitDynamicCall(NativeContract.Policy.Hash, "getFeePerByte");
                builder.EmitDynamicCall(NativeContract.Policy.Hash, "getStoragePrice");
                builder.EmitDynamicCall(NativeContract.Policy.Hash, "getExecFeeFactor");

                var result = await invokeAsync(builder.ToArray()).ConfigureAwait(false);

                if (result.State != VMState.HALT) throw new Exception(result.Exception);
                if (result.Stack.Length != 7) throw new InvalidOperationException();

                return new PolicyValues()
                {
                    GasPerBlock = new BigDecimal(result.Stack[0].GetInteger(), NativeContract.GAS.Decimals),
                    MinimumDeploymentFee = new BigDecimal(result.Stack[1].GetInteger(), NativeContract.GAS.Decimals),
                    CandidateRegistrationFee = new BigDecimal(result.Stack[2].GetInteger(), NativeContract.GAS.Decimals),
                    OracleRequestFee = new BigDecimal(result.Stack[3].GetInteger(), NativeContract.GAS.Decimals),
                    NetworkFeePerByte = new BigDecimal(result.Stack[4].GetInteger(), NativeContract.GAS.Decimals),
                    StorageFeeFactor = (uint)result.Stack[5].GetInteger(),
                    ExecutionFeeFactor = (uint)result.Stack[6].GetInteger(),
                };
            }
        }
    }
}
