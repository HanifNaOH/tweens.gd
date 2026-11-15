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

        var runOptions = (transitionOptions ?? Options) with
        {
            VisualPath = Options.VisualPath,
            DiscoverNested = Options.DiscoverNested,
        };
        LayoutItem[] items;
        Snapshot[] snapshots;
        try
        {
            runOptions.Validate(explicitItems is null);
            items = DiscoverItems();
            if (items.Length == 0 && !(explicitItems is null && runOptions.AnimateEnter))
                throw new ArgumentException("A layout update needs at least one item.");
            ValidateItems(items);
            snapshots = Capture(root, items);
        }
        catch (Exception error)
        {
            transition.Fail(error);
            return transition;
        }

        var tree = root.GetTree();
        active = transition;
        Exception? mutationError = null;
        try { change(root); }
        catch (Exception error) { mutationError = error; }

        if (!RootIsLive())
        {
            Settle(snapshots);
            active = null;
            transition.Complete(Reason.OwnerExited);
            return transition;
        }

        if (explicitItems is null && (runOptions.AnimateEnter || runOptions.AnimateExit))
            snapshots = [.. snapshots, .. PrepareEnterExit(snapshots, runOptions)];
        HoldFirstState(snapshots);
        HoldFirstStateDeferred(transition, snapshots);
        _ = RunAsync(transition, snapshots, runOptions, mutationError, tree);
        return transition;
    }

    private LayoutItem[] DiscoverItems()
    {
        if (explicitItems is not null) return [.. explicitItems];
        var items = new List<LayoutItem>();
        Discover(root, items);
        return [.. items];
    }

    private void Discover(Node parent, List<LayoutItem> items, Control? skip = null)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child == skip) continue;
            Control? visual = null;
            if (child is Control slot && slot.GetNodeOrNull<Control>(Options.VisualPath) is { } found)
            {
                visual = found;
                items.Add(new LayoutItem(slot, found));
            }
            if (Options.DiscoverNested) Discover(child, items, visual);
        }
    }

    private List<Snapshot> PrepareEnterExit(Snapshot[] snapshots, LayoutOptions options)
    {
        var extra = new List<Snapshot>();
        if (options.AnimateExit)
        {
            foreach (var snapshot in snapshots)
            {
                var slot = snapshot.Item.LayoutControl;
                var visual = snapshot.Item.VisualControl;
                if (snapshot.ItemIsLive || !GodotObject.IsInstanceValid(slot) || !GodotObject.IsInstanceValid(visual)
                    || visual.IsQueuedForDeletion() || visual.GetParent() != slot) continue;
                var freed = slot.IsQueuedForDeletion();
                if (!freed && slot.IsInsideTree()) continue;
                snapshot.Kind = SnapshotKind.Exiting;
                snapshot.FreeOnSettle = freed;
                snapshot.RestoreIndex = (int)visual.GetIndex();
                slot.RemoveChild(visual);
                root.AddChild(visual);
                visual.TopLevel = true;
                visual.GlobalPosition = snapshot.FirstPosition;
                visual.Size = snapshot.FirstSize;
            }
        }
        if (options.AnimateEnter)
        {
            var known = snapshots.Select(snapshot => snapshot.Item.LayoutControl.GetInstanceId()).ToHashSet();
            foreach (var item in DiscoverItems())
            {
                if (known.Contains(item.LayoutControl.GetInstanceId())) continue;
                try { item.Validate(root, requireFill: false); }
                catch (ArgumentException) { continue; }
                var entering = new Snapshot(root, item) { Kind = SnapshotKind.Entering };
                item.VisualControl.Modulate = entering.OriginalModulate with { A = 0 };
                extra.Add(entering);
            }
        }
        return extra;
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

    private static Snapshot[] Capture(Control root, IEnumerable<LayoutItem> items)
        => [.. items.Select(item => new Snapshot(root, item))];

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
            var baseZ = options.UseGlobalZOrder ? matched.Select(snapshot => EffectiveZ(snapshot.Item.VisualControl)).DefaultIfEmpty(0).Max() : 0;
            for (var index = 0; index < matched.Count; index++)
            {
                var snapshot = matched[index];
                if (options.UseGlobalZOrder) RaiseVisual(snapshot, baseZ + index, absolute: true);
                else if (options.UseLayoutZOrder) RaiseVisual(snapshot, index, absolute: false);
                var delay = options.Delay.Seconds + index * options.StaggerEach;
                if (snapshot.Kind == SnapshotKind.Entering)
                {
                    handles.Add(root.TweenFloat(1, options.Duration,
                        tween =>
                        {
                            tween.From = 0;
                            tween.Delay = delay;
                            tween.Ease = options.Ease;
                            tween.OnUpdate = (_, progress) => ApplyEnterProgress(snapshot, progress, options.EnterScale);
                        }, options.Playback));
                    continue;
                }
                var position = root.TweenFloat(1, options.Duration,
                    tween =>
                    {
                        tween.From = 0;
                        tween.Delay = delay;
                        tween.Ease = options.Ease;
                        tween.OnUpdate = (_, progress) => ApplyPositionProgress(snapshot, progress);
                    }, options.Playback);
                var size = root.TweenFloat(1, options.Duration,
                    tween =>
                    {
                        tween.From = 0;
                        tween.Delay = delay;
                        tween.Ease = options.SizeEase ?? options.Ease;
                        tween.OnUpdate = (_, progress) => ApplySizeProgress(snapshot, progress);
                    }, options.Playback);
                handles.Add(position);
                handles.Add(size);
            }
            var leaving = snapshots.Where(snapshot => snapshot.Kind == SnapshotKind.Exiting).ToList();
            for (var index = 0; index < leaving.Count; index++)
            {
                var snapshot = leaving[index];
                var delay = options.Delay.Seconds + index * options.StaggerEach;
                handles.Add(root.TweenFloat(1, options.Duration,
                    tween =>
                    {
                        tween.From = 0;
                        tween.Delay = delay;
                        tween.Ease = options.Ease;
                        tween.OnUpdate = (_, progress) => ApplyExitProgress(snapshot, progress, options.ExitScale);
                    }, options.Playback));
                handles.Add(root.TweenFloat(1, options.Delay.Seconds + index * options.StaggerEach + options.Duration,
                    tween => tween.OnUpdate = (_, _) => PlaceExiting(snapshot), options.Playback));
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
            if (reason == Reason.Completed)
            {
                transition.SetFollowing(true);
                var finalRectsStable = await FollowLiveRects(tree, transition, snapshots, options.Playback);
                transition.SetFollowing(false);
                if (!RootIsLive())
                {
                    Settle(snapshots);
                    transition.Complete(Reason.OwnerExited);
                    return;
                }
                if (transition.IsCancellationRequested)
                {
                    Settle(snapshots);
                    transition.Complete(Reason.Cancelled);
                    return;
                }
                if (!finalRectsStable)
                    throw new InvalidOperationException("The layout transition stopped before its targets settled.");
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

    private async Task<bool> FollowLiveRects(SceneTree tree, LayoutTransition transition, Snapshot[] snapshots,
        PlaybackOptions playback)
    {
        var previous = LayoutRects(snapshots);
        var consecutive = 0;
        var frameSignal = playback.ProcessMode == TweenProcessMode.Physics
            ? SceneTree.SignalName.PhysicsFrame : SceneTree.SignalName.ProcessFrame;
        while (RootIsLive() && !transition.IsCancellationRequested)
        {
            await tree.ToSignal(tree, frameSignal);
            if (!RootIsLive() || transition.IsCancellationRequested) return false;
            var current = LayoutRects(snapshots);
            if (transition.IsPaused || !CanFollow(playback))
            {
                consecutive = 0;
                previous = current;
                continue;
            }
            ApplyCurrentRects(snapshots);
            if (SameRects(current, previous)) consecutive++;
            else consecutive = 0;
            previous = current;
            if (consecutive >= 2) return true;
        }
        return false;
    }

    private bool CanFollow(PlaybackOptions playback) => playback.PauseMode switch
    {
        TweenPauseMode.Always => true,
        TweenPauseMode.SceneTree => !root.GetTree().Paused,
        _ => root.CanProcess(),
    };

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
            if (!IsItemLive(root, item) || !bySlot.TryGetValue(item.LayoutControl.GetInstanceId(), out var existing)
                || item.VisualControl != existing.Item.VisualControl) continue;
            ordered.Add(existing);
        }
        return ordered;
    }

    private static bool IsItemLive(Control root, LayoutItem item)
        => GodotObject.IsInstanceValid(root) && root.IsInsideTree() && !root.IsQueuedForDeletion()
            && GodotObject.IsInstanceValid(item.LayoutControl) && GodotObject.IsInstanceValid(item.VisualControl)
            && item.LayoutControl.IsInsideTree() && item.VisualControl.IsInsideTree()
            && !item.LayoutControl.IsQueuedForDeletion() && !item.VisualControl.IsQueuedForDeletion()
            && (item.LayoutControl == root || root.IsAncestorOf(item.LayoutControl))
            && item.VisualControl.GetParent() == item.LayoutControl;

    private static void CaptureFinalRects(IEnumerable<Snapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!snapshot.ItemIsLive) continue;
            snapshot.FinalPosition = snapshot.Item.LayoutControl.GlobalPosition;
            snapshot.FinalSize = snapshot.Item.LayoutControl.Size;
            snapshot.HasFinal = true;
        }
    }

    private static void HoldFirstState(IEnumerable<Snapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Kind == SnapshotKind.Exiting) PlaceExiting(snapshot);
            if (!snapshot.ItemIsLive || snapshot.Kind != SnapshotKind.Matched) continue;
            snapshot.Item.VisualControl.GlobalPosition = snapshot.CurrentFirstPosition;
            snapshot.Item.VisualControl.Size = snapshot.CurrentFirstSize;
            snapshot.Item.VisualControl.Scale = snapshot.OriginalScale;
        }
    }

    private void HoldFirstStateDeferred(LayoutTransition transition, Snapshot[] snapshots)
        => Callable.From(() =>
        {
            if (ReferenceEquals(active, transition) && !transition.IsTerminal)
                HoldFirstState(snapshots);
        }).CallDeferred();

    private static void RaiseVisual(Snapshot snapshot, int order, bool absolute)
    {
        var visual = snapshot.Item.VisualControl;
        visual.ZAsRelative = !absolute;
        visual.ZIndex = (int)Math.Clamp(order, RenderingServer.CanvasItemZMin, RenderingServer.CanvasItemZMax);
        snapshot.ZOrderChanged = true;
    }

    private static int EffectiveZ(CanvasItem item)
    {
        var z = item.ZIndex;
        return item.ZAsRelative && item.GetParent() is CanvasItem parent ? z + EffectiveZ(parent) : z;
    }

    private static void ApplyEnterProgress(Snapshot snapshot, float progress, double from)
    {
        if (!snapshot.ItemIsLive) return;
        var visual = snapshot.Item.VisualControl;
        visual.PivotOffset = visual.Size / 2;
        visual.Scale = snapshot.OriginalScale * Mathf.Lerp((float)from, 1f, progress);
        visual.Modulate = snapshot.OriginalModulate with { A = snapshot.OriginalModulate.A * progress };
    }

    // The rect an exiting visual held when the update began, carried along if the root is resized or scrolled.
    private static void PlaceExiting(Snapshot snapshot)
    {
        var visual = snapshot.Item.VisualControl;
        if (snapshot.Settled || !GodotObject.IsInstanceValid(visual)) return;
        visual.GlobalPosition = snapshot.CurrentFirstPosition;
        visual.Size = snapshot.CurrentFirstSize;
    }

    private static void ApplyExitProgress(Snapshot snapshot, float progress, double to)
    {
        var visual = snapshot.Item.VisualControl;
        if (snapshot.Settled || !GodotObject.IsInstanceValid(visual)) return;
        PlaceExiting(snapshot);
        visual.PivotOffset = visual.Size / 2;
        visual.Scale = snapshot.OriginalScale * Mathf.Lerp(1f, (float)to, progress);
        visual.Modulate = snapshot.OriginalModulate with { A = snapshot.OriginalModulate.A * (1 - progress) };
    }

    private static void SettleExit(Snapshot snapshot)
    {
        if (snapshot.Settled) return;
        snapshot.Settled = true;
        var visual = snapshot.Item.VisualControl;
        if (!GodotObject.IsInstanceValid(visual)) return;
        if (snapshot.FreeOnSettle || !GodotObject.IsInstanceValid(snapshot.Item.LayoutControl))
        {
            visual.GetParent()?.RemoveChild(visual);
            visual.QueueFree();
            return;
        }
        var slot = snapshot.Item.LayoutControl;
        visual.TopLevel = false;
        visual.GetParent()?.RemoveChild(visual);
        slot.AddChild(visual);
        slot.MoveChild(visual, Math.Min(snapshot.RestoreIndex, (int)slot.GetChildCount() - 1));
        visual.Position = snapshot.FirstLocalPosition;
        visual.Size = snapshot.FirstSize;
        visual.Scale = snapshot.OriginalScale;
        visual.PivotOffset = snapshot.OriginalPivot;
        visual.Modulate = snapshot.OriginalModulate;
    }

    private static void Settle(IEnumerable<Snapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Kind == SnapshotKind.Exiting) { SettleExit(snapshot); continue; }
            if (!GodotObject.IsInstanceValid(snapshot.Item.VisualControl)) continue;
            var visual = snapshot.Item.VisualControl;
            if (snapshot.Kind == SnapshotKind.Entering)
            {
                visual.Modulate = snapshot.OriginalModulate;
                visual.PivotOffset = snapshot.OriginalPivot;
            }
            if (GodotObject.IsInstanceValid(snapshot.Item.LayoutControl) && visual.GetParent() == snapshot.Item.LayoutControl)
            {
                if (snapshot.ItemIsLive)
                {
                    visual.GlobalPosition = snapshot.Item.LayoutControl.GlobalPosition;
                    visual.Size = snapshot.Item.LayoutControl.Size;
                    snapshot.FinalPosition = snapshot.Item.LayoutControl.GlobalPosition;
                    snapshot.FinalSize = snapshot.Item.LayoutControl.Size;
                    snapshot.HasFinal = true;
                }
                else if (!snapshot.Item.LayoutControl.IsInsideTree() && snapshot.HasFinal && visual.IsInsideTree())
                    visual.GlobalPosition = snapshot.FinalPosition;
                else if (!snapshot.Item.LayoutControl.IsInsideTree() && !visual.IsInsideTree()) visual.Position = Vector2.Zero;
                else if (!snapshot.Item.LayoutControl.IsInsideTree() && snapshot.HasFinal) visual.Size = snapshot.FinalSize;
                visual.Scale = snapshot.OriginalScale;
            }
            if (snapshot.ZOrderChanged)
            {
                visual.ZAsRelative = snapshot.OriginalZAsRelative;
                visual.ZIndex = snapshot.OriginalZIndex;
            }
        }
    }

    private static void ApplyPositionProgress(Snapshot snapshot, float progress)
    {
        if (snapshot.ItemIsLive)
            snapshot.Item.VisualControl.GlobalPosition = snapshot.CurrentFirstPosition.Lerp(
                snapshot.Item.LayoutControl.GlobalPosition, progress);
    }

    private static void ApplySizeProgress(Snapshot snapshot, float progress)
    {
        if (snapshot.ItemIsLive)
            snapshot.Item.VisualControl.Size = snapshot.CurrentFirstSize.Lerp(snapshot.Item.LayoutControl.Size, progress);
    }

    private static void ApplyCurrentRects(IEnumerable<Snapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!snapshot.ItemIsLive) continue;
            snapshot.Item.VisualControl.GlobalPosition = snapshot.Item.LayoutControl.GlobalPosition;
            snapshot.Item.VisualControl.Size = snapshot.Item.LayoutControl.Size;
        }
    }

    private enum SnapshotKind { Matched, Entering, Exiting }

    private sealed class Snapshot(Control root, LayoutItem item)
    {
        private Control Root { get; } = root;
        public SnapshotKind Kind { get; set; }
        public Color OriginalModulate { get; } = item.VisualControl.Modulate;
        public Vector2 OriginalPivot { get; } = item.VisualControl.PivotOffset;
        public Vector2 FirstLocalPosition { get; } = item.VisualControl.Position;
        public int RestoreIndex { get; set; }
        public bool FreeOnSettle { get; set; }
        public bool Settled { get; set; }
        public LayoutItem Item { get; } = item;
        public Vector2 FirstPosition { get; } = item.VisualControl.GlobalPosition;
        public Vector2 FirstSize { get; } = item.VisualControl.Size;
        private Rect2 RootRect { get; } = root.GetGlobalRect();
        private Vector2 RootMinimum { get; } = root.GetCombinedMinimumSize();
        public Vector2 CurrentFirstPosition => RootScale is { } scale
            ? Root.GetGlobalRect().Position + (FirstPosition - RootRect.Position) * scale : FirstPosition;
        public Vector2 CurrentFirstSize => RootScale is { } scale ? FirstSize * scale : FirstSize;
        // Only a resize from outside counts. A root that grew or shrank with its content moved nothing by itself.
        private Vector2? RootScale => GodotObject.IsInstanceValid(Root) && RootRect.Size.X > 0 && RootRect.Size.Y > 0
            && Root.GetCombinedMinimumSize().IsEqualApprox(RootMinimum)
            ? Root.GetGlobalRect().Size / RootRect.Size : null;
        public Vector2 OriginalScale { get; } = item.VisualControl.Scale;
        public int OriginalZIndex { get; } = item.VisualControl.ZIndex;
        public bool OriginalZAsRelative { get; } = item.VisualControl.ZAsRelative;
        public bool ZOrderChanged { get; set; }
        public Vector2 FinalPosition { get; set; }
        public Vector2 FinalSize { get; set; }
        public bool HasFinal { get; set; }
        public bool ItemIsLive => IsItemLive(Root, Item);
    }
}
