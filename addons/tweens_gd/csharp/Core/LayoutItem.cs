#nullable enable
using System;
using Godot;

namespace tweens.gd;

/// <summary>Pairs a Container-managed slot with the direct-child Control whose visual transform is animated.</summary>
public sealed record LayoutItem(Control LayoutControl, Control VisualControl)
{
    internal void Validate(Control root, bool requireFill)
    {
        ArgumentNullException.ThrowIfNull(LayoutControl);
        ArgumentNullException.ThrowIfNull(VisualControl);
        if (!GodotObject.IsInstanceValid(LayoutControl) || !GodotObject.IsInstanceValid(VisualControl))
            throw new ArgumentException("A layout item needs valid slot and visual Controls.");
        if (!LayoutControl.IsInsideTree() || !VisualControl.IsInsideTree() || LayoutControl.IsQueuedForDeletion() || VisualControl.IsQueuedForDeletion())
            throw new ArgumentException("A layout item needs in-tree slot and visual Controls.");
        if (VisualControl.GetParent() != LayoutControl)
            throw new ArgumentException("The visual Control must be a direct child of its layout slot.");
        if (LayoutControl != root && !root.IsAncestorOf(LayoutControl))
            throw new ArgumentException("The layout slot must be a descendant of the controller root.");
        if (LayoutControl.GetViewport() != VisualControl.GetViewport() || LayoutControl.GetCanvas() != VisualControl.GetCanvas())
            throw new ArgumentException("The slot and visual Control must share a viewport and canvas.");
        if (!Mathf.IsZeroApprox(VisualControl.Rotation) || !VisualControl.Scale.IsEqualApprox(Vector2.One))
            throw new ArgumentException("The visual Control must have zero rotation and unit scale.");
        if (!VisualControl.PivotOffset.IsEqualApprox(Vector2.Zero))
            throw new ArgumentException("The visual Control must have a zero pivot offset.");
        if (requireFill)
        {
            var slotRect = LayoutControl.GetGlobalRect();
            var visualRect = VisualControl.GetGlobalRect();
            if (!slotRect.Position.IsEqualApprox(visualRect.Position) || !slotRect.Size.IsEqualApprox(visualRect.Size))
                throw new ArgumentException("The visual Control must fill its layout slot before an update.");
        }
    }
}
