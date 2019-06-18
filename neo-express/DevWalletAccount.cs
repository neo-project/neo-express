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

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteString("private-key", key.PrivateKey.ToHexString());
            writer.WriteString("script-hash", this.ScriptHash.ToAddress());
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
