// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressPersistencePluginGateTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress.Node;
using System;
using System.Threading.Tasks;
using Xunit;

namespace test.workflowvalidation;

// The persist gate must guarantee two invariants: checkpoint actions never
// overlap each other, and a block persist cycle is never admitted while a
// checkpoint action is in flight (a checkpoint taken after a block is persisted
// but before its application logs are committed would capture the block without
// its logs). Each test drives the gate with a TaskCompletionSource-stepped
// schedule, so the interleaving is deterministic. The only sleeps are bounded
// windows in which a gate breach - if the bug is present - shows up immediately,
// because the buggy admission does not block at all.
public class ExpressPersistencePluginGateTests : IDisposable
{
    static readonly TimeSpan EnterTimeout = TimeSpan.FromSeconds(5);
    static readonly TimeSpan BreachWindow = TimeSpan.FromMilliseconds(200);

    static System.Threading.CancellationToken Token => TestContext.Current.CancellationToken;

    readonly ExpressPersistencePlugin plugin = new();

    public void Dispose()
    {
        plugin.Dispose();
        plugin.RemoveFromPluginList();
    }

    static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Second_checkpoint_waits_and_persist_cycle_is_excluded_from_the_checkpoint_still_running()
    {
        var c1Inside = Gate();
        var c1Release = Gate();
        var c2Inside = Gate();
        var c2Release = Gate();
        var persistInside = Gate();

        Task c1 = Task.CompletedTask, c2 = Task.CompletedTask, persist = Task.CompletedTask;
        bool secondCheckpointRanDuringFirst = false;
        bool persistBeganDuringSecond = false;

        try
        {
            // step 1: the first checkpoint enters the gate-protected region
            c1 = Task.Run(() => plugin.RunOutsidePersistCycle(() =>
            {
                c1Inside.SetResult();
                c1Release.Task.Wait();
            }), Token);
            await c1Inside.Task.WaitAsync(EnterTimeout, Token);

            // step 2: a second checkpoint tries to enter while the first is in flight
            c2 = Task.Run(() => plugin.RunOutsidePersistCycle(() =>
            {
                c2Inside.SetResult();
                c2Release.Task.Wait();
            }), Token);
            await Task.Delay(BreachWindow, Token);
            secondCheckpointRanDuringFirst = c2Inside.Task.IsCompleted;

            // step 3: the first checkpoint completes and releases the gate; with the
            // bug, its finally block clears `checkpointing` even though the second
            // checkpoint is still inside
            c1Release.SetResult();
            await c1.WaitAsync(EnterTimeout, Token);

            // step 4: the second checkpoint enters once the gate is free
            await c2Inside.Task.WaitAsync(EnterTimeout, Token);

            // step 5: a block persist cycle tries to begin while the second
            // checkpoint is still in flight; with the bug it is admitted, so a
            // block can be persisted without its application logs being captured
            persist = Task.Run(() =>
            {
                plugin.BeginPersistCycle();
                persistInside.SetResult();
                plugin.EndPersistCycle();
            }, Token);
            await Task.Delay(BreachWindow, Token);
            persistBeganDuringSecond = persistInside.Task.IsCompleted;

            // step 6: the second checkpoint completes and the persist cycle is admitted
            c2Release.SetResult();
            await c2.WaitAsync(EnterTimeout, Token);
            await persistInside.Task.WaitAsync(EnterTimeout, Token);
            await persist.WaitAsync(EnterTimeout, Token);
        }
        finally
        {
            c1Release.TrySetResult();
            c2Release.TrySetResult();
            try
            {
                await Task.WhenAll(c1, c2, persist).WaitAsync(EnterTimeout, Token);
            }
            catch (Exception) { }
        }

        secondCheckpointRanDuringFirst.Should().BeFalse(
            "the persist gate must not admit a second checkpoint while the first is still running");
        persistBeganDuringSecond.Should().BeFalse(
            "the persist gate must not begin a persist cycle while a checkpoint action is in flight");
    }

    [Fact]
    public async Task Checkpoint_waits_for_an_in_flight_persist_cycle()
    {
        var persistInside = Gate();
        var persistRelease = Gate();
        var checkpointInside = Gate();
        var checkpointRelease = Gate();

        Task persist = Task.CompletedTask, checkpoint = Task.CompletedTask;
        try
        {
            persist = Task.Run(() =>
            {
                plugin.BeginPersistCycle();
                persistInside.SetResult();
                persistRelease.Task.Wait();
                plugin.EndPersistCycle();
            }, Token);
            await persistInside.Task.WaitAsync(EnterTimeout, Token);

            checkpoint = Task.Run(() => plugin.RunOutsidePersistCycle(() =>
            {
                checkpointInside.SetResult();
                checkpointRelease.Task.Wait();
            }), Token);

            await Task.Delay(BreachWindow, Token);
            checkpointInside.Task.IsCompleted.Should().BeFalse(
                "a checkpoint must not run while a persist cycle is in flight");

            persistRelease.SetResult();
            await checkpointInside.Task.WaitAsync(EnterTimeout, Token);
            checkpointRelease.SetResult();
            await Task.WhenAll(persist, checkpoint).WaitAsync(EnterTimeout, Token);
        }
        finally
        {
            persistRelease.TrySetResult();
            checkpointRelease.TrySetResult();
            try
            {
                await Task.WhenAll(persist, checkpoint).WaitAsync(EnterTimeout, Token);
            }
            catch (Exception) { }
        }
    }

    [Fact]
    public async Task A_new_persist_cycle_supersedes_one_that_never_committed()
    {
        // If a persist failed so Committed never fired, the next Committing must
        // supersede the dead cycle instead of deadlocking checkpoint creators.
        plugin.BeginPersistCycle();
        plugin.BeginPersistCycle(); // supersedes; must not block

        var checkpointInside = Gate();
        var checkpointRelease = Gate();
        Task checkpoint = Task.CompletedTask;
        try
        {
            checkpoint = Task.Run(() => plugin.RunOutsidePersistCycle(() =>
            {
                checkpointInside.SetResult();
                checkpointRelease.Task.Wait();
            }), Token);

            await Task.Delay(BreachWindow, Token);
            checkpointInside.Task.IsCompleted.Should().BeFalse(
                "the gate must still hold checkpoints off while a persist cycle is flagged");

            plugin.EndPersistCycle();
            await checkpointInside.Task.WaitAsync(EnterTimeout, Token);
        }
        finally
        {
            checkpointRelease.TrySetResult();
            try
            {
                await checkpoint.WaitAsync(EnterTimeout, Token);
            }
            catch (Exception) { }
        }
    }
}
