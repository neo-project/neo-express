// Copyright (C) 2015-2024 The Neo Project.
//
// StorageItemAssertions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Neo.SmartContract;
using System.Numerics;

namespace Neo.Assertions
{
    public class StorageItemAssertions : ReferenceTypeAssertions<StorageItem, StorageItemAssertions>
    {
        public StorageItemAssertions(StorageItem subject) : base(subject)
        {
        }

        protected override string Identifier => nameof(StorageItem);

        AndConstraint<StorageItemAssertions> Be<T>(T expected, Func<ReadOnlyMemory<byte>, T> convert, string because = "", params object[] becauseArgs)
            where T : IEquatable<T>
        {
            Execute.Assertion
                .Given(() => convert(Subject.Value))
                .ForCondition(subject => subject.Equals(expected))
                .FailWith("Expected {context:StorageItem} to be {0}{reason}, but was {1}.",
                    _ => expected, subject => subject);

            return new AndConstraint<StorageItemAssertions>(this);
        }

        public AndConstraint<StorageItemAssertions> Be(BigInteger expected, string because = "", params object[] becauseArgs)
            => Be<BigInteger>(expected, bytes => new BigInteger(bytes.Span), because, becauseArgs);

        public AndConstraint<StorageItemAssertions> Be(UInt160 expected, string because = "", params object[] becauseArgs)
            => Be<UInt160>(expected, bytes => new UInt160(bytes.Span), because, becauseArgs);

        public AndConstraint<StorageItemAssertions> Be(UInt256 expected, string because = "", params object[] becauseArgs)
            => Be<UInt256>(expected, bytes => new UInt256(bytes.Span), because, becauseArgs);
    }
}
