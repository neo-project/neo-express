// Copyright (C) 2015-2024 The Neo Project.
//
// BranchInfo.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public record ContractInfo(
        int Id,
        UInt160 Hash,
        string Name);

    public record BranchInfo(
        uint Network,
        byte AddressVersion,
        uint Index,
        UInt256 IndexHash,
        UInt256 RootHash,
        IReadOnlyList<ContractInfo> Contracts)
    {
        public ProtocolSettings ProtocolSettings => ProtocolSettings.Default with
        {
            AddressVersion = AddressVersion,
            Network = Network,
        };

        public static BranchInfo Parse(JObject json)
        {
            var network = json.Value<uint>("network");
            var addressVersion = json.Value<byte>("address-version");
            var index = json.Value<uint>("index");
            var indexHash = UInt256.Parse(json.Value<string>("index-hash"));
            var rootHash = UInt256.Parse(json.Value<string>("root-hash"));

            var contracts = new List<ContractInfo>();
            var contractsJson = json["contracts"] as JArray;
            if (contractsJson is not null)
            {
                foreach (var value in contractsJson)
                {
                    var id = value.Value<int>("id");
                    var hash = UInt160.Parse(value.Value<string>("hash"));
                    var name = value.Value<string>("name") ?? "";
                    contracts.Add(new ContractInfo(id, hash, name));
                }
            }
            return new BranchInfo(network, addressVersion, index, indexHash, rootHash, contracts);
        }

        public void WriteJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteProperty("network", Network);
            writer.WriteProperty("address-version", AddressVersion);
            writer.WriteProperty("index", Index);
            writer.WriteProperty("index-hash", $"{IndexHash}");
            writer.WriteProperty("root-hash", $"{RootHash}");

            writer.WritePropertyName("contracts");
            writer.WriteStartArray();
            foreach (var contract in Contracts)
            {
                writer.WriteStartObject();
                writer.WriteProperty("id", contract.Id);
                writer.WriteProperty("hash", $"{contract.Hash}");
                writer.WriteProperty("name", contract.Name);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
