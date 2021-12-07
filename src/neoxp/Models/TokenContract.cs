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

        public static IEnumerable<(UInt160 scriptHash, TokenStandard standard)> Enumerate(DataCache snapshot)
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

                // Return contracts that declare either NEP-11 or NEP-17 but not both. Obviously, if
                // the contract doesn't specify that either standard is supported, skip it. However,
                // we also skip contracts that specify they support both NEP-11 and NEP-17. The transfer
                // operation has a different number of parameters for NEP-17, divisible NEP-11 and
                // non-divisible NEP-11 tokens, so it is impossible to implement NEP-11 and NEP-17
                // in the same contract

                if (nep11 != nep17)
                {
                    yield return (contract.Hash, standard: nep17
                        ? TokenStandard.Nep17
                        : TokenStandard.Nep11);
                }
            }
        }
    }
}

