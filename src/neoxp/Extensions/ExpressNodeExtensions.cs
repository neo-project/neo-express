using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;
using OneOf;
using OneOf.Types;

namespace NeoExpress
{
    static class ExpressNodeExtensions
    {
        public static Task<RpcInvokeResult> GetResultAsync(this IExpressNode expressNode, ScriptBuilder builder)
            => expressNode.GetResultAsync(builder.ToArray());

        public static async Task<RpcInvokeResult> GetResultAsync(this IExpressNode expressNode, Script script)
        {
            var result = await expressNode.InvokeAsync(script).ConfigureAwait(false);
            if (result.State != VMState.HALT) throw new Exception(result.Exception ?? string.Empty);
            return result;
        }

        static bool TryGetContractHash(IReadOnlyList<(UInt160 hash, ContractManifest manifest)> contracts, string name, StringComparison comparison, out UInt160 scriptHash)
        {
            UInt160? _scriptHash = null;
            for (int i = 0; i < contracts.Count; i++)
            {
                if (contracts[i].manifest.Name.Equals(name, comparison))
                {
                    if (_scriptHash == null)
                    {
                        _scriptHash = contracts[i].hash;
                    }
                    else
                    {
                        throw new Exception($"More than one deployed script named {name}");
                    }
                }
            }

            scriptHash = _scriptHash!;
            return _scriptHash != null;
        }

        public static async Task<OneOf<UInt160,OneOf.Types.NotFound>> TryGetContractHashAsync(this IExpressNode expressNode, string name)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            if (TryGetContractHash(contracts, name, StringComparison.OrdinalIgnoreCase, out var contractHash))
            {
                return contractHash;
            }
            return default(OneOf.Types.NotFound);
        }

        public static async Task<ContractParameterParser> GetContractParameterParserAsync(this IExpressNode expressNode, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            ContractParameterParser.TryGetUInt160 tryGetContract =
                (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) => TryGetContractHash(contracts, name, comparison, out scriptHash);

            return new ContractParameterParser(expressNode.ProtocolSettings, expressNode.Chain.TryResolveAccountHash, tryGetContract);
        }

        public static async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync(this IExpressNode expressNode, string contractName, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            return contracts.Where(c => c.manifest.Name.Equals(contractName, comparison)).ToList();
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
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(NativeContract.Ledger.Hash, "currentIndex");
            var indexResult = await expressNode.InvokeAsync(builder.ToArray()).ConfigureAwait(false);
            if (indexResult.State != VMState.HALT) throw new Exception(indexResult.Exception ?? string.Empty);
            var currentIndex = indexResult.Stack[0].GetInteger();

            var role = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)(byte)Role.Oracle };
            var index = new ContractParameter(ContractParameterType.Integer) { Value = currentIndex + 1 };

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

            return Array.Empty<ECPoint>();
        }

        public static async Task<IReadOnlyList<UInt256>> SubmitOracleResponseAsync(this IExpressNode expressNode, string url, OracleResponseCode responseCode, Newtonsoft.Json.Linq.JObject? responseJson, ulong? requestId)
        {
            if (responseCode == OracleResponseCode.Success && responseJson == null)
            {
                throw new ArgumentException("responseJson cannot be null when responseCode is Success", nameof(responseJson));
            }

            var oracleNodes = await ListOracleNodesAsync(expressNode);

            var txHashes = new List<UInt256>();
            var requests = await expressNode.ListOracleRequestsAsync().ConfigureAwait(false);
            for (var x = 0; x < requests.Count; x++)
            {
                var (id, request) = requests[x];
                if (requestId.HasValue && requestId.Value != id) continue;
                if (!string.Equals(url, request.Url, StringComparison.OrdinalIgnoreCase)) continue;

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
                    return Array.Empty<byte>();
                }

                System.Diagnostics.Debug.Assert(responseJson != null);

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

        public static Task<PolicyValues> GetPolicyAsync(this RpcClient rpcClient)
        {
            return ExpressNodeExtensions.GetPolicyAsync(script => rpcClient.InvokeScriptAsync(script));
        }

        static async Task<PolicyValues> GetPolicyAsync(Func<Script, Task<RpcInvokeResult>> invokeAsync)
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
