// Copyright (C) 2015-2026 The Neo Project.
//
// ToolkitPersistencePluginTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.Plugins;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using RocksDbSharp;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using NeoArray = Neo.VM.Types.Array;

namespace test.bctklib
{
    using static Utility;

    public class ToolkitPersistencePluginTests
    {
        const int RocksDbTimeout = 30000;

        static readonly UInt160 ContractA = UInt160.Parse("0x0101010101010101010101010101010101010101");
        static readonly UInt160 ContractB = UInt160.Parse("0x0202020202020202020202020202020202020202");

        static byte[] NotificationKey(uint block, ushort tx, ushort index)
        {
            var key = new byte[sizeof(uint) + (2 * sizeof(ushort))];
            BinaryPrimitives.WriteUInt32BigEndian(key.AsSpan(0, sizeof(uint)), block);
            BinaryPrimitives.WriteUInt16BigEndian(key.AsSpan(sizeof(uint), sizeof(ushort)), tx);
            BinaryPrimitives.WriteUInt16BigEndian(key.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(ushort)), index);
            return key;
        }

        // Serialize a record in the exact layout NotificationRecord.Deserialize expects,
        // independent of NotificationRecord.Serialize (so this read-path test does not
        // depend on the write path).
        static byte[] SerializeRecord(UInt160 scriptHash, string eventName)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(scriptHash.ToArray());
            writer.Write(BinarySerializer.Serialize(new NeoArray(), ExecutionEngineLimits.Default));
            writer.WriteVarString(eventName);
            writer.Write(UInt256.Zero.ToArray());
            writer.Write((byte)InventoryType.TX);
            writer.Flush();
            return stream.ToArray();
        }

        static RocksDb SeedTwoTransfers(string path)
        {
            var db = RocksDbUtility.OpenDb(path);
            var cf = db.GetOrCreateColumnFamily(nameof(ToolkitPersistencePlugin) + ".notifications");
            db.Put(NotificationKey(0, 0, 0), SerializeRecord(ContractA, "Transfer"), cf);
            db.Put(NotificationKey(0, 0, 1), SerializeRecord(ContractB, "Transfer"), cf);
            return db;
        }

        // A contract+event filter must KEEP matching notifications and must terminate.
        // The pre-fix code inverted the filter and never advanced the iterator on a
        // filtered record, so this would return the wrong record or loop forever.
        [Fact(Timeout = RocksDbTimeout)]
        public void GetNotifications_returns_only_records_matching_the_filter()
        {
            using var path = new CleanupPath();
            using var db = SeedTwoTransfers(path);
            using var plugin = new ToolkitPersistencePlugin(db);

            var result = plugin.GetNotifications(
                SeekDirection.Forward,
                new HashSet<UInt160> { ContractA },
                new HashSet<string> { "Transfer" }).ToList();

            result.Should().ContainSingle();
            result[0].Notification.ScriptHash.Should().Be(ContractA);
        }

        [Fact(Timeout = RocksDbTimeout)]
        public void GetNotifications_returns_every_record_when_unfiltered()
        {
            using var path = new CleanupPath();
            using var db = SeedTwoTransfers(path);
            using var plugin = new ToolkitPersistencePlugin(db);

            var hashes = plugin.GetNotifications(SeekDirection.Forward)
                .Select(n => n.Notification.ScriptHash)
                .ToList();

            hashes.Should().BeEquivalentTo(new[] { ContractA, ContractB });
        }

        // The event-name filter is compared case-insensitively so a caller-supplied
        // "transfer" still matches a stored "Transfer", matching ExpressPersistencePlugin.
        [Fact(Timeout = RocksDbTimeout)]
        public void GetNotifications_matches_event_names_case_insensitively()
        {
            using var path = new CleanupPath();
            using var db = SeedTwoTransfers(path);
            using var plugin = new ToolkitPersistencePlugin(db);

            var result = plugin.GetNotifications(
                SeekDirection.Forward,
                null,
                new HashSet<string> { "transfer" }).ToList();

            result.Should().HaveCount(2);
        }

        // The backward direction walks the iterator with Prev() rather than Next();
        // pin that path so the reverse advance cannot regress.
        [Fact(Timeout = RocksDbTimeout)]
        public void GetNotifications_walks_records_in_reverse_when_backward()
        {
            using var path = new CleanupPath();
            using var db = SeedTwoTransfers(path);
            using var plugin = new ToolkitPersistencePlugin(db);

            var hashes = plugin.GetNotifications(SeekDirection.Backward)
                .Select(n => n.Notification.ScriptHash)
                .ToList();

            hashes.Should().Equal(new[] { ContractB, ContractA });
        }
    }
}
