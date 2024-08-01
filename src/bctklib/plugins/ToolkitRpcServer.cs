// Copyright (C) 2015-2024 The Neo Project.
//
// ToolkitRpcServer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Diagnostics;
using System.Numerics;
using Buffer = Neo.VM.Types.Buffer;
using ByteString = Neo.VM.Types.ByteString;
using Integer = Neo.VM.Types.Integer;
using InteropInterface = Neo.VM.Types.InteropInterface;
using Map = Neo.VM.Types.Map;
using PrimitiveType = Neo.VM.Types.PrimitiveType;
using StackItem = Neo.VM.Types.StackItem;
using StackItemType = Neo.VM.Types.StackItemType;

namespace Neo.BlockchainToolkit.Plugins
{
    public static partial class ToolkitRpcServer
    {
        const string TRANSFER = "Transfer";
        const string NEP_11 = "NEP-11";
        const string NEP_17 = "NEP-17";

        static readonly Lazy<IReadOnlyList<string>> nep11PropertyNames = new(() => new List<string>
        {
            "name",
            "description",
            "image",
            "tokenURI"
        });

        public static IReadOnlyList<string> Nep11PropertyNames => nep11PropertyNames.Value;

        public record Nep11Balance(
            UInt160 AssetHash,
            string Name,
            string Symbol,
            byte Decimals,
            IReadOnlyList<Nep11TokenBalance> Tokens);

        public record Nep11TokenBalance(
            ReadOnlyMemory<byte> TokenId,
            BigInteger Balance,
            uint LastUpdatedBlock);

        public static IEnumerable<Nep11Balance> GetNep11Balances(DataCache snapshot, INotificationsProvider notificationProvider, UInt160 address, ProtocolSettings settings)
        {
            List<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId, BigInteger balance)> tokens = new();
            foreach (var contract in NativeContract.ContractManagement.ListContracts(snapshot))
            {
                if (!contract.Manifest.SupportedStandards.Contains(NEP_11))
                    continue;
                var balanceOf = contract.Manifest.Abi.GetMethod("balanceOf", -1);
                if (balanceOf is null)
                    continue;
                var divisible = balanceOf.Parameters.Length == 2;

                foreach (var tokenId in snapshot.GetNep11Tokens(contract.Hash, address, settings))
                {
                    var balance = divisible
                        ? snapshot.GetDivisibleNep11Balance(contract.Hash, tokenId, address, settings)
                        : snapshot.GetIndivisibleNep11Owner(contract.Hash, tokenId, settings) == address
                            ? BigInteger.One
                            : BigInteger.Zero;

                    if (balance.IsZero)
                        continue;

                    tokens.Add((contract.Hash, tokenId, balance));
                }
            }

            // collect the last block index a transfer occurred for all tokens
            Dictionary<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId), uint> updateIndexes = new(TokenEqualityComparer.Instance);
            if (tokens.Count > 0)
            {
                var notifications = notificationProvider.GetNotifications(
                    SeekDirection.Backward,
                    tokens.Select(b => b.scriptHash).ToHashSet(),
                    TRANSFER);

                foreach (var (blockIndex, _, _, notification) in notifications)
                {
                    var from = ParseAddress(notification.State[0]);
                    if (from is null)
                        continue;
                    var to = ParseAddress(notification.State[1]);
                    if (to is null)
                        continue;
                    if (from != address && to != address)
                        continue;
                    ReadOnlyMemory<byte> tokenId = (ByteString)notification.State[3];
                    if (tokenId.Length == 0)
                        continue;

                    var key = (notification.ScriptHash, tokenId);
                    if (updateIndexes.ContainsKey(key))
                        continue;

                    updateIndexes.Add(key, blockIndex);
                    if (updateIndexes.Count == tokens.Count)
                        break;
                }
            }

            foreach (var asset in tokens.GroupBy(t => t.scriptHash))
            {
                var (name, symbol, decimals) = snapshot.GetTokenDetails(asset.Key, settings);

                List<Nep11TokenBalance> tokenBalances = new();

                foreach (var (_, tokenId, balance) in asset)
                {
                    if (balance.IsZero)
                        continue;
                    var lastUpdatedBlock = updateIndexes.TryGetValue((asset.Key, tokenId), out var value)
                        ? value : 0;
                    tokenBalances.Add(new Nep11TokenBalance(tokenId, balance, lastUpdatedBlock));
                }

                yield return new Nep11Balance(asset.Key, name, symbol, decimals, tokenBalances);
            }
        }

        public static IEnumerable<TransferRecord> GetNep11Transfers(DataCache snapshot, INotificationsProvider notificationProvider, UInt160 address, ulong startTime, ulong endTime)
            => GetTransfers(snapshot, NEP_11, notificationProvider, address, startTime, endTime);

        public static IEnumerable<(string key, StackItem value)> GetNep11Properties(DataCache snapshot, UInt160 contractHash, ReadOnlyMemory<byte> tokenId, ProtocolSettings settings)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(contractHash, "properties", CallFlags.ReadOnly, tokenId);

            using var engine = builder.Invoke(settings, snapshot);
            if (engine.State == VMState.HALT)
            {
                var map = engine.ResultStack.Pop<Map>();
                foreach (var (key, value) in map)
                {
                    var keyString = key.GetString() ?? string.Empty;
                    yield return (keyString, value);
                }
            }
        }

        public record Nep17Balance(
            UInt160 AssetHash,
            string Name,
            string Symbol,
            byte Decimals,
            BigInteger Balance,
            uint LastUpdatedBlock);

        public static IEnumerable<Nep17Balance> GetNep17Balances(DataCache snapshot, INotificationsProvider notificationProvider, UInt160 address, ProtocolSettings settings)
        {
            // collect the non-zero balances of all the deployed Nep17 contracts for the specified account
            var addressBalances = NativeContract.ContractManagement.ListContracts(snapshot)
                .Where(c => c.Manifest.SupportedStandards.Contains(NEP_17))
                .Select(c => (
                    scriptHash: c.Hash,
                    balance: snapshot.GetNep17Balance(c.Hash, address, settings)))
                .Where(t => !t.balance.IsZero)
                .ToList();

            // collect the last block index a transfer occurred for all account balances
            var updateIndexes = new Dictionary<UInt160, uint>();
            if (addressBalances.Count > 0)
            {
                var notifications = notificationProvider.GetNotifications(
                    SeekDirection.Backward,
                    addressBalances.Select(b => b.scriptHash).ToHashSet(),
                    TRANSFER);

                foreach (var (blockIndex, _, _, notification) in notifications)
                {
                    // iterate backwards thru the notifications looking for all the Transfer events from a contract
                    // in assets where a Transfer event hasn't already been recorded
                    if (!updateIndexes.ContainsKey(notification.ScriptHash))
                    {
                        var from = ParseAddress(notification.State[0]);
                        if (from is null)
                            continue;
                        var to = ParseAddress(notification.State[1]);
                        if (to is null)
                            continue;
                        if (from != address && to != address)
                            continue;
                        // if the specified account was the sender or receiver of the current transfer,
                        // record the update index. Stop the iteration if indexes for all the assets are 
                        // have been recorded
                        updateIndexes.Add(notification.ScriptHash, blockIndex);
                        if (updateIndexes.Count == addressBalances.Count)
                            break;
                    }
                }
            }

            for (int i = 0; i < addressBalances.Count; i++)
            {
                var (scriptHash, balance) = addressBalances[i];
                var lastUpdatedBlock = updateIndexes.TryGetValue(scriptHash, out var _index) ? _index : 0;
                var (name, symbol, decimals) = snapshot.GetTokenDetails(scriptHash, settings);

                yield return new Nep17Balance(scriptHash, name, symbol, decimals, balance, lastUpdatedBlock);
            }
        }

        public static IEnumerable<TransferRecord> GetNep17Transfers(DataCache snapshot, INotificationsProvider notificationProvider, UInt160 address, ulong startTime, ulong endTime)
            => GetTransfers(snapshot, NEP_17, notificationProvider, address, startTime, endTime);

        static IEnumerable<TransferRecord> GetTransfers(DataCache snapshot, string standard, INotificationsProvider notificationProvider, UInt160 address, ulong startTime, ulong endTime)
        {
            Debug.Assert(standard == NEP_11 || standard == NEP_17);
            if (endTime < startTime)
                throw new ArgumentException("", nameof(startTime));

            var contracts = NativeContract.ContractManagement.ListContracts(snapshot)
                .Where(c => c.Manifest.SupportedStandards.Contains(standard))
                .Select(c => c.Hash)
                .ToHashSet();
            var notifications = notificationProvider.GetNotifications(SeekDirection.Forward, contracts, TRANSFER);

            foreach (var (blockIndex, txIndex, notIndex, notification) in notifications)
            {
                var header = NativeContract.Ledger.GetHeader(snapshot, blockIndex);
                if (startTime > header.Timestamp || header.Timestamp > endTime)
                    continue;
                if (notification.State[2] is not ByteString and not Integer)
                    continue;
                if (standard == NEP_17 && notification.State.Count != 3)
                    continue;
                if (standard == NEP_11 && notification.State.Count != 4)
                    continue;

                var asset = notification.ScriptHash;
                var from = ParseAddress(notification.State[0]);
                if (from is null)
                    continue;
                var to = ParseAddress(notification.State[0]);
                if (to is null)
                    continue;
                if (from != address && to != address)
                    continue;

                var amount = notification.State[2].GetInteger();
                var tokenId = standard == NEP_11
                    ? (ReadOnlyMemory<byte>)(ByteString)notification.State[3]
                    : default;

                yield return new TransferRecord(
                    header.Timestamp, asset, from, to, amount, header.Index, notIndex, notification.InventoryHash, tokenId);
            }
        }

        static UInt160? ParseAddress(StackItem item)
        {
            if (item.IsNull)
                return null;
            if (item is not PrimitiveType and not Buffer)
                return null;
            var span = item.GetSpan();
            if (span.Length != UInt160.Length)
                return null;
            return new UInt160(span);
        }

        public static IEnumerable<NotificationInfo> GetNotifications(
            this INotificationsProvider @this,
            SeekDirection direction,
            IReadOnlySet<UInt160>? contracts,
            string eventName) => string.IsNullOrEmpty(eventName)
                ? @this.GetNotifications(direction, contracts)
                : @this.GetNotifications(direction, contracts,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { eventName });

        public static BigInteger GetNep17Balance(this DataCache snapshot, UInt160 asset, UInt160 address, ProtocolSettings settings)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "balanceOf", address.ToArray());
            return TryGetBalance(snapshot, builder, settings, out var balance) ? balance : BigInteger.Zero;
        }

        static bool TryGetBalance(DataCache snapshot, ScriptBuilder builder, ProtocolSettings settings, out BigInteger balance)
        {
            using var engine = builder.Invoke(settings, snapshot);
            if (engine.State != VMState.FAULT
                && engine.ResultStack.Count >= 1
                && engine.ResultStack.Pop() is Neo.VM.Types.Integer integer)
            {
                balance = integer.GetInteger();
                return true;
            }

            balance = default;
            return false;
        }

        public static ApplicationEngine Invoke(this ScriptBuilder builder, ProtocolSettings settings, DataCache snapshot, IVerifiable? container = null)
            => Invoke(builder.ToArray(), settings, snapshot, container);

        static public ApplicationEngine Invoke(this Script script, ProtocolSettings settings, DataCache snapshot, IVerifiable? container = null)
            => ApplicationEngine.Run(
                script: script,
                snapshot: snapshot,
                settings: settings,
                container: container);

        public static (string name, string symbol, byte decimals) GetTokenDetails(this DataCache snapshot, UInt160 asset, ProtocolSettings settings)
        {
            var contract = NativeContract.ContractManagement.GetContract(snapshot, asset);
            if (contract is not null)
            {
                var name = contract.Manifest.Name;

                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(asset, "decimals");
                builder.EmitDynamicCall(asset, "symbol");

                using var engine = builder.Invoke(settings, snapshot);
                if (engine.State != VMState.FAULT && engine.ResultStack.Count >= 2)
                {
                    var symbol = engine.ResultStack.Pop().GetString()!;
                    var decimals = (byte)engine.ResultStack.Pop().GetInteger();

                    return (name, symbol, decimals);
                }
            }
            return ("<Unknown>", "<UNK>", 0);
        }

        public static IEnumerable<ReadOnlyMemory<byte>> GetNep11Tokens(this DataCache snapshot, UInt160 asset, UInt160 address, ProtocolSettings settings)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "tokensOf", address.ToArray());
            using var engine = builder.Invoke(settings, snapshot);
            if (engine.State != VMState.FAULT
                && engine.ResultStack.Count >= 1
                && engine.ResultStack.Pop() is InteropInterface interop
                && interop.GetInterface<object>() is IIterator iterator)
            {
                while (iterator.Next())
                {
                    var value = iterator.Value(null);
                    var byteString = value.Type == StackItemType.ByteString
                        ? (ByteString)value
                        : (ByteString)value.ConvertTo(StackItemType.ByteString);

                    yield return (ReadOnlyMemory<byte>)byteString;
                }
            }
        }

        public static BigInteger GetDivisibleNep11Balance(this DataCache snapshot, UInt160 asset, ReadOnlyMemory<byte> tokenId, UInt160 address, ProtocolSettings settings)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "balanceOf", address.ToArray(), (ByteString)tokenId);
            return TryGetBalance(snapshot, builder, settings, out var balance)
                ? balance
                : BigInteger.Zero;
        }

        public static UInt160 GetIndivisibleNep11Owner(this DataCache snapshot, UInt160 asset, ReadOnlyMemory<byte> tokenId, ProtocolSettings settings)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "ownerOf", (ByteString)tokenId);

            using var engine = builder.Invoke(settings, snapshot);
            if (engine.State != VMState.FAULT
                && engine.ResultStack.Count >= 1
                && engine.ResultStack.Pop() is ByteString byteString
                && byteString.Size == UInt160.Length)
            {
                return new UInt160(byteString);
            }

            return UInt160.Zero;
        }
    }
}
