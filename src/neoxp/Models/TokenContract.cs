using System.Collections.Generic;
using Neo;
using Neo.IO.Json;
using Neo.Persistence;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoExpress.Models
{
    public enum TokenStandard
    {
        Nep11,
        Nep17
    }

    public struct TokenContract
    {
        public readonly string Symbol;
        public readonly byte Decimals;
        public readonly UInt160 ScriptHash;
        public readonly TokenStandard Standard;

        public TokenContract(string symbol, byte decimals, UInt160 scriptHash, TokenStandard standard)
        {
            Symbol = symbol;
            Decimals = decimals;
            ScriptHash = scriptHash;
            Standard = standard;
        }

        public JObject ToJson()
        {
            var json = new JObject();
            json["scriptHash"] = ScriptHash.ToString();
            json["symbol"] = Symbol;
            json["decimals"] = Decimals;
            json["standard"] = (byte)Standard;
            return json;
        }

        public static TokenContract FromJson(JObject json)
        {
            var symbol = json["symbol"].AsString();
            var scriptHash = UInt160.Parse(json["scriptHash"].AsString());
            var decimals = (byte)json["decimals"].AsNumber();
            var standard = (TokenStandard)(byte)json["standard"].AsNumber();
            return new TokenContract(symbol, decimals, scriptHash, standard);
        }

        static IEnumerable<(UInt160 contractHash, TokenStandard standard)> GetTokenContracts(DataCache snapshot)
        {
            foreach (var contract in NativeContract.ContractManagement.ListContracts(snapshot))
            {
                var nep11 = false; var nep17 = false;

                var standards = contract.Manifest.SupportedStandards;
                for (var i = 0; i < standards.Length; i++)
                {
                    if (standards[i] == "NEP-11") nep11 = true;
                    if (standards[i] == "NEP-17") nep17 = true;
                }

                // return contracts that declare either NEP-11 or NEP-17 but not both.
                // The standard transfer operation has a different number of parameters
                // for NEP-17, divisible NEP-11 and non-divisible NEP-11, so ignore any
                // contract that claims to implement NEP-11 and NEP-17
                if (nep11 != nep17)
                {
                    yield return (contract.Hash, standard: nep17
                        ? TokenStandard.Nep17
                        : TokenStandard.Nep11);
                }
            }
        }
        
        public static IEnumerable<TokenContract> GetTokenContracts(NeoSystem neoSystem) => GetTokenContracts(neoSystem.StoreView, neoSystem.Settings);

        public static IEnumerable<TokenContract> GetTokenContracts(DataCache snapshot, ProtocolSettings settings)
        {
            foreach (var (contractHash, standard) in GetTokenContracts(snapshot))
            {
                if (TryLoadTokenInfo(contractHash, snapshot, settings, out var info))
                {
                    yield return new TokenContract(info.symbol, info.decimals, contractHash, standard);
                }
            }

            static bool TryLoadTokenInfo(UInt160 scriptHash, DataCache snapshot, ProtocolSettings settings, out (string symbol, byte decimals) info)
            {
                if (scriptHash == NativeContract.NEO.Hash)
                {
                    info = (NativeContract.NEO.Symbol, NativeContract.NEO.Decimals);
                    return true;
                }

                if (scriptHash == NativeContract.GAS.Hash)
                {
                    info = (NativeContract.GAS.Symbol, NativeContract.GAS.Decimals);
                    return true;
                }

                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(scriptHash, "symbol");
                builder.EmitDynamicCall(scriptHash, "decimals");
                using var engine = builder.Invoke(settings, snapshot);
                if (engine.State != VMState.FAULT && engine.ResultStack.Count == 2)
                {
                    var decimals = (byte)engine.ResultStack.Pop().GetInteger();
                    var symbol = engine.ResultStack.Pop().GetString();
                    if (symbol != null)
                    {
                        info = (symbol, decimals);
                        return true;
                    }
                }

                info = default;
                return false;
            }
        }
    }
}

