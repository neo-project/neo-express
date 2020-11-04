using System.Text;
using Neo;
using Neo.IO.Json;
using Neo.Persistence;
using Neo.VM;

namespace NeoExpress.Neo3.Models
{
    public struct Nep5Contract
    {
        public readonly string Name;
        public readonly string Symbol;
        public readonly byte Decimals;
        public readonly UInt160 ScriptHash;

        public Nep5Contract(string name, string symbol, byte decimals, UInt160 scriptHash)
        {
            Name = name;
            Symbol = symbol;
            Decimals = decimals;
            ScriptHash = scriptHash;
        }

        public static Nep5Contract Unknown(UInt160 scriptHash) => new Nep5Contract("unknown", "unknown", 0, scriptHash);

        public static bool TryLoad(IReadOnlyStore store, UInt160 scriptHash, out Nep5Contract contract)
        {
            using var sb = new ScriptBuilder();
            sb.EmitAppCall(scriptHash, "name");
            sb.EmitAppCall(scriptHash, "symbol");
            sb.EmitAppCall(scriptHash, "decimals");

            using var engine = Neo.SmartContract.ApplicationEngine.Run(sb.ToArray(), new ReadOnlyView(store));
            if (engine.State != VMState.FAULT && engine.ResultStack.Count >= 3)
            {
                var decimals = (byte)engine.ResultStack.Pop<Neo.VM.Types.Integer>().GetInteger();
                var symbol = Encoding.UTF8.GetString(engine.ResultStack.Pop().GetSpan());
                var name = Encoding.UTF8.GetString(engine.ResultStack.Pop().GetSpan());
                contract = new Nep5Contract(name, symbol, decimals, scriptHash);
                return true;
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

        public static Nep5Contract FromJson(JObject json)
        {
            var name = json["name"].AsString();
            var symbol = json["symbol"].AsString();
            var scriptHash = UInt160.Parse(json["scriptHash"].AsString());
            var decimals = (byte)json["decimals"].AsNumber();
            return new Nep5Contract(name, symbol, decimals, scriptHash);
        }
    }
}

