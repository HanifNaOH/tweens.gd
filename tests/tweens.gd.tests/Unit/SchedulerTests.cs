// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using tweens.gd.Tests.Support;

namespace tweens.gd.Tests.Unit;

public class SchedulerTests
{
    [Fact]
    public void AddValidatesItsArguments()
    {
        using var scheduler = new TweenScheduler();
        Assert.Throws<ArgumentNullException>(() => scheduler.Add<Box, float>(null!, new PlainTween()));
        Assert.Throws<ArgumentNullException>(() => scheduler.Add(new Box(), (ITweenDefinition<Box, float>)null!));
        Assert.Throws<ArgumentNullException>(() => scheduler.Add(new Box(), new PlainTween(), null!));
    }

    [Fact]
    public void ActiveCountTracksUnfinishedTweens()
    {
        using var scheduler = new TweenScheduler();
        Assert.Equal(0, scheduler.ActiveCount);
        var short1 = scheduler.Add(new Box(), new PlainTween { Duration = 1 });
        scheduler.Add(new Box(), new PlainTween { Duration = 2 });
        Assert.Equal(2, scheduler.ActiveCount);
        short1.Cancel();
        Assert.Equal(1, scheduler.ActiveCount);
        scheduler.Update(2);
        Assert.Equal(0, scheduler.ActiveCount);
    }

    [Fact]
    public void UpdateValidatesItsArguments()
    {
        using var scheduler = new TweenScheduler();
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Update(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Update(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Update(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Update(1, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Update(1, mode: (TweenProcessMode)2));
    }

    [Fact]
    public void RecursiveUpdatesFaultTheCallingTween()
    {
        using var scheduler = new TweenScheduler();
        var tween = scheduler.Add(new Box(), new PlainTween { Duration = 1, OnUpdate = (t, _) => t.Scheduler.Update(0) });
        scheduler.Update(0.5);
        Assert.Equal(TweenState.Faulted, tween.State);
        Assert.IsType<InvalidOperationException>(tween.Error);
    }

    [Fact]
    public void UnscaledTweensUseTheUnscaledDeltaWhenGiven()
    {
        using var scheduler = new TweenScheduler();
        var scaled = new Box(); var unscaled = new Box();
        scheduler.Add(scaled, new PlainTween { To = 1, Duration = 1 });
        scheduler.Add(unscaled, new PlainTween { To = 1, Duration = 1  }, new PlaybackOptions { UseUnscaledTime = true });
        scheduler.Update(0.25);
        Assert.Equal(0.25f, unscaled.Value);
        scheduler.Update(0.25, 0.5);
        Assert.Equal(0.5f, scaled.Value);
        Assert.Equal(0.75f, unscaled.Value);
    }

    [Fact]
    public void TweensAddedDuringAnUpdateWaitForTheNextOne()
    {
        using var scheduler = new TweenScheduler();
        var later = new Box();
        TweenInstance? added = null;
        scheduler.Add(new Box(), new PlainTween
        {
            Duration = 1,
            OnStart = t => added = t.Scheduler.Add(later, new PlainTween { To = 4, Duration = 1 }),
        });
        scheduler.Update(0.5);
        Assert.NotNull(added);
        Assert.Equal(0, later.Value);
        scheduler.Update(0.5);
        Assert.Equal(2, later.Value);
    }

    [Fact]
    public void CancelAllToleratesCallbacksThatAddOrRemoveTweens()
    {
        using var scheduler = new TweenScheduler();
        var survivors = new List<TweenInstance>();
        scheduler.Add(new Box(), new PlainTween
        {
            Duration = 1,
            OnCancel = t => survivors.Add(t.Scheduler.Add(new Box(), new PlainTween { Duration = 1 })),
        });
        scheduler.Add(new Box(), new PlainTween { Duration = 1 });
        scheduler.Update(0);
        scheduler.CancelAll();
        Assert.Single(survivors);
        Assert.False(survivors[0].IsTerminal);
        Assert.Equal(1, scheduler.ActiveCount);
    }

    [Fact]
    public void CancelCallbacksCanCancelAllOrDisposeTheScheduler()
    {
        using var scheduler = new TweenScheduler();
        scheduler.Add(new Box(), new PlainTween { OnCancel = static t => t.Scheduler.CancelAll() });
        scheduler.Add(new Box(), new PlainTween { OnCancel = static t => t.Scheduler.Dispose() });
        var last = scheduler.Add(new Box(), new PlainTween());
        scheduler.CancelAll();
        Assert.True(last.IsTerminal);
        Assert.Equal(0, scheduler.ActiveCount);
    }

    [Fact]
    public void SteadyStateUpdatesDoNotAllocate()
    {
        using var scheduler = new TweenScheduler();
        for (var i = 0; i < 100; i++)
            scheduler.Add(new Box(), new PlainTween { Duration = 1, Repeats = TweenOptions.Infinite });
        for (var i = 0; i < 100; i++) scheduler.Update(0.01);
        // Bytes per update, so a one-off runtime allocation from parallel tests cannot fail it.
        const int updates = 1024;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < updates; i++) scheduler.Update(0.01);
        Assert.Equal(0, (GC.GetAllocatedBytesForCurrentThread() - before) / updates);
    }

    [Fact]
    public void ObserverExceptionsAreSwallowed()
    {
        using var scheduler = new TweenScheduler();
        scheduler.Report(new FormatException("nobody listens"));
        scheduler.UnhandledException += _ => throw new InvalidOperationException("observer");
        var failing = scheduler.Add(new Box(), new PlainTween { OnStart = _ => throw new FormatException() });
        scheduler.Update(0);
        Assert.Equal(TweenState.Faulted, failing.State);
        Assert.IsType<FormatException>(failing.Error);
    }

    [Fact]
    public void DisposalIsIdempotentAndRejectsFurtherUse()
    {
        var scheduler = new TweenScheduler();
        var tween = scheduler.Add(new Box(), new PlainTween { Duration = 1 });
        scheduler.UnhandledException += _ => { };
        scheduler.Dispose();
        scheduler.Dispose();
        Assert.Equal(Reason.RunnerDisposed, tween.CompletionReason);
        Assert.Throws<ObjectDisposedException>(() => scheduler.Add(new Box(), new PlainTween()));
        Assert.Throws<ObjectDisposedException>(() => scheduler.Update(0));
        Assert.Equal(0, scheduler.ActiveCount);
    }

    private sealed class CopyProbe : TweenBinding<Box, float>
    {
        public Action? OnCopy;
        public override TweenBinding<Box, float> Copy() { OnCopy?.Invoke(); return this; }
        protected override float Read(Box target) => target.Value;
        protected override void Write(Box target, float value) => target.Value = value;
    }

    [Theory]
    [InlineData("single")]
    [InlineData("sampled")]
    [InlineData("group")]
    [InlineData("chain")]
    public void DisposalDuringBindingCopyRejectsEveryStartPath(string start)
    {
        using var scheduler = new TweenScheduler();
        var binding = new CopyProbe();
        var definition = new PropertyTween<Box, float>(binding, Interpolators.Float) { To = 1, Duration = 1 };
        var sampled = new Tweens.Sampled<Box, float>(binding, KeyframeCurve<float>.EvenlySpaced([0, 1]));
        binding.OnCopy = scheduler.Dispose;
        var existing = scheduler.Add(new Box(), new PlainTween { Duration = 1 });
        Assert.Throws<ObjectDisposedException>(() =>
        {
            switch (start)
            {
                case "single": scheduler.Add(new Box(), definition); break;
                case "sampled": scheduler.Add(new Box(), sampled); break;
                case "group": scheduler.AddAll(new Box(), [new PlainTween(), definition]); break;
                case "chain": scheduler.AddChain(new Box(), [new PlainTween(), definition]); break;
            }
        });
        Assert.Equal(Reason.RunnerDisposed, existing.CompletionReason);
        Assert.True(existing.End.IsCompletedSuccessfully);
        Assert.Equal(0, scheduler.ActiveCount);
        scheduler.Dispose();
        Assert.Equal(0, scheduler.ActiveCount);
    }

    [Fact]
    public void DisposalDuringUpdateStopsTheUpdate()
    {
        var scheduler = new TweenScheduler();
        var after = new Box();
        scheduler.Add(new Box(), new PlainTween { Duration = 1, OnUpdate = (t, _) => t.Scheduler.Dispose() });
        var second = scheduler.Add(after, new PlainTween { To = 1, Duration = 1 });
        var paused = scheduler.Add(new Box(), new PlainTween { Duration = 1 });
        paused.Pause();
        scheduler.Update(0.5);
        Assert.Equal(0, after.Value);
        Assert.Equal(Reason.RunnerDisposed, second.CompletionReason);
        Assert.Equal(Reason.RunnerDisposed, paused.CompletionReason);
        Assert.True(paused.End.IsCompletedSuccessfully);
        Assert.Equal(0, scheduler.ActiveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposalDuringPreparationSettlesTheNewTween(bool whileReading)
    {
        var scheduler = new TweenScheduler();
        var released = false;
        var tween = scheduler.Add(new Box(), new ProbeTween
        {
            Duration = 1,
            Preparing = _ => { if (!whileReading) scheduler.Dispose(); },
            Reader = b => { if (whileReading) scheduler.Dispose(); return b.Value; },
            Releasing = () => released = true,
        });
        Assert.False(released);
        scheduler.Update(0);
        Assert.Equal(Reason.RunnerDisposed, tween.CompletionReason);
        Assert.True(released);
        Assert.True(tween.End.IsCompletedSuccessfully);
    }

    [Fact]
    public void SchedulersBelongToTheirCreatingThread()
    {
        using var scheduler = new TweenScheduler();
        var tween = scheduler.Add(new Box(), new PlainTween { Duration = 1 });
        Threads.RunElsewhere(() =>
        {
            Assert.Throws<InvalidOperationException>(() => scheduler.Update(0));
            Assert.Throws<InvalidOperationException>(() => scheduler.Add(new Box(), new PlainTween()));
            Assert.Throws<InvalidOperationException>(() => scheduler.ActiveCount);
            Assert.Throws<InvalidOperationException>(() => scheduler.CancelAll());
            Assert.Throws<InvalidOperationException>(() => scheduler.Dispose());
            Assert.Throws<InvalidOperationException>(() => tween.Pause());
            Assert.Throws<InvalidOperationException>(() => tween.Cancel());
            Assert.Throws<InvalidOperationException>(() => { _ = tween.End; });
        });
        Assert.False(tween.IsTerminal);
    }
}
