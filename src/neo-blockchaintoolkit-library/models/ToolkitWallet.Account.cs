// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public partial class ToolkitWallet
    {
        internal class Account : WalletAccount
        {
            readonly KeyPair key;

            public Account(KeyPair key, UInt160 scriptHash, ProtocolSettings settings)
                : base(scriptHash, settings)
            {
                this.key = key;
            }

            public override bool HasKey => key is not null;

            public override KeyPair GetKey() => key;

            public static Account Parse(JObject json, ProtocolSettings settings)
            {
                var isDefault = json.TryGetValue("is-default", out var token) && token.Value<bool>();
                var label = json.TryGetValue("label", out token) ? token.Value<string>() : null;

                if (json.TryGetValue("private-key", out token) && token.Type == JTokenType.String)
                {
                    var key = token.Value<string>();
                    if (!string.IsNullOrEmpty(key))
                    {
                        var keyPair = new KeyPair(Convert.FromHexString(key));
                        var contract = Contract.CreateSignatureContract(keyPair.PublicKey);
                        return new Account(keyPair, contract.ScriptHash, settings)
                        {
                            Contract = contract,
                            IsDefault = isDefault,
                            Label = label
                        };
                    }
                }

                throw new NotSupportedException("Toolkit Wallet Accounts must have a private key");
            }

            public static Account Load(JObject json, ProtocolSettings settings)
            {
                var scriptHash = UInt160.Parse(json.Value<string>("script-hash"));

                KeyPair keyPair = null;
                if (json.TryGetValue("private-key", out var token)
                    && token.Type != JTokenType.Null)
                {
                    if (token.Type != JTokenType.String)
                        throw new Exception();
                    keyPair = new KeyPair(Convert.FromBase64String(token.Value<string>() ?? ""));
                }

                Contract contract = null;
                if (json.TryGetValue("contract", out token)
                    && token.Type != JTokenType.Null)
                {
                    var script = Convert.FromBase64String(token.Value<string>("script") ?? "");
                    var @params = Array.Empty<ContractParameterType>();
                    var paramArray = token["parameters"] as JArray;
                    if (paramArray is not null)
                    {
                        @params = paramArray
                            .Select(t => Enum.Parse<ContractParameterType>(t.Value<string>() ?? ""))
                            .ToArray();
                    }
                    contract = new Contract() { Script = script, ParameterList = @params };
                    scriptHash = contract.ScriptHash;
                }

                var label = json.TryGetValue("label", out token) ? token.Value<string>() : null;
                var @lock = json.TryGetValue("lock", out token) && token.Value<bool>();
                var isDefault = json.TryGetValue("is-default", out token) && token.Value<bool>();

                return new Account(keyPair, scriptHash, settings)
                {
                    Contract = contract,
                    IsDefault = isDefault,
                    Label = label,
                    Lock = @lock,
                };
            }

            public void WriteJson(JsonWriter writer)
            {
                var key = GetKey();
                if (key is null)
                    throw new Exception("Invalid ToolkitWallet Address");

                writer.WriteStartObject();
                writer.WriteProperty("private-key", Convert.ToHexString(key.PrivateKey));
                writer.WriteProperty("address", Address);
                writer.WriteProperty("script-hash", ScriptHash.ToString());
                if (Label is not null)
                    writer.WriteProperty("label", Label);
                writer.WriteProperty("is-default", IsDefault);
                if (Lock)
                    writer.WriteProperty("lock", Lock);

                if (Contract is not null)
                {
                    writer.WritePropertyName("contract");
                    writer.WriteStartObject();
                    writer.WriteProperty("script", Convert.ToHexString(Contract.Script));
                    writer.WritePropertyName("parameters");
                    writer.WriteStartArray();
                    foreach (var p in Contract.ParameterList)
                    {
                        var type = Enum.GetName(p) ?? throw new Exception($"Invalid {nameof(ContractParameterType)}");
                        writer.WriteValue(type);
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
        }
    }
}
