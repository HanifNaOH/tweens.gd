// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System.Linq;
using Godot;
namespace testbed;

/// <summary>A word as one Label3D per letter at the font's advances, centered on this node; spaces only advance.
/// Each letter stands on its baseline at its origin, so it rests on the page and hinges at its foot. Lays itself out
/// in the editor too.</summary>
[Tool, GlobalClass]
public partial class Lettering : Node3D
{
    /// <summary>World units per font pixel.</summary>
    public const float Pixel = 0.004f;
    /// <summary>Printed pieces float this far above the page; coplanar, the page's depth test against them would
    /// flicker.</summary>
    public const float Lift = 0.005f;

    private string text = "";
    private Font? font;
    private int fontSize = 170;
    private Color color = Colors.White, outlineColor = Colors.White, shade = Colors.Transparent;
    private bool printed = true, castShadow = true;
    private int outlineSize;
    private Vector3 shadeOffset = new(0.012f, -0.03f, -0.02f);

    [Export] public string Text { get => text; set { text = value; Relayout(); } }
    [Export] public Font? Font { get => font; set { font = value; Relayout(); } }
    [Export] public int FontSize { get => fontSize; set { fontSize = value; Relayout(); } }
    [Export] public Color Color { get => color; set { color = value; Relayout(); } }
    /// <summary>Lies flat on the page, as if printed, rather than standing.</summary>
    [Export] public bool Printed { get => printed; set { printed = value; Relayout(); } }
    [Export] public bool CastShadow { get => castShadow; set { castShadow = value; Relayout(); } }
    [Export] public int OutlineSize { get => outlineSize; set { outlineSize = value; Relayout(); } }
    [Export] public Color OutlineColor { get => outlineColor; set { outlineColor = value; Relayout(); } }
    /// <summary>A copy of each letter behind it, with a heavier outline, as in the tweens.gd logo. Transparent for
    /// none.</summary>
    [Export] public Color Shade { get => shade; set { shade = value; Relayout(); } }
    [Export] public Vector3 ShadeOffset { get => shadeOffset; set { shadeOffset = value; Relayout(); } }

    /// <summary>The letters, in reading order, once the node is ready.</summary>
    public Label3D[] Letters => [.. GetChildren().OfType<Label3D>().Where(letter => letter.Owner is null)];

    public override void _Ready() => Layout();

    private void Relayout()
    {
        if (IsNodeReady()) Layout();
    }

    private void Layout()
    {
        foreach (var letter in Letters) letter.Free();
        if (font is null) return;
        var widths = text.Select(c => font.GetStringSize(c.ToString(), HorizontalAlignment.Left, -1, fontSize).X * Pixel).ToArray();
        var x = -widths.Sum() / 2;
        for (var i = 0; i < text.Length; x += widths[i++])
        {
            if (text[i] == ' ') continue;
            var letter = Type(this, text[i].ToString(), font, fontSize, color, new Vector3(x + widths[i] / 2, 0, 0), printed);
            letter.CastShadow = castShadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
            letter.OutlineSize = outlineSize;
            letter.OutlineModulate = outlineColor;
            if (shade.A == 0) continue;
            var copy = Type(letter, letter.Text, font, fontSize, shade, shadeOffset, printed: false);
            copy.OutlineSize = fontSize / 36;
            copy.OutlineModulate = shade;
            copy.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            copy.AlphaCut = Label3D.AlphaCutMode.Disabled;
        }
    }

    /// <summary>Unshaded type standing on its baseline at <paramref name="position"/>, or printed on the page.</summary>
    public static Label3D Type(Node parent, string text, Font font, int size, Color color, Vector3 position, bool printed)
        => parent.Add(new Label3D
        {
            Text = text, Font = font, FontSize = size, PixelSize = Pixel, Modulate = color, OutlineSize = 0, Shaded = false,
            DoubleSided = true, AlphaCut = Label3D.AlphaCutMode.OpaquePrepass, VerticalAlignment = VerticalAlignment.Bottom,
            Offset = new Vector2(0, -font.GetDescent(size)), Position = printed ? position + Vector3.Up * Lift : position,
            RotationDegrees = printed ? new Vector3(-90, 0, 0) : Vector3.Zero,
        });
}
