// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System.Text.Json;
using tweens.gd.Tests.Support;

namespace tweens.gd.Tests.Unit;

public class GroupTests
{
    private static TweenInstance Start(TweenScheduler scheduler, double duration, Action<PlainTween>? configure = null)
    {
        var definition = new PlainTween { Duration = duration };
        configure?.Invoke(definition);
        return scheduler.Add(new Box(), definition);
    }

    [Fact]
    public void OfValidatesItsMembers()
    {
        using var scheduler = new TweenScheduler();
        Assert.Throws<ArgumentNullException>(() => Group.Of(null!));
        Assert.Throws<ArgumentException>(() => Group.Of());
        Assert.Throws<ArgumentNullException>(() => Group.Of(Start(scheduler, 1), null!));
    }

    [Fact]
    public async Task CompletesWhenEveryMemberCompleted()
    {
        using var scheduler = new TweenScheduler();
        var members = new[] { Start(scheduler, 1), Start(scheduler, 2) };
        var group = Group.Of(members);
        Assert.Equal(members, group.Members);
        var end = group.End;
        Assert.Same(end, group.End);
        scheduler.Update(1);
        Assert.False(group.IsTerminal);
        scheduler.Update(1);
        Assert.True(group.IsTerminal);
        Assert.Equal(Reason.Completed, group.CompletionReason);
        Assert.Null(group.Error);
        Assert.Equal(Reason.Completed, await end);
    }

    [Fact]
    public async Task EndRequestedAfterSettlementIsAlreadyComplete()
    {
        using var scheduler = new TweenScheduler();
        var group = Group.Of(Start(scheduler, 0));
        scheduler.Update(0);
        Assert.True(group.End.IsCompletedSuccessfully);
        Assert.Equal(Reason.Completed, await group.End);
    }

    [Fact]
    public async Task OneStoppedMemberCancelsTheOthersAndGivesTheGroupItsReason()
    {
        using var scheduler = new TweenScheduler();
        var a = Start(scheduler, 1);
        var b = Start(scheduler, 1);
        var c = Start(scheduler, 1);
        var group = Group.Of(a, b, c);
        b.Cancel();
        Assert.All(new[] { a, b, c }, t => Assert.Equal(Reason.Cancelled, t.CompletionReason));
        Assert.Equal(Reason.Cancelled, await group.End);

        var disposing = new TweenScheduler();
        var disposed = Group.Of(Start(disposing, 1), Start(disposing, 1));
        disposing.Dispose();
        Assert.All(disposed.Members, t => Assert.True(t.IsTerminal));
        Assert.Equal(Reason.RunnerDisposed, await disposed.End);
    }

    [Fact]
    public void AlreadySettledMembersAreAccountedAtCreation()
    {
        using var scheduler = new TweenScheduler();
        var done = Start(scheduler, 0);
        scheduler.Update(0);
        var running = Start(scheduler, 1);
        var group = Group.Of(done, running);
        Assert.False(group.IsTerminal);
        scheduler.Update(1);
        Assert.Equal(Reason.Completed, group.CompletionReason);

        var cancelled = Start(scheduler, 1);
        cancelled.Cancel();
        var sibling = Start(scheduler, 1);
        var stopped = Group.Of(cancelled, sibling);
        Assert.True(stopped.IsTerminal);
        Assert.Equal(Reason.Cancelled, sibling.CompletionReason);
    }

    [Fact]
    public async Task AlreadySettledFailuresAreCollectedOnceRegardlessOfMemberOrder()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "conformance", "groups.json")));
        foreach (var order in data.RootElement.GetProperty("settled_failure_orders").EnumerateArray())
        {
            using var scheduler = new TweenScheduler();
            var cancelled = Start(scheduler, 1);
            cancelled.Cancel();
            var running = Start(scheduler, 1);
            var failed = Start(scheduler, 1, d => d.OnStart = _ => throw new FormatException("member failure"));
            scheduler.Update(0);
            TweenInstance[] members = [cancelled, running, failed];
            var group = Group.Of(order.EnumerateArray().Select(i => members[i.GetInt32()]).ToArray());
            Assert.True(group.IsTerminal);
            Assert.Equal(Reason.Cancelled, running.CompletionReason);
            Assert.Same(failed.Error, group.Error);
            await Assert.ThrowsAsync<FormatException>(() => group.End);
        }
    }

    [Fact]
    public async Task MemberErrorsFaultTheGroup()
    {
        using var scheduler = new TweenScheduler();
        var single = Group.Of(Start(scheduler, 1, d => d.OnStart = _ => throw new FormatException()), Start(scheduler, 1));
        var several = Group.Of(
            Start(scheduler, 1, d => d.OnStart = _ => throw new FormatException()),
            Start(scheduler, 1, d => d.OnCancel = _ => throw new InvalidCastException()));
        several.Members[1].Initialize();
        scheduler.Update(0.5);
        Assert.IsType<FormatException>(single.Error);
        Assert.Equal(Reason.Cancelled, single.CompletionReason);
        await Assert.ThrowsAsync<FormatException>(() => single.End);
        var aggregate = Assert.IsType<AggregateException>(several.Error);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        await Assert.ThrowsAsync<AggregateException>(() => several.End);
    }

    [Fact]
    public void PauseResumeAndCancelApplyToEveryMember()
    {
        using var scheduler = new TweenScheduler();
        var a = Start(scheduler, 1);
        var b = Start(scheduler, 2);
        var group = Group.Of(a, b);
        Assert.False(group.IsPaused);
        a.Pause();
        Assert.False(group.IsPaused);
        group.Pause();
        Assert.True(group.IsPaused);
        group.Resume();
        Assert.False(a.IsPaused || b.IsPaused);
        scheduler.Update(1);
        b.Pause();
        Assert.True(group.IsPaused);
        group.Cancel();
        Assert.Equal(Reason.Completed, a.CompletionReason);
        Assert.Equal(Reason.Cancelled, b.CompletionReason);
        Assert.False(group.IsPaused);
        Assert.Equal(Reason.Cancelled, group.CompletionReason);
    }

    [Fact]
    public void PollingPausedMembersDoesNotAllocate()
    {
        using var scheduler = new TweenScheduler();
        var group = Group.Of(Start(scheduler, 1), Start(scheduler, 2));
        group.Pause();
        for (var i = 0; i < 128; i++) _ = group.IsPaused;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var paused = true;
        for (var i = 0; i < 1024; i++) paused &= group.IsPaused;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(paused);
        Assert.Equal(0, allocated);
    }
}
