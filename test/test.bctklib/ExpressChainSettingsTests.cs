// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressChainSettingsTests.cs file belongs to neo-express project and is free
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
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Numerics;
using Xunit;

namespace test.bctklib
{
    public class ExpressChainSettingsTests
    {
        [Fact]
        public void get_protocol_settings_reads_protocol_settings()
        {
            var chain = CreateExpressChain();
            chain.Settings["protocol.MillisecondsPerBlock"] = "7000";
            chain.Settings["protocol.MaxTransactionsPerBlock"] = "64";
            chain.Settings["protocol.MemoryPoolMaxTransactions"] = "500";
            chain.Settings["protocol.MaxTraceableBlocks"] = "1000";
            chain.Settings["protocol.MaxValidUntilBlockIncrement"] = "100";
            chain.Settings["protocol.InitialGasDistribution"] = "123456789";
            chain.Settings["protocol.Hardforks.HF_Faun"] = "42";

            var settings = chain.GetProtocolSettings();

            settings.MillisecondsPerBlock.Should().Be(7000);
            settings.MaxTransactionsPerBlock.Should().Be(64);
            settings.MemoryPoolMaxTransactions.Should().Be(500);
            settings.MaxTraceableBlocks.Should().Be(1000);
            settings.MaxValidUntilBlockIncrement.Should().Be(100);
            settings.InitialGasDistribution.Should().Be(123456789);
            settings.Hardforks[Hardfork.HF_Faun].Should().Be(42);
        }

        [Fact]
        public void chain_seconds_per_block_overrides_protocol_milliseconds_per_block()
        {
            var chain = CreateExpressChain();
            chain.Settings["protocol.MillisecondsPerBlock"] = "7000";
            chain.Settings["chain.SecondsPerBlock"] = "9";

            var settings = chain.GetProtocolSettings();

            settings.MillisecondsPerBlock.Should().Be(9000);
        }

        [Fact]
        public void seconds_per_block_argument_overrides_chain_settings()
        {
            var chain = CreateExpressChain();
            chain.Settings["protocol.MillisecondsPerBlock"] = "7000";
            chain.Settings["chain.SecondsPerBlock"] = "9";

            var settings = chain.GetProtocolSettings(secondsPerBlock: 3);

            settings.MillisecondsPerBlock.Should().Be(3000);
        }

        [Fact]
        public void apply_native_policy_settings_updates_initial_policy_values()
        {
            var chain = CreateExpressChain();
            chain.Settings["policy.GasPerBlock"] = "400000000";
            chain.Settings["policy.MinimumDeploymentFee"] = "2000000000";
            chain.Settings["policy.CandidateRegistrationFee"] = "200000000000";
            chain.Settings["policy.OracleRequestFee"] = "60000000";
            chain.Settings["policy.NetworkFeePerByte"] = "2000";
            chain.Settings["policy.StorageFeeFactor"] = "200000";
            chain.Settings["policy.ExecutionFeeFactor"] = "40";
            var settings = chain.GetProtocolSettings();

            using var store = new MemoryStore();
            store.EnsureLedgerInitialized(settings);
            using var snapshot = new StoreCache(store.GetSnapshot());

            chain.ApplyNativePolicySettings(snapshot, settings).Should().BeTrue();

            NativeContract.NEO.GetGasPerBlock(snapshot).Should().Be(400000000);
            InvokeInteger(settings, snapshot, NativeContract.ContractManagement.Hash, "getMinimumDeploymentFee")
                .Should().Be(2000000000);
            NativeContract.NEO.GetRegisterPrice(snapshot).Should().Be(200000000000);
            NativeContract.Oracle.GetPrice(snapshot).Should().Be(60000000);
            NativeContract.Policy.GetFeePerByte(snapshot).Should().Be(2000);
            NativeContract.Policy.GetStoragePrice(snapshot).Should().Be(200000);
            using var engine = new TestApplicationEngine(snapshot, settings);
            NativeContract.Policy.GetExecFeeFactor(engine).Should().Be(40);
        }

        [Fact]
        public void apply_native_policy_settings_ignores_invalid_policy_values()
        {
            var chain = CreateExpressChain();
            chain.Settings["policy.NetworkFeePerByte"] = "-1";
            chain.Settings["policy.StorageFeeFactor"] = "0";
            chain.Settings["policy.ExecutionFeeFactor"] = "101";
            var settings = chain.GetProtocolSettings();

            using var store = new MemoryStore();
            store.EnsureLedgerInitialized(settings);
            using var snapshot = new StoreCache(store.GetSnapshot());

            chain.ApplyNativePolicySettings(snapshot, settings).Should().BeFalse();

            NativeContract.Policy.GetFeePerByte(snapshot).Should().Be(PolicyContract.DefaultFeePerByte);
            NativeContract.Policy.GetStoragePrice(snapshot).Should().Be(PolicyContract.DefaultStoragePrice);
            using var engine = new TestApplicationEngine(snapshot, settings);
            NativeContract.Policy.GetExecFeeFactor(engine).Should().Be(PolicyContract.DefaultExecFeeFactor);
        }

        [Fact]
        public void apply_native_policy_settings_updates_echidna_policy_values_from_protocol_settings()
        {
            var chain = CreateExpressChain();
            chain.Settings["protocol.Hardforks.HF_Echidna"] = "0";
            chain.Settings["protocol.MillisecondsPerBlock"] = "7000";
            chain.Settings["protocol.MaxValidUntilBlockIncrement"] = "100";
            chain.Settings["protocol.MaxTraceableBlocks"] = "1000";
            var settings = chain.GetProtocolSettings();

            using var store = new MemoryStore();
            store.EnsureLedgerInitialized(settings);
            using var snapshot = new StoreCache(store.GetSnapshot());

            chain.ApplyNativePolicySettings(snapshot, settings).Should().BeTrue();

            NativeContract.Policy.GetMillisecondsPerBlock(snapshot).Should().Be(7000);
            NativeContract.Policy.GetMaxValidUntilBlockIncrement(snapshot).Should().Be(100);
            NativeContract.Policy.GetMaxTraceableBlocks(snapshot).Should().Be(1000);
            snapshot.GetTimePerBlock(settings).Should().Be(TimeSpan.FromSeconds(7));
            snapshot.GetMaxValidUntilBlockIncrement(settings).Should().Be(100);
            snapshot.GetMaxTraceableBlocks(settings).Should().Be(1000);
        }

        [Fact]
        public void apply_native_policy_settings_updates_echidna_policy_values_from_policy_settings()
        {
            var chain = CreateExpressChain();
            chain.Settings["protocol.Hardforks.HF_Echidna"] = "0";
            chain.Settings["protocol.MillisecondsPerBlock"] = "7000";
            chain.Settings["policy.MillisecondsPerBlock"] = "6000";
            chain.Settings["policy.MaxValidUntilBlockIncrement"] = "100";
            chain.Settings["policy.MaxTraceableBlocks"] = "1000";
            chain.Settings["policy.AttributeFee.HighPriority"] = "1234";
            chain.Settings["policy.AttributeFee.NotaryAssisted"] = "5678";
            chain.Settings["notary.MaxNotValidBeforeDelta"] = "40";
            var settings = chain.GetProtocolSettings();

            using var store = new MemoryStore();
            store.EnsureLedgerInitialized(settings);
            using var snapshot = new StoreCache(store.GetSnapshot());

            chain.ApplyNativePolicySettings(snapshot, settings).Should().BeTrue();

            NativeContract.Policy.GetMillisecondsPerBlock(snapshot).Should().Be(6000);
            NativeContract.Policy.GetMaxValidUntilBlockIncrement(snapshot).Should().Be(100);
            NativeContract.Policy.GetMaxTraceableBlocks(snapshot).Should().Be(1000);
            NativeContract.Policy.GetAttributeFeeV1(snapshot, (byte)TransactionAttributeType.HighPriority).Should().Be(1234);
            NativeContract.Policy.GetAttributeFeeV1(snapshot, (byte)TransactionAttributeType.NotaryAssisted).Should().Be(5678);
            NativeContract.Notary.GetMaxNotValidBeforeDelta(snapshot).Should().Be(40);
        }

        static BigInteger InvokeInteger(ProtocolSettings settings, DataCache snapshot, UInt160 contractHash, string operation)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(contractHash, operation);
            using var engine = ApplicationEngine.Run(
                script: builder.ToArray(),
                snapshot: snapshot,
                settings: settings);

            engine.State.Should().Be(VMState.HALT);
            engine.ResultStack.Should().HaveCount(1);
            return engine.ResultStack.Pop().GetInteger();
        }

        static ExpressChain CreateExpressChain()
        {
            var key = new KeyPair(Convert.FromHexString("2A79CFA210832EB7139C36168E415DC78E2649EA7735060BF1DAE04C05050A98"));
            return new ExpressChain
            {
                Network = 0x334F454Eu,
                AddressVersion = ProtocolSettings.Default.AddressVersion,
                ConsensusNodes =
                [
                    new ExpressConsensusNode
                    {
                        TcpPort = 50013,
                        RpcPort = 50012,
                        Wallet = new ExpressWallet
                        {
                            Name = "node1",
                            Accounts =
                            [
                                new ExpressWalletAccount
                                {
                                    PrivateKey = key.PrivateKey.ToHexString(),
                                    IsDefault = true,
                                },
                            ],
                        },
                    },
                ],
            };
        }
    }
}
