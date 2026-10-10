// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System;
using System.Linq;
using Godot;
using tweens.gd;
namespace testbed;

public sealed partial class Showreel : GalleryEffect
{
    // Warm paper and ink from the tweens.gd mascot; the wordmark keeps its blue and green.
    private static readonly Color Paper = new("f6f1e7"), Rule = new("ebe2d3"), Ink = new("3d2a22"), Ember = new("e0573c"),
        Saffron = new("f0a830"), Sand = new("e9c4a4"), Caramel = new("a36d48"), Terracotta = new("c8685a"), Coral = new("e07a5f"),
        Shade = new("49201c"), TweensBlue = new("67a2dd"), GdGreen = new("9bd69c");
    private const float Pixel = 0.004f, Radius = 0.14f;
    // Bricolage Grotesque's cap height, in em.
    private const float CapHeight = 0.66f;

    private Node3D rig = null!;
    private Camera3D camera = null!;
    private Node3D dot = null!;
    private Sprite3D disc = null!;
    private GpuParticles3D trail = null!, dust = null!, confetti = null!;
    private MeshInstance3D[] ripples = [], ticks = [];
    private Label3D[] from = [], to = [], ease = [], bounce = [], elastic = [], keyframes = [], diamonds = [], logo = [];
    private Label3D subtitle = null!;
    private Vector3 period;
    private float cap;
    private ColorRect fade = null!;
    private Font display = null!, mono = null!;

    public override string Title => "Showreel";
    public override string Caption => "A 20-second loop at the default leg duration. A dot crosses the page; each word it passes stands up and acts out its name.";

    protected override void Build()
    {
        var scene = World();
        var environment = scene.Environment;
        environment.BackgroundColor = Paper;
        environment.TonemapMode = Godot.Environment.ToneMapper.Linear;
        environment.GlowEnabled = false;
        environment.AmbientLightColor = Colors.White;
        environment.AmbientLightEnergy = 0.62f;
        environment.FogLightColor = Paper;
        environment.FogDensity = 0.012f;
        // A low sun at the front left: the lit page matches the background, and type casts soft, light shadows.
        scene.Sun.RotationDegrees = new Vector3(-38, -30, 0);
        scene.Sun.LightEnergy = 0.5f;
        scene.Sun.ShadowOpacity = 0.75f;
        scene.Sun.LightAngularDistance = 0.6f;
        scene.Sun.DirectionalShadowMaxDistance = 40;

        // The camera sits on a rig: the rig glides over the page and turns, the camera dollies.
        rig = scene.View.Add(new Node3D());
        camera = scene.Camera;
        camera.Reparent(rig, false);
        camera.Transform = new Transform3D(Basis.Identity, new Vector3(0, 0, 5));
        camera.Fov = 34;
        camera.Far = 80;

        var page = Own(new StandardMaterial3D
        {
            AlbedoTexture = Grid(), Uv1Scale = new Vector3(300, 300, 1), Roughness = 1, MetallicSpecular = 0,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            // Drawn first and never written to depth, so the page cannot clip anything on or below it.
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, RenderPriority = (int)Material.RenderPriorityMin,
        });
        // Large enough that the fog carries it into the background before its edge.
        Mesh(scene.View, new PlaneMesh { Size = new Vector2(600, 600) }, page, new Vector3(14, 0, 0));

        display = GD.Load<Font>("res://Fonts/BricolageGrotesque-ExtraBold.ttf");
        mono = GD.Load<Font>("res://Fonts/JetBrainsMono-Regular.ttf");
        cap = CapHeight * 170 * Pixel;

        // Each station word lies printed on the page until the animation stands it up.
        from = Word(scene.View, "FROM", new Vector3(-2, 0, -0.8f), 170, Caramel);
        to = Word(scene.View, "TO", new Vector3(1.2f, 0, -0.8f), 170, Terracotta);
        ease = Word(scene.View, "EASE", new Vector3(5.1f, 0, -1.25f), 170, Ink);
        bounce = Word(scene.View, "BOUNCE", new Vector3(10.2f, 0, 0), 170, Terracotta);
        elastic = Word(scene.View, "ELASTIC", new Vector3(15.6f, 0, -0.6f), 170, Coral);
        keyframes = Word(scene.View, "KEYFRAMES", new Vector3(21.2f, 0, -0.85f), 150, Ink);

        // A ruler at the positions an InOut.Cubic tween passes at equal time steps: an animator's spacing chart.
        var tickMaterial = Flat(Ember);
        ticks = Enumerable.Range(0, 9).Select(i =>
        {
            var tick = Mesh(scene.View, new QuadMesh { Size = new Vector2(0.05f, 0.34f), CenterOffset = new Vector3(0, 0.17f, 0) },
                tickMaterial, new Vector3(2.6f + 5 * Easing.Evaluate(InOut.Cubic, i / 8f), 0, -0.3f));
            tick.RotationDegrees = new Vector3(-90, 0, 0);
            return tick;
        }).ToArray();
        // One key per KEYFRAMES letter, weaving to either side of the dot's path.
        diamonds = keyframes.Select((letter, i) => Text(scene.View, "◆", mono, 110, Sand,
            new Vector3(letter.Position.X, 0, i % 2 == 0 ? 0.25f : -0.25f), flat: true)).ToArray();

        BuildLogo(scene.View, new Vector3(27.6f, 0, -0.6f));
        subtitle = Text(scene.View, "procedural animation for Godot · C# and GDScript", mono, 48, Ink, new Vector3(27.6f, 0, 0.6f), flat: true);

        // Printed notes. The percentages mark where the dot is at each quarter of its one keyframed journey.
        Note(scene.View, "0%", new Vector3(Middle(from), 0, 0.55f));
        Note(scene.View, "→", new Vector3((Middle(from) + Middle(to)) / 2, 0, 0.25f));
        Note(scene.View, "slow", new Vector3(ticks[0].Position.X + 0.25f, 0, -0.7f));
        Note(scene.View, "fast", new Vector3(ticks[4].Position.X, 0, -0.7f));
        Note(scene.View, "slow", new Vector3(ticks[8].Position.X - 0.25f, 0, -0.7f));
        Note(scene.View, "25%", new Vector3(ticks[2].Position.X, 0, 0.55f));
        Note(scene.View, "50%", new Vector3(bounce[^1].Position.X, 0, 0.55f));
        Note(scene.View, "75%", new Vector3(diamonds[7].Position.X, 0, 0.55f));
        Note(scene.View, "100%", new Vector3(period.X, 0, period.Z + 0.45f));

        // The traveller: a flat disc on a carrier that moves over the page; the disc hops and squashes above it.
        dot = scene.View.Add(new Node3D { Position = new Vector3(Middle(from), 0, 0) });
        disc = dot.Add(new Sprite3D
        {
            Texture = Circle(), PixelSize = 2 * Radius / 128, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Shaded = false, AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass, Modulate = Ember, Position = new Vector3(0, 3.2f, 0),
        });

        // Lays one ink dot at a steady rate, so the trail doubles as a spacing chart: dense where the dot was slow.
        trail = Particles(scene.View, 360, 20, new ParticleProcessMaterial
        {
            Gravity = Vector3.Zero, ScaleMin = 0.8f, ScaleMax = 1.2f, Color = Ink with { A = 0.7f },
        }, new QuadMesh { Size = new Vector2(0.07f, 0.07f), Orientation = PlaneMesh.OrientationEnum.Y }, billboard: false);
        trail.Position = new Vector3(Middle(from), 0.012f, 0);
        var fadeOut = Own(new GradientTexture1D { Gradient = Own(new Gradient { Colors = [Colors.White, Colors.White with { A = 0 }] }) });
        dust = Particles(scene.View, 140, 0.9, new ParticleProcessMaterial
        {
            Direction = Vector3.Up, Spread = 75, InitialVelocityMin = 0.6f, InitialVelocityMax = 1.6f, Gravity = new Vector3(0, -2.5f, 0),
            DampingMin = 1.5f, DampingMax = 3, ScaleMin = 0.6f, ScaleMax = 1.3f, ColorRamp = fadeOut,
            ColorInitialRamp = Pick(Sand, Caramel, Coral),
        }, new QuadMesh { Size = new Vector2(0.06f, 0.06f) }, billboard: true);
        dust.Position = new Vector3(Middle(from), 0.03f, 0);
        confetti = Particles(scene.View, 240, 2.6, new ParticleProcessMaterial
        {
            Direction = Vector3.Up, Spread = 40, InitialVelocityMin = 3, InitialVelocityMax = 5.5f, Gravity = new Vector3(0, -3.5f, 0),
            DampingMin = 0.6f, DampingMax = 1.2f, AngleMin = 0, AngleMax = 360, AngularVelocityMin = -180, AngularVelocityMax = 180,
            ScaleMin = 0.7f, ScaleMax = 1.3f, ColorInitialRamp = Pick(Ember, Saffron, Coral, TweensBlue, GdGreen),
        }, new QuadMesh { Size = new Vector2(0.08f, 0.05f) }, billboard: true);
        confetti.Position = new Vector3(27.6f, 0.4f, -0.6f);
        // A few paper flakes drift along the direction of travel, near wherever the camera looks.
        var drift = Particles(rig, 50, 7, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(6, 2.5f, 4),
            Direction = Vector3.Right, Spread = 15, InitialVelocityMin = 0.25f, InitialVelocityMax = 0.6f, Gravity = new Vector3(0, -0.06f, 0),
            AngleMin = 0, AngleMax = 360, AngularVelocityMin = -40, AngularVelocityMax = 40, ScaleMin = 0.6f, ScaleMax = 1.2f,
            ColorInitialRamp = Pick(Sand, Coral, Saffron, Caramel),
        }, new QuadMesh { Size = new Vector2(0.05f, 0.05f) }, billboard: true);
        drift.Position = new Vector3(0, 1.2f, 0);
        drift.AmountRatio = 1;
        drift.Preprocess = 5;

        var ring = Flat(Terracotta);
        ripples = Enumerable.Range(0, 3).Select(_ =>
        {
            var ripple = Mesh(scene.View, new TorusMesh { InnerRadius = 0.96f, OuterRadius = 1, Rings = 64 }, ring, new Vector3(Middle(from), 0.01f, 0));
            ripple.Transparency = 1;
            ripple.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            return ripple;
        }).ToArray();

        fade = Fill(new ColorRect { Color = Paper, MouseFilter = Control.MouseFilterEnum.Ignore }, 0);
    }

    /// <summary>Flat type standing on its baseline at the node origin, so it rests on the page and hinges at its foot.</summary>
    private Label3D Text(Node parent, string text, Font font, int size, Color color, Vector3 position, bool flat) => parent.Add(new Label3D
    {
        Text = text, Font = font, FontSize = size, PixelSize = Pixel, Modulate = color, OutlineSize = 0, Shaded = false,
        DoubleSided = true, AlphaCut = Label3D.AlphaCutMode.OpaquePrepass, VerticalAlignment = VerticalAlignment.Bottom,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        Offset = new Vector2(0, -font.GetDescent(size)), Position = position,
        RotationDegrees = flat ? new Vector3(-90, 0, 0) : Vector3.Zero,
    });

    /// <summary>A word as separate letters at their natural advance, centered on <paramref name="center"/>, printed flat.</summary>
    private Label3D[] Word(Node parent, string text, Vector3 center, int size, Color color)
    {
        var widths = text.Select(c => display.GetStringSize(c.ToString(), HorizontalAlignment.Left, -1, size).X * Pixel).ToArray();
        var x = center.X - widths.Sum() / 2;
        var letters = new Label3D[text.Length];
        for (var i = 0; i < letters.Length; i++)
        {
            letters[i] = Text(parent, text[i].ToString(), display, size, color, new Vector3(x + widths[i] / 2, 0, center.Z), flat: true);
            x += widths[i];
        }
        return letters;
    }

    /// <summary>The wordmark standing on the page, with the logo's white outline and offset shade. The dot becomes its period.</summary>
    private void BuildLogo(Node parent, Vector3 center)
    {
        const string wordmark = "TWEENS.GD";
        const int size = 225;
        var widths = wordmark.Select(c => display.GetStringSize(c.ToString(), HorizontalAlignment.Left, -1, size).X * Pixel).ToArray();
        var x = center.X - widths.Sum() / 2;
        logo = new Label3D[wordmark.Length - 1];
        for (int i = 0, letter = 0; i < wordmark.Length; x += widths[i++])
        {
            var position = new Vector3(x + widths[i] / 2, 0, center.Z);
            if (wordmark[i] == '.')
            {
                period = position;
                continue;
            }
            var glyph = logo[letter++] = Text(parent, wordmark[i].ToString(), display, size, i < 6 ? TweensBlue : GdGreen, position, flat: false);
            glyph.OutlineSize = size / 48;
            glyph.OutlineModulate = Colors.White;
            var shade = Text(glyph, glyph.Text, display, size, Shade with { A = 0.45f }, new Vector3(0.012f, -0.03f, -0.02f), flat: false);
            shade.OutlineSize = size / 36;
            shade.OutlineModulate = Shade with { A = 0.45f };
            shade.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            shade.AlphaCut = Label3D.AlphaCutMode.Disabled;
        }
    }

    /// <summary>A small printed annotation in ink.</summary>
    private void Note(Node parent, string text, Vector3 position)
    {
        var note = Text(parent, text, mono, 56, Ink with { A = 0.55f }, position, flat: true);
        note.AlphaCut = Label3D.AlphaCutMode.Disabled;
        note.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private static float Middle(Label3D[] letters) => (letters[0].Position.X + letters[^1].Position.X) / 2;

    private StandardMaterial3D Flat(Color color) => Own(new StandardMaterial3D
    {
        AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    });

    /// <summary>Each particle picks one of the colors.</summary>
    private GradientTexture1D Pick(params Color[] colors)
    {
        var gradient = Own(new Gradient
        {
            InterpolationMode = Gradient.InterpolationModeEnum.Constant, Colors = colors,
            Offsets = colors.Select((_, i) => (float)i / colors.Length).ToArray(),
        });
        return Own(new GradientTexture1D { Gradient = gradient });
    }

    /// <summary>Flat round particles. Keyframes raise the amount ratio to emit.</summary>
    private GpuParticles3D Particles(Node parent, int amount, double lifetime, ParticleProcessMaterial process, QuadMesh mesh, bool billboard)
    {
        mesh.Material = Own(new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoTexture = Circle(),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Particles : BaseMaterial3D.BillboardModeEnum.Disabled,
        });
        return parent.Add(new GpuParticles3D
        {
            Amount = amount, Lifetime = lifetime, AmountRatio = 0, ProcessMaterial = Own(process), DrawPass1 = Own(mesh),
            LocalCoords = false, VisibilityAabb = new Aabb(new Vector3(-40, -10, -20), new Vector3(80, 20, 40)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>A white antialiased disc.</summary>
    private ImageTexture Circle()
    {
        const int size = 128;
        using var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).Length();
                image.SetPixel(x, y, Colors.White with { A = Math.Clamp(size / 2f - distance, 0, 1) });
            }
        image.GenerateMipmaps();
        return Own(ImageTexture.CreateFromImage(image));
    }

    /// <summary>One faintly ruled cell of the page, mipmapped so the rules stay calm in the distance.</summary>
    private ImageTexture Grid()
    {
        using var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        image.Fill(Paper);
        image.FillRect(new Rect2I(0, 0, 64, 1), Rule);
        image.FillRect(new Rect2I(0, 0, 1, 64), Rule);
        image.GenerateMipmaps();
        return Own(ImageTexture.CreateFromImage(image));
    }

    public override Godot.Collections.Dictionary SceneTargets => new()
    {
        ["rig"] = rig,
        ["camera"] = camera,
        ["dot"] = dot,
        ["disc"] = disc,
        ["trail"] = trail,
        ["dust"] = dust,
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
        ["period"] = period,
        ["cap"] = cap,
        ["fade"] = fade,
    };

    protected override void Animate() => Sequence = Run(AnimateAsync());
}
