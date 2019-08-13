using System;
using System.Linq;
using NeoExpress.Abstractions;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Express.Backend2
{
    public class DevWalletAccount : WalletAccount
    {
        private readonly KeyPair key;

        public DevWalletAccount(KeyPair key, Contract contract, UInt160 scriptHash) : base(scriptHash)
        {
            this.key = key;
            Contract = contract;
        }

        public override bool HasKey => key != null;

        public override KeyPair GetKey()
        {
            return key;
        }

        public ExpressWalletAccount ToExpressWalletAccount() => new ExpressWalletAccount()
        {
            PrivateKey = key.PrivateKey.ToHexString(),
            ScriptHash = ScriptHash.ToAddress(),
            Label = Label,
            IsDefault = IsDefault,
            Contract = new ExpressWalletAccount.AccountContract()
            {
                Script = Contract?.Script.ToHexString(),
                Parameters = Contract?.ParameterList
                        .Select(p => Enum.GetName(typeof(ContractParameterType), p))
                        .ToList()
            }
        };

        public void ToJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("private-key");
            writer.WriteValue(key.PrivateKey.ToHexString());
            writer.WritePropertyName("script-hash");
            writer.WriteValue(ScriptHash.ToAddress());
            writer.WritePropertyName("label");
            writer.WriteValue(Label);
            writer.WritePropertyName("is-default");
            writer.WriteValue(IsDefault);
            writer.WritePropertyName("contract");
            writer.WriteStartObject();
            writer.WritePropertyName("script");
            writer.WriteValue(Contract?.Script.ToHexString());
            writer.WritePropertyName("parameters");
            writer.WriteStartArray();
            foreach (var cpt in Contract?.ParameterList.Select(p => Enum.GetName(typeof(ContractParameterType), p)))
            {
                writer.WriteValue(cpt);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        public static DevWalletAccount FromExpressWalletAccount(ExpressWalletAccount account)
        {
            var keyPair = new KeyPair(account.PrivateKey.HexToBytes());
            var contract = new Contract()
            {
                Script = account.Contract?.Script.HexToBytes(),
                ParameterList = account.Contract?.Parameters
                    .Select(Enum.Parse<ContractParameterType>)
                    .ToArray()
            };
            var scriptHash = account.ScriptHash.ToScriptHash();

            return new DevWalletAccount(keyPair, contract, scriptHash)
            {
                Label = account.Label,
                IsDefault = account.IsDefault
            };
        }

        public static DevWalletAccount FromJson(JToken json)
        {
            var jsonContract = (JObject)json["contract"];
            var script = jsonContract.Value<string>("script").HexToBytes();
            var @params = jsonContract["parameters"]
                .Select(cpt => Enum.Parse<ContractParameterType>(cpt.Value<string>()));

            var privateKey = json.Value<string>("private-key").HexToBytes();
            var scriptHash = json.Value<string>("script-hash").ToScriptHash();
            var label = json.Value<string>("label");
            var isDefault = json.Value<bool>("is-default");

            return new DevWalletAccount(new KeyPair(privateKey), new Contract
            {
                Script = script,
                ParameterList = @params.ToArray()
            }, scriptHash)
            {
                Label = label,
                IsDefault = isDefault,
            };
        }

        public static string PrivateKeyFromJson(JToken json)
        {
            return json.Value<string>("private-key");
        }
    }
}
