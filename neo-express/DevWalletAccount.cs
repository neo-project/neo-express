using System;
using System.Linq;
using System.Text.Json;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.Express
{
    class DevWalletAccount : WalletAccount
    {
        readonly KeyPair key;

        public DevWalletAccount(KeyPair key, Contract contract, UInt160 scriptHash) : base(scriptHash)
        {
            this.key = key;
            this.Contract = contract;
        }

        public override bool HasKey => key != null;

        public override KeyPair GetKey()
        {
            return key;
        }

        public static string ParsePrivateKey(JsonElement json)
        {
            return json.GetProperty("private-key").GetString();
        }

        public static DevWalletAccount Parse(JsonElement json)
        {
            var jsonContract = json.GetProperty("contract");
            var contract = new Contract()
            {
                Script = jsonContract
                    .GetProperty("script")
                    .GetString()
                    .HexToBytes(),
                ParameterList = jsonContract
                    .GetProperty("parameters")
                    .EnumerateArray()
                    .Select(cpt => Enum.Parse<ContractParameterType>(cpt.GetString()))
                    .ToArray(),
            };

            return new DevWalletAccount(
                new KeyPair(json.GetProperty("private-key").GetString().HexToBytes()),
                contract,
                json.GetProperty("script-hash").GetString().ToScriptHash())
            {
                Label = json.GetProperty("label").GetString(),
                IsDefault = json.GetProperty("is-default").GetBoolean()
            };
        }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteString("private-key", key.PrivateKey.ToHexString());
            writer.WriteString("script-hash", ScriptHash.ToAddress());
            writer.WriteString("label", Label);
            writer.WriteBoolean("is-default", IsDefault);
            writer.WriteStartObject("contract");
            writer.WriteString("script", Contract?.Script.ToHexString());
            writer.WriteStartArray("parameters");
            foreach (var cpt in Contract?.ParameterList.Select(p => Enum.GetName(typeof(ContractParameterType), p)))
            {
                writer.WriteStringValue(cpt);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }
}
