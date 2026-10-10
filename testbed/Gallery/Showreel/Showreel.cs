// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System.Linq;
using Godot;
namespace testbed;

public sealed partial class Showreel : GalleryEffect
{
    private const string Lettering = "TWEENS.GD";
    private static readonly string[] Channels = ["POSITION", "ROTATION", "SCALE", "COLOR"];
    private static readonly Color Night = new("04060a"), Cyan = new("22e3ff"), Yellow = new("ffd23f"), Pink = new("ff3d8b"),
        Violet = new("8a6cff"), GridLine = new("1f5266");
    // The blue and green of the tweens.gd wordmark.
    private static readonly Color TweensBlue = new("67a2dd"), GdGreen = new("9bd69c");

    private Node3D rig = null!;
    private Camera3D camera = null!;
    private Label3D[] letters = [];
    private Label3D[] words = [];
    private Label3D ticker = null!;
    private Node3D hero = null!;
    private MeshInstance3D[] shockwaves = [];
    private Node3D stream = null!;
    private MeshInstance3D[] streaks = [];
    private MeshInstance3D[] chunks = [];
    private GpuParticles3D wind = null!, sparks = null!;
    private MeshInstance3D[] wipes = [];
    private ColorRect[] bars = [];
    private ColorRect flash = null!, fade = null!;
    private Font display = null!;

    public override string Title => "Showreel";
    public override string Caption => "A 20-second loop at the default leg duration. Each animated node plays one keyframe definition: the 20-second timeline, or a short pass that repeats along the stream.";

    protected override void Build()
    {
        // Flat, unlit colors on a near-black world. Only the overbright streaks and sparks glow.
        var scene = World();
        scene.Sun.Free();
        var environment = scene.Environment;
        environment.BackgroundColor = Night;
        environment.FogLightColor = Night;
        environment.FogDensity = 0.035f;
        environment.GlowIntensity = 1.2f;
        environment.GlowBloom = 0.03f;
        environment.GlowHdrThreshold = 1;
        environment.AdjustmentEnabled = true;
        environment.AdjustmentContrast = 1.2f;
        environment.AdjustmentSaturation = 1.3f;

        // The camera sits on a rig at the origin: the rig orbits and pans, the camera dollies and zooms.
        rig = scene.View.Add(new Node3D());
        camera = scene.Camera;
        camera.Reparent(rig, false);
        camera.Transform = new Transform3D(Basis.Identity, new Vector3(0, 0, 14));
        camera.Far = 80;

        var floor = Own(new StandardMaterial3D
        {
            AlbedoTexture = Grid(), Uv1Scale = new Vector3(80, 80, 1), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            // Drawn first and never written to depth, so the floor cannot clip anything below or behind it.
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, RenderPriority = (int)Material.RenderPriorityMin,
        });
        Mesh(scene.View, new PlaneMesh { Size = new Vector2(80, 80) }, floor, new Vector3(0, -1.4f, 0));

        display = GD.Load<Font>("res://Fonts/BricolageGrotesque-ExtraBold.ttf");
        BuildTitle(scene.View);
        words = Channels.Select((word, i) =>
        {
            var facing = scene.View.Add(new Node3D { RotationDegrees = new Vector3(0, 90 * (i + 1), 0) });
            return Text(facing, word, 140, i switch { 0 => Cyan, 1 => Yellow, 2 => Pink, _ => Violet });
        }).ToArray();
        // A child of the camera crosses the frame whatever the rig does.
        ticker = Text(camera, "KEYFRAMES", 140, Yellow);

        hero = scene.View.Add(new Node3D());
        Mesh(hero, new TorusMesh { InnerRadius = 0.78f, OuterRadius = 0.95f, Rings = 48 }, Flat(Yellow));
        Mesh(hero, new TorusMesh { InnerRadius = 0.5f, OuterRadius = 0.64f, Rings = 48 }, Flat(Violet)).RotationDegrees = new Vector3(90, 0, 0);
        Mesh(hero, new SphereMesh { Radius = 0.3f, Height = 0.6f }, Flat(Cyan));

        var ring = Flat(Cyan);
        shockwaves = Enumerable.Range(0, 3).Select(_ =>
            Mesh(scene.View, new TorusMesh { InnerRadius = 0.97f, OuterRadius = 1, Rings = 64 }, ring, new Vector3(0, 0.3f, 0))).ToArray();

        BuildStream();
        sparks = Particles(scene.View, 900, 0.8, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.3f,
            Direction = Vector3.Up, Spread = 180, InitialVelocityMin = 5, InitialVelocityMax = 13, Gravity = new Vector3(0, -6, 0),
            DampingMin = 2, DampingMax = 5, ScaleMin = 0.6f, ScaleMax = 1.4f, ColorInitialRamp = Confetti(),
        }, new Vector2(0.05f, 0.45f));

        // Flat bars in front of the lens wipe across the frame between acts.
        Color[] wipeColors = [TweensBlue, GdGreen, Pink];
        wipes = wipeColors.Select(color =>
        {
            var wipe = Mesh(camera, new QuadMesh(), Flat(color), new Vector3(-4, 0, -2));
            wipe.RotationDegrees = new Vector3(0, 0, -20);
            wipe.Scale = new Vector3(0.7f, 4, 1);
            return wipe;
        }).ToArray();

        bars = [Bar(0, 0.12f, 0), Bar(0.88f, 1, 1)];
        flash = Fill(new ColorRect { Color = Colors.White, Modulate = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore }, 0);
        fade = Fill(new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore }, 0);
    }

    /// <summary>Streaks, flat shapes and wind particles ride lanes around the stream's X axis, so the stream's rotation
    /// sets the direction everything flows. It hangs on the rig and turns with the camera.</summary>
    private void BuildStream()
    {
        stream = rig.Add(new Node3D());
        Material[] glowing = [Flat(Hot(TweensBlue), true), Flat(Hot(GdGreen), true), Flat(Hot(Pink), true), Flat(Hot(Colors.White), true)];
        var streak = Streak();
        streaks = new MeshInstance3D[36];
        for (var i = 0; i < streaks.Length; i++)
        {
            streaks[i] = stream.Add(new MeshInstance3D { Mesh = streak, MaterialOverride = glowing[i % 4], Position = Lane(i, 1.2f, 2.6f) });
            var thickness = 0.03f + i % 3 * 0.015f;
            streaks[i].Scale = new Vector3(1.2f + i * 7 % 5 * 0.5f, thickness, thickness);
        }
        Material[] solid = [Flat(TweensBlue), Flat(GdGreen), Flat(Pink), Flat(Yellow)];
        chunks = new MeshInstance3D[8];
        for (var i = 0; i < chunks.Length; i++)
        {
            Mesh shape = i % 2 == 0 ? new BoxMesh { Size = Vector3.One * 0.5f } : new PrismMesh { Size = Vector3.One * 0.6f };
            chunks[i] = Mesh(stream, shape, solid[i % 4], Lane(i + 40, 2.2f, 1.8f));
            chunks[i].Scale = Vector3.One * (0.7f + i % 3 * 0.25f);
        }
        foreach (var node in streaks.Concat(chunks))
        {
            node.Transparency = 1;
            node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        wind = Particles(stream, 600, 1.4, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(0.5f, 4, 4),
            Direction = Vector3.Right, Spread = 2, InitialVelocityMin = 20, InitialVelocityMax = 30, Gravity = Vector3.Zero,
            ScaleMin = 0.6f, ScaleMax = 1.4f, ColorInitialRamp = Confetti(),
        }, new Vector2(0.03f, 1.4f));
        wind.Position = new Vector3(-16, 0, 0);
    }

    /// <summary>A start point on the stream's far end, spread around its axis by the golden angle.</summary>
    private static Vector3 Lane(int i, float inner, float span)
    {
        var angle = i * 2.39996f;
        var radius = inner + i * 0.618034f % 1 * span;
        return new Vector3(-16, radius * Mathf.Sin(angle), radius * Mathf.Cos(angle));
    }

    /// <summary>Lays the letters out at their natural advance; the animation returns them to these positions.</summary>
    private void BuildTitle(Node parent)
    {
        const int size = 225;
        const float pixel = 0.004f, gap = 0.03f;
        var widths = Lettering.Select(c => display.GetStringSize(c.ToString(), HorizontalAlignment.Left, -1, size).X * pixel + gap).ToArray();
        var x = -widths.Sum() / 2;
        letters = new Label3D[Lettering.Length];
        for (var i = 0; i < letters.Length; i++)
        {
            letters[i] = Text(parent, Lettering[i].ToString(), size, i < 6 ? TweensBlue : GdGreen);
            letters[i].Position = new Vector3(x + widths[i] / 2, 0.15f, 0);
            x += widths[i];
        }
    }

    private Label3D Text(Node parent, string text, int size, Color color) => parent.Add(new Label3D
    {
        Text = text, Font = display, FontSize = size, PixelSize = 0.004f, Modulate = color, OutlineSize = size / 48,
        OutlineModulate = Colors.White, DoubleSided = true, Shaded = false,
    });

    private StandardMaterial3D Flat(Color color, bool doubleSided = false) => Own(new StandardMaterial3D
    {
        AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = doubleSided ? BaseMaterial3D.CullModeEnum.Disabled : BaseMaterial3D.CullModeEnum.Back,
    });

    /// <summary>Two crossed unit quads along X. Stretched, a streak shows its length from any side.</summary>
    private ArrayMesh Streak()
    {
        var quad = Own(new QuadMesh());
        using var tool = new SurfaceTool();
        tool.AppendFrom(quad, 0, Transform3D.Identity);
        tool.AppendFrom(quad, 0, new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), Vector3.Zero));
        return Own(tool.Commit());
    }

    /// <summary>Above the glow threshold, so the color blooms.</summary>
    private static Color Hot(Color color) => new(color.R * 1.6f, color.G * 1.6f, color.B * 1.6f);

    /// <summary>Each particle picks one of the wordmark and accent colors, overbright so it glows.</summary>
    private GradientTexture1D Confetti()
    {
        var gradient = Own(new Gradient
        {
            InterpolationMode = Gradient.InterpolationModeEnum.Constant, Offsets = [0, 0.25f, 0.5f, 0.75f],
            Colors = [Hot(TweensBlue), Hot(GdGreen), Hot(Pink), Hot(Colors.White)],
        });
        return Own(new GradientTexture1D { Gradient = gradient, UseHdr = true });
    }

    /// <summary>Stretched quads facing the camera along their velocity. Keyframes raise the amount ratio to emit.</summary>
    private GpuParticles3D Particles(Node parent, int amount, double lifetime, ParticleProcessMaterial process, Vector2 size)
    {
        var material = Own(new StandardMaterial3D
        {
            // Additive and transparent, so particles draw after the floor and brighten what they cross.
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        });
        return parent.Add(new GpuParticles3D
        {
            // World coordinates: aligning to velocity ignores the emitter rotation in local coordinates.
            Amount = amount, Lifetime = lifetime, AmountRatio = 0, ProcessMaterial = Own(process), LocalCoords = false,
            DrawPass1 = Own(new QuadMesh { Size = size, Material = material }),
            TransformAlign = GpuParticles3D.TransformAlignEnum.ZBillboardYToVelocity,
            VisibilityAabb = new Aabb(new Vector3(-30, -30, -30), new Vector3(60, 60, 60)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>One floor cell with a line on two edges, mipmapped so the grid stays calm in the distance.</summary>
    private ImageTexture Grid()
    {
        using var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        image.Fill(new Color("070a10"));
        image.FillRect(new Rect2I(0, 0, 64, 2), GridLine);
        image.FillRect(new Rect2I(0, 0, 2, 64), GridLine);
        image.GenerateMipmaps();
        return Own(ImageTexture.CreateFromImage(image));
    }

    /// <summary>A letterbox bar that scales vertically from the stage edge it touches.</summary>
    private ColorRect Bar(float top, float bottom, float pivot)
    {
        var bar = Stage.Add(new ColorRect
        {
            Color = new Color("05080c"), Scale = new Vector2(1, 0), PivotOffsetRatio = new Vector2(0, pivot),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        bar.AnchorTop = top;
        bar.AnchorBottom = bottom;
        bar.AnchorRight = 1;
        return bar;
    }

    public override Godot.Collections.Dictionary SceneTargets => new()
    {
        ["rig"] = rig,
        ["camera"] = camera,
        ["letters"] = new Godot.Collections.Array<Label3D>(letters),
        ["words"] = new Godot.Collections.Array<Label3D>(words),
        ["ticker"] = ticker,
        ["hero"] = hero,
        ["shockwaves"] = new Godot.Collections.Array<MeshInstance3D>(shockwaves),
        ["stream"] = stream,
        ["streaks"] = new Godot.Collections.Array<MeshInstance3D>(streaks),
        ["chunks"] = new Godot.Collections.Array<MeshInstance3D>(chunks),
        ["wind"] = wind,
        ["sparks"] = sparks,
        ["wipes"] = new Godot.Collections.Array<MeshInstance3D>(wipes),
        ["bars"] = new Godot.Collections.Array<ColorRect>(bars),
        ["flash"] = flash,
        ["fade"] = fade,
    };

    protected override void Animate() => Sequence = Run(AnimateAsync());
}
