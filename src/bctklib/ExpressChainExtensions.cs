// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressChainExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace Neo.BlockchainToolkit
{
    public static class ExpressChainExtensions
    {
        const string ChainSecondsPerBlockSetting = "chain.SecondsPerBlock";
        const string ProtocolMillisecondsPerBlockSetting = "protocol.MillisecondsPerBlock";
        const string ProtocolMaxTransactionsPerBlockSetting = "protocol.MaxTransactionsPerBlock";
        const string ProtocolMemoryPoolMaxTransactionsSetting = "protocol.MemoryPoolMaxTransactions";
        const string ProtocolMaxTraceableBlocksSetting = "protocol.MaxTraceableBlocks";
        const string ProtocolMaxValidUntilBlockIncrementSetting = "protocol.MaxValidUntilBlockIncrement";
        const string ProtocolInitialGasDistributionSetting = "protocol.InitialGasDistribution";
        const string ProtocolHardforksPrefix = "protocol.Hardforks.";
        const string PolicyGasPerBlockSetting = "policy.GasPerBlock";
        const string PolicyMinimumDeploymentFeeSetting = "policy.MinimumDeploymentFee";
        const string PolicyCandidateRegistrationFeeSetting = "policy.CandidateRegistrationFee";
        const string PolicyOracleRequestFeeSetting = "policy.OracleRequestFee";
        const string PolicyNetworkFeePerByteSetting = "policy.NetworkFeePerByte";
        const string PolicyStorageFeeFactorSetting = "policy.StorageFeeFactor";
        const string PolicyExecutionFeeFactorSetting = "policy.ExecutionFeeFactor";
        const string PolicyMillisecondsPerBlockSetting = "policy.MillisecondsPerBlock";
        const string PolicyMaxValidUntilBlockIncrementSetting = "policy.MaxValidUntilBlockIncrement";
        const string PolicyMaxTraceableBlocksSetting = "policy.MaxTraceableBlocks";
        const string PolicyAttributeFeePrefix = "policy.AttributeFee.";
        const string NotaryMaxNotValidBeforeDeltaSetting = "notary.MaxNotValidBeforeDelta";

        const byte NeoGasPerBlockPrefix = 29;
        const byte NeoRegisterPricePrefix = 13;
        const byte ContractMinimumDeploymentFeePrefix = 20;
        const byte OraclePricePrefix = 5;
        const byte PolicyFeePerBytePrefix = 10;
        const byte PolicyExecFeeFactorPrefix = 18;
        const byte PolicyStoragePricePrefix = 19;
        const byte PolicyAttributeFeeStoragePrefix = 20;
        const byte PolicyMillisecondsPerBlockPrefix = 21;
        const byte PolicyMaxValidUntilBlockIncrementPrefix = 22;
        const byte PolicyMaxTraceableBlocksPrefix = 23;
        const byte NotaryMaxNotValidBeforeDeltaPrefix = 10;

        public static ProtocolSettings GetProtocolSettings(this ExpressChain? chain, uint secondsPerBlock = 0)
        {
            if (chain is null)
            {
                return ProtocolSettings.Default;
            }

            var settings = ProtocolSettings.Default with
            {
                Network = chain.Network,
                AddressVersion = chain.AddressVersion,
                ValidatorsCount = chain.ConsensusNodes.Count,
                StandbyCommittee = chain.ConsensusNodes.Select(GetPublicKey).ToArray(),
                SeedList = chain.ConsensusNodes
                    .Select(n => $"{System.Net.IPAddress.Loopback}:{n.TcpPort}")
                    .ToArray(),
            };

            settings = ApplyProtocolSettings(chain, settings);

            if (secondsPerBlock > 0)
            {
                settings = settings with { MillisecondsPerBlock = secondsPerBlock * 1000 };
            }
            else if (TryReadSetting<uint>(chain, ChainSecondsPerBlockSetting, uint.TryParse, out var chainSecondsPerBlock)
                && chainSecondsPerBlock > 0)
            {
                settings = settings with { MillisecondsPerBlock = chainSecondsPerBlock * 1000 };
            }

            return settings;

            static ECPoint GetPublicKey(ExpressConsensusNode node)
                => new KeyPair(node.Wallet.Accounts.Select(a => a.PrivateKey).Distinct().Single().HexToBytes()).PublicKey;
        }

        static ProtocolSettings ApplyProtocolSettings(ExpressChain chain, ProtocolSettings settings)
        {
            if (TryReadSetting<uint>(chain, ProtocolMillisecondsPerBlockSetting, uint.TryParse, out var millisecondsPerBlock)
                && millisecondsPerBlock > 0)
            {
                settings = settings with { MillisecondsPerBlock = millisecondsPerBlock };
            }

            if (TryReadSetting<uint>(chain, ProtocolMaxTransactionsPerBlockSetting, uint.TryParse, out var maxTransactionsPerBlock)
                && maxTransactionsPerBlock > 0)
            {
                settings = settings with { MaxTransactionsPerBlock = maxTransactionsPerBlock };
            }

            if (TryReadSetting<int>(chain, ProtocolMemoryPoolMaxTransactionsSetting, int.TryParse, out var memoryPoolMaxTransactions)
                && memoryPoolMaxTransactions > 0)
            {
                settings = settings with { MemoryPoolMaxTransactions = memoryPoolMaxTransactions };
            }

            if (TryReadSetting<uint>(chain, ProtocolMaxTraceableBlocksSetting, uint.TryParse, out var maxTraceableBlocks)
                && maxTraceableBlocks > 0)
            {
                settings = settings with { MaxTraceableBlocks = maxTraceableBlocks };
            }

            if (TryReadSetting<uint>(chain, ProtocolMaxValidUntilBlockIncrementSetting, uint.TryParse, out var maxValidUntilBlockIncrement)
                && maxValidUntilBlockIncrement > 0)
            {
                settings = settings with { MaxValidUntilBlockIncrement = maxValidUntilBlockIncrement };
            }

            if (TryReadSetting<ulong>(chain, ProtocolInitialGasDistributionSetting, ulong.TryParse, out var initialGasDistribution))
            {
                settings = settings with { InitialGasDistribution = initialGasDistribution };
            }

            var hardforks = settings.Hardforks.ToBuilder();
            var hardforkChanged = false;
            foreach (var (key, value) in chain.Settings)
            {
                if (!key.StartsWith(ProtocolHardforksPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var hardforkName = key[ProtocolHardforksPrefix.Length..];
                if (Enum.TryParse<Hardfork>(hardforkName, ignoreCase: true, out var hardfork)
                    && uint.TryParse(value, out var height))
                {
                    hardforks[hardfork] = height;
                    hardforkChanged = true;
                }
            }

            return hardforkChanged
                ? settings with { Hardforks = hardforks.ToImmutableDictionary() }
                : settings;
        }

        public static bool ApplyNativePolicySettings(this ExpressChain? chain, DataCache snapshot, ProtocolSettings settings)
        {
            var currentIndex = NativeContract.Ledger.CurrentIndex(snapshot);
            if (chain is null || currentIndex != 0)
            {
                return false;
            }

            var changed = false;
            changed |= ApplyBigIntegerSetting(chain, PolicyGasPerBlockSetting, 0, 10 * NativeContract.GAS.Factor,
                value => SetIndexedStorage(snapshot, NativeContract.NEO.Id, NeoGasPerBlockPrefix, 0u, value));
            changed |= ApplyBigIntegerSetting(chain, PolicyMinimumDeploymentFeeSetting, 0, null,
                value => SetStorageValue(snapshot, NativeContract.ContractManagement.Id, ContractMinimumDeploymentFeePrefix, value));
            changed |= ApplyBigIntegerSetting(chain, PolicyCandidateRegistrationFeeSetting, 1, null,
                value => SetStorageValue(snapshot, NativeContract.NEO.Id, NeoRegisterPricePrefix, value));
            changed |= ApplyBigIntegerSetting(chain, PolicyOracleRequestFeeSetting, 1, null,
                value => SetStorageValue(snapshot, NativeContract.Oracle.Id, OraclePricePrefix, value));
            changed |= ApplyBigIntegerSetting(chain, PolicyNetworkFeePerByteSetting, 0, 1_00000000,
                value => SetStorageValue(snapshot, NativeContract.Policy.Id, PolicyFeePerBytePrefix, value));
            changed |= ApplyUIntSetting(chain, PolicyStorageFeeFactorSetting, 1, PolicyContract.MaxStoragePrice,
                value => SetStorageValue(snapshot, NativeContract.Policy.Id, PolicyStoragePricePrefix, value));
            changed |= ApplyUIntSetting(chain, PolicyExecutionFeeFactorSetting, 1, (uint)PolicyContract.MaxExecFeeFactor,
                value =>
                {
                    var storageValue = settings.IsHardforkEnabled(Hardfork.HF_Faun, currentIndex)
                        ? (BigInteger)value * ApplicationEngine.FeeFactor
                        : value;
                    SetStorageValue(snapshot, NativeContract.Policy.Id, PolicyExecFeeFactorPrefix, storageValue);
                });
            changed |= ApplyAttributeFeeSettings(chain, snapshot, settings, currentIndex);
            if (settings.IsHardforkEnabled(Hardfork.HF_Echidna, currentIndex))
            {
                changed |= ApplyUIntSettingOrProtocolValue(chain, PolicyMillisecondsPerBlockSetting, ProtocolMillisecondsPerBlockSetting,
                    settings.MillisecondsPerBlock, ProtocolSettings.Default.MillisecondsPerBlock, 1, PolicyContract.MaxMillisecondsPerBlock,
                    value => SetStorageValue(snapshot, NativeContract.Policy.Id, PolicyMillisecondsPerBlockPrefix, value));
                changed |= ApplyTraceableBlockSettings(chain, snapshot, settings);
                changed |= ApplyNotarySettings(chain, snapshot, settings);
            }

            if (changed)
            {
                snapshot.Commit();
            }

            return changed;

            static bool ApplyBigIntegerSetting(ExpressChain chain, string setting, BigInteger min, BigInteger? max, Action<BigInteger> apply)
            {
                if (!TryReadSetting<BigInteger>(chain, setting, TryParseBigInteger, out var value)
                    || value < min
                    || (max.HasValue && value > max.Value))
                {
                    return false;
                }

                apply(value);
                return true;
            }

            static bool ApplyUIntSetting(ExpressChain chain, string setting, uint min, uint max, Action<uint> apply)
            {
                if (!TryReadSetting<uint>(chain, setting, uint.TryParse, out var value)
                    || value < min
                    || value > max)
                {
                    return false;
                }

                apply(value);
                return true;
            }

            static bool TryParseBigInteger(string value, [MaybeNullWhen(false)] out BigInteger parsedValue)
                => BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue);

            static void SetStorageValue(DataCache snapshot, int contractId, byte prefix, BigInteger value)
                => snapshot.GetAndChange(StorageKey.Create(contractId, prefix), () => new StorageItem())!.Set(value);

            static void SetIndexedStorage(DataCache snapshot, int contractId, byte prefix, uint index, BigInteger value)
                => snapshot.GetAndChange(StorageKey.Create(contractId, prefix, index), () => new StorageItem())!.Set(value);

            static void SetByteIndexedStorage(DataCache snapshot, int contractId, byte prefix, byte index, BigInteger value)
                => snapshot.GetAndChange(StorageKey.Create(contractId, prefix, index), () => new StorageItem())!.Set(value);

            static bool ApplyAttributeFeeSettings(ExpressChain chain, DataCache snapshot, ProtocolSettings settings, uint currentIndex)
            {
                var changed = false;
                var echidnaEnabled = settings.IsHardforkEnabled(Hardfork.HF_Echidna, currentIndex);
                foreach (var (key, value) in chain.Settings)
                {
                    if (!key.StartsWith(PolicyAttributeFeePrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var attributeName = key[PolicyAttributeFeePrefix.Length..];
                    if (!Enum.TryParse<TransactionAttributeType>(attributeName, ignoreCase: true, out var attributeType)
                        || !Enum.IsDefined(typeof(TransactionAttributeType), attributeType)
                        || (attributeType == TransactionAttributeType.NotaryAssisted && !echidnaEnabled)
                        || !uint.TryParse(value, out var attributeFee)
                        || attributeFee > PolicyContract.MaxAttributeFee)
                    {
                        continue;
                    }

                    SetByteIndexedStorage(snapshot, NativeContract.Policy.Id, PolicyAttributeFeeStoragePrefix,
                        (byte)attributeType, attributeFee);
                    changed = true;
                }

                return changed;
            }

            static bool ApplyUIntSettingOrProtocolValue(
                ExpressChain chain,
                string policySetting,
                string protocolSetting,
                uint protocolValue,
                uint defaultProtocolValue,
                uint min,
                uint max,
                Action<uint> apply)
            {
                if (chain.Settings.ContainsKey(policySetting))
                {
                    return ApplyUIntSetting(chain, policySetting, min, max, apply);
                }

                if ((chain.Settings.ContainsKey(protocolSetting) || protocolValue != defaultProtocolValue)
                    && protocolValue >= min
                    && protocolValue <= max)
                {
                    apply(protocolValue);
                    return true;
                }

                return false;
            }

            static bool ApplyTraceableBlockSettings(ExpressChain chain, DataCache snapshot, ProtocolSettings settings)
            {
                var maxValidUntilBlockIncrement = GetUIntPolicyOrProtocolValue(
                    chain,
                    PolicyMaxValidUntilBlockIncrementSetting,
                    ProtocolMaxValidUntilBlockIncrementSetting,
                    settings.MaxValidUntilBlockIncrement,
                    ProtocolSettings.Default.MaxValidUntilBlockIncrement,
                    1,
                    PolicyContract.MaxMaxValidUntilBlockIncrement);
                var maxTraceableBlocks = GetUIntPolicyOrProtocolValue(
                    chain,
                    PolicyMaxTraceableBlocksSetting,
                    ProtocolMaxTraceableBlocksSetting,
                    settings.MaxTraceableBlocks,
                    ProtocolSettings.Default.MaxTraceableBlocks,
                    1,
                    PolicyContract.MaxMaxTraceableBlocks);

                if (maxValidUntilBlockIncrement is null && maxTraceableBlocks is null)
                {
                    return false;
                }

                var resultingMaxValidUntilBlockIncrement = maxValidUntilBlockIncrement
                    ?? NativeContract.Policy.GetMaxValidUntilBlockIncrement(snapshot);
                var resultingMaxTraceableBlocks = maxTraceableBlocks
                    ?? NativeContract.Policy.GetMaxTraceableBlocks(snapshot);
                if (resultingMaxValidUntilBlockIncrement >= resultingMaxTraceableBlocks)
                {
                    return false;
                }

                var changed = false;
                if (maxValidUntilBlockIncrement is not null)
                {
                    SetStorageValue(snapshot, NativeContract.Policy.Id, PolicyMaxValidUntilBlockIncrementPrefix,
                        maxValidUntilBlockIncrement.Value);
                    changed = true;
                }
                if (maxTraceableBlocks is not null)
                {
                    SetStorageValue(snapshot, NativeContract.Policy.Id, PolicyMaxTraceableBlocksPrefix,
                        maxTraceableBlocks.Value);
                    changed = true;
                }
                return changed;
            }

            static bool ApplyNotarySettings(ExpressChain chain, DataCache snapshot, ProtocolSettings settings)
            {
                if (!TryReadSetting<uint>(chain, NotaryMaxNotValidBeforeDeltaSetting, uint.TryParse, out var maxNotValidBeforeDelta))
                {
                    return false;
                }

                var maxValidUntilBlockIncrement = NativeContract.Policy.GetMaxValidUntilBlockIncrement(snapshot);
                if (maxNotValidBeforeDelta < settings.ValidatorsCount
                    || maxNotValidBeforeDelta > maxValidUntilBlockIncrement / 2)
                {
                    return false;
                }

                SetStorageValue(snapshot, NativeContract.Notary.Id, NotaryMaxNotValidBeforeDeltaPrefix, maxNotValidBeforeDelta);
                return true;
            }

            static uint? GetUIntPolicyOrProtocolValue(
                ExpressChain chain,
                string policySetting,
                string protocolSetting,
                uint protocolValue,
                uint defaultProtocolValue,
                uint min,
                uint max)
            {
                if (chain.Settings.ContainsKey(policySetting))
                {
                    return TryReadSetting<uint>(chain, policySetting, uint.TryParse, out var policyValue)
                        && policyValue >= min
                        && policyValue <= max
                        ? policyValue
                        : null;
                }

                return (chain.Settings.ContainsKey(protocolSetting) || protocolValue != defaultProtocolValue)
                    && protocolValue >= min
                    && protocolValue <= max
                    ? protocolValue
                    : null;
            }
        }

        delegate bool TryParse<T>(string value, [MaybeNullWhen(false)] out T parsedValue);

        static bool TryReadSetting<T>(ExpressChain chain, string setting, TryParse<T> tryParse, [MaybeNullWhen(false)] out T value)
        {
            if (chain.Settings.TryGetValue(setting, out var stringValue)
                && tryParse(stringValue, out var result))
            {
                value = result;
                return true;
            }

            value = default;
            return false;
        }

        public static ExpressWallet GetWallet(this ExpressChain chain, string name)
            => TryGetWallet(chain, name, out var wallet)
                ? wallet
                : throw new Exception($"wallet {name} not found");

        public static bool TryGetWallet(this ExpressChain chain, string name, [MaybeNullWhen(false)] out ExpressWallet wallet)
        {
            for (int i = 0; i < chain.Wallets.Count; i++)
            {
                if (string.Equals(name, chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                {
                    wallet = chain.Wallets[i];
                    return true;
                }
            }

            wallet = null;
            return false;
        }

        public static Contract CreateGenesisContract(this ExpressChain chain) => chain.ConsensusNodes.CreateGenesisContract();

        public static Contract CreateGenesisContract(this IReadOnlyList<ExpressConsensusNode> nodes)
        {
            if (nodes.Count <= 0)
                throw new ArgumentException("Invalid consensus node list", nameof(nodes));

            var keys = new ECPoint[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var account = nodes[i].Wallet.DefaultAccount ??
                    throw new ArgumentException($"{nodes[i].Wallet.Name} consensus node wallet is missing a default account", nameof(nodes));
                keys[i] = new KeyPair(account.PrivateKey.HexToBytes()).PublicKey;
            }

            return Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);
        }

        public static ExpressWalletAccount GetDefaultAccount(this ExpressChain chain, string name)
            => TryGetDefaultAccount(chain, name, out var account)
                ? account
                : throw new Exception($"default account for {name} wallet not found");

        public static UInt160 GetDefaultAccountScriptHash(this ExpressChain chain, string name)
            => TryGetDefaultAccount(chain, name, out var account)
                ? account.ToScriptHash(chain.AddressVersion)
                : throw new Exception($"default account for {name} wallet not found");

        public static bool TryGetDefaultAccount(this ExpressChain chain, string name, [MaybeNullWhen(false)] out ExpressWalletAccount account)
        {
            if (chain.TryGetWallet(name, out var wallet) && wallet.DefaultAccount != null)
            {
                account = wallet.DefaultAccount;
                return true;
            }

            account = null;
            return false;
        }

        public static UInt160 ToScriptHash(this ExpressWalletAccount account, byte addressVersion)
            => account.ScriptHash.ToScriptHash(addressVersion);

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot)
            => new TestApplicationEngine(snapshot, chain.GetProtocolSettings());

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot, UInt160 signer, WitnessScope witnessScope = WitnessScope.CalledByEntry)
            => new TestApplicationEngine(snapshot, chain.GetProtocolSettings(), signer, witnessScope);

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, DataCache snapshot, Transaction transaction)
            => new TestApplicationEngine(snapshot, chain.GetProtocolSettings(), transaction);

        public static TestApplicationEngine GetTestApplicationEngine(this ExpressChain chain, TriggerType trigger, IVerifiable? container, DataCache snapshot, Block? persistingBlock, long gas, Func<byte[], bool>? witnessChecker)
            => new TestApplicationEngine(trigger, container, snapshot, persistingBlock, chain.GetProtocolSettings(), gas, witnessChecker);
    }
}
