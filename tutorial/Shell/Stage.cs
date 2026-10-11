using System;
using Godot;

namespace tutorial;

/// <summary>
/// Runs a lesson scene in its own viewport of <see cref="World"/> pixels, scaled to fit, as if it were the game window.
/// Pages draw their annotations on <see cref="Underlay"/> and <see cref="Overlay"/>, outside the lesson's scene.
/// </summary>
[Tool, GlobalClass]
public partial class Stage : PanelContainer
{
    private Vector2I world = new(1152, 648);

    [Export]
    public Vector2I World
    {
        get => world;
        set
        {
            world = value;
            if (IsNodeReady())
                Fit();
        }
    }

    /// <summary>Godot's 64-pixel editor grid, in world pixels; zero hides it.</summary>
    [Export] public int GridStep { get; set; } = 64;

    /// <summary>The scene link above this example, when hosted by a tutorial page.</summary>
    [Export] public SceneLink? SceneLink { get; set; }

    public SubViewport Viewport { get; private set; } = null!;
    public Node2D Underlay { get; private set; } = null!;
    public Node2D Overlay { get; private set; } = null!;
    public Node? Lesson { get; private set; }

    /// <summary>World pixels per screen pixel.</summary>
    public float Zoom => Size.X > 0 ? World.X / Size.X : 1;

    public override void _Ready()
    {
        Viewport = GetNode<SubViewport>("%Viewport");
        Underlay = GetNode<Node2D>("%Underlay");
        Overlay = GetNode<Node2D>("%Overlay");
        Resized += Fit;
        Fit();
        if (!Engine.IsEditorHint() && SceneLink is not null)
            SceneLink.Pressed += OpenScene;
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() || SceneLink is null)
            return;
        SceneLink.Available = Lesson is not null && EngineDebugger.IsActive();
    }

    private void OpenScene()
    {
        if (Lesson is not null && EngineDebugger.IsActive())
            EditorBridge.OpenScene(Lesson.SceneFilePath);
    }

    private void Fit()
    {
        // During editor assembly reload, the native node is ready before C# references are restored.
        if (Viewport is null)
            return;
        // The stage keeps the world's aspect ratio at any width.
        CustomMinimumSize = CustomMinimumSize with { Y = Mathf.Round(Size.X * World.Y / World.X) };
        Viewport.Size2DOverride = World;
        Viewport.Size2DOverrideStretch = true;
        GetNode<CanvasItem>("%Grid").QueueRedraw();
    }

    /// <summary>Replaces the lesson. <paramref name="configure"/> runs before the scene enters the tree.</summary>
    public Node Load(PackedScene scene, Action<Node>? configure = null)
    {
        Unload();
        var lesson = scene.Instantiate();
        configure?.Invoke(lesson);
        Lesson = lesson;
        if (SceneLink is not null)
            SceneLink.Path = scene.ResourcePath;
        Viewport.AddChild(lesson);
        Viewport.MoveChild(lesson, Overlay.GetIndex());
        return lesson;
    }

    public void Unload()
    {
        if (Lesson is null)
            return;
        Viewport.RemoveChild(Lesson);
        Lesson.QueueFree();
        Lesson = null;
    }

    /// <summary>The stage a node of its world belongs to.</summary>
    public static Stage? Of(Node node)
    {
        for (var parent = node.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is Stage stage)
                return stage;
        return null;
    }

    /// <summary>Converts a position on this control to world pixels.</summary>
    public Vector2 ToWorld(Vector2 local) => local * Zoom;

    /// <summary>Clicks the lesson at a world position, as the player would.</summary>
    public void Click(Vector2 world)
    {
        foreach (var pressed in new[] { true, false })
        {
            var click = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = pressed,
                Position = world,
                GlobalPosition = world,
            };
            Viewport.PushInput(click, inLocalCoords: true);
        }
    }
}
