using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using NeoExpress.Models;
using Newtonsoft.Json.Linq;
using OneOf;

namespace NeoExpress
{
    using All = OneOf.Types.All;

    class TransactionExecutor : IDisposable
    {
        readonly ExpressChainManager chainManager;
        readonly IExpressNode expressNode;
        readonly IFileSystem fileSystem;
        readonly bool json;
        readonly System.IO.TextWriter writer;

        public TransactionExecutor(IFileSystem fileSystem, ExpressChainManager chainManager, bool trace, bool json, TextWriter writer)
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
            if (!chainManager.TryGetSigningAccount(accountName, password, out var wallet, out var accountHash))
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

            var txHash = await expressNode.DeployAsync(nefFile, manifest, wallet, accountHash, witnessScope).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, "Deployment", json).ConfigureAwait(false);
        }

        public async Task<Script> LoadInvocationScriptAsync(string invocationFile)
        {
            if (!fileSystem.File.Exists(invocationFile))
            {
                throw new Exception($"Invocation file {invocationFile} couldn't be found");
            }

            var parser = await expressNode.GetContractParameterParserAsync(chainManager.Chain).ConfigureAwait(false);
            return await parser.LoadInvocationScriptAsync(invocationFile).ConfigureAwait(false);
        }

        public async Task<Script> BuildInvocationScriptAsync(string contract, string operation, IReadOnlyList<string>? arguments = null)
        {
            if (string.IsNullOrEmpty(operation))
                throw new InvalidOperationException($"invalid contract operation \"{operation}\"");

            var parser = await expressNode.GetContractParameterParserAsync(chainManager.Chain).ConfigureAwait(false);
            var scriptHash = parser.TryLoadScriptHash(contract, out var value)
                ? value
                : UInt160.TryParse(contract, out var uint160)
                    ? uint160
                    : throw new InvalidOperationException($"contract \"{contract}\" not found");

            arguments ??= Array.Empty<string>();
            var @params = new ContractParameter[arguments.Count];
            for (int i = 0; i < arguments.Count; i++)
            {
                @params[i] = ConvertArg(arguments[i], parser);
            }

            using var scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitDynamicCall(scriptHash, operation, @params);
            return scriptBuilder.ToArray();

            static ContractParameter ConvertArg(string arg, ContractParameterParser parser)
            {
                if (bool.TryParse(arg, out var boolArg))
                {
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Boolean,
                        Value = boolArg
                    };
                }

                if (long.TryParse(arg, out var longArg))
                {
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Integer,
                        Value = new BigInteger(longArg)
                    };
                }

                return parser.ParseParameter(arg);
            }
        }

        public async Task ContractInvokeAsync(Script script, string accountName, string password, WitnessScope witnessScope, decimal additionalGas = 0m)
        {
            if (!chainManager.TryGetSigningAccount(accountName, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{accountName} account not found.");
            }

            var txHash = await expressNode.ExecuteAsync(wallet, accountHash, witnessScope, script, additionalGas).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, "Invocation", json).ConfigureAwait(false);
        }

        public async Task InvokeForResultsAsync(Script script, string accountName, WitnessScope witnessScope)
        {
            Signer? signer = chainManager.TryGetSigningAccount(accountName, string.Empty, out _, out var accountHash)
                ? signer = new Signer
                {
                    Account = accountHash,
                    Scopes = witnessScope,
                    AllowedContracts = Array.Empty<UInt160>(),
                    AllowedGroups = Array.Empty<Neo.Cryptography.ECC.ECPoint>()
                }
                : null;

            var result = await expressNode.InvokeAsync(script, signer).ConfigureAwait(false);
            if (json)
            {
                await writer.WriteLineAsync(result.ToJson().ToString(true)).ConfigureAwait(false);
            }
            else
            {
                await writer.WriteLineAsync($"VM State:     {result.State}").ConfigureAwait(false);
                await writer.WriteLineAsync($"Gas Consumed: {result.GasConsumed}").ConfigureAwait(false);
                if (!string.IsNullOrEmpty(result.Exception))
                {
                    await writer.WriteLineAsync($"Exception:   {result.Exception}").ConfigureAwait(false);
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
            if (!chainManager.TryGetSigningAccount(accountName, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{accountName} account not found.");
            }

            var oracles = chainManager.Chain.ConsensusNodes
                .Select(n => DevWalletAccount.FromExpressWalletAccount(chainManager.ProtocolSettings, n.Wallet.DefaultAccount ?? throw new Exception()))
                .Select(a => a.GetKey()?.PublicKey ?? throw new Exception());
            var txHash = await expressNode.DesignateOracleRolesAsync(wallet, accountHash, oracles).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, "Oracle Enable", json).ConfigureAwait(false);
        }

        public async Task OracleResponseAsync(string url, string responsePath, ulong? requestId = null)
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
            if (!chainManager.TryGetSigningAccount(sender, password, out var senderWallet, out var senderAccountHash))
            {
                throw new Exception($"{sender} sender not found.");
            }

            if (!chainManager.Chain.TryGetAccountHash(receiver, out var receiverHash))
            {
                throw new Exception($"{receiver} account not found.");
            }

            var assetHash = await expressNode.ParseAssetAsync(asset).ConfigureAwait(false);
            var txHash = await expressNode.TransferAsync(assetHash, ParseQuantity(quantity), senderWallet, senderAccountHash, receiverHash);
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

        public async Task SetPolicyAsync(PolicyName policy, BigInteger value, string account, string password)
        {
            if (!chainManager.TryGetSigningAccount(account, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{account} account not found.");
            }

            var txHash = await expressNode.SetPolicyAsync(wallet, accountHash, policy, value).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, $"{policy} Policy Set", json).ConfigureAwait(false);
        }

        public async Task BlockAsync(string scriptHash, string account, string password)
        {
            if (!chainManager.TryGetSigningAccount(account, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{account} account not found.");
            }

            var _scriptHash = await expressNode.ParseScriptHashToBlockAsync(chainManager.Chain, scriptHash).ConfigureAwait(false);
            if (_scriptHash.IsT1)
            {
                throw new Exception($"{scriptHash} script hash not found or not supported");
            }

            var txHash = await expressNode.BlockAccountAsync(wallet, accountHash, _scriptHash.AsT0).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, $"{scriptHash} blocked", json).ConfigureAwait(false);
        }

        public async Task UnblockAsync(string scriptHash, string account, string password)
        {
            if (!chainManager.TryGetSigningAccount(account, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{account} account not found.");
            }

            var _scriptHash = await expressNode.ParseScriptHashToBlockAsync(chainManager.Chain, scriptHash).ConfigureAwait(false);
            if (_scriptHash.IsT1)
            {
                throw new Exception($"{scriptHash} script hash not found or not supported");
            }

            var txHash = await expressNode.UnblockAccountAsync(wallet, accountHash, _scriptHash.AsT0).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, $"{scriptHash} unblocked", json).ConfigureAwait(false);
        }
    }
}
