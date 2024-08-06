// Copyright (C) 2015-2024 The Neo Project.
//
// TransactionExecutor.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneOf;
using OneOf.Types;
using System.IO.Abstractions;
using System.Numerics;
using static Neo.BlockchainToolkit.Constants;
using static Neo.BlockchainToolkit.Utility;

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

        public async Task ContractUpdateAsync(string contract, string nefFilePath, string accountName, string password, WitnessScope witnessScope, object? data = null)
        {
            if (!chainManager.TryGetSigningAccount(accountName, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{accountName} account not found.");
            }

            var parser = await expressNode.GetContractParameterParserAsync(chainManager.Chain).ConfigureAwait(false);
            var scriptHash = parser.TryLoadScriptHash(contract, out var value)
                ? value
                : UInt160.TryParse(contract, out var uint160)
                    ? uint160
                    : throw new InvalidOperationException($"contract \"{contract}\" not found");

            var originalManifest = await expressNode.GetContractAsync(scriptHash).ConfigureAwait(false);
            var updateMethod1 = originalManifest.Abi.GetMethod("update", 2);
            var updateMethod2 = originalManifest.Abi.GetMethod("update", 3);
            if (updateMethod1 == null && updateMethod2 == null)
            {
                throw new Exception($"update method on {contract} contract not found.");
            }
            if ((updateMethod1?.Parameters[0].Type != ContractParameterType.ByteArray || updateMethod1?.Parameters[1].Type != ContractParameterType.String) &&
                (updateMethod2?.Parameters[0].Type != ContractParameterType.ByteArray || updateMethod2?.Parameters[1].Type != ContractParameterType.String))
            {
                throw new Exception($"update method on {contract} contract has unexpected signature.");
            }

            var (nefFile, manifest) = await fileSystem.LoadContractAsync(nefFilePath).ConfigureAwait(false);
            var txHash = await expressNode
                .UpdateAsync(scriptHash, nefFile, manifest, wallet, accountHash, witnessScope, data)
                .ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, "Update", json).ConfigureAwait(false);
        }

        public async Task ContractDeployAsync(string contract, string accountName, string password, WitnessScope witnessScope, string data, bool force)
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

                var nep11 = false;
                var nep17 = false;
                var standards = manifest.SupportedStandards;
                for (var i = 0; i < standards.Length; i++)
                {
                    if (standards[i] == "NEP-11")
                        nep11 = true;
                    if (standards[i] == "NEP-17")
                        nep17 = true;
                }

                if (nep11 && nep17)
                {
                    throw new Exception($"{manifest.Name} Contract declares support for both NEP-11 and NEP-17 standards. Use --force to deploy contract with invalid supported standards declarations.");
                }

                if (nep17 && manifest.IsNep17Compliant() == false)
                {
                    throw new Exception($"{manifest.Name} Contract declares support for NEP-17 standards. However is not NEP-17 compliant. Invalid methods/events.");
                }

                if (nep11 && manifest.IsNep11Compliant() == false)
                {
                    throw new Exception($"{manifest.Name} Contract declares support for NEP-11 standards. However is not NEP-11 compliant. Invalid methods/events.");
                }
            }

            ContractParameter dataParam;
            if (string.IsNullOrEmpty(data))
            {
                dataParam = new ContractParameter(ContractParameterType.Any);
            }
            else
            {
                var parser = await expressNode.GetContractParameterParserAsync(chainManager.Chain).ConfigureAwait(false);
                dataParam = parser.ParseParameter(data);
            }

            var txHash = await expressNode
                .DeployAsync(nefFile, manifest, wallet, accountHash, witnessScope, dataParam)
                .ConfigureAwait(false);

            var contractHash = Neo.SmartContract.Helper.GetContractHash(accountHash, nefFile.CheckSum, manifest.Name);
            if (json)
            {
                using var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
                await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                await jsonWriter.WritePropertyNameAsync("contract-name").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync(manifest.Name).ConfigureAwait(false);
                await jsonWriter.WritePropertyNameAsync("contract-hash").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync($"{contractHash}").ConfigureAwait(false);
                await jsonWriter.WritePropertyNameAsync("tx-hash").ConfigureAwait(false);
                await jsonWriter.WriteValueAsync($"{txHash}").ConfigureAwait(false);
                await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
            }
            else
            {
                await writer.WriteLineAsync($"Deployment of {manifest.Name} ({contractHash}) Transaction {txHash} submitted").ConfigureAwait(false);
            }
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

        public ContractParameter ContractParameterParser(string parameterJson)
        {
            var parser = new ContractParameterParser(expressNode.ProtocolSettings, chainManager.Chain.TryGetAccountHash);
            return parser.ParseParameter(parameterJson);
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

                try
                {
                    return parser.ParseParameter(JToken.Parse(arg));
                }
                catch
                {
                    return parser.ParseParameter(arg);
                }
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
                        await WriteLineAsync(Neo.Extensions.ByteExtensions.ToHexString(buffer.GetSpan())).ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.ByteString byteString:
                        if (byteString.GetSpan().TryGetUtf8String(out var text))
                        {
                            await WriteLineAsync($"{Neo.Extensions.ByteExtensions.ToHexString(byteString.GetSpan())}({text.EscapeString()})").ConfigureAwait(false);
                        }
                        else
                        {
                            await WriteLineAsync(Neo.Extensions.ByteExtensions.ToHexString(byteString.GetSpan())).ConfigureAwait(false);
                        }
                        break;
                    case Neo.VM.Types.Null _:
                        await WriteLineAsync("<null>").ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.Array array:
                        if (item is Neo.VM.Types.Struct)
                            await WriteLineAsync($"Struct: ({array.Count})").ConfigureAwait(false);
                        else
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
                    case Neo.VM.Types.InteropInterface iop:
                        if (iop.GetInterface<object>() is IIterator iter)
                        {
                            await WriteLineAsync($"{iop.Type}: ({iter.GetType().Name})").ConfigureAwait(false);
                            while (iter.Next())
                                await WriteStackItemAsync(writer, iter.Value(null), indent + 1).ConfigureAwait(false);
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
            if (!fileSystem.File.Exists(responsePath))
                throw new Exception($"Response File {responsePath} couldn't be found");

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

        public async Task TransferAsync(string quantity, string asset, string sender, string password, string receiver, string data)
        {
            if (!chainManager.TryGetSigningAccount(sender, password, out var senderWallet, out var senderAccountHash))
            {
                throw new Exception($"{sender} sender not found.");
            }

            var getHashResult = await expressNode.TryGetAccountHashAsync(chainManager.Chain, receiver).ConfigureAwait(false);
            if (getHashResult.TryPickT1(out _, out var receiverHash))
            {
                throw new Exception($"{receiver} account not found.");
            }

            ContractParameter? dataParam = null;
            if (!string.IsNullOrEmpty(data))
            {
                var parser = await expressNode.GetContractParameterParserAsync(chainManager.Chain).ConfigureAwait(false);
                dataParam = parser.ParseParameter(data);
            }

            var assetHash = await expressNode.ParseAssetAsync(asset).ConfigureAwait(false);
            var txHash = await expressNode.TransferAsync(assetHash, ParseQuantity(quantity), senderWallet, senderAccountHash, receiverHash, dataParam);
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

        public async Task TransferNFTAsync(string contract, string tokenId, string sender, string password, string receiver, string data)
        {
            if (!chainManager.TryGetSigningAccount(sender, password, out var senderWallet, out var senderAccountHash))
            {
                throw new Exception($"{sender} sender not found.");
            }

            if (!UInt160.TryParse(receiver, out var receiverHash)) //script hash
            {
                if (!chainManager.Chain.TryParseScriptHash(receiver, out receiverHash)) //address
                {
                    var getHashResult = await expressNode.TryGetAccountHashAsync(chainManager.Chain, receiver).ConfigureAwait(false); //wallet name
                    if (getHashResult.TryPickT1(out _, out receiverHash))
                    {
                        throw new Exception($"{receiver} account not found.");
                    }
                }
            }

            ContractParameter? dataParam = null;
            if (!string.IsNullOrEmpty(data))
            {
                var parser = await expressNode.GetContractParameterParserAsync(chainManager.Chain).ConfigureAwait(false);
                dataParam = parser.ParseParameter(data);
            }
            var parser2 = await expressNode.GetContractParameterParserAsync(chainManager.Chain).ConfigureAwait(false);
            var assetHash = parser2.TryLoadScriptHash(contract, out var value)
                ? value
                : UInt160.TryParse(contract, out var uint160)
                    ? uint160
                    : throw new InvalidOperationException($"contract \"{contract}\" not found");
            var txHash = await expressNode.TransferNFTAsync(assetHash, tokenId, senderWallet, senderAccountHash, receiverHash, dataParam);
            await writer.WriteTxHashAsync(txHash, "TransferNFT", json).ConfigureAwait(false);
        }

        public async Task RegisterCandidateAsync(string account, string password)
        {
            if (!chainManager.TryGetSigningAccount(account, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{account} account not found.");
            }
            var publicKey = wallet.GetAccount(accountHash).GetKey()?.PublicKey;
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(NativeContract.NEO.Hash, "registerCandidate", publicKey);

            var txHash = await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray()).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, $"Register Candidate", json).ConfigureAwait(false);
        }

        public async Task UnregisterCandidateAsync(string account, string password)
        {
            if (!chainManager.TryGetSigningAccount(account, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{account} account not found.");
            }
            var publicKey = wallet.GetAccount(accountHash).GetKey()?.PublicKey;
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(NativeContract.NEO.Hash, "unregisterCandidate", publicKey);

            var txHash = await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray()).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, $"Unregister Candidate", json).ConfigureAwait(false);
        }

        public async Task VoteAsync(string account, string? publicKey, string password)
        {
            if (!chainManager.TryGetSigningAccount(account, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{account} account not found.");
            }

            using var builder = new ScriptBuilder();
            if (!string.IsNullOrEmpty(publicKey))
            {
                if (!ECPoint.TryParse(publicKey, ECCurve.Secp256r1, out ECPoint point))
                {
                    throw new Exception($"PublicKey is not valid.");
                }
                builder.EmitDynamicCall(NativeContract.NEO.Hash, "vote", accountHash, point);
            }
            else
            {
                builder.EmitDynamicCall(NativeContract.NEO.Hash, "vote", accountHash, null);
            }

            var txHash = await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray()).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, $"Vote/Unvote", json).ConfigureAwait(false);
        }

        public async Task<List<string>> ListCandidatesAsync()
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(NativeContract.NEO.Hash, "getCandidates");

            var result = await expressNode.InvokeAsync(builder.ToArray()).ConfigureAwait(false);
            var stack = result.Stack;
            var list = new List<string>();
            try
            {
                if (result.State != VMState.FAULT
                        && result.Stack.Length >= 1
                        && result.Stack[0] is Neo.VM.Types.Array array)
                {
                    for (var i = 0; i < array.Count; i++)
                    {
                        var value = (Neo.VM.Types.Array)array[i];
                        list.Add($"{((Neo.VM.Types.ByteString?)value?[0])?.GetSpan().ToHexString(),-67}{((Neo.VM.Types.Integer?)value?[1])?.GetInteger()}");
                    }
                }
            }
            catch (Exception)
            {
                throw new Exception("invalid script results");
            }
            return list;
        }

        public async Task<OneOf<PolicyValues, None>> TryGetRemoteNetworkPolicyAsync(string rpcUri)
        {
            if (TryParseRpcUri(rpcUri, out var uri))
            {
                using var rpcClient = new RpcClient(uri);
                return await rpcClient.GetPolicyAsync().ConfigureAwait(false);
            }

            return default(None);
        }

        public async Task<OneOf<PolicyValues, None>> TryLoadPolicyFromFileSystemAsync(string filename)
        {
            filename = fileSystem.ResolveFileName(filename, JSON_EXTENSION, () => DEAULT_POLICY_FILENAME);
            if (fileSystem.File.Exists(filename))
            {
                using var stream = fileSystem.File.OpenRead(filename);
                using var reader = new StreamReader(stream);
                var text = await reader.ReadToEndAsync().ConfigureAwait(false);
                var json = (Neo.Json.JObject)Neo.Json.JObject.Parse(text)!;
                try
                {
                    return PolicyValues.FromJson(json!);
                }
                catch { }
            }

            return new None();
        }

        public async Task SetPolicyAsync(PolicyValues policyValues, string account, string password)
        {
            if (!chainManager.TryGetSigningAccount(account, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{account} account not found.");
            }

            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(NativeContract.NEO.Hash, "setGasPerBlock", policyValues.GasPerBlock.Value);
            builder.EmitDynamicCall(NativeContract.ContractManagement.Hash, "setMinimumDeploymentFee", policyValues.MinimumDeploymentFee.Value);
            builder.EmitDynamicCall(NativeContract.NEO.Hash, "setRegisterPrice", policyValues.CandidateRegistrationFee.Value);
            builder.EmitDynamicCall(NativeContract.Oracle.Hash, "setPrice", policyValues.OracleRequestFee.Value);
            builder.EmitDynamicCall(NativeContract.Policy.Hash, "setFeePerByte", policyValues.NetworkFeePerByte.Value);
            builder.EmitDynamicCall(NativeContract.Policy.Hash, "setStoragePrice", policyValues.StorageFeeFactor);
            builder.EmitDynamicCall(NativeContract.Policy.Hash, "setExecFeeFactor", policyValues.ExecutionFeeFactor);

            var txHash = await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray()).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, $"Policies Set", json).ConfigureAwait(false);
        }

        public async Task SetPolicyAsync(PolicySettings policy, decimal value, string account, string password)
        {
            if (!chainManager.TryGetSigningAccount(account, password, out var wallet, out var accountHash))
            {
                throw new Exception($"{account} account not found.");
            }

            var (hash, operation) = policy switch
            {
                PolicySettings.GasPerBlock => (NativeContract.NEO.Hash, "setGasPerBlock"),
                PolicySettings.MinimumDeploymentFee => (NativeContract.ContractManagement.Hash, "setMinimumDeploymentFee"),
                PolicySettings.CandidateRegistrationFee => (NativeContract.NEO.Hash, "setRegisterPrice"),
                PolicySettings.OracleRequestFee => (NativeContract.Oracle.Hash, "setPrice"),
                PolicySettings.NetworkFeePerByte => (NativeContract.Policy.Hash, "setFeePerByte"),
                PolicySettings.StorageFeeFactor => (NativeContract.Policy.Hash, "setStoragePrice"),
                PolicySettings.ExecutionFeeFactor => (NativeContract.Policy.Hash, "setExecFeeFactor"),
                _ => throw new InvalidOperationException($"Unknown policy {policy}"),
            };


            // Calculate decimal count : https://stackoverflow.com/a/13493771/1179731
            int decimalCount = BitConverter.GetBytes(decimal.GetBits(value)[3])[2];
            var decimalValue = new BigDecimal(value, (byte)decimalCount);
            using var builder = new ScriptBuilder();
            if (GasPolicySetting(policy))
            {
                if (decimalValue.Decimals > NativeContract.GAS.Decimals)
                    throw new InvalidOperationException($"{policy} policy requires a value with no more than eight decimal places");
                decimalValue = decimalValue.ChangeDecimals(NativeContract.GAS.Decimals);
                builder.EmitDynamicCall(hash, operation, decimalValue.Value);
            }
            else
            {
                if (decimalCount != 0)
                    throw new InvalidOperationException($"{policy} policy requires a whole number value");
                if (decimalValue.Value > uint.MaxValue)
                    throw new InvalidOperationException($"{policy} policy requires a value less than {uint.MaxValue}");
                builder.EmitDynamicCall(hash, operation, (uint)decimalValue.Value);
            }

            var txHash = await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray()).ConfigureAwait(false);
            await writer.WriteTxHashAsync(txHash, $"{policy} Policy Set", json).ConfigureAwait(false);

            static bool GasPolicySetting(PolicySettings policy)
            {
                switch (policy)
                {
                    case PolicySettings.GasPerBlock:
                    case PolicySettings.MinimumDeploymentFee:
                    case PolicySettings.CandidateRegistrationFee:
                    case PolicySettings.OracleRequestFee:
                    case PolicySettings.NetworkFeePerByte:
                        return true;
                    default:
                        return false;
                }
            }
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
