// Copyright (C) 2015-2024 The Neo Project.
//
// ICheckpointStore.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using Neo.SmartContract;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface ICheckpointStore : IReadOnlyStore<StorageKey, StorageItem>
    {
        ProtocolSettings Settings { get; }
    }
}
