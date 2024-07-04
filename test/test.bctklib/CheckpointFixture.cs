// Copyright (C) 2015-2024 The Neo Project.
//
// CheckpointFixture.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography;
using System;

namespace test.bctklib;

using static Utility;

public class CheckpointFixture : IDisposable
{
    readonly CleanupPath checkpointPath = new();
    public string CheckpointPath => checkpointPath;
    public readonly uint Network = ProtocolSettings.Default.Network;
    public readonly byte AddressVersion = ProtocolSettings.Default.AddressVersion;
    public readonly UInt160 ScriptHash = new UInt160(Crypto.Hash160(Bytes("sample-script-hash")));

    public CheckpointFixture()
    {
        using var dbPath = new CleanupPath();
        using var db = RocksDbUtility.OpenDb(dbPath);
        RocksDbFixture.Populate(db);
        RocksDbUtility.CreateCheckpoint(db, CheckpointPath, Network, AddressVersion, ScriptHash);
    }

    public void Dispose()
    {
        checkpointPath.Dispose();
        GC.SuppressFinalize(this);
    }
}
