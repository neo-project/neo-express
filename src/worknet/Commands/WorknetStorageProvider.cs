// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Persistence;
using System;

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
