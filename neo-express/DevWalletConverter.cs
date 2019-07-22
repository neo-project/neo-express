using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Express
{
    class DevWalletConverter : JsonConverter<DevWallet>
    {
        public override DevWallet ReadJson(JsonReader reader, Type objectType, DevWallet existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return DevWallet.FromJson(reader);
        }

        public override void WriteJson(JsonWriter writer, DevWallet value, JsonSerializer serializer)
        {
            value.WriteJson(writer);
        }
    }
}
