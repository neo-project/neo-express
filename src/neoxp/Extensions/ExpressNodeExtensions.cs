using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;

namespace NeoExpress
{
    static class ExpressNodeExtensions
    {
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

        public static async Task<ContractParameterParser> GetContractParameterParserAsync(this IExpressNode expressNode, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            ContractParameterParser.TryGetUInt160 tryGetContract =
                (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) => TryGetContractHash(contracts, name, comparison, out scriptHash);
            return new ContractParameterParser(expressNode.ProtocolSettings, expressNode.Chain.TryResolveAccountHash, tryGetContract);

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
        }

        public static async Task<UInt256> SubmitTransactionAsync(this IExpressNode expressNode, Script script, string accountName, string password, WitnessScope witnessScope, decimal additionalGas = 0m)
        {
            var (wallet, accountHash) = expressNode.Chain.ResolveSigner(accountName, password);
            return await expressNode.ExecuteAsync(wallet, accountHash, witnessScope, script, additionalGas).ConfigureAwait(false);
        }

        public static async Task<RpcInvokeResult> InvokeForResultsAsync(this IExpressNode expressNode, Script script, string accountName, WitnessScope witnessScope)
        {
            Signer? signer = expressNode.Chain.TryResolveSigner(accountName, string.Empty, out _, out var accountHash)
                ? signer = new Signer
                {
                    Account = accountHash,
                    Scopes = witnessScope,
                    AllowedContracts = Array.Empty<UInt160>(),
                    AllowedGroups = Array.Empty<Neo.Cryptography.ECC.ECPoint>(),
                    Rules = Array.Empty<WitnessRule>()
                }
                : null;

            return await expressNode.InvokeAsync(script, signer).ConfigureAwait(false);
        }

        public static async Task<RpcInvokeResult> GetResultAsync(this IExpressNode expressNode, Script script)
        {
            var result = await expressNode.InvokeAsync(script).ConfigureAwait(false);
            if (result.State != VMState.HALT) throw new Exception(result.Exception ?? string.Empty);
            return result;
        }

        public static async Task<Script> BuildInvocationScriptAsync(this IExpressNode expressNode, string contract, string operation, IReadOnlyList<string>? arguments = null)
        {
            if (string.IsNullOrEmpty(operation))
                throw new InvalidOperationException($"invalid contract operation \"{operation}\"");

            var parser = await expressNode.GetContractParameterParserAsync().ConfigureAwait(false);
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
                        Value = new System.Numerics.BigInteger(longArg)
                    };
                }

                return parser.ParseParameter(arg);
            }
        }
    }
}
