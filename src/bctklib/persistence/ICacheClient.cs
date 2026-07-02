// Copyright (C) 2015-2026 The Neo Project.
//
// ICacheClient.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.BlockchainToolkit.Persistence;

internal interface ICacheClient : IDisposable
{
    bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value);
    void CacheStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, byte[]? value);
    bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value);
    // Point lookup into the cached found states for (contractHash, prefix). Returns false when
    // that record set has not been cached yet; returns true with a null value when the record
    // set is cached but does not contain the key.
    bool TryGetCachedState(UInt160 contractHash, byte? prefix, ReadOnlyMemory<byte> key, out byte[]? value);
    void DropCachedFoundStates(UInt160 contractHash, byte? prefix);
    ICacheSnapshot GetFoundStatesSnapshot(UInt160 contractHash, byte? prefix);
}
