// Copyright (C) 2015-2026 The Neo Project.
//
// JsonWriterExtensionsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.SmartContract.Iterators;
using Neo.VM;
using Neo.VM.Types;
using Newtonsoft.Json;
using System.Reflection;
using Xunit;

namespace test.runner;

public class JsonWriterExtensionsTests
{
    [Fact]
    public async Task WriteStackItemAsync_writes_iterator_values()
    {
        var iterator = new SingleItemIterator(new Integer(42));
        var item = new InteropInterface(iterator);
        using var textWriter = new StringWriter();
        using var jsonWriter = new JsonTextWriter(textWriter);

        var action = async () => await InvokeWriteStackItemAsync(jsonWriter, item, maxIteratorCount: 10);

        await action.Should().NotThrowAsync();
        textWriter.ToString().Should().Contain("\"iterator\"").And.Contain("\"truncated\":false");
    }

    private static async Task InvokeWriteStackItemAsync(JsonWriter writer, StackItem item, int maxIteratorCount)
    {
        var assembly = Assembly.Load("neo-test-runner");
        var type = assembly.GetType("Neo.Test.Runner.JsonWriterExtensions", throwOnError: true)!;
        var method = type.GetMethod("WriteStackItemAsync", BindingFlags.Public | BindingFlags.Static)!;
        var task = (Task)method.Invoke(null, [writer, item, maxIteratorCount, null])!;

        await task;
    }

    private sealed class SingleItemIterator : IIterator
    {
        private readonly StackItem value;
        private bool moved;

        public SingleItemIterator(StackItem value)
        {
            this.value = value;
        }

        public bool Next()
        {
            if (moved)
                return false;

            moved = true;
            return true;
        }

        public StackItem Value()
            => value;

        public void Dispose()
        {
        }
    }
}
