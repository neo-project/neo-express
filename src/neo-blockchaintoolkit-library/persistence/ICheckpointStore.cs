// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    public interface ICheckpointStore : IReadOnlyStore
    {
        ProtocolSettings Settings { get; }
    }
}
