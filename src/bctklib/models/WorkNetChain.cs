// Copyright (C) 2015-2024 The Neo Project.
//
// WorkNetChain.cs file belongs to neo-express project and is free
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
    public record WorknetChain(
        Uri Uri,
        BranchInfo BranchInfo,
        ToolkitConsensusNode ConsensusNode,
        IReadOnlyList<ToolkitWallet> Wallets)
    {
        public ToolkitWallet ConsensusWallet => ConsensusNode.Wallet;
        public ProtocolSettings ProtocolSettings => BranchInfo.ProtocolSettings;

        public WorknetChain(Uri uri, BranchInfo branchInfo, ToolkitConsensusNode consensusNode, IEnumerable<ToolkitWallet>? wallets = null)
            : this(uri, branchInfo, consensusNode, (wallets ?? Enumerable.Empty<ToolkitWallet>()).ToList())
        {
        }

        public WorknetChain(WorknetChain chain, IEnumerable<ToolkitWallet>? wallets = null)
            : this(chain.Uri, chain.BranchInfo, chain.ConsensusNode, (wallets ?? chain.Wallets).ToList())
        {
        }

        public static WorknetChain Parse(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return Parse(reader);
        }

        public static async Task<WorknetChain> ParseAsync(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return await ParseAsync(reader).ConfigureAwait(false);
        }

        public static WorknetChain Parse(TextReader reader)
        {
            using var jsonReader = new JsonTextReader(reader);
            return Parse(jsonReader);
        }

        public static async Task<WorknetChain> ParseAsync(TextReader reader)
        {
            using var jsonReader = new JsonTextReader(reader);
            return await ParseAsync(jsonReader).ConfigureAwait(false);
        }

        public static WorknetChain Parse(JsonTextReader reader) => Parse(JObject.Load(reader));

        public static async Task<WorknetChain> ParseAsync(JsonTextReader reader)
        {
            var json = await JObject.LoadAsync(reader).ConfigureAwait(false);
            return Parse(json);
        }

        public static WorknetChain Parse(JObject json)
        {
            var uri = json.Value<string>("uri") ?? throw new JsonException("invalid uri property");
            var branchInfoJson = (json["branch-info"] as JObject) ?? throw new JsonException("invalid branch-info property");
            var branchInfo = BranchInfo.Parse(branchInfoJson);
            var settings = ProtocolSettings.Default with
            {
                Network = branchInfo.Network,
                AddressVersion = branchInfo.AddressVersion,
            };
            var node = (json["consensus-nodes"] ?? throw new JsonException("invalid consensus-nodes property"))
                .Cast<JObject>()
                .Select(n => ToolkitConsensusNode.Parse(n, settings))
                .First();

            var wallets = (json["wallets"] ?? Enumerable.Empty<JToken>())
                .Cast<JObject>()
                .Select(w => ToolkitWallet.Parse(w, settings));

            return new(new Uri(uri), branchInfo, node, wallets);
        }

        public void WriteJson(JsonWriter writer)
        {
            // write out network as magic + address version for neo express file compat
            writer.WriteStartObject();
            writer.WriteProperty("magic", BranchInfo.Network);
            writer.WriteProperty("address-version", BranchInfo.AddressVersion);
            writer.WriteProperty("uri", $"{Uri}");

            writer.WritePropertyName("branch-info");
            BranchInfo.WriteJson(writer);

            writer.WritePropertyName("consensus-nodes");
            writer.WriteStartArray();
            ConsensusNode.WriteJson(writer);
            writer.WriteEndArray();

            writer.WritePropertyName("wallets");
            writer.WriteStartArray();
            foreach (var wallet in Wallets)
            {
                wallet.WriteJson(writer);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
