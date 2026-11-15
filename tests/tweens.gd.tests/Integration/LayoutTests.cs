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
    public async Task ReparentedSlotIsSkippedAfterLeavingControllerRoot()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 2);
        var outside = scope.Add(new Control { Position = new Vector2(320, 0) });
        scope.Frames(2);
        var movedPosition = new Vector2(410, 170);
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.08 });
        var transition = layout.Update(container =>
        {
            container.RemoveChild(slots[0]);
            outside.AddChild(slots[0]);
            visuals[0].GlobalPosition = movedPosition;
            root.QueueSort();
        });

        Assert.True(visuals[0].GlobalPosition.IsEqualApprox(movedPosition));
        scope.FramesUntil(() => transition.End.IsCompleted, 100);
        Assert.Equal(Reason.Completed, await transition.End);
        Assert.True(visuals[0].GlobalPosition.IsEqualApprox(movedPosition));
    }

    [Fact]
    public async Task FreeingRootDuringMutationReturnsOwnerExitedImmediately()
    {
        using var scope = new SceneScope(godot);
        var (root, _, _) = MakeRow(scope, 2);
        scope.Frames(2);
        var layout = Tweens.CreateLayout(root);

        var transition = layout.Update(container => container.Free());

        Assert.True(transition.IsTerminal);
        Assert.Equal(Reason.OwnerExited, await transition.End);
        Assert.False(layout.IsActive);
    }

    [Fact]
    public async Task TransitionPreservesCustomVisualDrawOrder()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 2);
        scope.Frames(2);
        visuals[0].ZIndex = 100;
        visuals[0].ZAsRelative = false;
        visuals[1].ZIndex = -100;
        visuals[1].ZAsRelative = true;
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.3 });
        var transition = layout.Update(container =>
        {
            container.MoveChild(slots[1], 0);
            root.QueueSort();
        });

        scope.FramesUntil(() => transition.IsAnimating, 80);
        Assert.True(transition.IsAnimating);
        Assert.Equal((100, false), (visuals[0].ZIndex, visuals[0].ZAsRelative));
        Assert.Equal((-100, true), (visuals[1].ZIndex, visuals[1].ZAsRelative));
        transition.Cancel();
        Assert.Equal(Reason.Cancelled, await transition.End);
        Assert.Equal((100, false), (visuals[0].ZIndex, visuals[0].ZAsRelative));
        Assert.Equal((-100, true), (visuals[1].ZIndex, visuals[1].ZAsRelative));
    }

    [Fact]
    public async Task OptInLayoutZOrderRestoresVisualSettingsAfterCancellation()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 2);
        scope.Frames(2);
        visuals[0].ZIndex = 100;
        visuals[0].ZAsRelative = false;
        visuals[1].ZIndex = -100;
        visuals[1].ZAsRelative = true;
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.3, UseLayoutZOrder = true });
        var transition = layout.Update(container =>
        {
            container.MoveChild(slots[1], 0);
            root.QueueSort();
        });

        scope.FramesUntil(() => transition.IsAnimating, 80);
        Assert.True(transition.IsAnimating);
        Assert.Equal((1, true), (visuals[0].ZIndex, visuals[0].ZAsRelative));
        Assert.Equal((0, true), (visuals[1].ZIndex, visuals[1].ZAsRelative));
        transition.Cancel();
        Assert.Equal(Reason.Cancelled, await transition.End);
        Assert.Equal((100, false), (visuals[0].ZIndex, visuals[0].ZAsRelative));
        Assert.Equal((-100, true), (visuals[1].ZIndex, visuals[1].ZAsRelative));
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
    public async Task ExplicitItemsInDifferentParentBranchesAnimateBothVisuals()
    {
        using var scope = new SceneScope(godot);
        var root = new HBoxContainer { Size = new Vector2(320, 240), CustomMinimumSize = new Vector2(320, 240) };
        var leftGroup = new VBoxContainer { Name = "LeftGroup", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var rightGroup = new VBoxContainer { Name = "RightGroup", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var leftSlot = new Control
        {
            Name = "LeftSlot",
            CustomMinimumSize = new Vector2(60, 40),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var rightSlot = new Control
        {
            Name = "RightSlot",
            CustomMinimumSize = new Vector2(60, 40),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var leftVisual = new ColorRect { Name = "Visual" };
        var rightVisual = new ColorRect { Name = "Visual" };
        leftVisual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rightVisual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        leftSlot.AddChild(leftVisual);
        rightSlot.AddChild(rightVisual);
        leftGroup.AddChild(leftSlot);
        rightGroup.AddChild(rightSlot);
        root.AddChild(leftGroup);
        root.AddChild(rightGroup);
        scope.Add(root);
        scope.Frames(2);

        var initialLeftSize = leftVisual.Size;
        var initialRightSize = rightVisual.Size;
        var layout = Tweens.CreateLayoutFromItems(root,
            [new LayoutItem(leftSlot, leftVisual), new LayoutItem(rightSlot, rightVisual)],
            new LayoutOptions { Duration = 0.08 });
        var transition = layout.Update(_ =>
        {
            leftSlot.CustomMinimumSize = new Vector2(200, 40);
            leftGroup.QueueSort();
            rightGroup.QueueSort();
            root.QueueSort();
        });

        scope.FramesUntil(() => transition.End.IsCompleted, 120);
        Assert.Equal(Reason.Completed, await transition.End);
        Assert.NotEqual(initialLeftSize, leftVisual.Size);
        Assert.NotEqual(initialRightSize, rightVisual.Size);
        Assert.True(leftVisual.GetGlobalRect().IsEqualApprox(leftSlot.GetGlobalRect()));
        Assert.True(rightVisual.GetGlobalRect().IsEqualApprox(rightSlot.GetGlobalRect()));
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
    public async Task ImmediateRootFreeReturnsOwnerExitedTransition()
    {
        using var scope = new SceneScope(godot);
        var (root, _, _) = MakeRow(scope, 2);
        scope.Frames(2);
        var layout = Tweens.CreateLayout(root);
        var transition = layout.Update(container => container.Free());

        Assert.Equal(Reason.OwnerExited, await transition.End);
        Assert.False(layout.IsActive);
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

    private static Control MakeSlot(string name)
    {
        var slot = new Control { Name = name, CustomMinimumSize = new Vector2(60, 40) };
        var visual = new ColorRect { Name = "Visual", Color = Colors.Red };
        visual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        slot.AddChild(visual);
        return slot;
    }

    [Fact]
    public async Task NestedSlotsAreFoundOnlyWhenRequested()
    {
        using var scope = new SceneScope(godot);
        var column = new VBoxContainer { Size = new Vector2(320, 240), CustomMinimumSize = new Vector2(320, 240) };
        var group = new HBoxContainer { Name = "Group" };
        var slots = new[] { MakeSlot("A"), MakeSlot("B") };
        foreach (var slot in slots) group.AddChild(slot);
        column.AddChild(group);
        scope.Add(column);
        scope.Frames(2);

        var flat = Tweens.CreateLayout(column).Update(_ => { });
        Assert.Throws<ArgumentException>(() => flat.End.GetAwaiter().GetResult());

        var layout = Tweens.CreateLayout(column, new LayoutOptions { DiscoverNested = true, Duration = 0.05 });
        var before = slots[1].GetNode<Control>("Visual").GlobalPosition;
        var transition = layout.Update(_ =>
        {
            group.MoveChild(slots[1], 0);
            group.QueueSort();
        });
        Assert.Equal(before, slots[1].GetNode<Control>("Visual").GlobalPosition);
        scope.FramesUntil(() => transition.End.IsCompleted, 200);
        Assert.Equal(Reason.Completed, await transition.End);
        Assert.True(slots[1].GetNode<Control>("Visual").GlobalPosition.IsEqualApprox(slots[1].GlobalPosition));
        Assert.NotEqual(before, slots[1].GlobalPosition);
    }

    [Fact]
    public async Task NewSlotsFadeAndScaleInWhenEnterIsAnimated()
    {
        using var scope = new SceneScope(godot);
        var (root, _, _) = MakeRow(scope, 2);
        scope.Frames(2);
        var added = MakeSlot("New");
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.2, AnimateEnter = true, EnterScale = 0.5 });
        var transition = layout.Update(container => container.AddChild(added));
        var visual = added.GetNode<Control>("Visual");

        Assert.Equal(0, visual.Modulate.A);
        scope.FramesUntil(() => visual.Modulate.A > 0.2f, 200);
        Assert.InRange(visual.Modulate.A, 0.2f, 0.99f);
        Assert.True(visual.Scale.X < 1 && visual.Scale.X >= 0.5f);
        scope.FramesUntil(() => transition.End.IsCompleted, 200);
        Assert.Equal(Reason.Completed, await transition.End);
        Assert.Equal(1, visual.Modulate.A);
        Assert.Equal(Vector2.One, visual.Scale);
        Assert.Equal(Vector2.Zero, visual.PivotOffset);
        Assert.True(visual.GlobalPosition.IsEqualApprox(added.GlobalPosition));
    }

    [Fact]
    public async Task NewSlotsAppearAtOnceWithoutAnimateEnter()
    {
        using var scope = new SceneScope(godot);
        var (root, _, _) = MakeRow(scope, 2);
        scope.Frames(2);
        var added = MakeSlot("New");
        var transition = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.05 })
            .Update(container => container.AddChild(added));
        Assert.Equal(1, added.GetNode<Control>("Visual").Modulate.A);
        scope.FramesUntil(() => transition.End.IsCompleted, 200);
        Assert.Equal(Reason.Completed, await transition.End);
    }

    [Fact]
    public async Task FreedSlotsFadeOutAboveTheLayoutAndAreFreedAfterwards()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 3);
        scope.Frames(2);
        var gone = visuals[1];
        var start = gone.GlobalPosition;
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.2, AnimateExit = true, ExitScale = 0.5 });
        var transition = layout.Update(container => slots[1].QueueFree());

        Assert.True(GodotObject.IsInstanceValid(gone));
        Assert.True(gone.TopLevel);
        Assert.Equal(root, gone.GetParent());
        Assert.Equal(start, gone.GlobalPosition);
        scope.FramesUntil(() => gone.Modulate.A < 0.8f, 200);
        Assert.True(gone.Modulate.A < 0.8f);
        scope.FramesUntil(() => transition.End.IsCompleted, 200);
        Assert.Equal(Reason.Completed, await transition.End);
        scope.Frames(2);
        Assert.False(GodotObject.IsInstanceValid(gone));
        Assert.True(visuals[2].GlobalPosition.IsEqualApprox(slots[2].GlobalPosition));
    }

    [Fact]
    public async Task DetachedSlotsGetTheirVisualBackAfterTheExit()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 2);
        scope.Frames(2);
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.05, AnimateExit = true });
        var transition = layout.Update(container => container.RemoveChild(slots[0]));
        Assert.True(visuals[0].TopLevel);
        scope.FramesUntil(() => transition.End.IsCompleted, 200);
        Assert.Equal(Reason.Completed, await transition.End);
        Assert.Equal(slots[0], visuals[0].GetParent());
        Assert.False(visuals[0].TopLevel);
        Assert.Equal(1, visuals[0].Modulate.A);
        Assert.Equal(Vector2.One, visuals[0].Scale);
        slots[0].Free();
    }

    [Fact]
    public async Task GlobalZOrderHoldsAcrossBranchesWithDifferentAncestorZ()
    {
        using var scope = new SceneScope(godot);
        var root = new Control { Size = new Vector2(320, 240) };
        var left = new HBoxContainer { Name = "Left", ZIndex = 50 };
        var right = new HBoxContainer { Name = "Right", ZIndex = -30, Position = new Vector2(0, 100) };
        var a = MakeSlot("A");
        var b = MakeSlot("B");
        left.AddChild(a);
        right.AddChild(b);
        root.AddChild(left);
        root.AddChild(right);
        scope.Add(root);
        scope.Frames(2);
        var visualA = a.GetNode<Control>("Visual");
        var visualB = b.GetNode<Control>("Visual");
        var items = new[] { new LayoutItem(b, visualB), new LayoutItem(a, visualA) };
        var layout = Tweens.CreateLayoutFromItems(root, items, new LayoutOptions { Duration = 0.3, UseGlobalZOrder = true });
        var transition = layout.Update(_ => { });
        scope.FramesUntil(() => transition.IsAnimating, 80);

        Assert.False(visualA.ZAsRelative);
        Assert.False(visualB.ZAsRelative);
        Assert.True(visualB.ZIndex < visualA.ZIndex);
        Assert.True(visualB.ZIndex >= 50);
        transition.Cancel();
        Assert.Equal(Reason.Cancelled, await transition.End);
        Assert.True(visualA.ZAsRelative && visualB.ZAsRelative);
        Assert.Equal(0, visualA.ZIndex);
    }

    [Fact]
    public void InvalidEnterAndExitScalesAreRejected()
    {
        using var scope = new SceneScope(godot);
        var (root, _, _) = MakeRow(scope, 1);
        scope.Frames(2);
        var transition = Tweens.CreateLayout(root, new LayoutOptions { EnterScale = -1 }).Update(_ => { });
        Assert.Throws<ArgumentOutOfRangeException>(() => transition.End.GetAwaiter().GetResult());
    }

    [Fact]
    public async Task EmptyLayoutAnimatesItsFirstItemIn()
    {
        using var scope = new SceneScope(godot);
        var root = new HBoxContainer { Size = new Vector2(320, 240), CustomMinimumSize = new Vector2(320, 240) };
        scope.Add(root);
        scope.Frames(2);
        var added = MakeSlot("First");
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.05, AnimateEnter = true });
        var transition = layout.Update(container => container.AddChild(added));
        var visual = added.GetNode<Control>("Visual");

        Assert.Equal(0, visual.Modulate.A);
        scope.FramesUntil(() => transition.End.IsCompleted, 200);
        Assert.Equal(Reason.Completed, await transition.End);
        Assert.Equal(1, visual.Modulate.A);

        var plain = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.05 });
        var none = new HBoxContainer { Size = new Vector2(10, 10) };
        scope.Add(none);
        var rejected = Tweens.CreateLayout(none).Update(_ => { });
        Assert.Throws<ArgumentException>(() => rejected.End.GetAwaiter().GetResult());
    }

    [Fact]
    public void ExplicitItemsRejectEnterAndExitBeforeMutation()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 1);
        scope.Frames(2);
        var items = new[] { new LayoutItem(slots[0], visuals[0]) };
        foreach (var options in new[] { new LayoutOptions { AnimateEnter = true }, new LayoutOptions { AnimateExit = true } })
        {
            var called = false;
            var transition = Tweens.CreateLayoutFromItems(root, items, options).Update(_ => called = true);
            Assert.False(called);
            Assert.Throws<ArgumentException>(() => transition.End.GetAwaiter().GetResult());
        }
    }

    [Fact]
    public async Task ExitingVisualFollowsTheRootWhenItIsResizedMidTransition()
    {
        using var scope = new SceneScope(godot);
        var (root, slots, visuals) = MakeRow(scope, 3);
        scope.Frames(2);
        var gone = visuals[1];
        var before = gone.Size;
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.05, Delay = 0.3, AnimateExit = true });
        var transition = layout.Update(_ => slots[1].QueueFree());
        root.Size = new Vector2(root.Size.X * 2, root.Size.Y);
        scope.Frames(3);

        Assert.True(gone.TopLevel);
        Assert.Equal(before.X * 2, gone.Size.X, 0.5);
        transition.Cancel();
        await transition.End;
    }

    [Fact]
    public async Task RootGrowingWithItsContentDoesNotScaleTheFirstRects()
    {
        using var scope = new SceneScope(godot);
        var root = new VBoxContainer();
        var slots = new[] { MakeSlot("A"), MakeSlot("B") };
        foreach (var slot in slots) root.AddChild(slot);
        scope.Add(root);
        scope.Frames(2);
        var visual = slots[1].GetNode<Control>("Visual");
        var before = visual.GlobalPosition;
        var layout = Tweens.CreateLayout(root, new LayoutOptions { Duration = 0.05, Delay = 1 });
        var transition = layout.Update(container => container.AddChild(MakeSlot("C")));
        scope.Frames(6);

        Assert.True(root.Size.Y > 80);
        Assert.True(visual.GlobalPosition.IsEqualApprox(before), $"{visual.GlobalPosition} != {before}");
        transition.Cancel();
        await transition.End;
    }
}
