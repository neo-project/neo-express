using System;
using System.Text;
using Neo;
using Neo.IO.Json;
using Neo.Persistence;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoExpress.Models
{
    public struct Nep17Contract
    {
        public readonly string Name;
        public readonly string Symbol;
        public readonly byte Decimals;
        public readonly UInt160 ScriptHash;

        public Nep17Contract(string name, string symbol, byte decimals, UInt160 scriptHash)
        {
            Name = name;
            Symbol = symbol;
            Decimals = decimals;
            ScriptHash = scriptHash;
        }

        public Nep17Contract(Nep17Token<AccountState> token)
        {
            Name = token.Name;
            Symbol = token.Symbol;
            Decimals = token.Decimals;
            ScriptHash = token.Hash;
        }

        public static Nep17Contract Unknown(UInt160 scriptHash) => new Nep17Contract("unknown", "unknown", 0, scriptHash);

        public static bool TryLoad(StoreView snapshot, UInt160 scriptHash, out Nep17Contract contract)
        {
            if (scriptHash == NativeContract.NEO.Hash)
            {
                contract = new Nep17Contract(
                    NativeContract.NEO.Name,
                    NativeContract.NEO.Symbol,
                    NativeContract.NEO.Decimals,
                    NativeContract.NEO.Hash);
                return true;
            }

            if (scriptHash == NativeContract.GAS.Hash)
            {
                contract = new Nep17Contract(
                    NativeContract.GAS.Name,
                    NativeContract.GAS.Symbol,
                    NativeContract.GAS.Decimals,
                    NativeContract.GAS.Hash);
                return true;
            }

            var contractState = NativeContract.Management.GetContract(snapshot, scriptHash);

            if (contractState != null)
            {
                using var sb = new ScriptBuilder();
                sb.EmitAppCall(scriptHash, "symbol");
                sb.EmitAppCall(scriptHash, "decimals");

                using var engine = Neo.SmartContract.ApplicationEngine.Run(sb.ToArray(), snapshot);
                if (engine.State != VMState.FAULT && engine.ResultStack.Count >= 2)
                {
                    var decimals = (byte)engine.ResultStack.Pop<Neo.VM.Types.Integer>().GetInteger();
                    var symbol = Encoding.UTF8.GetString(engine.ResultStack.Pop().GetSpan());
                    contract = new Nep17Contract(contractState.Manifest.Name, symbol, decimals, scriptHash);
                    return true;
                }
            }

            contract = default;
            return false;
        }

        public JObject ToJson()
        {
            var json = new JObject();
            json["scriptHash"] = ScriptHash.ToString();
            json["name"] = Name;
            json["symbol"] = Symbol;
            json["decimals"] = Decimals;
            return json;
        }

        public static Nep17Contract FromJson(JObject json)
        {
            var name = json["name"].AsString();
            var symbol = json["symbol"].AsString();
            var scriptHash = UInt160.Parse(json["scriptHash"].AsString());
            var decimals = (byte)json["decimals"].AsNumber();
            return new Nep17Contract(name, symbol, decimals, scriptHash);
        }
    }
}

