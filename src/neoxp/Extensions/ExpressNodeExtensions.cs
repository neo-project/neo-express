using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using All = OneOf.Types.All;

namespace NeoExpress
{
    static class ExpressNodeExtensions
    {
        public static async Task<ContractParameterParser.TryGetUInt160> GetTryGetContractFunctionAsync(this IExpressNode expressNode, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            ContractParameterParser.TryGetUInt160 tryGetContract = (string name, out UInt160 scriptHash) =>
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
            };            
            return tryGetContract;
        }

        public static async Task<ContractParameterParser> GetContractParameterParserAsync(this IExpressNode expressNode, IExpressChainManager chainManager, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            var tryGetContract = await expressNode.GetTryGetContractFunctionAsync(comparison).ConfigureAwait(false);
            return new ContractParameterParser(expressNode.ProtocolSettings, chainManager.Chain.TryGetAccountHash, tryGetContract);
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

            var contracts = await expressNode.ListNep17ContractsAsync().ConfigureAwait(false);
            for (int i = 0; i < contracts.Count; i++)
            {
                if (contracts[i].Symbol.Equals(asset, StringComparison.OrdinalIgnoreCase))
                {
                    return contracts[i].ScriptHash;
                }
            }

            throw new ArgumentException($"Unknown Asset \"{asset}\"", nameof(asset));
        }

        public static async Task<UInt256> TransferAsync(this IExpressNode expressNode, UInt160 asset, OneOf<decimal, All> quantity, Wallet sender, UInt160 senderHash, UInt160 receiverHash)
        {
            if (quantity.IsT0)
            {
                var results = await expressNode.InvokeAsync(asset.MakeScript("decimals")).ConfigureAwait(false);
                if (results.Stack.Length > 0 && results.Stack[0].Type == Neo.VM.Types.StackItemType.Integer)
                {
                    var decimals = (byte)(results.Stack[0].GetInteger());
                    var value = quantity.AsT0.ToBigInteger(decimals);
                    var script = asset.MakeScript("transfer", senderHash, receiverHash, value, null);
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
                // push null onto the stack and then switch positions of the top
                // two items on eval stack so null is 4th arg and balance is 3rd
                sb.Emit(OpCode.PUSHNULL);
                sb.Emit(OpCode.SWAP);
                sb.EmitPush(receiverHash);
                sb.EmitPush(senderHash);
                sb.EmitPush(4);
                sb.Emit(OpCode.PACK);
                sb.EmitPush("transfer");
                sb.EmitPush(asset);
                sb.EmitSysCall(ApplicationEngine.System_Contract_Call);
                return await expressNode.ExecuteAsync(sender, senderHash, WitnessScope.CalledByEntry, sb.ToArray()).ConfigureAwait(false);
            }
        }

        public static async Task<UInt256> DeployAsync(this IExpressNode expressNode, NefFile nefFile, ContractManifest manifest, Wallet wallet, UInt160 accountHash, WitnessScope witnessScope)
        {
            // check for bad opcodes (logic borrowed from neo-cli LoadDeploymentScript)
            Neo.VM.Script script = nefFile.Script;
            for (var i = 0; i < script.Length;)
            {
                var instruction = script.GetInstruction(i);
                if (instruction == null)
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

            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(NativeContract.ContractManagement.Hash, "deploy", nefFile.ToArray(), manifest.ToJson().ToString());
            return await expressNode.ExecuteAsync(wallet, accountHash, witnessScope, sb.ToArray()).ConfigureAwait(false);
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

        public static async Task<BigInteger> GetPolicyAsync(this IExpressNode expressNode, PolicyName policy)
        {
            var methodName = policy switch
            {
                PolicyName.ExecFeeFactor => "getExecFeeFactor",
                PolicyName.FeePerByte => "getFeePerByte",
                PolicyName.StoragePrice => "getStoragePrice", 
                _ => throw new ArgumentException($"Invalid Policy {policy}", nameof(policy))
            };

            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(NativeContract.Policy.Hash, methodName);

            var result = await expressNode.InvokeAsync(sb.ToArray()).ConfigureAwait(false);
            var stack = result.Stack;
            if (stack.Length >= 1)
            {
                var value = stack[0].GetInteger();
                return value;
            }

            throw new Exception("invalid script results");
        }

        public static async Task<UInt256> SetPolicyAsync(this IExpressNode expressNode, Wallet wallet, UInt160 accountHash, PolicyName policy, BigInteger value)
        {
            var methodName = policy switch
            {
                PolicyName.ExecFeeFactor => "setExecFeeFactor",
                PolicyName.FeePerByte => "setFeePerByte",
                PolicyName.StoragePrice => "setStoragePrice", 
                _ => throw new ArgumentException($"Invalid Policy {policy}", nameof(policy))
            };

            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(NativeContract.Policy.Hash, methodName, value);
            return await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, sb.ToArray()).ConfigureAwait(false);
        }
    }
}
