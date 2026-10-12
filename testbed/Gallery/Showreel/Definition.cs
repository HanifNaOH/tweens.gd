// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace testbed;

/// <summary>A dictionary definition printed on the page, one Label3D per word, from this node and wrapped at
/// <see cref="Width"/>. Its first two words are the headword and part of speech, which stay when it shrinks; the punch
/// line's words then follow them on the first line. Lays itself out in the editor too.</summary>
[Tool, GlobalClass]
public partial class Definition : Node3D
{
    private static readonly char[] Whitespace = [' ', '\n', '\t'];
    private string text = "", punchLine = "";
    private float width = 4.6f, row = 0.27f;
    private Font? font;
    private int fontSize = 44;
    private Color color = Colors.Black, bodyColor = Colors.Black, punchColor = Colors.Black;

    [Export(PropertyHint.MultilineText)] public string Text { get => text; set { text = value; Relayout(); } }
    [Export] public string PunchLine { get => punchLine; set { punchLine = value; Relayout(); } }
    [Export] public float Width { get => width; set { width = value; Relayout(); } }
    /// <summary>The distance between lines.</summary>
    [Export] public float Row { get => row; set { row = value; Relayout(); } }
    [Export] public Font? Font { get => font; set { font = value; Relayout(); } }
    [Export] public int FontSize { get => fontSize; set { fontSize = value; Relayout(); } }
    /// <summary>The headword and part of speech.</summary>
    [Export] public Color Color { get => color; set { color = value; Relayout(); } }
    [Export] public Color BodyColor { get => bodyColor; set { bodyColor = value; Relayout(); } }
    [Export] public Color PunchColor { get => punchColor; set { punchColor = value; Relayout(); } }

    /// <summary>The definition's words, once the node is ready.</summary>
    public Label3D[] Words { get; private set; } = [];
    /// <summary>The punch line's words, hidden until the animation shows them.</summary>
    public Label3D[] Extra { get; private set; } = [];

    public override void _Ready() => Layout();

    private void Relayout()
    {
        if (IsNodeReady()) Layout();
    }

    private void Layout()
    {
        foreach (var word in Words.Concat(Extra)) word.Free();
        (Words, Extra) = ([], []);
        if (font is null) return;
        float Measure(string word) => font.GetStringSize(word, HorizontalAlignment.Left, -1, fontSize).X * Lettering.Pixel;
        var space = Measure(" ");
        var words = new List<Label3D>();
        float x = 0, z = 0, headEnd = 0;
        foreach (var (word, i) in text.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries).Select((word, i) => (word, i)))
        {
            if (x > 0 && x + Measure(word) > width) (x, z) = (0, z + row);
            words.Add(Print(word, i < 2 ? color : bodyColor, new Vector3(x + Measure(word) / 2, 0, z)));
            x += Measure(word) + space;
            if (i == 1) headEnd = x;
        }
        x = headEnd;
        var extra = new List<Label3D>();
        foreach (var word in punchLine.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries))
        {
            var label = Print(word, punchColor, new Vector3(x + Measure(word) / 2, 0, 0));
            label.Transparency = 1;
            extra.Add(label);
            x += Measure(word) + space;
        }
        (Words, Extra) = ([.. words], [.. extra]);
    }

    private Label3D Print(string word, Color tint, Vector3 position)
    {
        var label = Lettering.Type(this, word, font!, fontSize, tint, position, printed: true);
        label.AlphaCut = Label3D.AlphaCutMode.Disabled;
        label.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return label;
    }
}
