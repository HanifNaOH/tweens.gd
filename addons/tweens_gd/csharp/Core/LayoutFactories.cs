#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace tweens.gd;

public static partial class Tweens
{
    /// <summary>Creates a reusable FLIP controller that discovers direct-child slots and their Visual wrappers.</summary>
    public static LayoutController CreateLayout(Control root, LayoutOptions? options = null)
        => new(root, options, null);

    /// <summary>Creates a reusable FLIP controller from explicit slot and visual Control pairs.</summary>
    public static LayoutController CreateLayoutFromItems(Control root, IReadOnlyList<LayoutItem> items, LayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new LayoutController(root, options, items);
    }
}
