// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using Godot;
using tweens.gd.Tests.Support;
using twodog.Testing;
using twodog.Testing.Xunit;

namespace tweens.gd.Tests.Integration;

/// <summary>The automatic per-tree runner: creation, attachment, counting, cancellation and threading.</summary>
[Collection<HeadlessCollection>]
public class RuntimeTests(HeadlessFixture godot)
{
    // Replaces the tree's runner, so a test observes one it created.
    private TweenRunner FreshRunner()
    {
        var old = TweenRuntime.GetRunner(godot.Tree);
        old.Shutdown();
        old.Free();
        return TweenRuntime.GetRunner(godot.Tree);
    }

    [Fact]
    public void RunnerIsCreatedOncePerTreeAndAttachedDeferred()
    {
        using var scope = new SceneScope(godot);
        var runner = FreshRunner();
        Assert.False(runner.IsInsideTree());
        Assert.Same(runner, TweenRuntime.GetRunner(godot.Tree));
        var node = scope.Add(new Node2D());
        node.TweenPosition(new Vector2(10, 0), 1);
        Assert.Same(runner, TweenRuntime.GetRunner(node));
        scope.Frames();
        Assert.True(runner.IsInsideTree());
        Assert.Same(godot.Tree.Root, runner.GetParent());
        Assert.Equal("TweensGd", runner.Name.ToString());
        Assert.Equal(Node.ProcessModeEnum.Always, runner.ProcessMode);
        Assert.Equal(1000, runner.ProcessPriority);
        Assert.Equal(1000, runner.ProcessPhysicsPriority);
    }

    [Fact]
    public void ReplacedOrFreedRunnersAreNeverAttached()
    {
        FreshRunner();
        var replaced = TweenRuntime.GetRunner(godot.Tree);
        replaced.Shutdown();
        var freed = TweenRuntime.GetRunner(godot.Tree);
        freed.Shutdown();
        freed.Free();
        var current = TweenRuntime.GetRunner(godot.Tree);
        godot.Engine.Iteration();
        Assert.False(replaced.IsInsideTree());
        Assert.True(current.IsInsideTree());
        replaced.Free();

        // A runner shut down without a successor is not attached either.
        var orphaned = FreshRunner();
        orphaned.Shutdown();
        godot.Engine.Iteration();
        Assert.False(orphaned.IsInsideTree());
        orphaned.Free();
    }

    [Fact]
    public void RemovingAnotherTreesRunnerKeepsTheCurrentOne()
    {
        var current = TweenRuntime.GetRunner(godot.Tree);
        var stranger = new TweenRunner();
        TweenRuntime.Remove(godot.Tree, stranger);
        Assert.Same(current, TweenRuntime.GetRunner(godot.Tree));
        current.Shutdown();
        TweenRuntime.Remove(godot.Tree, stranger);
        Assert.NotSame(current, TweenRuntime.GetRunner(godot.Tree));
        current.Free();
        stranger.Free();
    }

    [Fact]
    public void RunnerAdvancesBothLanesWithEngineFrames()
    {
        using var scope = new SceneScope(godot);
        var process = scope.Add(new Node2D());
        var physics = scope.Add(new Node2D());
        var a = process.TweenPositionX(100, 10);
        var b = physics.TweenPositionX(100, 10, playback: new PlaybackOptions { ProcessMode = TweenProcessMode.Physics });
        var runner = TweenRuntime.GetRunner(godot.Tree);
        var ticks = runner.Ticks;
        scope.FramesUntil(() => a.Progress > 0 && b.Progress > 0);
        Assert.True(runner.Ticks > ticks);
    }

    [Fact]
    public void UnscaledTweensIgnoreTheEngineTimeScale()
    {
        using var scope = new SceneScope(godot);
        var scaled = scope.Add(new Node2D()).TweenPositionX(100, 10);
        var unscaled = scope.Add(new Node2D()).TweenPositionX(100, 10, playback: new PlaybackOptions { UseUnscaledTime = true });
        var unscaledPhysics = scope.Add(new Node2D()).TweenPositionX(100, 10, playback: new PlaybackOptions { UseUnscaledTime = true, ProcessMode = TweenProcessMode.Physics });
        scope.Frames();
        var scale = Engine.TimeScale;
        try
        {
            Engine.TimeScale = 0;
            var (scaledBefore, unscaledBefore, physicsBefore) = (scaled.Progress, unscaled.Progress, unscaledPhysics.Progress);
            scope.FramesUntil(() => unscaled.Progress > unscaledBefore && unscaledPhysics.Progress > physicsBefore);
            Assert.Equal(scaledBefore, scaled.Progress);
        }
        finally { Engine.TimeScale = scale; }
    }

    [Fact]
    public void StoppedRunnersIgnoreFrames()
    {
        using var scope = new SceneScope(godot);
        var runner = FreshRunner();
        scope.Frames();
        var tween = scope.Add(new Node2D()).TweenPositionX(100, 1);
        runner.Shutdown();
        runner.Shutdown();
        Assert.Equal(Reason.RunnerDisposed, tween.CompletionReason);
        var ticks = runner.Ticks;
        scope.Frames(2);
        Assert.True(runner.Ticks > ticks);
        runner.Free();
    }

    [Fact]
    public void RunnersRemovedWithoutInitializationShutDownCleanly()
    {
        using var scope = new SceneScope(godot);
        var runner = scope.Add(new TweenRunner());
        scope.Frames();
        Assert.True(runner.Ticks > 0);
        runner.Free();
    }

    [Fact]
    public async Task FreedRunnersSettleTheirTweensAndReadyHandlersBootstrapTheNext()
    {
        using var scope = new SceneScope(godot);
        var stale = scope.Add(new Node2D()).TweenPositionX(10, 10);
        var runner = TweenRuntime.GetRunner(godot.Tree);
        scope.Frames();
        runner.Free();
        Assert.Equal(Reason.RunnerDisposed, await stale.End);

        // Ready runs while the scope adds the node, so the runner it creates attaches later.
        var node = new Node2D();
        TweenInstance<Node2D, Vector2>? movement = null;
        node.Ready += () => movement = node.TweenPosition(new Vector2(20, 30), 0);
        scope.Add(node);
        Assert.NotNull(movement);
        var fresh = TweenRuntime.GetRunner(node);
        Assert.False(fresh.IsInsideTree());
        var thread = System.Environment.CurrentManagedThreadId;
        var continuation = -1;
        var observed = Observe();
        scope.FramesUntil(() => observed.IsCompleted, 20);
        await observed;
        Assert.True(fresh.IsInsideTree());
        Assert.Equal(thread, continuation);
        Assert.Equal(new Vector2(20, 30), node.Position);

        async Task Observe()
        {
            Assert.Equal(Reason.Completed, await movement!.End);
            continuation = System.Environment.CurrentManagedThreadId;
            // The continuation may start playback, which only Godot's main thread can.
            node.TweenPosition(new Vector2(30, 40), 1).Cancel();
        }
    }

    [Fact]
    public void ActiveCountReflectsTheTreeRunner()
    {
        using var scope = new SceneScope(godot);
        var node = scope.Add(new Node2D());
        var detached = FreshRunner();
        detached.Shutdown();
        Assert.Equal(0, TweenRuntime.GetActiveCount(node));
        node.CancelTweens(includeChildren: true);
        detached.Free();
        FreshRunner();
        node.TweenPositionX(10, 1);
        node.TweenRotation(1, 1);
        Assert.Equal(2, TweenRuntime.GetActiveCount(node));
    }

    [Fact]
    public void CancelTweensCancelsOwnedTweensAndOptionallyDescendants()
    {
        using var scope = new SceneScope(godot);
        var parent = scope.Add(new Node2D());
        var child = new Node2D();
        parent.AddChild(child);
        var material = scope.Track(new StandardMaterial3D());
        var own = parent.TweenPositionX(10, 1);
        var childTween = child.TweenPositionX(10, 1);
        var childResource = child.Tween(material, new MaterialMetallicTween { To = 1, Duration = 1 });
        var unowned = material.TweenRoughness(0, 1, godot.Tree);
        var sibling = scope.Add(new Node2D()).TweenPositionX(10, 1);
        var finished = scope.Add(new Node2D()).TweenPositionX(10, 1);
        finished.Cancel();

        parent.CancelTweens();
        Assert.Equal(Reason.Cancelled, own.CompletionReason);
        Assert.False(childTween.IsTerminal || childResource.IsTerminal);
        parent.CancelTweens(includeChildren: true);
        Assert.Equal(Reason.Cancelled, childTween.CompletionReason);
        Assert.Equal(Reason.Cancelled, childResource.CompletionReason);
        Assert.False(unowned.IsTerminal || sibling.IsTerminal);
        unowned.Cancel();
    }

    [Fact]
    public void CancelDescendantsAllowsCallbacksToFreeTheOwner()
    {
        using var scope = new SceneScope(godot);
        var parent = scope.Add(new Node2D());
        var other = scope.Add(new Node2D());
        var own = parent.TweenPositionX(10, 10, d => d.OnCancel = _ => parent.Free());
        var unrelated = other.TweenPositionX(10, 10);
        scope.Advance(0);
        parent.CancelTweens(includeChildren: true);
        Assert.Equal(Reason.Cancelled, own.CompletionReason);
        Assert.True(own.IsSettled);
        Assert.False(unrelated.IsTerminal);
    }

    [Fact]
    public void OwnersMustBeLiveNodesInTheTree()
    {
        using var scope = new SceneScope(godot);
        var orphan = new Node2D();
        var queued = scope.Add(new Node2D());
        queued.QueueFree();
        var freed = new Node2D();
        freed.Free();
        var material = scope.Track(new StandardMaterial3D());
        foreach (var node in new[] { orphan, queued, freed })
        {
            Assert.Throws<ArgumentException>(() => node.TweenPositionX(1, 1));
            Assert.Throws<ArgumentException>(() => material.TweenMetallic(1, 1, node));
            Assert.Throws<ArgumentException>(() => material.TweenMetallic(1, 1, godot.Tree, owner: node));
            Assert.Throws<ArgumentException>(() => TweenRuntime.GetActiveCount(node));
            Assert.Throws<ArgumentException>(() => node.CancelTweens());
        }
        orphan.Free();
    }

    [Fact]
    public void TreesMustBeLive()
    {
        using var scope = new SceneScope(godot);
        var material = scope.Track(new StandardMaterial3D());
        Assert.Throws<ArgumentException>(() => material.TweenMetallic(1, 1, (SceneTree)null!));
    }

    [Fact]
    public void GodotTargetsRequireTheMainThread()
    {
        using var scope = new SceneScope(godot);
        var node = scope.Add(new Node2D());
        var material = scope.Track(new StandardMaterial3D());
        Threads.RunElsewhere(() =>
        {
            Assert.Throws<InvalidOperationException>(() => node.TweenPositionX(1, 1));
            using var scheduler = new TweenScheduler();
            Assert.Throws<InvalidOperationException>(() => scheduler.Add(material, new MaterialMetallicTween()));
        });
    }

    [Fact]
    public void ManualSchedulersValidateGodotTargetsAndOwners()
    {
        using var scope = new SceneScope(godot);
        using var scheduler = new TweenScheduler();
        var node = scope.Add(new Node2D());
        var other = scope.Add(new Node2D());
        var disposed = new StandardMaterial3D();
        disposed.Dispose();
        Assert.Throws<ArgumentException>(() => scheduler.Add(disposed, new MaterialMetallicTween()));
        Assert.Throws<ArgumentException>(() => scheduler.Add(node, new Position2DTween(), other));
        var orphan = new Node();
        Assert.Throws<ArgumentException>(() => scheduler.Add(new Box(), new PlainTween(), orphan));
        orphan.Free();
        var owned = scheduler.Add(new Box(), new PlainTween { To = 1, Duration = 1 }, other);
        scheduler.Update(0.5);
        Assert.Equal(0.5f, owned.Progress);
    }

    [Fact]
    public void GroupsStartTogetherOnOneTarget()
    {
        using var scope = new SceneScope(godot);
        // Members may target any base type of the node.
        var node = scope.Add(new Sprite2D());
        var group = node.Tween(new Tweens.Position2DX { To = 10, Duration = 1 }, new Tweens.Rotation2D { To = 1, Duration = 2 },
            new Tweens.Scale2D { To = Vector2.One * 2, Duration = 1 }, new Tweens.ModulateAlpha(0, 1));
        Assert.Equal(4, group.Members.Count);
        scope.Advance(1);
        Assert.False(group.IsTerminal);
        scope.Advance(1);
        Assert.Equal(Reason.Completed, group.CompletionReason);
        Assert.Equal(new Vector2(10, 0), node.Position);
        Assert.Equal(0, node.Modulate.A);
    }

    [Fact]
    public void GroupStartValidatesDefinitionsAndCancelsDeferredEnrollments()
    {
        using var scope = new SceneScope(godot);
        var node = scope.Add(new Node2D());
        var first = new Tweens.Position2DX { To = 10, Duration = 1 };
        Assert.Throws<ArgumentNullException>(() => node.Tween(first, first, null!));
        Assert.Equal(0, TweenRuntime.GetActiveCount(node));
        Assert.Throws<ArgumentNullException>(() => node.Tween<Node2D>(first, null!));
        Assert.Equal(0, TweenRuntime.GetActiveCount(node));
        var calls = 0;
        var deferred = first with { OnAdd = _ => calls++, OnCancel = _ => calls++ };
        var invalid = new Position2DXTween { Duration = 1, EaseFunction = static x => x, Curve = scope.Track(new Curve()) };
        Assert.Throws<ArgumentException>(() => node.Tween(deferred, deferred, invalid));
        scope.Advance(0);
        Assert.Equal(0, calls);
        Assert.Equal(0, TweenRuntime.GetActiveCount(node));
    }

    [Fact]
    public void OptionsOverloadsValidateTheExplicitDuration()
    {
        using var scope = new SceneScope(godot);
        // The offset exceeds the options' own duration, which the explicit one replaces before validation.
        var tween = scope.Add(new Node2D()).TweenPositionX(10, 10, new TweenOptions { Duration = 1, Offset = 5 });
        scope.Advance(1);
        Assert.Equal(0.6f, tween.Progress);
    }
}
