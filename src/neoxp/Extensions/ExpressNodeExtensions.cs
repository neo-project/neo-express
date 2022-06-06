using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;
using OneOf;

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
            await Task.FromException(new NotImplementedException());
            return null!;
            // var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            // ContractParameterParser.TryGetUInt160 tryGetContract =
            //     (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) => TryGetContractHash(contracts, name, comparison, out scriptHash);

            // return new ContractParameterParser(expressNode.ProtocolSettings, expressNode.TryResolveAccountHash, tryGetContract);
        }

        public static async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsByNameAsync(this IExpressNode expressNode, string name, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            return contracts.Where(c => c.manifest.Name.Equals(name, comparison)).ToList();
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
    }
}
