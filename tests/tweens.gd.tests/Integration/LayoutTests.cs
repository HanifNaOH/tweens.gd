using Godot;
using tweens.gd.Tests.Support;
using twodog.Testing;
using twodog.Testing.Xunit;

namespace tweens.gd.Tests.Integration;

[Collection<HeadlessCollection>]
public class LayoutTests(HeadlessFixture godot)
{
    private static (HBoxContainer Root, Control[] Slots, Control[] Visuals) MakeRow(SceneScope scope, int count)
    {
        var root = new HBoxContainer { Size = new Vector2(320, 240), CustomMinimumSize = new Vector2(320, 240) };
        var slots = new Control[count];
        var visuals = new Control[count];
        for (var index = 0; index < count; index++)
        {
            var slot = new Control { Name = $"Slot{index}", CustomMinimumSize = new Vector2(60, 40) };
            var visual = new ColorRect { Name = "Visual", Color = new Color(0.2f + index * 0.1f, 0.4f, 0.7f) };
            visual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            slot.AddChild(visual);
            root.AddChild(slot);
            slots[index] = slot;
            visuals[index] = visual;
        }
        scope.Add(root);
        return (root, slots, visuals);
    }

    private static (VBoxContainer Root, Control[] Slots, Control[] Visuals) MakeColumn(SceneScope scope, int count)
    {
        var root = new VBoxContainer { Size = new Vector2(320, 240), CustomMinimumSize = new Vector2(320, 240) };
        var slots = new Control[count];
        var visuals = new Control[count];
        for (var index = 0; index < count; index++)
        {
            var slot = new Control { Name = $"Slot{index}", CustomMinimumSize = new Vector2(60, 40) };
            var visual = new ColorRect { Name = "Visual", Color = new Color(0.2f + index * 0.1f, 0.4f, 0.7f) };
            visual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            slot.AddChild(visual);
            root.AddChild(slot);
            slots[index] = slot;
            visuals[index] = visual;
        }
        scope.Add(root);
        return (root, slots, visuals);
    }

    [Fact]
    public async Task ReorderAnimatesExistingVisualsWithoutChangingDefaults()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 3);
        scope.Frames(2);
        var firstPositions = visuals.Select(visual => visual.GlobalPosition).ToArray();
        var defaults = new LayoutOptions { Duration = 0.2 };
        var layout = Tweens.CreateLayout(root, defaults);
        var transition = layout.Update(container =>
        {
            container.MoveChild(slots[2], 0);
            root.QueueSort();
        }, defaults with { Duration = 0.08 });

        Assert.Equal(firstPositions, visuals.Select(visual => visual.GlobalPosition));
        scope.FramesUntil(() => transition.End.IsCompleted, 200);
        Assert.Equal(Reason.Completed, await transition.End);
        Assert.True(visuals[2].GlobalPosition.IsEqualApprox(slots[2].GlobalPosition));
        Assert.Equal(0.2, layout.Options.Duration.Seconds);
    }

    [Fact]
    public async Task ResizeAnimatesSlotSizeAndFollowingItems()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeColumn(scope, 2);
        scope.Frames(2);

        var oldSize = visuals[0].Size;
        var oldSecondPosition = visuals[1].GlobalPosition;
        var layout = Tweens.CreateLayout(root);
        var transition = layout.Update(_ =>
        {
            slots[0].CustomMinimumSize = new Vector2(60, 100);
            root.QueueSort();
        }, new LayoutOptions { Duration = 0.08, StaggerEach = 0.01, SizeEase = Out.Cubic });

        scope.FramesUntil(() => transition.End.IsCompleted, 200);
        Assert.Equal(Reason.Completed, await transition.End);
        Assert.NotEqual(oldSize, visuals[0].Size);
        Assert.True(visuals[0].Size.IsEqualApprox(slots[0].Size));
        Assert.NotEqual(oldSecondPosition, visuals[1].GlobalPosition);
        Assert.True(visuals[1].GlobalPosition.IsEqualApprox(slots[1].GlobalPosition));
    }

    [Fact]
    public async Task RepeatedResizeDuringAnimationFollowsCurrentSlotBounds()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 2);
        foreach (var slot in slots) slot.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scope.Frames(2);

        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.08 });
        var transition = layout.Update(container =>
        {
            container.MoveChild(slots[1], 0);
            root.QueueSort();
        });
        scope.FramesUntil(() => transition.IsAnimating, 80);
        var resizing = false;
        var resizeFrame = 0;
        var resizeTimer = new Godot.Timer { WaitTime = 0.001, OneShot = false };
        resizeTimer.Timeout += () =>
        {
            if (!resizing) return;
            root.Size = new Vector2(340 + resizeFrame++ * 8, 240);
            root.QueueSort();
        };
        root.AddChild(resizeTimer);
        resizing = true;
        resizeTimer.Start();
        scope.Frames(24);
        resizeTimer.Stop();
        Assert.True(transition.IsAnimating,
            $"Transition ended after {resizeFrame} resizes: reason={transition.CompletionReason}, error={transition.Error}");
        for (var index = 0; index < slots.Length; index++)
        {
            Assert.True(visuals[index].GlobalPosition.IsEqualApprox(slots[index].GlobalPosition));
            Assert.True(visuals[index].Size.IsEqualApprox(slots[index].Size));
        }

        scope.FramesUntil(() => transition.End.IsCompleted, 40);
        Assert.Equal(Reason.Completed, await transition.End);
    }

    [Fact]
    public async Task DeferredContainerReflowKeepsVisualsAtTheirFirstRects()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeColumn(scope, 3);
        scope.Frames(2);
        var firstRects = visuals.Select(visual => visual.GetGlobalRect()).ToArray();
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.5, Delay = 0.5 });
        var transition = layout.Update(container =>
        {
            container.MoveChild(slots[2], 0);
            root.QueueSort();
        });
        for (var sample = 0; sample < 3; sample++)
        {
            scope.Frames(1);
            for (var index = 0; index < visuals.Length; index++)
            {
                var current = visuals[index].GetGlobalRect();
                Assert.True(current.Position.DistanceTo(firstRects[index].Position) < 0.05f,
                    $"Visual {index} moved from {firstRects[index].Position} to {current.Position}");
                Assert.True(current.Size.DistanceTo(firstRects[index].Size) < 0.05f,
                    $"Visual {index} resized from {firstRects[index].Size} to {current.Size}");
            }
        }

        transition.Cancel();
        scope.FramesUntil(() => transition.End.IsCompleted, 40);
        Assert.Equal(Reason.Cancelled, await transition.End);
    }

    [Fact]
    public async Task ZeroDurationCanBeReusedAndFreedItemsDoNotBlockSurvivors()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 3);
        scope.Frames(2);
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0 });
        var first = layout.Update(container =>
        {
            container.MoveChild(slots[2], 0);
            root.QueueSort();
        });
        scope.FramesUntil(() => first.End.IsCompleted, 100);
        Assert.Equal(Reason.Completed, await first.End);
        Assert.True(visuals[2].GlobalPosition.IsEqualApprox(slots[2].GlobalPosition));

        var second = layout.Update(container =>
        {
            slots[1].QueueFree();
            container.MoveChild(slots[0], 0);
            root.QueueSort();
        }, new LayoutOptions { Duration = 0.05 });
        scope.FramesUntil(() => second.End.IsCompleted, 100);
        Assert.Equal(Reason.Completed, await second.End);
        Assert.True(visuals[0].GlobalPosition.IsEqualApprox(slots[0].GlobalPosition));
        Assert.True(visuals[2].GlobalPosition.IsEqualApprox(slots[2].GlobalPosition));
        Assert.False(layout.IsActive);
    }

    [Fact]
    public async Task MutationFailureFaultsAfterSettlingTheLayout()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 3);
        scope.Frames(2);
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.05 });
        var transition = layout.Update(container =>
        {
            container.MoveChild(slots[2], 0);
            root.QueueSort();
            throw new InvalidOperationException("mutation failed");
        });

        scope.FramesUntil(() => transition.End.IsCompleted, 100);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => transition.End);
        Assert.Contains("layout mutation failed", error.Message, StringComparison.OrdinalIgnoreCase);
        for (var index = 0; index < slots.Length; index++)
            Assert.True(visuals[index].GlobalPosition.IsEqualApprox(slots[index].GlobalPosition));
        Assert.False(layout.IsActive);
    }

    [Fact]
    public async Task ContinuouslyChangingContainerFailsWithinTheReflowBound()
    {
        using var scope = new SceneScope(godot);
        var (root, _, _) = MakeColumn(scope, 1);
        scope.Frames(2);
        var slot = (Control)root.GetChild(0);
        var jitter = new Godot.Timer { WaitTime = 0.001, OneShot = false, Autostart = true };
        var expanded = false;
        jitter.Timeout += () =>
        {
            expanded = !expanded;
            slot.CustomMinimumSize = new Vector2(60, expanded ? 41 : 40);
            root.QueueSort();
        };
        root.AddChild(jitter);
        var layout = Tweens.CreateLayout(root);
        var transition = layout.Update(_ => { });

        scope.FramesUntil(() => transition.End.IsCompleted, 40);
        await Assert.ThrowsAsync<TimeoutException>(() => transition.End);
        Assert.False(layout.IsActive);
    }

    [Fact]
    public async Task OverlappingUpdateFaultsWithoutCallingMutationAndCancelSettlesVisuals()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 3);
        scope.Frames(2);
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 1 });
        var transition = layout.Update(container =>
        {
            container.MoveChild(slots[2], 0);
            root.QueueSort();
        });
        var mutationCalled = false;
        var rejected = layout.Update(_ => mutationCalled = true);
        Assert.Throws<InvalidOperationException>(() => rejected.End.GetAwaiter().GetResult());
        Assert.False(mutationCalled);

        scope.FramesUntil(() => transition.IsAnimating, 40);
        transition.Cancel();
        Assert.Equal(Reason.Cancelled, await transition.End);
        for (var index = 0; index < slots.Length; index++)
            Assert.True(visuals[index].GlobalPosition.IsEqualApprox(slots[index].GlobalPosition));
    }

    [Fact]
    public async Task ExplicitItemsLimitWhichVisualsAreAnimated()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 3);
        scope.Frames(2);
        var layout = Tweens.CreateLayoutFromItems(root, [new LayoutItem(slots[2], visuals[2])], new LayoutOptions { Duration = 0.08 });
        var transition = layout.Update(container =>
        {
            container.MoveChild(slots[2], 0);
            root.QueueSort();
        });

        scope.FramesUntil(() => transition.End.IsCompleted, 200);
        Assert.Equal(Reason.Completed, await transition.End);
        Assert.True(visuals[2].GlobalPosition.IsEqualApprox(slots[2].GlobalPosition));
        Assert.True(visuals[0].GlobalPosition.IsEqualApprox(slots[0].GlobalPosition));
    }

    [Fact]
    public async Task CancelDuringReflowWaitsThenSnapsToFinalLayout()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 3);
        scope.Frames(2);
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.5 });
        var transition = layout.Update(container =>
        {
            container.MoveChild(slots[2], 0);
            root.QueueSort();
        });
        transition.Cancel();

        scope.FramesUntil(() => transition.End.IsCompleted, 40);
        Assert.Equal(Reason.Cancelled, await transition.End);
        for (var index = 0; index < slots.Length; index++)
            Assert.True(visuals[index].GlobalPosition.IsEqualApprox(slots[index].GlobalPosition));
    }

    [Fact]
    public async Task RootExitSettlesTheTransitionWithOwnerExited()
    {
        using var scope = new SceneScope(godot);
        var (root, _, _) = MakeRow(scope, 2);
        scope.Frames(2);
        var layout = Tweens.CreateLayout(root);
        var transition = layout.Update(container => container.QueueFree());

        scope.FramesUntil(() => transition.End.IsCompleted, 40);
        Assert.Equal(Reason.OwnerExited, await transition.End);
    }

    [Fact]
    public void InvalidOptionsFaultBeforeMutation()
    {
        using var scope = new SceneScope(godot);
        var (root, _, _) = MakeRow(scope, 1);
        scope.Frames(2);
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = double.NaN });
        var mutationCalled = false;
        var transition = layout.Update(_ => mutationCalled = true);
        Assert.False(mutationCalled);
        Assert.Throws<ArgumentOutOfRangeException>(() => transition.End.GetAwaiter().GetResult());
    }
}
