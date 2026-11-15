using Godot;
using tweens.gd;

namespace tutorial;

/// <summary>
/// Lays out the overview like the website: each language's steps under its door, and the ferret in the other column.
/// Below the steps, the leftover length of ferret gets a speech bubble.
/// </summary>
[Tool]
public partial class TutorialPath : Container
{
    private const float Gap = 16;
    private const float Bubble = 160;

    private Language shown;
    private float ferretX = float.NaN;
    private TweenInstance? slide;
    private bool switching;

    [Export] public Control CSharpTrack { get; set; } = null!;
    [Export] public Control GDScriptTrack { get; set; } = null!;
    /// <summary>The ferret, typed as a Control: this tool script also lays it out in the editor.</summary>
    [Export] public Control Ferret { get; set; } = null!;
    [Export] public SpeechBubble Speech { get; set; } = null!;

    private Control Track => shown == Language.GDScript ? GDScriptTrack : CSharpTrack;
    private float ColumnWidth => Mathf.Max(0, (Size.X - Gap) / 2);
    // The ferret is wider than its column; placement keeps it inside the path instead.
    private float FerretWidth => Mathf.Min(ColumnWidth * 1.24f, 600);
    private float FerretHeight => FerretWidth * 3667 / 1332;

    public override Vector2 _GetMinimumSize()
        => Track is null ? Vector2.Zero : new Vector2(0, Mathf.Max(Track.GetCombinedMinimumSize().Y, FerretHeight));

    /// <summary>Shows a language's steps. The ferret slides to the other column and lands as it brakes.</summary>
    public void Show(Language language, bool animate)
    {
        shown = language;
        CSharpTrack.Visible = language == Language.CSharp;
        GDScriptTrack.Visible = language == Language.GDScript;
        UpdateMinimumSize();
        QueueSort();
        switching = animate;
        if (!animate)
            return;

        Track.Modulate = Colors.Transparent;
        Track.OffsetTransformEnabled = true;
        Track.OffsetTransformPosition = new Vector2(0, 12);
        Track.TweenModulateAlpha(1, 0.45, Out.Quad, 0.25);
        Track.TweenOffsetTransformPositionY(0, 0.45, Out.Cubic, 0.25);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren || Track is null)
            return;
        var column = ColumnWidth;
        var trackX = shown == Language.GDScript ? column + Gap : 0;
        var trackHeight = Track.GetCombinedMinimumSize().Y;
        FitChildInRect(Track, new Rect2(trackX, 0, column, trackHeight));

        var x = (shown == Language.GDScript ? 0 : column + Gap) + (column - FerretWidth) / 2;
        x = Mathf.Clamp(x, 0, Mathf.Max(0, Size.X - FerretWidth));
        FitChildInRect(Ferret, new Rect2(x, 0, FerretWidth, FerretHeight));
        // Only a language switch slides the ferret; resizing moves it at once.
        if (switching && !float.IsNaN(ferretX) && !Mathf.IsEqualApprox(ferretX, x))
            Slide(ferretX + Ferret.OffsetTransformPosition.X - x, x < ferretX ? 1 : -1);
        switching = false;
        ferretX = x;

        // The bubble shows only when the ferret runs well past the steps, and points its tail at the ferret.
        var leftover = Size.Y - trackHeight;
        Speech.Visible = leftover >= Bubble;
        if (!Speech.Visible)
            return;
        var size = Speech.GetCombinedMinimumSize();
        var bubbleX = shown == Language.GDScript ? trackX + 24 : trackX + column - size.X - 24;
        FitChildInRect(Speech, new Rect2(bubbleX, trackHeight + (leftover - size.Y) / 2, size.X, size.Y));
        Speech.TailRight = shown == Language.CSharp;
    }

    private async void Slide(float from, int direction)
    {
        slide?.Cancel();
        Ferret.OffsetTransformPosition = Ferret.OffsetTransformPosition with { X = from };
        var current = slide = Ferret.TweenOffsetTransformPositionX(0, 0.7, Out.Back);
        // The slide brakes 0.25 s into its Back ease, just before it crosses its target: that's when it lands.
        // A value tween makes the pause, so it ends with this node instead of firing after it's gone.
        if (await this.TweenFloat(0, 0.25).End == Reason.Completed && !current.IsTerminal)
            ((Ferret)Ferret).Bounce(direction);
    }
}
