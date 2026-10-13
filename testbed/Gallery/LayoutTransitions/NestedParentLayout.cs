using System.Collections.Generic;
using Godot;
namespace testbed;

public sealed partial class NestedParentLayout : GalleryEffect
{
    private PanelContainer root = null!;
    private HBoxContainer parentRow = null!;
    private PanelContainer leftParent = null!, rightParent = null!;
    private VBoxContainer leftGroup = null!, rightGroup = null!;
    private Control[] slotsA = [], visualsA = [];
    private Control[] slotsB = [], visualsB = [];

    public override string Title => "Nested parents";
    public override string Caption => "B4 rises to the top in Parent 1, then returns to the bottom of Parent 2.";

    protected override void Build()
    {
        root = Fill(new PanelContainer { Name = "LayoutRoot", MouseFilter = Control.MouseFilterEnum.Ignore }, 8);
        root.AddThemeStyleboxOverride("panel", Own(GalleryTheme.Box(Palette.Surface, 9, 2, Palette.Mint, 12, 10)));
        var content = root.Add(new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        content.AddThemeConstantOverride("separation", 10);
        var rootLabel = content.Add(new HBoxContainer());
        rootLabel.Add(GalleryTheme.Label("CONTROLLER ROOT", 12, Palette.Mint));
        rootLabel.Add(GalleryTheme.Label("PanelContainer", 11, Palette.Muted));

        parentRow = content.Add(new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        parentRow.AddThemeConstantOverride("separation", 12);
        (leftParent, leftGroup, slotsA, visualsA) = Branch("PARENT 1", "A", 3, Palette.Amber);
        (rightParent, rightGroup, slotsB, visualsB) = Branch("PARENT 2", "B", 4, Palette.Blue);
        parentRow.AddChild(leftParent);
        parentRow.AddChild(rightParent);
    }

    private (PanelContainer Parent, VBoxContainer Group, Control[] Slots, Control[] Visuals) Branch(
        string parentName, string itemPrefix, int itemCount, Color color)
    {
        var parent = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddThemeStyleboxOverride("panel", Own(GalleryTheme.Box(new Color("172230"), 8, 2, color, 10, 10)));
        var group = parent.Add(new VBoxContainer
        {
            CustomMinimumSize = new Vector2(140, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        group.AddThemeConstantOverride("separation", 8);
        group.Add(GalleryTheme.Label($"{parentName}  /  VBoxContainer", 11, color));

        var slots = new List<Control>(itemCount);
        var visuals = new List<Control>(itemCount);
        for (var index = 1; index <= itemCount; index++)
        {
            var itemName = $"{itemPrefix}{index}";
            var slot = group.Add(new Control
            {
                Name = $"{itemName}Slot",
                CustomMinimumSize = new Vector2(0, 42),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            var visual = slot.Add(new PanelContainer { Name = "Visual", MouseFilter = Control.MouseFilterEnum.Ignore });
            visual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            visual.AddThemeStyleboxOverride("panel", Own(GalleryTheme.Box(color, 7, 1, new Color("ffffff", 0.35f), 10, 4)));
            var label = visual.Add(GalleryTheme.Label(itemName, 17, Palette.Background));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            slots.Add(slot);
            visuals.Add(visual);
        }

        return (parent, group, [.. slots], [.. visuals]);
    }

    public override Godot.Collections.Dictionary SceneTargets => new()
    {
        ["root"] = root,
        ["parent_row"] = parentRow,
        ["left_parent"] = leftParent,
        ["right_parent"] = rightParent,
        ["left_group"] = leftGroup,
        ["right_group"] = rightGroup,
        ["slots_a"] = new Godot.Collections.Array<Control>(slotsA),
        ["visuals_a"] = new Godot.Collections.Array<Control>(visualsA),
        ["slots_b"] = new Godot.Collections.Array<Control>(slotsB),
        ["visuals_b"] = new Godot.Collections.Array<Control>(visualsB),
    };

    protected override void Animate() => Sequence = Run(AnimateAsync());
}
