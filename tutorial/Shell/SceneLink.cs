using Godot;
using tweens.gd;

namespace tutorial;

/// <summary>
/// The scene path above a lesson's stage. While a click can open the scene in the Godot editor, the pill fills with the
/// accent, breathes a glow, and its beacon pings in time with each breath; otherwise it rests as a quiet label.
/// </summary>
[Tool, GlobalClass]
public partial class SceneLink : CardButton
{
    /// <summary>One leg of a breath, in seconds; the beacon pings once per breath.</summary>
    private const double Leg = 1.1;
    private const double Ping = 1.6;
    private const float Beacon = 4.5f;

    private string path = "res://Lessons/";
    private bool available;
    private float breath;
    private float ping = 1;
    private float hover;
    private float flash;
    private TweenInstance? breathing;
    private TweenInstance? pinging;
    private TweenInstance? hovering;
    private TweenInstance? flashing;
    private readonly StyleBoxFlat pill = new() { AntiAliasingSize = 0.8f };
    private readonly StyleBoxFlat glow = new() { BgColor = Colors.Transparent, ShadowSize = 20 };
    private readonly StyleBoxFlat ripple = new() { DrawCenter = false, AntiAliasingSize = 0.8f };

    [Export]
    public string Path
    {
        get => path;
        set
        {
            path = value;
            Fill();
        }
    }

    /// <summary>Whether a click opens the scene: the tutorial runs from the Godot editor.</summary>
    public bool Available
    {
        get => available;
        set
        {
            if (available == value)
                return;
            available = value;
            Fill();
            Breathe();
        }
    }

    public override void _Ready()
    {
        base._Ready();
        Fill();
        GetNode<Control>("%Beacon").ItemRectChanged += QueueRedraw;
        if (Engine.IsEditorHint())
            return;
        Pressed += Flash;
        Breathe();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
            Fill();
    }

    protected override void HoverChanged(bool on)
    {
        hovering?.Cancel();
        hovering = this.TweenFloat(on && available ? 1 : 0, 0.3, options =>
        {
            options.From = hover;
            options.Ease = Out.Cubic;
            options.OnUpdate = (_, value) =>
            {
                hover = value;
                QueueRedraw();
            };
        });
    }

    private void Flash()
    {
        if (!available)
            return;
        flashing?.Cancel();
        flashing = this.TweenFloat(0, 0.8, options =>
        {
            options.From = 1;
            options.Ease = Out.Expo;
            options.OnUpdate = (_, value) =>
            {
                flash = value;
                QueueRedraw();
            };
        });
    }

    /// <summary>Breathes and pings forever while available; both share one period, so they stay in step.</summary>
    private void Breathe()
    {
        breathing?.Cancel();
        pinging?.Cancel();
        breath = 0;
        ping = 1;
        QueueRedraw();
        if (!available || !IsNodeReady() || Engine.IsEditorHint())
            return;
        breathing = this.TweenFloat(1, Leg, options =>
        {
            options.From = 0;
            options.Ease = InOut.SmoothStep;
            options.PingPong = true;
            options.Repeats = TweenOptions.Infinite;
            options.OnUpdate = (_, value) =>
            {
                breath = value;
                QueueRedraw();
            };
        });
        pinging = this.TweenFloat(1, Ping, options =>
        {
            options.From = 0;
            options.RepeatInterval = 2 * Leg - Ping;
            options.Repeats = TweenOptions.Infinite;
            options.OnUpdate = (_, value) =>
            {
                ping = value;
                QueueRedraw();
            };
        });
    }

    private void Fill()
    {
        // During editor assembly reload, the native node is ready before C# references are restored.
        if (GetNodeOrNull<Label>("%Caption") is not { } caption)
            return;
        caption.Text = available ? "Open in editor" : "Scene";
        var ink = GetThemeColor(available ? "on_accent" : "muted", "Tutorial");
        caption.AddThemeColorOverride("font_color", ink);
        var label = GetNode<Label>("%Path");
        label.Text = path;
        label.AddThemeColorOverride("font_color", available ? ink with { A = 0.75f } : ink);
        Normal = available ? "SceneLinkLive" : "SceneLink";
        TooltipText = available
            ? "Open this scene in the Godot editor"
            : "Run this tutorial from the Godot editor to open the scene.";
        Rise = available ? 2 : 0;
        if (!Engine.IsEditorHint())
            MouseDefaultCursorShape = available ? CursorShape.PointingHand : CursorShape.Arrow;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var radius = (int)Mathf.Ceil(Size.Y / 2);
        pill.SetCornerRadiusAll(radius);
        pill.SetBorderWidthAll(1);
        ripple.SetBorderWidthAll(2);
        var center = GetNodeOrNull<Control>("%Beacon") is { } beacon
            ? beacon.GetParentControl().Position + beacon.Position + beacon.Size / 2
            : new Vector2(radius, radius);
        if (!available)
        {
            pill.BgColor = Colors.Transparent;
            pill.BorderColor = GetThemeColor("outline", "Tutorial");
            DrawStyleBox(pill, rect);
            DrawCircle(center, Beacon, GetThemeColor("muted", "Tutorial"), false, 1.5f, true);
            return;
        }

        var accent = GetThemeColor("accent", "Tutorial");
        var lit = Mathf.Max(breath, hover);
        glow.SetCornerRadiusAll(radius);
        glow.ShadowColor = accent with { A = 0.25f + 0.4f * lit };
        DrawStyleBox(glow, rect.Grow(2 + 4 * lit));

        // The ripple and the beacon's ring spread out fast and fade slowly.
        var spread = 1 - Mathf.Pow(1 - ping, 3);
        var fade = 1 - ping;
        ripple.SetCornerRadiusAll(radius + (int)(18 * spread));
        ripple.BorderColor = accent with { A = 0.6f * fade };
        DrawStyleBox(ripple, rect.Grow(18 * spread));

        // A solid accent pill, brightening under the pointer and on a click; the beacon takes the pill's ink.
        var ink = GetThemeColor("on_accent", "Tutorial");
        var text = GetThemeColor("text", "Tutorial");
        pill.BgColor = accent.Lerp(text, 0.15f * lit + 0.1f * hover + 0.4f * flash);
        pill.BorderColor = accent.Lerp(text, 0.3f + 0.4f * lit);
        DrawStyleBox(pill, rect);

        DrawCircle(center, Beacon + 8 * spread, ink with { A = 0.7f * fade }, false, 1.5f, true);
        DrawCircle(center, Beacon, ink, antialiased: true);
    }
}
