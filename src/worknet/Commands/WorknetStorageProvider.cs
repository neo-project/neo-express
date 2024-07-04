// Copyright (C) 2015-2024 The Neo Project.
//
// WorknetStorageProvider.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;

namespace NeoWorkNet.Commands;

class WorknetStorageProvider : IStoreProvider
{
    readonly IStore store;
    readonly Lazy<IStore> consensusStateStore = new(() => new MemoryStore());

    public WorknetStorageProvider(IStore store)
    {
        this.store = store;
    }

    public string Name => nameof(WorknetStorageProvider);

    public IStore GetStore(string path)
    {
        if (string.IsNullOrEmpty(path))
            return store;
        if (path == "ConsensusState")
            return consensusStateStore.Value;
        throw new NotSupportedException();
    }
}
