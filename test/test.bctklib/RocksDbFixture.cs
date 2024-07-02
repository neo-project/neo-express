// Copyright (C) 2015-2024 The Neo Project.
//
// RocksDbFixture.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Persistence;
using RocksDbSharp;
using System;

namespace test.bctklib;

using static Utility;

public class RocksDbFixture : IDisposable
{
    CleanupPath dbPath = new();

    public string DbPath => dbPath;

    public RocksDbFixture()
    {
        using var db = RocksDbUtility.OpenDb(dbPath);
        Populate(db);
    }

    public void Dispose()
    {
        dbPath.Dispose();
        GC.SuppressFinalize(this);
    }

    public static void Populate(RocksDb db)
    {
        var cf = db.GetDefaultColumnFamily();
        foreach (var (key, value) in TestData)
        {
            db.Put(key, value, cf);
        }
    }
}
