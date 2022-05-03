using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Network.RPC;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "get", Description = "Retrieve current value of a blockchain policy")]
        internal class Get
        {
            readonly IExpressFile expressFile;

            public Get(IExpressFile expressFile)
            {
                this.expressFile = expressFile;
            }

            public Get(CommandLineApplication app) : this(app.GetExpressFile())
            {
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
                    using var expressNode = expressFile.GetExpressNode();
                    policy = await expressNode.GetPolicyAsync().ConfigureAwait(false);
                }
                else
                {
                    if (!TransactionExecutor.TryParseRpcUri(RpcUri, out var uri))
                        throw new ArgumentException($"Invalid RpcUri value \"{RpcUri}\"");
                    using var rpcClient = new RpcClient(uri);

                    policy = await rpcClient.GetPolicyAsync().ConfigureAwait(false);
                }

                if (Json)
                {
                    await console.Out.WriteLineAsync(policy.ToJson().ToString(true));
                }
                else
                {
                    await WritePolicyValueAsync(console, $"             {nameof(PolicyValues.GasPerBlock)}", policy.GasPerBlock);
                    await WritePolicyValueAsync(console, $"    {nameof(PolicyValues.MinimumDeploymentFee)}", policy.MinimumDeploymentFee);
                    await WritePolicyValueAsync(console, $"{nameof(PolicyValues.CandidateRegistrationFee)}", policy.CandidateRegistrationFee);
                    await WritePolicyValueAsync(console, $"        {nameof(PolicyValues.OracleRequestFee)}", policy.OracleRequestFee);
                    await WritePolicyValueAsync(console, $"       {nameof(PolicyValues.NetworkFeePerByte)}", policy.NetworkFeePerByte);
                    await WritePolicyValueAsync(console, $"        {nameof(PolicyValues.StorageFeeFactor)}", policy.StorageFeeFactor);
                    await WritePolicyValueAsync(console, $"      {nameof(PolicyValues.ExecutionFeeFactor)}", policy.ExecutionFeeFactor);
                }
            }

            static Task WritePolicyValueAsync(IConsole console, string name, Neo.BigDecimal value)
                => console.Out.WriteLineAsync($"{name}: {value.Value} ({value} GAS)");

            static Task WritePolicyValueAsync(IConsole console, string name, uint value)
                => console.Out.WriteLineAsync($"{name}: {value}");
        }
    }
}
