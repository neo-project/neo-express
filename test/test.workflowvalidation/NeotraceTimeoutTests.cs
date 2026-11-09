// Copyright (C) 2015-2026 The Neo Project.
//
// NeotraceTimeoutTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Xunit;

namespace test.workflowvalidation;

public class NeotraceTimeoutTests
{
    [Fact]
    public async Task RunWithTimeoutAsync_throws_clear_timeout()
    {
        Func<Task> act = () => NeoTrace.Program.RunWithTimeoutAsync(
            async token => await Task.Delay(TimeSpan.FromSeconds(10), token),
            timeoutSeconds: 1,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<TimeoutException>();
        exception.Which.Message.Should().Be("NeoTrace did not complete within 1 seconds. Use --timeout 0 to disable the timeout.");
    }

    [Fact]
    public async Task RunWithTimeoutAsync_allows_timeout_to_be_disabled()
    {
        var completed = false;

        await NeoTrace.Program.RunWithTimeoutAsync(
            async token =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), token);
                completed = true;
            },
            timeoutSeconds: 0,
            CancellationToken.None);

        completed.Should().BeTrue();
    }

    [Fact]
    public async Task RunWithTimeoutAsync_rejects_negative_timeout()
    {
        Func<Task> act = () => NeoTrace.Program.RunWithTimeoutAsync(
            _ => Task.CompletedTask,
            timeoutSeconds: -1,
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
