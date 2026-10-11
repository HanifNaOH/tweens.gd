// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System;
using System.Collections.Generic;
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
    // Printed pieces float this far above the page; coplanar, the page's depth test against them would flicker.
    private const float Lift = 0.005f;
    // Bricolage Grotesque's cap height, in em.
    private const float CapHeight = 0.66f;
    // Bricolage Grotesque's period, in em: the height of its center, and half its width and height.
    private const float PeriodCenter = 0.0775f, PeriodWidth = 0.104f, PeriodHeight = 0.0905f;
    // The camera always shows this frame: a 16:9 view with this vertical field of view.
    private const float SafeAspect = 16f / 9, SafeFov = 46;

    private Node3D rig = null!;
    // Each scene lies on its own patch of the page, turned and moved a little, so the path meanders between them.
    private Node3D[] stations = [];
    private Camera3D camera = null!;
    private Node3D dot = null!;
    private Sprite3D disc = null!, godot = null!;
    private MeshInstance3D rim = null!;
    private GpuParticles3D trail = null!, dust = null!, confetti = null!;
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
    private Font display = null!, mono = null!, italic = null!;

    public override string Title => "Showreel";
    public override string Caption => "A 55-second loop at the default leg duration, timed to an 80 bpm track. A dot travels a page of words; each stands up and acts out its name.";
    /// <summary>Records exactly one loop.</summary>
    public override double RecordingSeconds => Length * Tempo;

    protected override void Build()
    {
        var scene = World();
        var environment = scene.Environment;
        environment.BackgroundColor = Paper;
        environment.TonemapMode = Godot.Environment.ToneMapper.Linear;
        environment.GlowEnabled = false;
        environment.AmbientLightColor = Colors.White;
        environment.AmbientLightEnergy = 0.69f;
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
        Frame(scene.View);
        scene.View.SizeChanged += () => Frame(scene.View);
        camera.Far = 80;

        var page = Own(new StandardMaterial3D
        {
            AlbedoTexture = Grid(), Uv1Scale = new Vector3(300, 300, 1), Roughness = 1, MetallicSpecular = 0,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled, DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
            // Drawn first and never written to depth, so the page cannot clip anything on or below it.
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, RenderPriority = (int)Material.RenderPriorityMin,
        });
        // Large enough that the fog carries it into the background before its edge.
        Mesh(scene.View, new PlaneMesh { Size = new Vector2(600, 600) }, page, new Vector3(14, 0, 0));

        display = GD.Load<Font>("res://Fonts/BricolageGrotesque-ExtraBold.ttf");
        mono = GD.Load<Font>("res://Fonts/JetBrainsMono-Regular.ttf");
        italic = Own(new FontVariation { BaseFont = mono, VariationTransform = new Transform2D(1, 0.2f, 0, 1, 0, 0) });
        cap = CapHeight * 170 * Pixel;

        stations = [
            Station(scene.View, -0.4f, new Vector2(0, 0), 8), Station(scene.View, 5.1f, new Vector2(0.1f, 0.4f), -13),
            Station(scene.View, 10.2f, new Vector2(0.4f, 0.45f), 16), Station(scene.View, 15.6f, new Vector2(1.25f, -0.55f), -11),
            Station(scene.View, 21.2f, new Vector2(1.05f, -0.7f), 14), Station(scene.View, 27.6f, new Vector2(1.2f, -1.25f), -5),
        ];
        // Each station word lies printed on the page until the animation stands it up.
        from = Word(stations[0], "FROM", new Vector3(-2, 0, -0.8f), 170, Caramel);
        to = Word(stations[0], "TO", new Vector3(1.2f, 0, -0.8f), 170, Terracotta);
        ease = Word(stations[1], "EASE", new Vector3(5.1f, 0, -1.25f), 170, Ink);
        bounce = Word(stations[2], "BOUNCE", new Vector3(10.2f, 0, 0), 170, Terracotta);
        elastic = Word(stations[3], "ELASTIC", new Vector3(15.6f, 0, -0.6f), 170, Coral);
        keyframes = Word(stations[4], "KEYFRAMES", new Vector3(21.2f, 0, -0.85f), 150, Ink);
        // The credit floats in the sky above the dot before it drops; the animation raises it from below. High above
        // the page, its shadow would only smudge the paper.
        var logotype = GD.Load<Texture2D>("res://Logos/godot-logotype.svg");
        const float logoWidth = 4, logoBottom = 2.5f;
        var logoHeight = logoWidth * logotype.GetHeight() / logotype.GetWidth();
        godot = stations[0].Add(new Sprite3D
        {
            Texture = logotype, PixelSize = logoWidth / logotype.GetWidth(), Shaded = false,
            AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass, Position = new Vector3(Middle(from), logoBottom + logoHeight / 2, -1.6f),
        });
        credit = Word(stations[0], "FULLY CREATED IN", new Vector3(Middle(from), logoBottom + logoHeight + 0.14f, -1.6f), 100, Ink, flat: false);
        foreach (var node in credit.Append<GeometryInstance3D>(godot))
        {
            node.Transparency = 1;
            node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        glosses = [
            Define(stations[0], new Vector3(-2.4f, 0, 0.8f), 4.6f,
                "tweens, n. An intermediate frame drawn between two keyframes to create smooth, continuous motion in animation.",
                "fun, juice, polish"),
            Define(stations[1], new Vector3(3.5f, 0, 0.8f), 5,
                "easing, n. A mathematical curve or function that controls the rate of acceleration or deceleration of an animation between keyframes to produce natural, organic motion.",
                "pleasant, easy"),
            Define(stations[2], new Vector3(8.7f, 0, 0.95f), 4.6f,
                "bounce, v. To spring back after striking a surface, a little lower each time.",
                "to leave the party"),
            Define(stations[3], new Vector3(13.6f, 0, 0.85f), 5,
                "elastic, a. Describing an easing function or motion that overshoots its target value and oscillates before coming to rest, mimicking the springy recoil of stretched rubber.",
                "something that ferrets are"),
            Define(stations[4], new Vector3(19.4f, 0, 0.9f), 4.6f,
                "keyframes, n. The values an animator sets at chosen moments; everything in between is interpolated.",
                "frames, but important"),
        ];

        // A ruler at the positions an InOut.Cubic tween passes at equal time steps: an animator's spacing chart.
        var tickMaterial = Flat(Ember);
        ticks = Enumerable.Range(0, 9).Select(i =>
        {
            var tick = Mesh(stations[1], new QuadMesh { Size = new Vector2(0.05f, 0.34f), CenterOffset = new Vector3(0, 0.17f, 0) },
                tickMaterial, new Vector3(2.6f + 5 * Easing.Evaluate(InOut.Cubic, i / 8f), Lift, -0.3f));
            tick.RotationDegrees = new Vector3(-90, 0, 0);
            return tick;
        }).ToArray();
        // One key per KEYFRAMES letter, weaving to either side of the dot's path.
        diamonds = keyframes.Select((letter, i) => Text(stations[4], "◆", mono, 110, Sand,
            new Vector3(letter.Position.X, 0, i % 2 == 0 ? 0.25f : -0.25f), flat: true)).ToArray();

        BuildLogo(stations[5], new Vector3(27.6f, 0, -0.6f));
        subtitle = Text(stations[5], "procedural animation for Godot · C# and GDScript", mono, 48, Ink, new Vector3(27.6f, 0, 0.6f), flat: true);
        // Two closing phrases, centered together as one line in front of the subtitle.
        const int tagline = 44;
        float Mono(string text) => mono.GetStringSize(text, HorizontalAlignment.Left, -1, tagline).X * Pixel;
        var (free, store) = ("free and open source", "· available on Godot Asset Store");
        var left = 27.6f - Mono(free + " " + store) / 2;
        taglines = [
            Text(stations[5], free, mono, tagline, Ink, new Vector3(left + Mono(free) / 2, 0, 1.25f), flat: true),
            Text(stations[5], store, mono, tagline, Ink, new Vector3(left + Mono(free + " ") + Mono(store) / 2, 0, 1.25f), flat: true),
        ];

        // Printed notes. The percentages mark where the dot is at each quarter of its one keyframed journey.
        Note(stations[0], "0%", new Vector3(Middle(from), 0, 0.55f));
        Note(stations[0], "→", new Vector3((Middle(from) + Middle(to)) / 2, 0, 0.25f));
        Note(stations[1], "slow", new Vector3(ticks[0].Position.X + 0.25f, 0, -0.7f));
        Note(stations[1], "fast", new Vector3(ticks[4].Position.X, 0, -0.7f));
        Note(stations[1], "slow", new Vector3(ticks[8].Position.X - 0.25f, 0, -0.7f));
        Note(stations[1], "25%", new Vector3(ticks[0].Position.X, 0, 0.55f));
        Note(stations[2], "50%", new Vector3(bounce[3].Position.X, 0, 0.55f));
        Note(stations[4], "75%", new Vector3((diamonds[5].Position.X + diamonds[6].Position.X) / 2, 0, 0.55f));
        Note(stations[5], "100%", new Vector3(period.X, 0, period.Z + 0.45f));

        // The traveller: a flat disc on a carrier that moves over the page; the disc hops and squashes above it.
        var start = On(0, new Vector3(Middle(from), 0, 0));
        dot = scene.View.Add(new Node3D { Position = start });
        disc = dot.Add(new Sprite3D
        {
            Texture = Circle(), PixelSize = 2 * Radius / 128, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Shaded = false, AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass, Modulate = Ember, Position = new Vector3(0, 3.2f, 0),
        });
        // A white ball around the disc that draws only its back faces: they lie behind the disc, which covers all but a rim.
        // It sits 4 cm behind the disc; at the period, that is 1 cm behind the letters, which then cover its edge.
        rim = disc.Add(new MeshInstance3D
        {
            Position = new Vector3(0, 0, -0.04f),
            Mesh = Own(new SphereMesh { Radius = Radius, Height = 2 * Radius, RadialSegments = 64, Rings = 32 }),
            MaterialOverride = Own(new StandardMaterial3D
            {
                AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Front,
            }),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        // Lays one ink dot at a steady rate, so the trail doubles as a spacing chart: dense where the dot was slow.
        trail = Particles(scene.View, 1000, 55, new ParticleProcessMaterial
        {
            Gravity = Vector3.Zero, ScaleMin = 0.8f, ScaleMax = 1.2f, Color = Ink with { A = 0.7f },
        }, new QuadMesh { Size = new Vector2(0.07f, 0.07f), Orientation = PlaneMesh.OrientationEnum.Y }, billboard: false);
        trail.Position = start with { Y = 0.012f };
        var fadeOut = Own(new GradientTexture1D { Gradient = Own(new Gradient { Colors = [Colors.White, Colors.White with { A = 0 }] }) });
        dust = Particles(scene.View, 140, 0.9, new ParticleProcessMaterial
        {
            Direction = Vector3.Up, Spread = 75, InitialVelocityMin = 0.6f, InitialVelocityMax = 1.6f, Gravity = new Vector3(0, -2.5f, 0),
            DampingMin = 1.5f, DampingMax = 3, ScaleMin = 0.6f, ScaleMax = 1.3f, ColorRamp = fadeOut,
            ColorInitialRamp = Pick(Sand, Caramel, Coral),
        }, new QuadMesh { Size = new Vector2(0.06f, 0.06f) }, billboard: true);
        dust.Position = start with { Y = 0.03f };
        confetti = Particles(stations[5], 240, 2.6, new ParticleProcessMaterial
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
            var ripple = Mesh(scene.View, new TorusMesh { InnerRadius = 0.96f, OuterRadius = 1, Rings = 64 }, ring, start with { Y = 0.01f });
            ripple.Transparency = 1;
            ripple.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            return ripple;
        }).ToArray();

        fade = Fill(new ColorRect { Color = Paper, MouseFilter = Control.MouseFilterEnum.Ignore }, 0);
    }

    /// <summary>Wider stages keep the safe frame's height, narrower ones its width, so nothing in it is cropped.</summary>
    private void Frame(SubViewport view)
    {
        var wide = view.Size.X >= view.Size.Y * SafeAspect;
        camera.KeepAspect = wide ? Camera3D.KeepAspectEnum.Height : Camera3D.KeepAspectEnum.Width;
        camera.Fov = wide ? SafeFov : Mathf.RadToDeg(2 * MathF.Atan(MathF.Tan(Mathf.DegToRad(SafeFov) / 2) * SafeAspect));
    }

    /// <summary>A station whose content keeps its page coordinates: it turns by <paramref name="yaw"/> degrees about
    /// the page point (<paramref name="pivot"/>, 0, 0) and moves by <paramref name="shift"/> over the page.</summary>
    private static Node3D Station(Node parent, float pivot, Vector2 shift, float yaw)
    {
        var station = parent.Add(new Node3D { RotationDegrees = new Vector3(0, yaw, 0) });
        var hinge = new Vector3(pivot, 0, 0);
        station.Position = hinge + new Vector3(shift.X, 0, shift.Y) - station.Basis * hinge;
        return station;
    }

    /// <summary>A page point on a station, in the world.</summary>
    private Vector3 On(int station, Vector3 page) => stations[station].Transform * page;

    /// <summary>Flat type standing on its baseline at the node origin, so it rests on the page and hinges at its foot.</summary>
    private Label3D Text(Node parent, string text, Font font, int size, Color color, Vector3 position, bool flat) => parent.Add(new Label3D
    {
        Text = text, Font = font, FontSize = size, PixelSize = Pixel, Modulate = color, OutlineSize = 0, Shaded = false,
        DoubleSided = true, AlphaCut = Label3D.AlphaCutMode.OpaquePrepass, VerticalAlignment = VerticalAlignment.Bottom,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        Offset = new Vector2(0, -font.GetDescent(size)), Position = flat ? position + Vector3.Up * Lift : position,
        RotationDegrees = flat ? new Vector3(-90, 0, 0) : Vector3.Zero,
    });

    /// <summary>Text as separate letters at their natural advance, centered on <paramref name="center"/>; spaces only
    /// advance. Printed flat unless it stands.</summary>
    private Label3D[] Word(Node parent, string text, Vector3 center, int size, Color color, bool flat = true)
    {
        var widths = text.Select(c => display.GetStringSize(c.ToString(), HorizontalAlignment.Left, -1, size).X * Pixel).ToArray();
        var x = center.X - widths.Sum() / 2;
        var letters = new List<Label3D>();
        for (var i = 0; i < text.Length; x += widths[i++])
        {
            if (text[i] == ' ') continue;
            letters.Add(Text(parent, text[i].ToString(), display, size, color, new Vector3(x + widths[i] / 2, center.Y, center.Z), flat));
        }
        return [.. letters];
    }

    /// <summary>A dictionary definition printed word by word from <paramref name="origin"/>, wrapped at
    /// <paramref name="width"/>. Its first two words are the headword and part of speech, which stay when it shrinks;
    /// <paramref name="extra"/> then follows them on the first line.</summary>
    private (Label3D[] Words, Label3D[] Extra) Define(Node parent, Vector3 origin, float width, string text, string extra)
    {
        const int size = 44;
        const float row = 0.27f;
        float Width(string word) => italic.GetStringSize(word, HorizontalAlignment.Left, -1, size).X * Pixel;
        var space = Width(" ");
        var words = new List<Label3D>();
        float x = origin.X, z = origin.Z, headEnd = origin.X;
        foreach (var (word, i) in text.Split(' ').Select((word, i) => (word, i)))
        {
            if (x > origin.X && x + Width(word) > origin.X + width) (x, z) = (origin.X, z + row);
            var color = i < 2 ? Ink : Ink with { A = 0.7f };
            words.Add(Printed(parent, word, size, color, new Vector3(x + Width(word) / 2, 0, z)));
            x += Width(word) + space;
            if (i == 1) headEnd = x;
        }
        x = headEnd;
        var added = new List<Label3D>();
        foreach (var word in extra.Split(' '))
        {
            added.Add(Printed(parent, word, size, Terracotta, new Vector3(x + Width(word) / 2, 0, origin.Z)));
            x += Width(word) + space;
        }
        return ([.. words], [.. added]);
    }

    /// <summary>An italic word printed on the page, hidden until the animation shows it.</summary>
    private Label3D Printed(Node parent, string word, int size, Color color, Vector3 position)
    {
        var label = Text(parent, word, italic, size, color, position, flat: true);
        label.AlphaCut = Label3D.AlphaCutMode.Disabled;
        label.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        label.Transparency = 1;
        return label;
    }

    /// <summary>The wordmark standing on the page, with the logo's white outline and offset shade. The dot becomes its period.</summary>
    private void BuildLogo(Node parent, Vector3 center)
    {
        const string wordmark = "TWEENS.GD";
        const int size = 225, outlineSize = size / 48;
        var widths = wordmark.Select(c => display.GetStringSize(c.ToString(), HorizontalAlignment.Left, -1, size).X * Pixel).ToArray();
        var x = center.X - widths.Sum() / 2;
        logo = new Label3D[wordmark.Length - 1];
        for (int i = 0, letter = 0; i < wordmark.Length; x += widths[i++])
        {
            var position = new Vector3(x + widths[i] / 2, 0, center.Z);
            if (wordmark[i] == '.')
            {
                // The disc takes the period's place a little in front of the letters, so it never shares their depth.
                var em = size * Pixel;
                period = position + new Vector3(0, PeriodCenter * em, 0.03f);
                var radii = new Vector3(PeriodWidth, PeriodHeight, PeriodWidth) * em;
                periodScale = (radii / Radius) with { Z = 1 };
                // Label3D draws MSDF outlines in MSDF texels, of which the font has msdf_size per em.
                var outline = (float)outlineSize / ((FontFile)display).MsdfSize * em;
                // Flat in the letters' plane, the rim fits between their fills and their shades as their outlines do.
                rimScale = ((radii + Vector3.One * outline) / radii) with { Z = 0.006f / Radius };
                continue;
            }
            var glyph = logo[letter++] = Text(parent, wordmark[i].ToString(), display, size, i < 6 ? TweensBlue : GdGreen, position, flat: false);
            glyph.OutlineSize = outlineSize;
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
        ["stations"] = new Godot.Collections.Array<Node3D>(stations),
        ["camera"] = camera,
        ["dot"] = dot,
        ["disc"] = disc,
        ["rim"] = rim,
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
