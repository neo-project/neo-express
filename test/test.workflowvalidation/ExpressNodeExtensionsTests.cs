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
using System.Linq;
using System.Numerics;
using System.Text;
using Xunit;
using SysArray = System.Array;

namespace test.workflowvalidation;

public class ExpressNodeExtensionsTests
{
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

        public Task<Block> GetBlockAsync(UInt256 blockHash) => throw new NotSupportedException();

        public Task<Block> GetBlockAsync(uint blockIndex) => throw new NotSupportedException();

        public Task<ContractManifest> GetContractAsync(UInt160 scriptHash) => throw new NotSupportedException();

        public Task<Block> GetLatestBlockAsync() => throw new NotSupportedException();

        public Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash) => throw new NotSupportedException();

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash) => throw new NotSupportedException();

        public Task<IReadOnlyList<(TokenContract contract, BigInteger balance)>> ListBalancesAsync(UInt160 address) =>
            Task.FromResult<IReadOnlyList<(TokenContract contract, BigInteger balance)>>(SysArray.Empty<(TokenContract contract, BigInteger balance)>());

        public Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync() =>
            Task.FromResult<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>>(Contracts);

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
