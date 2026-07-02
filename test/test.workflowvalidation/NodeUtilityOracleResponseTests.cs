// Copyright (C) 2015-2026 The Neo Project.
//
// NodeUtilityOracleResponseTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Node;
using Xunit;

namespace test.workflowvalidation;

public class NodeUtilityOracleResponseTests
{
    const byte Prefix_Transaction = 11;

    [Fact]
    public void CreateResponseTx_throws_descriptive_error_when_original_transaction_missing()
    {
        // An empty ledger does not contain the request's original transaction, so
        // GetTransactionState returns null. CreateResponseTx must surface a clear
        // error rather than dereferencing the null state (NullReferenceException).
        using var store = new MemoryStore();
        using var snapshot = new StoreCache(store.GetSnapshot());

        var request = new OracleRequest
        {
            OriginalTxid = UInt256.Zero,
            GasForResponse = 0,
            Url = string.Empty,
            Filter = string.Empty,
            CallbackContract = UInt160.Zero,
            CallbackMethod = string.Empty,
            UserData = System.Array.Empty<byte>(),
        };
        var response = new OracleResponse
        {
            Id = 1,
            Code = OracleResponseCode.Success,
            Result = System.Array.Empty<byte>(),
        };
        var oracleNodes = new[] { ECCurve.Secp256r1.G };

        var action = () => NodeUtility.CreateResponseTx(snapshot, request, response, oracleNodes, ProtocolSettings.Default);

        action.Should().Throw<System.Exception>()
            .Which.Should().NotBeOfType<System.NullReferenceException>();
        action.Should().Throw<System.Exception>()
            .WithMessage($"*original transaction {UInt256.Zero}*");
    }

    [Fact]
    public void CreateResponseTx_uses_policy_max_valid_until_block_increment()
    {
        var policySettings = ProtocolSettings.Default with
        {
            MaxValidUntilBlockIncrement = 55,
            StandbyCommittee = [ECCurve.Secp256r1.G],
            ValidatorsCount = 1
        };
        var runtimeSettings = policySettings with
        {
            MaxValidUntilBlockIncrement = 100
        };

        using var store = new MemoryStore();
        store.EnsureLedgerInitialized(policySettings);
        using var snapshot = new StoreCache(store.GetSnapshot());

        var originalTx = new Transaction
        {
            Attributes = System.Array.Empty<TransactionAttribute>(),
            Script = System.Array.Empty<byte>(),
            Signers = System.Array.Empty<Signer>(),
            Witnesses = System.Array.Empty<Witness>()
        };
        snapshot.Add(
            new KeyBuilder(NativeContract.Ledger.Id, Prefix_Transaction).Add(originalTx.Hash),
            new StorageItem(new TransactionState
            {
                BlockIndex = 7,
                State = VMState.HALT,
                Transaction = originalTx
            }));

        var request = new OracleRequest
        {
            OriginalTxid = originalTx.Hash,
            GasForResponse = 0,
            Url = string.Empty,
            Filter = string.Empty,
            CallbackContract = UInt160.Zero,
            CallbackMethod = string.Empty,
            UserData = System.Array.Empty<byte>(),
        };
        var response = new OracleResponse
        {
            Id = 1,
            Code = OracleResponseCode.Success,
            Result = System.Array.Empty<byte>(),
        };
        var oracleNodes = new[] { ECCurve.Secp256r1.G };

        var tx = NodeUtility.CreateResponseTx(snapshot, request, response, oracleNodes, runtimeSettings);

        tx.Should().NotBeNull();
        tx!.ValidUntilBlock.Should().Be(7 + policySettings.MaxValidUntilBlockIncrement);
    }
}
