// Copyright (C) 2015-2026 The Neo Project.
//
// StateServiceStoreCursorTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using System;
using Xunit;

namespace test.bctklib
{
    public class StateServiceStoreCursorTests
    {
        [Fact]
        public void EnsureCursorAdvanced_returns_an_advancing_cursor()
        {
            var key = new byte[] { 1, 2, 3 };

            StateServiceStore.EnsureCursorAdvanced(Array.Empty<byte>(), key).Should().Equal(key);
            StateServiceStore.EnsureCursorAdvanced(new byte[] { 1, 2 }, key).Should().Equal(key);
            StateServiceStore.EnsureCursorAdvanced(new byte[] { 1, 2, 2 }, key).Should().Equal(key);
        }

        [Fact]
        public void EnsureCursorAdvanced_rejects_a_non_advancing_cursor()
        {
            var key = new byte[] { 1, 2, 3 };

            // node returned the same last key
            Assert.Throws<InvalidOperationException>(
                () => StateServiceStore.EnsureCursorAdvanced(key, new byte[] { 1, 2, 3 }));
            // cursor moved backward
            Assert.Throws<InvalidOperationException>(
                () => StateServiceStore.EnsureCursorAdvanced(key, new byte[] { 1, 2, 2 }));
            // cursor is an earlier (shorter) prefix
            Assert.Throws<InvalidOperationException>(
                () => StateServiceStore.EnsureCursorAdvanced(key, new byte[] { 1, 2 }));
        }
    }
}
