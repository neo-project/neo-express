// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressChainManagerMutexTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress;
using Xunit;

namespace test.workflowvalidation;

public class ExpressChainManagerMutexTests
{
    static string UniqueName() => @"Global\neoxp-test-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void IsHeldNamedMutex_is_false_when_the_mutex_does_not_exist()
    {
        ExpressChainManager.IsHeldNamedMutex(UniqueName()).Should().BeFalse();
    }

    [Fact]
    public void IsHeldNamedMutex_is_true_when_another_owner_holds_it()
    {
        var name = UniqueName();
        using var acquired = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var owner = new Thread(() =>
        {
            using var mutex = new Mutex(true, name);
            acquired.Set();
            release.Wait();
        });
        owner.IsBackground = true;
        owner.Start();
        acquired.Wait(TestContext.Current.CancellationToken);
        try
        {
            ExpressChainManager.IsHeldNamedMutex(name).Should().BeTrue();
        }
        finally
        {
            release.Set();
            owner.Join();
        }
    }

    [Fact]
    public void IsHeldNamedMutex_is_false_when_the_named_mutex_is_not_held()
    {
        var name = UniqueName();
        using var created = new Mutex(false, name);
        ExpressChainManager.IsHeldNamedMutex(name).Should().BeFalse();
    }

    [Fact]
    public void IsHeldNamedMutex_is_false_when_the_owning_handle_is_closed()
    {
        var name = UniqueName();
        Mutex? owned = null;
        using var started = new ManualResetEventSlim(false);
        var owner = new Thread(() =>
        {
            owned = new Mutex(true, name, out var createdNew);
            createdNew.Should().BeTrue();
            started.Set();
        });
        owner.IsBackground = true;
        owner.Start();
        started.Wait(TestContext.Current.CancellationToken);
        owner.Join();

        owned!.Dispose();
        ExpressChainManager.IsHeldNamedMutex(name).Should().BeFalse();
    }
}
