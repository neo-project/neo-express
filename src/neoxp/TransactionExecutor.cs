using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Neo.Network.P2P.Payloads;
using NeoExpress.Models;
using Newtonsoft.Json.Linq;
using OneOf;

namespace NeoExpress
{
    using All = OneOf.Types.All;

    class TransactionExecutor : ITransactionExecutor
    {
        readonly IExpressChainManager chainManager;
        readonly IExpressNode expressNode;
        readonly IFileSystem fileSystem;
        readonly bool json;
        readonly System.IO.TextWriter writer;

        public TransactionExecutor(IFileSystem fileSystem, IExpressChainManager chainManager, bool trace, bool json, TextWriter writer)
        {
            this.chainManager = chainManager;
            expressNode = chainManager.GetExpressNode(trace);
            this.fileSystem = fileSystem;
            this.json = json;
            this.writer = writer;
        }

        public void Dispose()
        {
            expressNode.Dispose();
        }

        public IExpressNode ExpressNode => expressNode;

        public async Task ContractDeployAsync(string contract, string accountName, string password, WitnessScope witnessScope, bool force)
        {
            if (!chainManager.Chain.TryGetAccount(accountName, out var wallet, out var account, chainManager.ProtocolSettings))
            {
                throw new Exception($"{accountName} account not found.");
            }

            var (nefFile, manifest) = await fileSystem.LoadContractAsync(contract).ConfigureAwait(false);

            if (!force)
            {
                var contracts = await expressNode.ListContractsAsync(manifest.Name).ConfigureAwait(false);
                if (contracts.Count > 0)
                {
                    throw new Exception($"Contract named {manifest.Name} already deployed. Use --force to deploy contract with conflicting name.");
                }
            }

            var txHash = await expressNode.DeployAsync(nefFile, manifest, wallet, account.ScriptHash, witnessScope).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, "Deployment", json).ConfigureAwait(false);
        }

        public async Task ContractInvokeAsync(string invocationFile, string accountName, string password, WitnessScope witnessScope)
        {
            if (!fileSystem.File.Exists(invocationFile))
            {
                throw new Exception($"Invocation file {invocationFile} couldn't be found");
            }

            if (!chainManager.Chain.TryGetAccount(accountName, out var wallet, out var account, chainManager.ProtocolSettings))
            {
                throw new Exception($"{accountName} account not found.");
            }

            var parser = await expressNode.GetContractParameterParserAsync(chainManager).ConfigureAwait(false);
            var script = await parser.LoadInvocationScriptAsync(invocationFile).ConfigureAwait(false);
            var txHash = await expressNode.ExecuteAsync(wallet, account.ScriptHash, witnessScope, script).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, "Deployment", json).ConfigureAwait(false);
        }

        public async Task InvokeForResultsAsync(string invocationFile)
        {
            if (!fileSystem.File.Exists(invocationFile))
            {
                throw new Exception($"Invocation file {invocationFile} couldn't be found");
            }

            var parser = await expressNode.GetContractParameterParserAsync(chainManager).ConfigureAwait(false);
            var script = await parser.LoadInvocationScriptAsync(invocationFile).ConfigureAwait(false);

            var result = await expressNode.InvokeAsync(script).ConfigureAwait(false);
            if (json)
            {
                await writer.WriteLineAsync(result.ToJson().ToString(true)).ConfigureAwait(false);
            }
            else
            {
                await writer.WriteLineAsync($"VM State:     {result.State}").ConfigureAwait(false);
                await writer.WriteLineAsync($"Gas Consumed: {result.GasConsumed}").ConfigureAwait(false);
                if (result.Exception != null)
                {
                    await writer.WriteLineAsync($"Expception:   {result.Exception}").ConfigureAwait(false);
                }
                if (result.Stack.Length > 0)
                {
                    var stack = result.Stack;
                    await writer.WriteLineAsync("Result Stack:").ConfigureAwait(false);
                    for (int i = 0; i < stack.Length; i++)
                    {
                        await WriteStackItemAsync(writer, stack[i]).ConfigureAwait(false);
                    }
                }
            }

            static async Task WriteStackItemAsync(System.IO.TextWriter writer, Neo.VM.Types.StackItem item, int indent = 1, string prefix = "")
            {
                switch (item)
                {
                    case Neo.VM.Types.Boolean _:
                        await WriteLineAsync(item.GetBoolean() ? "true" : "false").ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.Integer @int:
                        await WriteLineAsync(@int.GetInteger().ToString()).ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.Buffer buffer:
                        await WriteLineAsync(Neo.Helper.ToHexString(buffer.GetSpan())).ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.ByteString byteString:
                        await WriteLineAsync(Neo.Helper.ToHexString(byteString.GetSpan())).ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.Null _:
                        await WriteLineAsync("<null>").ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.Array array:
                        await WriteLineAsync($"Array: ({array.Count})").ConfigureAwait(false);
                        for (int i = 0; i < array.Count; i++)
                        {
                            await WriteStackItemAsync(writer, array[i], indent + 1).ConfigureAwait(false);
                        }
                        break;
                    case Neo.VM.Types.Map map:
                        await WriteLineAsync($"Map: ({map.Count})").ConfigureAwait(false);
                        foreach (var m in map)
                        {
                            await WriteStackItemAsync(writer, m.Key, indent + 1, "key:   ").ConfigureAwait(false);
                            await WriteStackItemAsync(writer, m.Value, indent + 1, "value: ").ConfigureAwait(false);
                        }
                        break;
                }

                async Task WriteLineAsync(string value)
                {
                    for (var i = 0; i < indent; i++)
                    {
                        await writer.WriteAsync("  ").ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(prefix))
                    {
                        await writer.WriteAsync(prefix).ConfigureAwait(false);
                    }

                    await writer.WriteLineAsync(value).ConfigureAwait(false);
                }
            }
        }

        public async Task OracleEnableAsync(string accountName, string password)
        {
            if (!chainManager.Chain.TryGetAccount(accountName, out var wallet, out var account, chainManager.ProtocolSettings))
            {
                throw new Exception($"{accountName} account not found.");
            }

            var oracles = chainManager.Chain.ConsensusNodes
                .Select(n => DevWalletAccount.FromExpressWalletAccount(chainManager.ProtocolSettings, n.Wallet.DefaultAccount ?? throw new Exception()))
                .Select(a => a.GetKey()?.PublicKey ?? throw new Exception());
            var txHash = await expressNode.DesignateOracleRolesAsync(wallet, account.ScriptHash, oracles).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, "Oracle Enable", json).ConfigureAwait(false);
        }

        public async Task OracleResponseAsync(string url, string responsePath, ulong? requestId)
        {
            if (!fileSystem.File.Exists(responsePath)) throw new Exception($"Response File {responsePath} couldn't be found");

            JObject responseJson;
            {
                using var stream = fileSystem.File.OpenRead(responsePath);
                using var reader = new System.IO.StreamReader(stream);
                using var jsonReader = new Newtonsoft.Json.JsonTextReader(reader);
                responseJson = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            }

            var txHashes = await expressNode.SubmitOracleResponseAsync(url, OracleResponseCode.Success, responseJson, requestId).ConfigureAwait(false);

            if (json)
            {
                using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(writer);
                await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
                for (int i = 0; i < txHashes.Count; i++)
                {
                    await jsonWriter.WriteValueAsync(txHashes[i].ToString()).ConfigureAwait(false);
                }
                await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
            }
            else
            {
                if (txHashes.Count == 0)
                {
                    await writer.WriteLineAsync("No oracle response transactions submitted").ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteLineAsync("Oracle response transactions submitted:").ConfigureAwait(false);
                    for (int i = 0; i < txHashes.Count; i++)
                    {
                        await writer.WriteLineAsync($"    {txHashes[i]}").ConfigureAwait(false);
                    }
                }
            }
        }

        public async Task TransferAsync(string quantity, string asset, string sender, string password, string receiver)
        {
            if (!chainManager.Chain.TryGetAccount(sender, out var senderWallet, out var senderAccount, chainManager.ProtocolSettings))
            {
                throw new Exception($"{sender} sender not found.");
            }

            if (!chainManager.Chain.TryGetAccountHash(receiver, out var receiverHash, chainManager.ProtocolSettings))
            {
                throw new Exception($"{receiver} account not found.");
            }

            var assetHash = await expressNode.ParseAssetAsync(asset).ConfigureAwait(false);
            var txHash = await expressNode.TransferAsync(assetHash, ParseQuantity(quantity), senderWallet, senderAccount.ScriptHash, receiverHash);
            await writer.WriteTxHashAsync(txHash, "Transfer", json).ConfigureAwait(false);

            static OneOf<decimal, All> ParseQuantity(string quantity)
            {
                if ("all".Equals(quantity, StringComparison.OrdinalIgnoreCase))
                {
                    return new All();
                }

                if (decimal.TryParse(quantity, out var amount))
                {
                    return amount;
                }

                throw new Exception($"Invalid quantity value {quantity}");
            }
        }
    }
}
