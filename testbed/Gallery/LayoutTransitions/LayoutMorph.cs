using System;
using System.Linq;
using Godot;

namespace testbed;

public sealed partial class LayoutMorph : GalleryEffect
{
    private static readonly (string Name, Color Color)[] Items =
    [
        ("EMBER", Palette.Amber),
        ("TIDE", Palette.Blue),
        ("LEAF", Palette.Mint),
        ("INK", new Color("ff8fa3")),
    ];

    private PatternGrid grid = null!;

    public override string Title => "Four-way reflow";
    public override string Caption => "The Container repositions each slot; its Visual wrapper animates between all four patterns.";

    protected override void Build()
    {
        grid = Fill(new PatternGrid { Name = "PatternGrid", MouseFilter = Control.MouseFilterEnum.Ignore }, 8);
        grid.SetMeta("pattern", 1);
        for (var index = 0; index < Items.Length; index++)
        {
            var (name, color) = Items[index];
            var slot = grid.Add(new Control { Name = $"Slot{(char)('A' + index)}" });
            var visual = slot.Add(new PanelContainer { Name = "Visual", MouseFilter = Control.MouseFilterEnum.Ignore });
            visual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            visual.AddThemeStyleboxOverride("panel", Own(GalleryTheme.Box(color, 7, 1, new Color("ffffff", 0.35f), 10, 0)));
            var content = visual.Add(new HBoxContainer());
            content.AddThemeConstantOverride("separation", 8);
            content.Add(GalleryTheme.Label(((char)('A' + index)).ToString(), 22, Palette.Background));
            content.Add(GalleryTheme.Label(name, 11, Palette.Background));
        }
        grid.QueueSort();
    }

    public override Godot.Collections.Dictionary SceneTargets => new()
    {
        ["grid"] = grid,
    };

    protected override void Animate() => Sequence = Run(AnimateAsync());

    private sealed partial class PatternGrid : Container
    {
        private const float Gap = 8;
        private static readonly (int Column, int Row, int ColumnSpan, int RowSpan)[][] Patterns =
        [
            [(0, 0, 1, 2), (1, 0, 1, 1), (0, 2, 1, 1), (1, 1, 1, 2)],
            [(0, 0, 1, 2), (1, 0, 1, 1), (1, 1, 1, 1), (2, 0, 1, 2)],
            [(1, 2, 1, 1), (0, 1, 1, 2), (1, 0, 1, 2), (0, 0, 1, 1)],
            [(0, 0, 1, 1), (0, 1, 1, 1), (1, 0, 1, 2), (2, 0, 1, 2)],
        ];

        public override Vector2 _GetMinimumSize() => new(280, 150);

        public override void _Notification(int what)
        {
            if (what != NotificationSortChildren)
                return;
            var slots = GetChildren().OfType<Control>().ToArray();
            if (slots.Length != Items.Length)
                return;
            var pattern = GetMeta("pattern", 1).AsInt32();
            if (pattern < 1 || pattern > Patterns.Length)
                return;

            var placements = Patterns[pattern - 1];
            var columns = pattern is 2 or 4 ? 3 : 2;
            var rows = pattern is 2 or 4 ? 2 : 3;
            var cellWidth = Math.Max(0, (Size.X - Gap * (columns + 1)) / columns);
            var cellHeight = Math.Max(0, (Size.Y - Gap * (rows + 1)) / rows);
            for (var index = 0; index < slots.Length; index++)
            {
                var item = placements[index];
                var rect = new Rect2(
                    Gap + item.Column * (cellWidth + Gap),
                    Gap + item.Row * (cellHeight + Gap),
                    item.ColumnSpan * cellWidth + (item.ColumnSpan - 1) * Gap,
                    item.RowSpan * cellHeight + (item.RowSpan - 1) * Gap);
                FitChildInRect(slots[index], rect);
            }
        }
    }
}
