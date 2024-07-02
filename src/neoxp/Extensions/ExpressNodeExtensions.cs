// Copyright (C) 2015-2024 The Neo Project.
//
// ExpressNodeExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using NeoExpress.Models;
using OneOf;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;
using All = OneOf.Types.All;
using None = OneOf.Types.None;

namespace NeoExpress
{
    static class ExpressNodeExtensions
    {
        static bool TryGetContractHash(IReadOnlyList<(UInt160 hash, ContractManifest manifest)> contracts, string name, out UInt160 scriptHash, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            UInt160? _scriptHash = null;
            for (int i = 0; i < contracts.Count; i++)
            {
                if (contracts[i].manifest.Name.Equals(name, comparison))
                {
                    if (_scriptHash is null)
                    {
                        _scriptHash = contracts[i].hash;
                    }
                    else
                    {
                        throw new Exception($"More than one deployed script named {name}");
                    }
                }
            }

            if (_scriptHash is not null)
            {
                scriptHash = _scriptHash;
                return true;
            }
            else
            {
                scriptHash = UInt160.Zero;
                return false;
            }
        }

        /// <summary>
        /// Find a contract by name. Output warning message to Console if 2 or more contracts were found, will use the last found contract as the result.
        /// </summary>
        /// <param name="contracts"></param>
        /// <param name="name"></param>
        /// <param name="scriptHash"></param>
        /// <param name="comparison"></param>
        /// <returns></returns>
        static bool TryGetContractHashByName(IReadOnlyList<(UInt160 hash, ContractManifest manifest)> contracts, string name, out UInt160 scriptHash, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            UInt160? _scriptHash = null;
            for (int i = contracts.Count - 1; i > 0; i--)
            {
                if (contracts[i].manifest.Name.Equals(name, comparison))
                {
                    if (_scriptHash is null)
                    {
                        _scriptHash = contracts[i].hash;
                    }
                    else
                    {
                        Extensions.WriteWarning($"More than one deployed contract named '{name}', using {_scriptHash}");
                        break;
                    }
                }
            }

            if (_scriptHash is not null)
            {
                scriptHash = _scriptHash;
                return true;
            }
            else
            {
                scriptHash = UInt160.Zero;
                return false;
            }
        }

        public static async Task<OneOf<UInt160, None>> ParseScriptHashToBlockAsync(this IExpressNode expressNode, ExpressChain chain, string name, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            // only check express chain wallets to block, not consensus nodes or genesis account
            if (chain.Wallets is not null && chain.Wallets.Count > 0)
            {
                for (int i = 0; i < chain.Wallets.Count; i++)
                {
                    if (string.Equals(name, chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return chain.Wallets[i].DefaultAccount.GetScriptHash();
                    }
                }
            }

            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            if (TryGetContractHash(contracts, name, out var contractHash, comparison))
            {
                // don't even try to block native contracts
                if (!NativeContract.Contracts.Any(c => c.Hash == contractHash))
                {
                    return contractHash;
                }
            }

            if (chain.TryParseScriptHash(name, out var scriptHash))
                return scriptHash;

            return new None();
        }

        public static async Task<ContractParameterParser> GetContractParameterParserAsync(this IExpressNode expressNode, ExpressChain chain, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            ContractParameterParser.TryGetUInt160 tryGetContract =
                (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) => TryGetContractHashByName(contracts, name, out scriptHash, comparison);

            return new ContractParameterParser(expressNode.ProtocolSettings, chain.TryGetAccountHash, tryGetContract);
        }

        public static async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync(this IExpressNode expressNode, string contractName, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            return contracts.Where(c => c.manifest.Name.Equals(contractName, comparison)).ToList();
        }

        public static async Task<OneOf<UInt160, None>> TryGetAccountHashAsync(this IExpressNode expressNode, ExpressChain chain, string name)
        {
            if (name.StartsWith('#'))
            {
                var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
                if (TryGetContractHash(contracts, name.Substring(1), out var contractHash))
                {
                    return contractHash;
                }
            }

            if (chain.TryGetAccountHash(name, out var accountHash))
            {
                return accountHash;
            }

            if (TryGetWIFAccountHash(name, out accountHash))
            {
                return accountHash;
            }

            return default(None);

            static bool TryGetWIFAccountHash(string wif, out UInt160 accountHash)
            {
                try
                {
                    var privateKey = Wallet.GetPrivateKeyFromWIF(wif);
                    var keyPair = new KeyPair(privateKey);
                    var contract = Contract.CreateSignatureContract(keyPair.PublicKey);
                    accountHash = contract.ScriptHash;
                    return true;
                }
                catch
                {
                    accountHash = UInt160.Zero;
                    return false;
                }
            }
        }

        public static async Task<UInt160> ParseAssetAsync(this IExpressNode expressNode, string asset)
        {
            if ("neo".Equals(asset, StringComparison.OrdinalIgnoreCase))
            {
                return NativeContract.NEO.Hash;
            }

            if ("gas".Equals(asset, StringComparison.OrdinalIgnoreCase))
            {
                return NativeContract.GAS.Hash;
            }

            if (UInt160.TryParse(asset, out var uint160))
            {
                return uint160;
            }

            var contracts = await expressNode.ListTokenContractsAsync().ConfigureAwait(false);
            for (int i = 0; i < contracts.Count; i++)
            {
                if (contracts[i].Standard == TokenStandard.Nep17
                    && contracts[i].Symbol.Equals(asset, StringComparison.OrdinalIgnoreCase))
                {
                    return contracts[i].ScriptHash;
                }
            }

            throw new ArgumentException($"Unknown Asset \"{asset}\"", nameof(asset));
        }

        public static async Task<UInt256> TransferAsync(this IExpressNode expressNode, UInt160 asset, OneOf<decimal, All> quantity, Wallet sender, UInt160 senderHash, UInt160 receiverHash, ContractParameter? data)
        {
            data ??= new ContractParameter(ContractParameterType.Any);

            if (quantity.IsT0)
            {
                var results = await expressNode.InvokeAsync(asset.MakeScript("decimals")).ConfigureAwait(false);
                if (results.Stack.Length > 0 && results.Stack[0].Type == Neo.VM.Types.StackItemType.Integer)
                {
                    var decimals = (byte)(results.Stack[0].GetInteger());
                    var value = quantity.AsT0.ToBigInteger(decimals);
                    var script = asset.MakeScript("transfer", senderHash, receiverHash, value, data);
                    return await expressNode.ExecuteAsync(sender, senderHash, WitnessScope.CalledByEntry, script).ConfigureAwait(false);
                }
                else
                {
                    throw new Exception("Invalid response from decimals operation");
                }
            }
            else
            {
                Debug.Assert(quantity.IsT1);

                using var sb = new ScriptBuilder();
                // balanceOf operation places current balance on eval stack
                sb.EmitDynamicCall(asset, "balanceOf", senderHash);
                // transfer operation takes 4 arguments, amount is 3rd parameter
                // push data parameter onto the stack then switch positions of the top
                // two items on eval stack so data is 4th arg and balance is 3rd
                sb.EmitPush(data);
                sb.Emit(OpCode.SWAP);
                sb.EmitPush(receiverHash);
                sb.EmitPush(senderHash);
                sb.EmitPush(4);
                sb.Emit(OpCode.PACK);
                sb.EmitPush(CallFlags.All);
                sb.EmitPush("transfer");
                sb.EmitPush(asset);
                sb.EmitSysCall(ApplicationEngine.System_Contract_Call);
                return await expressNode.ExecuteAsync(sender, senderHash, WitnessScope.CalledByEntry, sb.ToArray()).ConfigureAwait(false);
            }
        }

        public static async Task<UInt256> TransferNFTAsync(this IExpressNode expressNode, UInt160 contractHash, string tokenId, Wallet sender, UInt160 senderHash, UInt160 receiverHash, ContractParameter? data)
        {
            data ??= new ContractParameter(ContractParameterType.Any);
            var script = contractHash.MakeScript("transfer", receiverHash, tokenId, data);
            return await expressNode.ExecuteAsync(sender, senderHash, WitnessScope.CalledByEntry, script).ConfigureAwait(false);
        }

        public static async Task<UInt256> UpdateAsync(this IExpressNode expressNode,
                                                      UInt160 contractHash,
                                                      NefFile nefFile,
                                                      ContractManifest manifest,
                                                      Wallet wallet,
                                                      UInt160 accountHash,
                                                      WitnessScope witnessScope,
                                                      object? data = null)
        {
            CheckNefFile(nefFile);
            using var sb = new ScriptBuilder();
            if (data == null)
                sb.EmitDynamicCall(contractHash,
                    "update",
                    nefFile.ToArray(),
                    manifest.ToJson().ToString());
            else
                sb.EmitDynamicCall(contractHash,
                    "update",
                    nefFile.ToArray(),
                    manifest.ToJson().ToString(),
                    data);

            return await expressNode.ExecuteAsync(wallet, accountHash, witnessScope, sb.ToArray()).ConfigureAwait(false);
        }

        public static async Task<UInt256> DeployAsync(this IExpressNode expressNode,
                                                      NefFile nefFile,
                                                      ContractManifest manifest,
                                                      Wallet wallet,
                                                      UInt160 accountHash,
                                                      WitnessScope witnessScope,
                                                      ContractParameter? data)
        {
            CheckNefFile(nefFile);

            data ??= new ContractParameter(ContractParameterType.Any);
            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(NativeContract.ContractManagement.Hash,
                "deploy",
                nefFile.ToArray(),
                manifest.ToJson().ToString(),
                data);
            return await expressNode.ExecuteAsync(wallet, accountHash, witnessScope, sb.ToArray()).ConfigureAwait(false);
        }

        static void CheckNefFile(NefFile nefFile)
        {
            // check for bad opcodes (logic borrowed from neo-cli LoadDeploymentScript)
            Neo.VM.Script script = nefFile.Script;
            for (var i = 0; i < script.Length;)
            {
                var instruction = script.GetInstruction(i);
                if (instruction is null)
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
        }

        public static async Task<UInt256> DesignateOracleRolesAsync(this IExpressNode expressNode, Wallet wallet, UInt160 accountHash, IEnumerable<ECPoint> oracles)
        {
            var roleParam = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)(byte)Role.Oracle };
            var oraclesParam = new ContractParameter(ContractParameterType.Array)
            {
                Value = oracles
                    .Select(o => new ContractParameter(ContractParameterType.PublicKey) { Value = o })
                    .ToList()
            };

            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(NativeContract.RoleManagement.Hash, "designateAsRole", roleParam, oraclesParam);
            return await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, sb.ToArray()).ConfigureAwait(false);
        }

        public static async Task<IReadOnlyList<ECPoint>> ListOracleNodesAsync(this IExpressNode expressNode)
        {
            var lastBlock = await expressNode.GetLatestBlockAsync().ConfigureAwait(false);

            var role = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)(byte)Role.Oracle };
            var index = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)lastBlock.Index + 1 };

            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(NativeContract.RoleManagement.Hash, "getDesignatedByRole", role, index);
            var result = await expressNode.InvokeAsync(sb.ToArray()).ConfigureAwait(false);

            if (result.State == Neo.VM.VMState.HALT
                && result.Stack.Length >= 1
                && result.Stack[0] is Neo.VM.Types.Array array)
            {
                var nodes = new ECPoint[array.Count];
                for (var x = 0; x < array.Count; x++)
                {
                    nodes[x] = ECPoint.DecodePoint(array[x].GetSpan(), ECCurve.Secp256r1);
                }
                return nodes;
            }

            return System.Array.Empty<ECPoint>();
        }

        public static async Task<IReadOnlyList<UInt256>> SubmitOracleResponseAsync(this IExpressNode expressNode, string url, OracleResponseCode responseCode, Newtonsoft.Json.Linq.JObject? responseJson, ulong? requestId)
        {
            if (responseCode == OracleResponseCode.Success && responseJson is null)
            {
                throw new ArgumentException("responseJson cannot be null when responseCode is Success", nameof(responseJson));
            }

            var oracleNodes = await ListOracleNodesAsync(expressNode);

            var txHashes = new List<UInt256>();
            var requests = await expressNode.ListOracleRequestsAsync().ConfigureAwait(false);
            for (var x = 0; x < requests.Count; x++)
            {
                var (id, request) = requests[x];
                if (requestId.HasValue && requestId.Value != id)
                    continue;
                if (!string.Equals(url, request.Url, StringComparison.OrdinalIgnoreCase))
                    continue;

                var response = new OracleResponse
                {
                    Code = responseCode,
                    Id = id,
                    Result = GetResponseData(request.Filter),
                };

                var txHash = await expressNode.SubmitOracleResponseAsync(response, oracleNodes);
                txHashes.Add(txHash);
            }
            return txHashes;

            byte[] GetResponseData(string filter)
            {
                if (responseCode != OracleResponseCode.Success)
                {
                    return System.Array.Empty<byte>();
                }

                System.Diagnostics.Debug.Assert(responseJson is not null);

                var json = string.IsNullOrEmpty(filter)
                    ? (Newtonsoft.Json.Linq.JContainer)responseJson
                    : new Newtonsoft.Json.Linq.JArray(responseJson.SelectTokens(filter, true));
                return Neo.Utility.StrictUTF8.GetBytes(json.ToString());
            }
        }

        public static async Task<(RpcNep17Balance balance, Nep17Contract token)> GetBalanceAsync(this IExpressNode expressNode, UInt160 accountHash, string asset)
        {
            var assetHash = await expressNode.ParseAssetAsync(asset).ConfigureAwait(false);

            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(assetHash, "balanceOf", accountHash);
            sb.EmitDynamicCall(assetHash, "symbol");
            sb.EmitDynamicCall(assetHash, "decimals");

            var result = await expressNode.InvokeAsync(sb.ToArray()).ConfigureAwait(false);
            var stack = result.Stack;
            if (stack.Length >= 3)
            {
                var balance = stack[0].GetInteger();
                var symbol = Encoding.UTF8.GetString(stack[1].GetSpan());
                var decimals = (byte)(stack[2].GetInteger());

                return (
                    new RpcNep17Balance() { Amount = balance, AssetHash = assetHash },
                    new Nep17Contract(symbol, decimals, assetHash));
            }

            throw new Exception("invalid script results");
        }

        public static async Task<List<string>> GetNFTAsync(this IExpressNode expressNode, UInt160 accountHash, UInt160 assetHash)
        {
            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(assetHash, "tokensOf", accountHash);

            var result = await expressNode.InvokeAsync(sb.ToArray()).ConfigureAwait(false);
            var stack = result.Stack;
            var list = new List<string>();
            try
            {
                if (result.State != VMState.FAULT
                        && result.Stack.Length >= 1
                        && result.Stack[0] is InteropInterface interop
                        && interop.GetInterface<object>() is IIterator iterator)
                {
                    while (iterator.Next())
                    {
                        list.Add(Convert.ToBase64String(iterator.Value(null).GetSpan()));
                    }
                }
            }
            catch (Exception)
            {
                throw new Exception("invalid script results");
            }
            return list;
        }

        public static async Task<Block> GetBlockAsync(this IExpressNode expressNode, string blockHash)
        {
            if (string.IsNullOrEmpty(blockHash))
            {
                return await expressNode.GetLatestBlockAsync().ConfigureAwait(false);
            }

            if (UInt256.TryParse(blockHash, out var uint256))
            {
                return await expressNode.GetBlockAsync(uint256).ConfigureAwait(false);
            }

            if (uint.TryParse(blockHash, out var index))
            {
                return await expressNode.GetBlockAsync(index).ConfigureAwait(false);
            }

            throw new ArgumentException($"{nameof(blockHash)} must be block index, block hash or empty", nameof(blockHash));
        }

        internal static async Task<PolicyValues> GetPolicyAsync(Func<Script, Task<RpcInvokeResult>> invokeAsync)
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

            if (result.State != VMState.HALT)
                throw new Exception(result.Exception);
            if (result.Stack.Length != 7)
                throw new InvalidOperationException();

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

        public static Task<PolicyValues> GetPolicyAsync(this IExpressNode expressNode)
        {
            return GetPolicyAsync(script => expressNode.InvokeAsync(script));
        }

        public static async Task<bool> GetIsBlockedAsync(this IExpressNode expressNode, UInt160 scriptHash)
        {
            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(NativeContract.Policy.Hash, "isBlocked", scriptHash);
            var result = await expressNode.InvokeAsync(sb.ToArray()).ConfigureAwait(false);
            var stack = result.Stack;
            if (stack.Length >= 1)
            {
                var value = stack[0].GetBoolean();
                return value;
            }

            throw new Exception("invalid script results");
        }

        public static async Task<UInt256> BlockAccountAsync(this IExpressNode expressNode, Wallet wallet, UInt160 accountHash, UInt160 scriptHash)
        {
            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(NativeContract.Policy.Hash, "blockAccount", scriptHash);
            return await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, sb.ToArray()).ConfigureAwait(false);
        }

        public static async Task<UInt256> UnblockAccountAsync(this IExpressNode expressNode, Wallet wallet, UInt160 accountHash, UInt160 scriptHash)
        {
            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(NativeContract.Policy.Hash, "unblockAccount", scriptHash);
            return await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, sb.ToArray()).ConfigureAwait(false);
        }
    }
}
