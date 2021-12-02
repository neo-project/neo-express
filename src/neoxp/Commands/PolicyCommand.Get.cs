using System;
using System.ComponentModel.DataAnnotations;
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
                        using var writer = new Newtonsoft.Json.JsonTextWriter(Console.Out);
                        writer.Formatting = Newtonsoft.Json.Formatting.Indented;
                        writer.WriteStartObject();
                        writer.WritePropertyName(nameof(PolicyValues.GasPerBlock));
                        writer.WriteValue($"{values.GasPerBlock}");
                        writer.WritePropertyName(nameof(PolicyValues.MinimumDeploymentFee));
                        writer.WriteValue($"{values.MinimumDeploymentFee}");
                        writer.WritePropertyName(nameof(PolicyValues.CandidateRegistrationFee));
                        writer.WriteValue($"{values.CandidateRegistrationFee}");
                        writer.WritePropertyName(nameof(PolicyValues.OracleRequestFee));
                        writer.WriteValue($"{values.OracleRequestFee}");
                        writer.WritePropertyName(nameof(PolicyValues.NetworkFeePerByte));
                        writer.WriteValue($"{values.NetworkFeePerByte}");
                        writer.WritePropertyName(nameof(PolicyValues.StorageFeeFactor));
                        writer.WriteValue(values.StorageFeeFactor);
                        writer.WritePropertyName(nameof(PolicyValues.ExecutionFeeFactor));
                        writer.WriteValue(values.ExecutionFeeFactor);
                        writer.WriteEndObject();
                    }
                    else
                    {
                        await console.Out.WriteLineAsync($"             {nameof(PolicyValues.GasPerBlock)}: {values.GasPerBlock} GAS");
                        await console.Out.WriteLineAsync($"    {nameof(PolicyValues.MinimumDeploymentFee)}: {values.MinimumDeploymentFee} GAS");
                        await console.Out.WriteLineAsync($"{nameof(PolicyValues.CandidateRegistrationFee)}: {values.CandidateRegistrationFee} GAS");
                        await console.Out.WriteLineAsync($"        {nameof(PolicyValues.OracleRequestFee)}: {values.OracleRequestFee} GAS");
                        await console.Out.WriteLineAsync($"       {nameof(PolicyValues.NetworkFeePerByte)}: {values.NetworkFeePerByte} GAS");
                        await console.Out.WriteLineAsync($"        {nameof(PolicyValues.StorageFeeFactor)}: {values.StorageFeeFactor}");
                        await console.Out.WriteLineAsync($"      {nameof(PolicyValues.ExecutionFeeFactor)}: {values.ExecutionFeeFactor}");
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }

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
                    var uri = ParseRpcUri(RpcUri);
                    using var rpcClient = new RpcClient(uri);
                    return await rpcClient.GetPolicyAsync().ConfigureAwait(false);
                }
            }

            internal static Uri ParseRpcUri(string value)
            {
                if (value.Equals("mainnet", StringComparison.InvariantCultureIgnoreCase)) return new Uri("http://seed1.neo.org:10332");
                if (value.Equals("testnet", StringComparison.InvariantCultureIgnoreCase)) return new Uri("http://seed1t4.neo.org:20332");
                if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    return uri;
                }

                throw new ArgumentException($"Invalid Neo RPC Uri {value}");
            }
        }
    }
}
