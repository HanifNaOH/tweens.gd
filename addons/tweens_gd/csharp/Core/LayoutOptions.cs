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
    public bool UseLayoutZOrder { get; init; }
    public bool UseGlobalZOrder { get; init; }
    public bool AnimateEnter { get; init; }
    public bool AnimateExit { get; init; }
    public double EnterScale { get; init; } = 0.8;
    public double ExitScale { get; init; } = 0.8;
    public bool DiscoverNested { get; init; }
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
        if (!double.IsFinite(EnterScale) || EnterScale < 0)
            throw new ArgumentOutOfRangeException(nameof(EnterScale), "Layout enter scale must be finite and non-negative.");
        if (!double.IsFinite(ExitScale) || ExitScale < 0)
            throw new ArgumentOutOfRangeException(nameof(ExitScale), "Layout exit scale must be finite and non-negative.");
        if (!validateDiscovery && (AnimateEnter || AnimateExit))
            throw new ArgumentException("AnimateEnter and AnimateExit need automatic discovery, not explicit items.");
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
