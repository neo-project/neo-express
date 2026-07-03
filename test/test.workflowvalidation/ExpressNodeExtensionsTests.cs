// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressNodeExtensionsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using NeoExpress;
using NeoExpress.Commands;
using NeoExpress.Models;
using NeoExpress.Node;
using System.Linq;
using System.Numerics;
using System.Text;
using Xunit;
using SysArray = System.Array;

namespace test.workflowvalidation;

public class ExpressNodeExtensionsTests
{
    [Fact]
    public async Task GetBlockAsync_uses_latest_block_for_empty_identifier()
    {
        var node = new StubExpressNode(ProtocolSettings.Default);

        _ = await NeoExpress.ExpressNodeExtensions.GetBlockAsync(node, string.Empty);

        node.LatestBlockRequested.Should().BeTrue();
        node.RequestedBlockIndex.Should().BeNull();
        node.RequestedBlockHash.Should().BeNull();
    }

    [Theory]
    [InlineData("0", 0u)]
    [InlineData("123", 123u)]
    public async Task GetBlockAsync_uses_block_index_for_numeric_identifier(string identifier, uint expectedIndex)
    {
        var node = new StubExpressNode(ProtocolSettings.Default);

        _ = await NeoExpress.ExpressNodeExtensions.GetBlockAsync(node, identifier);

        node.RequestedBlockIndex.Should().Be(expectedIndex);
        node.RequestedBlockHash.Should().BeNull();
        node.LatestBlockRequested.Should().BeFalse();
    }

    [Fact]
    public async Task GetBlockAsync_uses_block_hash_for_uint256_identifier()
    {
        var node = new StubExpressNode(ProtocolSettings.Default);
        var hash = UInt256.Parse("0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20");

        _ = await NeoExpress.ExpressNodeExtensions.GetBlockAsync(node, hash.ToString());

        node.RequestedBlockHash.Should().Be(hash);
        node.RequestedBlockIndex.Should().BeNull();
        node.LatestBlockRequested.Should().BeFalse();
    }

    [Fact]
    public async Task GetBalanceAsync_respects_stack_order()
    {
        var invokeResult = new RpcInvokeResult
        {
            State = VMState.HALT,
            Stack = new StackItem[]
            {
                new Integer(new BigInteger(123)), // balance (bottom of stack)
                new ByteString(Encoding.UTF8.GetBytes("GAS")),
                new Integer(new BigInteger(8)), // decimals (top of stack)
            }
        };

        var node = new StubExpressNode(ProtocolSettings.Default) { InvokeResult = invokeResult };
        var (balance, token) = await node.GetBalanceAsync(UInt160.Zero, "gas");

        balance.Amount.Should().Be(new BigInteger(123));
        token.Decimals.Should().Be((byte)8);
        token.Symbol.Should().Be("GAS");
    }

    [Fact]
    public async Task Contract_parser_resolves_single_match()
    {
        var manifestJson = """
        {
          "name":"SampleContract",
          "groups":[],
          "features":{},
          "supportedstandards":[],
          "abi":{
            "methods":[{"name":"dummy","parameters":[],"returntype":"Void","offset":0,"safe":false}],
            "events":[]
          },
          "permissions":[],
          "trusts":[],
          "extra":{}
        }
        """;
        var manifest = ContractManifest.Parse(manifestJson);
        var hash = UInt160.Parse("0x0101010101010101010101010101010101010101");

        var node = new StubExpressNode(ProtocolSettings.Default);
        node.Contracts.Add((hash, manifest));

        var parser = await node.GetContractParameterParserAsync(new ExpressChain());
        parser.TryLoadScriptHash("SampleContract", out var resolved).Should().BeTrue();
        resolved.Should().Be(hash);
    }

    [Fact]
    public async Task TransferNFT_uses_raw_token_bytes()
    {
        var node = new StubExpressNode(ProtocolSettings.Default);
        var wallet = new DevWallet(ProtocolSettings.Default, "sender");
        var account = wallet.CreateAccount();
        account.IsDefault = true;

        var tokenBytes = new byte[] { 0xAA, 0xBB, 0xCC };
        var contractHash = UInt160.Parse("0x0202020202020202020202020202020202020202");
        var receiverHash = UInt160.Parse("0x0303030303030303030303030303030303030303");

        _ = await node.TransferNFTAsync(contractHash, tokenBytes, wallet, account.ScriptHash, receiverHash, null);

        var script = node.CapturedScript ?? throw new InvalidOperationException("Script was not captured");
        var span = script.AsSpan();

        ContainsSequence(span, tokenBytes).Should().BeTrue();
        ContainsSequence(span, tokenBytes.Reverse().ToArray()).Should().BeFalse();
    }

    [Fact]
    public void TokenIdParser_preserves_hex_order()
    {
        var token = TokenIdParser.Parse("0x0A0B0C");
        token.ToArray().Should().Equal(new byte[] { 0x0A, 0x0B, 0x0C });
    }

    [Fact]
    public void TokenIdParser_decodes_base64()
    {
        var token = TokenIdParser.Parse("AQID");
        token.ToArray().Should().Equal(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Fact]
    public void TokenIdParser_prefers_base64_over_literal_text()
    {
        // "test" is both meaningful text and valid base64; the parser decodes it
        // as base64 (preferring base64 over the UTF-8 fallback), so the result is
        // the decoded bytes, not the UTF-8 bytes of "test".
        var token = TokenIdParser.Parse("test");
        token.ToArray().Should().Equal(Convert.FromBase64String("test"));
        token.ToArray().Should().NotEqual(Encoding.UTF8.GetBytes("test"));
    }

    [Fact]
    public void TokenIdParser_falls_back_to_utf8_for_non_base64_text()
    {
        // 'tok!en' contains a character outside the base64 alphabet, so the
        // base64 decode fails and the raw UTF-8 bytes are used instead.
        var token = TokenIdParser.Parse("tok!en");
        token.ToArray().Should().Equal(Encoding.UTF8.GetBytes("tok!en"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TokenIdParser_rejects_null_or_whitespace(string? tokenId)
    {
        var act = () => TokenIdParser.Parse(tokenId!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ContractNotFoundMessage_includes_script_hash()
    {
        var scriptHash = UInt160.Parse("0x0101010101010101010101010101010101010101");

        NodeUtility.ContractNotFoundMessage(scriptHash).Should().Be($"Contract {scriptHash} not found");
    }

    static bool ContainsSequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
                return true;
        }
        return false;
    }

    private sealed class StubExpressNode : IExpressNode
    {
        public ProtocolSettings ProtocolSettings { get; }

        public RpcInvokeResult InvokeResult { get; set; } = new RpcInvokeResult { Stack = SysArray.Empty<StackItem>() };

        public List<(UInt160 hash, ContractManifest manifest)> Contracts { get; } = new();

        public Script? CapturedScript { get; private set; }

        public UInt256? RequestedBlockHash { get; private set; }

        public uint? RequestedBlockIndex { get; private set; }

        public bool LatestBlockRequested { get; private set; }

        public StubExpressNode(ProtocolSettings protocolSettings)
        {
            ProtocolSettings = protocolSettings;
        }

        public void Dispose()
        {
        }

        public Task<IExpressNode.CheckpointMode> CreateCheckpointAsync(string checkPointPath) => throw new NotSupportedException();

        public Task<RpcInvokeResult> InvokeAsync(Script script, Signer? signer = null) => Task.FromResult(InvokeResult);

        public Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta) => throw new NotSupportedException();

        public Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Script script, decimal additionalGas = 0)
        {
            CapturedScript = script;
            return Task.FromResult(UInt256.Zero);
        }

        public Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, IReadOnlyList<ECPoint> oracleNodes) => throw new NotSupportedException();

        public Task<Block> GetBlockAsync(UInt256 blockHash)
        {
            RequestedBlockHash = blockHash;
            return Task.FromResult<Block>(null!);
        }

        public Task<Block> GetBlockAsync(uint blockIndex)
        {
            RequestedBlockIndex = blockIndex;
            return Task.FromResult<Block>(null!);
        }

        public Task<ContractManifest> GetContractAsync(UInt160 scriptHash) => throw new NotSupportedException();

        public Task<Block> GetLatestBlockAsync()
        {
            LatestBlockRequested = true;
            return Task.FromResult<Block>(null!);
        }

        public Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash) => throw new NotSupportedException();

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash) => throw new NotSupportedException();

        public Task<IReadOnlyList<(TokenContract contract, BigInteger balance)>> ListBalancesAsync(UInt160 address) =>
            Task.FromResult<IReadOnlyList<(TokenContract contract, BigInteger balance)>>(SysArray.Empty<(TokenContract contract, BigInteger balance)>());

        public Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync() =>
            Task.FromResult<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>>(Contracts);

        public Task<IReadOnlyList<string>> ListNftTokenIdsAsync(UInt160 address, UInt160 assetHash) =>
            Task.FromResult<IReadOnlyList<string>>(SysArray.Empty<string>());

        public Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync() =>
            Task.FromResult<IReadOnlyList<(ulong requestId, OracleRequest request)>>(SysArray.Empty<(ulong requestId, OracleRequest request)>());

        public Task<IReadOnlyList<(string key, string value)>> ListStoragesAsync(UInt160 scriptHash) =>
            Task.FromResult<IReadOnlyList<(string key, string value)>>(SysArray.Empty<(string key, string value)>());

        public Task<IReadOnlyList<TokenContract>> ListTokenContractsAsync() =>
            Task.FromResult<IReadOnlyList<TokenContract>>(SysArray.Empty<TokenContract>());

        public Task<int> PersistContractAsync(ContractState state, IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force) => throw new NotSupportedException();

        public Task<int> PersistStorageKeyValueAsync(UInt160 scripthash, (string key, string value) storagePair) => throw new NotSupportedException();

        public async IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<bool> IsNep17CompliantAsync(UInt160 contractHash) => throw new NotSupportedException();

        public Task<bool> IsNep11CompliantAsync(UInt160 contractHash) => throw new NotSupportedException();
    }
}
