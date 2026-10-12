#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace tweens.gd;

/// <summary>Reusable FLIP controller for Controls whose bounds are managed by a Container.</summary>
public sealed class LayoutController
{
    private const int MaxReflowFrames = 12;
    private const int MaxZIndex = 4096;
    private readonly Control root;
    private readonly LayoutItem[]? explicitItems;
    private LayoutTransition? active;

    public LayoutOptions Options { get; }
    public bool IsActive => active is { IsTerminal: false };

    internal LayoutController(Control root, LayoutOptions? options, IReadOnlyList<LayoutItem>? items)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree() || root.IsQueuedForDeletion())
            throw new ArgumentException("A layout controller needs an in-tree Control root.", nameof(root));
        this.root = root;
        Options = (options ?? new LayoutOptions()) with { };
        explicitItems = items?.ToArray();
    }

    /// <summary>Runs a layout mutation and animates persistent child visuals to their reflowed slots.</summary>
    public LayoutTransition Update(Action<Control> change, LayoutOptions? transitionOptions = null)
    {
        TweenRuntime.EnsureMainThread();
        var transition = new LayoutTransition();
        if (IsActive)
        {
            transition.Fail(new InvalidOperationException("This layout already has an active update."));
            return transition;
        }
        if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree() || root.IsQueuedForDeletion())
        {
            transition.Fail(new InvalidOperationException("A layout update needs an in-tree Control root."));
            return transition;
        }
        if (change is null)
        {
            transition.Fail(new ArgumentNullException(nameof(change)));
            return transition;
        }

        var runOptions = (transitionOptions ?? Options) with { VisualPath = Options.VisualPath };
        LayoutItem[] items;
        Snapshot[] snapshots;
        try
        {
            runOptions.Validate(explicitItems is null);
            items = DiscoverItems();
            if (items.Length == 0) throw new ArgumentException("A layout update needs at least one item.");
            ValidateItems(items);
            snapshots = Capture(items);
        }
        catch (Exception error)
        {
            transition.Fail(error);
            return transition;
        }

        active = transition;
        Exception? mutationError = null;
        try { change(root); }
        catch (Exception error) { mutationError = error; }

        HoldFirstState(snapshots);
        HoldFirstStateDeferred(transition, snapshots);
        _ = RunAsync(transition, snapshots, runOptions, mutationError, root.GetTree());
        return transition;
    }

    private LayoutItem[] DiscoverItems()
    {
        if (explicitItems is not null) return [.. explicitItems];
        var items = new List<LayoutItem>();
        foreach (var child in root.GetChildren())
        {
            if (child is not Control slot) continue;
            if (slot.GetNodeOrNull<Control>(Options.VisualPath) is { } visual)
                items.Add(new LayoutItem(slot, visual));
        }
        return [.. items];
    }

    private void ValidateItems(IReadOnlyList<LayoutItem> items)
    {
        var slots = new HashSet<ulong>();
        var visuals = new HashSet<ulong>();
        foreach (var item in items)
        {
            if (item is null) throw new ArgumentException("A layout item cannot be null.");
            item.Validate(root, requireFill: true);
            if (!slots.Add(item.LayoutControl.GetInstanceId()))
                throw new ArgumentException("Layout items must use unique slots.");
            if (!visuals.Add(item.VisualControl.GetInstanceId()))
                throw new ArgumentException("Layout items must use unique visual Controls.");
        }
    }

    private static Snapshot[] Capture(IEnumerable<LayoutItem> items)
        => items.Select((item, index) => new Snapshot(item, index)).ToArray();

    private async Task RunAsync(LayoutTransition transition, Snapshot[] snapshots, LayoutOptions options,
        Exception? mutationError, SceneTree tree)
    {
        try
        {
            var stable = await WaitForReflow(tree, snapshots);
            if (!RootIsLive())
            {
                Settle(snapshots);
                transition.Complete(Reason.OwnerExited);
                return;
            }
            CaptureFinalRects(snapshots);
            if (!stable)
            {
                Settle(snapshots);
                if (transition.IsCancellationRequested)
                {
                    transition.Complete(Reason.Cancelled);
                    return;
                }
                throw new TimeoutException($"Container layout did not stabilize within {MaxReflowFrames} frames.");
            }
            if (mutationError is not null) throw new InvalidOperationException("The layout mutation failed.", mutationError);
            if (transition.IsCancellationRequested)
            {
                Settle(snapshots);
                transition.Complete(Reason.Cancelled);
                return;
            }

            var matched = MatchCurrentItems(snapshots);
            var handles = new List<TweenInstance>(matched.Count * 2);
            for (var index = 0; index < matched.Count; index++)
            {
                var snapshot = matched[index];
                RaiseVisual(snapshot.Item.VisualControl, index, matched.Count);
                var delay = options.Delay.Seconds + index * options.StaggerEach;
                var position = snapshot.Item.VisualControl.TweenGlobalPosition(snapshot.FinalPosition, options.Duration,
                    tween =>
                    {
                        tween.From = snapshot.FirstPosition;
                        tween.Delay = delay;
                        tween.Ease = options.Ease;
                    }, options.Playback);
                var size = snapshot.Item.VisualControl.TweenSize(snapshot.FinalSize, options.Duration,
                    tween =>
                    {
                        tween.From = snapshot.FirstSize;
                        tween.Delay = delay;
                        tween.Ease = options.SizeEase ?? options.Ease;
                    }, options.Playback);
                handles.Add(position);
                handles.Add(size);
            }

            if (handles.Count == 0)
            {
                Settle(snapshots);
                transition.Complete(Reason.Completed);
                return;
            }

            var group = Group.Of([.. handles]);
            transition.Attach(group);
            Reason reason;
            try { reason = await group.End; }
            catch
            {
                Settle(snapshots);
                throw;
            }
            Settle(snapshots);
            transition.Complete(reason);
        }
        catch (Exception error)
        {
            Settle(snapshots);
            transition.Fail(error);
        }
        finally
        {
            if (ReferenceEquals(active, transition)) active = null;
        }
    }

    private async Task<bool> WaitForReflow(SceneTree tree, Snapshot[] snapshots)
    {
        var previous = LayoutRects(snapshots);
        var consecutive = 0;
        for (var frame = 0; frame < MaxReflowFrames; frame++)
        {
            if (!RootIsLive()) return false;
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!RootIsLive()) return false;
            HoldFirstState(snapshots);
            HoldFirstStateDeferred(active!, snapshots);
            var current = LayoutRects(snapshots);
            if (frame > 0 && SameRects(current, previous)) consecutive++;
            else consecutive = 0;
            previous = current;
            if (consecutive >= 2) return true;
        }
        return false;
    }

    private bool RootIsLive() => GodotObject.IsInstanceValid(root) && root.IsInsideTree() && !root.IsQueuedForDeletion();

    private static Rect2[] LayoutRects(Snapshot[] snapshots)
        => snapshots.Where(snapshot => snapshot.ItemIsLive).Select(snapshot => snapshot.Item.LayoutControl.GetGlobalRect()).ToArray();

    private static bool SameRects(IReadOnlyList<Rect2> first, IReadOnlyList<Rect2> second)
    {
        if (first.Count != second.Count) return false;
        for (var index = 0; index < first.Count; index++)
            if (!first[index].Position.IsEqualApprox(second[index].Position) || !first[index].Size.IsEqualApprox(second[index].Size)) return false;
        return true;
    }

    private List<Snapshot> MatchCurrentItems(Snapshot[] snapshots)
    {
        var bySlot = snapshots.Where(snapshot => snapshot.ItemIsLive)
            .ToDictionary(snapshot => snapshot.Item.LayoutControl.GetInstanceId());
        var ordered = new List<Snapshot>();
        foreach (var item in DiscoverItems())
        {
            if (!itemIsLive(item) || !bySlot.TryGetValue(item.LayoutControl.GetInstanceId(), out var existing)
                || item.VisualControl != existing.Item.VisualControl) continue;
            ordered.Add(existing);
        }
        return ordered;
    }

    private static bool itemIsLive(LayoutItem item)
        => GodotObject.IsInstanceValid(item.LayoutControl) && GodotObject.IsInstanceValid(item.VisualControl)
            && !item.LayoutControl.IsQueuedForDeletion() && !item.VisualControl.IsQueuedForDeletion()
            && item.VisualControl.GetParent() == item.LayoutControl;

    private static void CaptureFinalRects(IEnumerable<Snapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!snapshot.ItemIsLive) continue;
            var rect = snapshot.Item.LayoutControl.GetGlobalRect();
            snapshot.FinalPosition = rect.Position;
            snapshot.FinalSize = rect.Size;
            snapshot.HasFinal = true;
        }
    }

    private static void HoldFirstState(IEnumerable<Snapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!snapshot.ItemIsLive) continue;
            snapshot.Item.VisualControl.GlobalPosition = snapshot.FirstPosition;
            snapshot.Item.VisualControl.Size = snapshot.FirstSize;
            snapshot.Item.VisualControl.Scale = snapshot.OriginalScale;
        }
    }

    private void HoldFirstStateDeferred(LayoutTransition transition, Snapshot[] snapshots)
        => Callable.From(() =>
        {
            if (ReferenceEquals(active, transition) && !transition.IsTerminal)
                HoldFirstState(snapshots);
        }).CallDeferred();

    private static void RaiseVisual(Control visual, int order, int count)
    {
        visual.ZAsRelative = false;
        visual.ZIndex = Math.Clamp(MaxZIndex - count + order + 1, -MaxZIndex, MaxZIndex);
    }

    private static void Settle(IEnumerable<Snapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!GodotObject.IsInstanceValid(snapshot.Item.VisualControl)) continue;
            var visual = snapshot.Item.VisualControl;
            if (GodotObject.IsInstanceValid(snapshot.Item.LayoutControl) && visual.GetParent() == snapshot.Item.LayoutControl)
            {
                if (snapshot.HasFinal && visual.IsInsideTree()) visual.GlobalPosition = snapshot.FinalPosition;
                else if (!visual.IsInsideTree()) visual.Position = Vector2.Zero;
                if (snapshot.HasFinal) visual.Size = snapshot.FinalSize;
                visual.Scale = snapshot.OriginalScale;
            }
            visual.ZAsRelative = snapshot.OriginalZAsRelative;
            visual.ZIndex = snapshot.OriginalZIndex;
        }
    }

    private sealed class Snapshot(LayoutItem item, int order)
    {
        public LayoutItem Item { get; } = item;
        public int Order { get; } = order;
        public Vector2 FirstPosition { get; } = item.VisualControl.GlobalPosition;
        public Vector2 FirstSize { get; } = item.VisualControl.Size;
        public Vector2 OriginalScale { get; } = item.VisualControl.Scale;
        public int OriginalZIndex { get; } = item.VisualControl.ZIndex;
        public bool OriginalZAsRelative { get; } = item.VisualControl.ZAsRelative;
        public Vector2 FinalPosition { get; set; }
        public Vector2 FinalSize { get; set; }
        public bool HasFinal { get; set; }
        public bool ItemIsLive => GodotObject.IsInstanceValid(Item.LayoutControl) && GodotObject.IsInstanceValid(Item.VisualControl)
            && !Item.LayoutControl.IsQueuedForDeletion() && !Item.VisualControl.IsQueuedForDeletion()
            && Item.VisualControl.GetParent() == Item.LayoutControl;
    }
}
