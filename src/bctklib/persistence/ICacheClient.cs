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
    object DownloadLockScope { get; }
    bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value);
    void CacheStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, byte[]? value);
    bool IsFoundStatesCached(UInt160 contractHash, byte? prefix);
    bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value);
    ICacheSnapshot GetFoundStatesSnapshot(UInt160 contractHash, byte? prefix);
    IDisposable AcquireDownloadLock(UInt160 contractHash, byte? prefix, CancellationToken token);
    Task<IDisposable> AcquireDownloadLockAsync(UInt160 contractHash, byte? prefix, CancellationToken token);
}
