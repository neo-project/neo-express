// Copyright (C) 2015-2026 The Neo Project.
//
// BranchInfo.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;

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
        IReadOnlyList<ContractInfo> Contracts,
        IReadOnlyDictionary<Hardfork, uint>? Hardforks = null)
    {
        public ProtocolSettings ProtocolSettings => ProtocolSettings.Default with
        {
            AddressVersion = AddressVersion,
            Network = Network,
            Hardforks = GetHardforkSettings(Hardforks),
        };

        public static BranchInfo Parse(JObject json)
        {
            var network = GetValidatedValue<uint>(json["network"], "network");
            var addressVersion = GetValidatedValue<byte>(json["address-version"], "address-version");
            var index = GetValidatedValue<uint>(json["index"], "index");
            var indexHash = UInt256.Parse(GetValidatedValue<string>(json["index-hash"], "index-hash") ?? "");
            var rootHash = UInt256.Parse(GetValidatedValue<string>(json["root-hash"], "root-hash") ?? "");
            var hardforks = ParseHardforks(json["hardforks"] as JObject);

            var contracts = new List<ContractInfo>();
            var contractsJson = json["contracts"] as JArray;
            if (contractsJson is not null)
            {
                foreach (var value in contractsJson)
                {
                    var id = GetValidatedValue<int>(value["id"], "id");
                    var hash = UInt160.Parse(GetValidatedValue<string>(value["hash"], "hash") ?? "");
                    var name = GetValidatedValue<string>(value["name"], "name") ?? "";
                    contracts.Add(new ContractInfo(id, hash, name));
                }
            }
            return new BranchInfo(network, addressVersion, index, indexHash, rootHash, contracts, hardforks);
        }

        public void WriteJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteProperty("network", Network);
            writer.WriteProperty("address-version", AddressVersion);
            writer.WriteProperty("index", Index);
            writer.WriteProperty("index-hash", $"{IndexHash}");
            writer.WriteProperty("root-hash", $"{RootHash}");

            if (Hardforks is not null)
            {
                writer.WritePropertyName("hardforks");
                writer.WriteStartObject();
                foreach (var hardfork in Hardforks.OrderBy(static h => h.Key))
                {
                    writer.WriteProperty($"{hardfork.Key}", hardfork.Value);
                }
                writer.WriteEndObject();
            }

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

        static IReadOnlyDictionary<Hardfork, uint>? ParseHardforks(JObject? json)
        {
            if (json is null)
                return null;

            return json.Properties()
                .ToDictionary(
                    static p => Enum.Parse<Hardfork>(p.Name, ignoreCase: true),
                    static p => GetValidatedValue<uint>(p.Value, p.Name));
        }

        // Guards JToken.Value<T> reads against wrong-typed tokens (raw
        // InvalidCastException from non-JValue or non-IConvertible values)
        // and out-of-range digits (OverflowException), reporting a clean
        // FormatException naming the offending field instead.
        static T? GetValidatedValue<T>(JToken? token, string fieldName)
        {
            if (token is null)
            {
                return default;
            }
            if (token is JValue value)
            {
                try
                {
                    return value.Value<T>();
                }
                catch (Exception ex) when (ex is InvalidCastException or OverflowException or FormatException)
                {
                }
            }
            throw new FormatException($"Invalid {fieldName} value");
        }

        static ImmutableDictionary<Hardfork, uint> GetHardforkSettings(IReadOnlyDictionary<Hardfork, uint>? hardforks)
        {
            if (hardforks is null)
                return ProtocolSettings.Default.Hardforks;

            var builder = ProtocolSettings.Default.Hardforks.ToBuilder();
            foreach (var hardfork in hardforks)
                builder[hardfork.Key] = hardfork.Value;
            return builder.ToImmutable();
        }
    }
}
