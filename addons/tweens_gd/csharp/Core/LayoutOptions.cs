#nullable enable
using System;
using Godot;

namespace tweens.gd;

/// <summary>Immutable defaults for one FLIP layout controller or transition.</summary>
public sealed record LayoutOptions
{
    public Duration Duration { get; init; } = 0.35;
    public Duration Delay { get; init; }
    public EaseType Ease { get; init; } = Out.Quad;
    public EaseType? SizeEase { get; init; }
    public double StaggerEach { get; init; }
    public PlaybackOptions Playback { get; init; }
    public NodePath VisualPath { get; init; } = new("Visual");

    internal void Validate(bool validateDiscovery)
    {
        if (!double.IsFinite(Duration.Seconds) || Duration.Seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(Duration), "Layout duration must be finite and non-negative.");
        if (!double.IsFinite(Delay.Seconds) || Delay.Seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(Delay), "Layout delay must be finite and non-negative.");
        if (!double.IsFinite(StaggerEach) || StaggerEach < 0)
            throw new ArgumentOutOfRangeException(nameof(StaggerEach), "Layout stagger must be finite and non-negative.");
        ValidateEase(Ease, nameof(Ease));
        if (SizeEase is { } sizeEase) ValidateEase(sizeEase, nameof(SizeEase));
        Playback.Validate();
        if (validateDiscovery && (VisualPath.IsEmpty || VisualPath.GetNameCount() != 1
            || VisualPath.ToString().Contains('/') || VisualPath.ToString().Contains(':')))
            throw new ArgumentException("VisualPath must identify one direct-child Control.", nameof(VisualPath));
    }

    private static void ValidateEase(EaseType ease, string name)
    {
        try { _ = Easing.Evaluate(ease, 0.5f); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            throw new ArgumentOutOfRangeException(name, ease, "Layout easing is invalid.");
        }
    }
}
