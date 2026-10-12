// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System;
using System.Linq;
using Godot;
namespace testbed;

public sealed partial class Showreel : GalleryEffect
{
    // The colors the animation keys; Showreel.tscn holds the rest of the palette, from the tweens.gd mascot.
    private static readonly Color Saffron = new("f0a830"), Sand = new("e9c4a4"), TweensBlue = new("67a2dd"), GdGreen = new("9bd69c");
    private const float Radius = 0.14f, Lift = Lettering.Lift;
    // Bricolage Grotesque's cap height, in em.
    private const float CapHeight = 0.66f;
    // Bricolage Grotesque's period, in em: half its width and height.
    private const float PeriodWidth = 0.104f, PeriodHeight = 0.0905f;
    // The camera always shows this frame: a 16:9 view with this vertical field of view.
    private const float SafeAspect = 16f / 9, SafeFov = 46;

    private Node3D rig = null!;
    // Each scene lies on its own patch of the page, turned and moved a little, so the path meanders between them.
    private Node3D[] stations = [];
    private Camera3D camera = null!;
    private Node3D dot = null!;
    private Sprite3D disc = null!, godot = null!;
    private Sprite3D rim = null!;
    private GpuParticles3D trail = null!, leaves = null!, impact = null!, confetti = null!;
    private MeshInstance3D[] ripples = [], ticks = [];
    private Label3D[] from = [], to = [], ease = [], bounce = [], elastic = [], keyframes = [], diamonds = [], logo = [];
    private Label3D subtitle = null!;
    private Label3D[] credit = [], taglines = [];
    // Dictionary definitions printed beside the path: their words, and the words that replace them when they shrink.
    private (Label3D[] Words, Label3D[] Extra)[] glosses = [];
    // Where the disc settles as the wordmark's period, its scale there, and the scale of its rim relative to it.
    private Vector3 period, periodScale, rimScale;
    private float cap;
    private ColorRect fade = null!;

    public override string Title => "Showreel";
    public override string Caption => "A 55-second loop at the default leg duration, timed to an 80 bpm track. A dot travels a page of words; each stands up and acts out its name.";
    /// <summary>Records exactly one loop.</summary>
    public override double RecordingSeconds => Length * Tempo;

    /// <summary>Showreel.tscn lays out the page, its words, the dot and the effects; this finds what the animation
    /// moves.</summary>
    protected override void Build()
    {
        var scene = World<Node3D>("res://Gallery/Showreel/Showreel.tscn");
        T Get<T>(string name) where T : Node => scene.GetNode<T>("%" + name);
        T[] Children<T>(string name) where T : Node => [.. Get<Node>(name).GetChildren().Cast<T>()];
        Label3D[] Letters(string name) => Get<Lettering>(name).Letters;

        rig = Get<Node3D>("Rig");
        camera = Get<Camera3D>("Camera");
        var view = scene.GetParent<SubViewport>();
        Frame(view);
        view.SizeChanged += () => Frame(view);
        stations = Children<Node3D>("Stations");
        (from, to, ease, bounce, elastic, keyframes) = (Letters("From"), Letters("To"), Letters("Ease"), Letters("Bounce"), Letters("Elastic"), Letters("Keyframes"));
        var credits = Get<Node3D>("Credit");
        credit = credits.GetNode<Lettering>("Line").Letters;
        godot = credits.GetNode<Sprite3D>("Godot");
        glosses = [.. stations.Select(station => station.GetNodeOrNull<Definition>("Definition")).OfType<Definition>().Select(gloss => (gloss.Words, gloss.Extra))];
        ticks = Children<MeshInstance3D>("Ruler");
        diamonds = Children<Label3D>("Keys");
        var (tweens, gd) = (Get<Lettering>("Tweens"), Get<Lettering>("Gd"));
        logo = [.. tweens.Letters, .. gd.Letters];
        subtitle = Get<Label3D>("Subtitle");
        taglines = Children<Label3D>("Taglines");
        dot = Get<Node3D>("Dot");
        disc = Get<Sprite3D>("Disc");
        rim = Get<Sprite3D>("Rim");
        (trail, leaves, impact, confetti) = (Get<GpuParticles3D>("Trail"), Get<GpuParticles3D>("Leaves"), Get<GpuParticles3D>("Impact"), Get<GpuParticles3D>("Confetti"));
        ripples = Children<MeshInstance3D>("Ripples");
        fade = Get<ColorRect>("Fade");

        cap = CapHeight * Get<Lettering>("Bounce").FontSize * Lettering.Pixel;
        // The disc takes the period's place and size. Between the letters' fills and their shades, its rim shows as their
        // outlines do; Label3D draws MSDF outlines in MSDF texels, of which the font has msdf_size per em.
        period = Page(Get<Node3D>("Period"));
        var em = gd.FontSize * Lettering.Pixel;
        var radii = new Vector3(PeriodWidth, PeriodHeight, PeriodWidth) * em;
        periodScale = (radii / Radius) with { Z = 1 };
        var outline = (float)gd.OutlineSize / ((FontFile)gd.Font!).MsdfSize * em;
        rimScale = ((radii + Vector3.One * outline) / radii) with { Z = 1 };
    }

    /// <summary>Wider stages keep the safe frame's height, narrower ones its width, so nothing in it is cropped.</summary>
    private void Frame(SubViewport view)
    {
        var wide = view.Size.X >= view.Size.Y * SafeAspect;
        camera.KeepAspect = wide ? Camera3D.KeepAspectEnum.Height : Camera3D.KeepAspectEnum.Width;
        camera.Fov = wide ? SafeFov : Mathf.RadToDeg(2 * MathF.Atan(MathF.Tan(Mathf.DegToRad(SafeFov) / 2) * SafeAspect));
    }

    /// <summary>A page point on a station, in the world.</summary>
    private Vector3 On(int station, Vector3 page) => stations[station].Transform * page;

    /// <summary>A node's position on its station's page, through the groups it sits in.</summary>
    private Vector3 Page(Node3D node)
    {
        var point = node.Position;
        for (var parent = node.GetParent<Node3D>(); !stations.Contains(parent); parent = parent.GetParent<Node3D>()) point = parent.Transform * point;
        return point;
    }

    private float Middle(Label3D[] letters) => (Page(letters[0]).X + Page(letters[^1]).X) / 2;

    public override Godot.Collections.Dictionary SceneTargets => new()
    {
        ["rig"] = rig,
        ["stations"] = new Godot.Collections.Array<Node3D>(stations),
        ["camera"] = camera,
        ["dot"] = dot,
        ["disc"] = disc,
        ["rim"] = rim,
        ["trail"] = trail,
        ["leaves"] = leaves,
        ["impact"] = impact,
        ["confetti"] = confetti,
        ["ripples"] = new Godot.Collections.Array<MeshInstance3D>(ripples),
        ["from"] = new Godot.Collections.Array<Label3D>(from),
        ["to"] = new Godot.Collections.Array<Label3D>(to),
        ["ticks"] = new Godot.Collections.Array<MeshInstance3D>(ticks),
        ["ease"] = new Godot.Collections.Array<Label3D>(ease),
        ["bounce"] = new Godot.Collections.Array<Label3D>(bounce),
        ["elastic"] = new Godot.Collections.Array<Label3D>(elastic),
        ["diamonds"] = new Godot.Collections.Array<Label3D>(diamonds),
        ["keyframes"] = new Godot.Collections.Array<Label3D>(keyframes),
        ["logo"] = new Godot.Collections.Array<Label3D>(logo),
        ["subtitle"] = subtitle,
        ["credit"] = new Godot.Collections.Array<Label3D>(credit),
        ["godot"] = godot,
        ["taglines"] = new Godot.Collections.Array<Label3D>(taglines),
        ["glosses"] = new Godot.Collections.Array<Godot.Collections.Dictionary>(glosses.Select(gloss => new Godot.Collections.Dictionary
        {
            ["words"] = new Godot.Collections.Array<Label3D>(gloss.Words), ["extra"] = new Godot.Collections.Array<Label3D>(gloss.Extra),
        })),
        ["period"] = period,
        ["periodScale"] = periodScale,
        ["rimScale"] = rimScale,
        ["cap"] = cap,
        ["fade"] = fade,
    };

    protected override void Animate() => Sequence = Run(AnimateAsync());
}
