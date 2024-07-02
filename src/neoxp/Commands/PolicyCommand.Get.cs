// Copyright (C) 2015-2024 The Neo Project.
//
// PolicyCommand.Get.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo.Network.RPC;
using NeoExpress.Models;
using static Neo.BlockchainToolkit.Utility;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "get", Description = "Retrieve current value of a blockchain policy")]
        internal class Get
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Get(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Option(Description = "URL of Neo JSON-RPC Node\nSpecify MainNet (default), TestNet or JSON-RPC URL")]
            internal string RpcUri { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var values = await GetPolicyValuesAsync().ConfigureAwait(false);

                    if (Json)
                    {
                        await console.Out.WriteLineAsync(values.ToJson().ToString(true));
                    }
                    else
                    {
                        await WritePolicyValueAsync(console, $"             {nameof(PolicyValues.GasPerBlock)}", values.GasPerBlock);
                        await WritePolicyValueAsync(console, $"    {nameof(PolicyValues.MinimumDeploymentFee)}", values.MinimumDeploymentFee);
                        await WritePolicyValueAsync(console, $"{nameof(PolicyValues.CandidateRegistrationFee)}", values.CandidateRegistrationFee);
                        await WritePolicyValueAsync(console, $"        {nameof(PolicyValues.OracleRequestFee)}", values.OracleRequestFee);
                        await WritePolicyValueAsync(console, $"       {nameof(PolicyValues.NetworkFeePerByte)}", values.NetworkFeePerByte);
                        await WritePolicyValueAsync(console, $"        {nameof(PolicyValues.StorageFeeFactor)}", values.StorageFeeFactor);
                        await WritePolicyValueAsync(console, $"      {nameof(PolicyValues.ExecutionFeeFactor)}", values.ExecutionFeeFactor);
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }

            static Task WritePolicyValueAsync(IConsole console, string name, Neo.BigDecimal value)
                => console.Out.WriteLineAsync($"{name}: {value.Value} ({value} GAS)");

            static Task WritePolicyValueAsync(IConsole console, string name, uint value)
                => console.Out.WriteLineAsync($"{name}: {value}");

            async Task<PolicyValues> GetPolicyValuesAsync()
            {
                if (string.IsNullOrEmpty(RpcUri))
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
                    return await expressNode.GetPolicyAsync().ConfigureAwait(false);
                }
                else
                {
                    if (!TryParseRpcUri(RpcUri, out var uri))
                    {
                        throw new ArgumentException($"Invalid RpcUri value \"{RpcUri}\"");
                    }
                    using var rpcClient = new RpcClient(uri);
                    return await rpcClient.GetPolicyAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
