// Copyright (C) 2015-2026 The Neo Project.
//
// StateServiceStoreBlockHashTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using Neo.Json;
using Neo.Network.RPC;
using System.Threading.Tasks;
using Xunit;

namespace test.bctklib
{
    public class StateServiceStoreBlockHashTests
    {
        // The block hash retrieval used by GetBranchInfoAsync must surface the real RPC
        // error rather than masking it as a TaskCanceledException.
        [Fact]
        public async Task ParseBlockHashAsync_surfaces_the_real_rpc_error()
        {
            var client = new FaultingRpcClient(new RpcException(-100, "block index out of range"));

            await FluentActions
                .Awaiting(() => StateServiceStore.ParseBlockHashAsync(client, 12345u))
                .Should().ThrowAsync<RpcException>()
                .WithMessage("*block index out of range*");
        }

        sealed class FaultingRpcClient : RpcClient
        {
            readonly RpcException exception;

            public FaultingRpcClient(RpcException exception) : base(null!) => this.exception = exception;

            public override Task<JToken> RpcSendAsync(string method, params JToken[] paraArgs)
                => Task.FromException<JToken>(exception);
        }
    }
}
