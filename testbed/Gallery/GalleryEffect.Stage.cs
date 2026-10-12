// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System;
using System.Linq;
using Godot;
namespace testbed;

/// <summary>A small 3D world rendered into an effect's stage.</summary>
public sealed record Scene3D(SubViewport View, Camera3D Camera, DirectionalLight3D Sun, Godot.Environment Environment);

/// <summary>Builders for stage contents. Resources they create are owned by the effect.</summary>
public abstract partial class GalleryEffect
{
    /// <summary>A 2D viewport over the stage, with the origin at its center.</summary>
    protected SubViewport View()
    {
        var view = Viewport(spatial: false);
        view.Add(new Camera2D { Position = Vector2.Zero });
        return view;
    }

    /// <summary>A lit, fogged 3D viewport over the stage, with a camera looking at the origin.</summary>
    protected Scene3D World()
    {
        var view = Viewport(spatial: true);
        var environment = Own(new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Palette.Stage,
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("9eb6e3"),
            AmbientLightEnergy = 0.3f, TonemapMode = Godot.Environment.ToneMapper.Filmic,
            GlowEnabled = true, GlowIntensity = 0.9f, GlowBloom = 0.02f, GlowHdrThreshold = 0.9f,
            FogEnabled = true, FogLightColor = Palette.Stage, FogDensity = 0.09f,
        });
        view.Add(new WorldEnvironment { Environment = environment });

        var camera = view.Add(new Camera3D { Position = new Vector3(0, 0.8f, 3.6f), Fov = 43, Current = true });
        camera.LookAt(Vector3.Zero);

        var sun = view.Add(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -30, 0), LightEnergy = 0.9f, ShadowEnabled = true,
        });
        return new Scene3D(view, camera, sun, environment);
    }

    /// <summary>A 3D viewport over the stage showing an authored scene, which brings its own environment, camera and
    /// lights.</summary>
    protected T World<T>(string path) where T : Node
        => Viewport(spatial: true).Add(Own(GD.Load<PackedScene>(path)).Instantiate<T>());

    private SubViewport Viewport(bool spatial)
    {
        var container = Stage.Add(new SubViewportContainer { Stretch = true, MouseFilter = Control.MouseFilterEnum.Ignore });
        container.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return container.Add(new SubViewport
        {
            Size = new Vector2I(512, 256), OwnWorld3D = spatial, TransparentBg = !spatial,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always, HandleInputLocally = false,
            // Godot warns when a viewport requests MSAA for content it does not draw.
            Msaa2D = !spatial && Supports2DMsaa ? Godot.Viewport.Msaa.Msaa4X : Godot.Viewport.Msaa.Disabled,
            Msaa3D = spatial ? Godot.Viewport.Msaa.Msaa4X : Godot.Viewport.Msaa.Disabled,
        });
    }

    /// <summary>The Compatibility renderer has no 2D MSAA and warns for every viewport that requests it.</summary>
    public static bool Supports2DMsaa => RenderingServer.GetCurrentRenderingMethod() != "gl_compatibility";

    /// <summary>Adds <paramref name="child"/> stretched over the stage, inset by <paramref name="inset"/>.</summary>
    protected T Fill<T>(T child, int inset) where T : Control
    {
        Stage.AddChild(child);
        child.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, inset);
        return child;
    }

    /// <summary>A matte ground plane close to the stage color, so objects sit on a surface and receive shadows.</summary>
    protected MeshInstance3D Floor(Node parent, float y)
    {
        var ground = Own(new StandardMaterial3D { AlbedoColor = new Color("1b283a"), Roughness = 0.85f });
        return Mesh(parent, new PlaneMesh { Size = new Vector2(16, 10) }, ground, new Vector3(0, y, 0));
    }

    protected StandardMaterial3D Surface(Color color) => Own(new StandardMaterial3D { AlbedoColor = color, Roughness = 0.35f });

    protected MeshInstance3D Mesh(Node parent, Mesh mesh, Material material, Vector3 position = default)
        => parent.Add(new MeshInstance3D { Mesh = Own(mesh), MaterialOverride = material, Position = position });

    protected ShaderMaterial Shader(string code) => Own(new ShaderMaterial { Shader = Own(new Shader { Code = code }) });

    protected ImageTexture Checker(int size, int cell, Color light, Color dark)
    {
        using var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                image.SetPixel(x, y, (x / cell + y / cell) % 2 == 0 ? light : dark);
        return Own(ImageTexture.CreateFromImage(image));
    }

    /// <summary>White in the center, fading linearly to black at the edge. Suits a light texture.</summary>
    protected ImageTexture RadialFalloff(int size)
    {
        using var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var center = (size - 1) / 2f;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var brightness = Math.Max(0, 1 - new Vector2(x - center, y - center).Length() / (size / 2f));
                image.SetPixel(x, y, new Color(brightness, brightness, brightness));
            }
        return Own(ImageTexture.CreateFromImage(image));
    }

    protected static Polygon2D Diamond(Node parent, Vector2 position, Color color, float radius = 16) => parent.Add(new Polygon2D
    {
        Polygon = [new(0, -radius), new(radius, 0), new(0, radius), new(-radius, 0)], Position = position, Color = color,
    });

    protected static Polygon2D Blob(Node parent, float rx, float ry, Color color, Vector2 position = default)
        => parent.Add(new Polygon2D { Polygon = Ellipse(rx, ry), Color = color, Position = position, Antialiased = true });

    /// <summary>A plain polyline. Lines that need more settings use an initializer instead.</summary>
    protected static Line2D Line(Node parent, Vector2[] points, Color color, float width = 2)
        => parent.Add(new Line2D { Points = points, DefaultColor = color, Width = width, Antialiased = true });

    protected static Line2D Ring(Node parent, Vector2 center, float radius, Color color, float width = 2, int segments = 32)
        => parent.Add(new Line2D
        {
            Points = Ellipse(radius, radius, segments, center), Closed = true, DefaultColor = color, Width = width,
            Antialiased = true,
        });

    protected static Vector2[] Ellipse(float rx, float ry, int segments = 32, Vector2 center = default)
        => Enumerable.Range(0, segments)
            .Select(i => i * MathF.Tau / segments)
            .Select(a => center + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry))
            .ToArray();

    /// <summary>Rounded rectangle outline with half extents <paramref name="w"/> by <paramref name="h"/>.</summary>
    protected static Vector2[] Rounded(float w, float h, float r)
    {
        Vector2[] corners = [new(w - r, h - r), new(-w + r, h - r), new(-w + r, -h + r), new(w - r, -h + r)];
        return corners.SelectMany((corner, k) => Enumerable.Range(0, 7).Select(i =>
        {
            var a = (k + i / 6f) * MathF.PI / 2;
            return corner + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
        })).ToArray();
    }
}
