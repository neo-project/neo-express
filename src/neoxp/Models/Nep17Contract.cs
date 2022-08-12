using System;
using System.Text;
using Neo;
using Neo.Json;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoExpress.Models
{
    public struct Nep17Contract
    {
        public readonly string Symbol;
        public readonly byte Decimals;
        public readonly UInt160 ScriptHash;

        public Nep17Contract(string symbol, byte decimals, UInt160 scriptHash)
        {
            Symbol = symbol;
            Decimals = decimals;
            ScriptHash = scriptHash;
        }

        public static Nep17Contract Create<TState>(FungibleToken<TState> token)
            where TState : AccountState, new()
        {
            return new Nep17Contract(token.Symbol, token.Decimals, token.Hash);
        }

        public static Nep17Contract Unknown(UInt160 scriptHash) => new Nep17Contract("unknown", 0, scriptHash);

        public static bool TryLoad(ProtocolSettings settings, DataCache snapshot, UInt160 scriptHash, out Nep17Contract contract)
        {
            if (scriptHash == NativeContract.NEO.Hash)
            {
                contract = Nep17Contract.Create(NativeContract.NEO);
                return true;
            }

            if (scriptHash == NativeContract.GAS.Hash)
            {
                contract = Nep17Contract.Create(NativeContract.GAS);
                return true;
            }

            var contractState = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);
            if (contractState is not null)
            {
                using var sb = new ScriptBuilder();
                sb.EmitDynamicCall(scriptHash, "symbol");
                sb.EmitDynamicCall(scriptHash, "decimals");

                using var engine = sb.Invoke(settings, snapshot);
                if (engine.State != VMState.FAULT && engine.ResultStack.Count >= 2)
                {
                    var decimals = (byte)engine.ResultStack.Pop<Neo.VM.Types.Integer>().GetInteger();
                    var symbol = Encoding.UTF8.GetString(engine.ResultStack.Pop().GetSpan());
                    contract = new Nep17Contract(symbol, decimals, scriptHash);
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
            json["symbol"] = Symbol;
            json["decimals"] = Decimals;
            return json;
        }

        public static Nep17Contract FromJson(JObject json)
        {
            var symbol = json["symbol"]!.AsString();
            var scriptHash = UInt160.Parse(json["scriptHash"]!.AsString());
            var decimals = (byte)json["decimals"]!.AsNumber();
            return new Nep17Contract(symbol, decimals, scriptHash);
        }
    }
}

