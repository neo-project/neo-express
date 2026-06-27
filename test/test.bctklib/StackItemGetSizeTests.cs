// Copyright (C) 2015-2026 The Neo Project.
//
// StackItemGetSizeTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using Neo.VM;
using System;
using System.Numerics;
using Xunit;
using NeoArray = Neo.VM.Types.Array;
using NeoByteString = Neo.VM.Types.ByteString;
using NeoInteger = Neo.VM.Types.Integer;

namespace test.bctklib
{
    public class StackItemGetSizeTests
    {
        [Fact]
        public void GetSize_computes_the_size_of_a_nested_structure()
        {
            var inner = new NeoArray(new Neo.VM.Types.StackItem[]
            {
                new NeoInteger(new BigInteger(1)),
                new NeoByteString(new byte[] { 1, 2, 3 }),
            });
            var outer = new NeoArray(new Neo.VM.Types.StackItem[] { inner, new NeoInteger(new BigInteger(42)) });

            outer.GetSize(ExecutionEngineLimits.Default.MaxItemSize).Should().Be(15);
        }

        [Fact]
        public void GetSize_throws_on_a_circular_reference()
        {
            var array = new NeoArray();
            array.Add(array);

            var act = () => array.GetSize(ExecutionEngineLimits.Default.MaxItemSize);

            act.Should().Throw<NotSupportedException>();
        }
    }
}
