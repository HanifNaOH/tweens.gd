// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using tweens.gd;
namespace testbed;

/// <summary>One card of a gallery page: builds its visuals, then runs its animation task.</summary>
public abstract partial class GalleryEffect
{
    private const double DefaultSeconds = 1.8;
    protected const EaseType DefaultEase = InOut.Cubic;
    private readonly List<Resource> resources = [];
    private GalleryGDScript? gdscript;
    public GalleryLanguage Language { get; private set; }

    /// <summary>Shared scene objects passed to the GDScript animation and comparison tests.</summary>
    public virtual Godot.Collections.Dictionary SceneTargets => new();

    public abstract string Title { get; }
    public abstract string Caption { get; }

    /// <summary>How long a recording of this example lasts, in seconds.</summary>
    public virtual double RecordingSeconds => 10;

    /// <summary>The complete animation task started by Animate.</summary>
    public Task? Sequence { get; protected set; }

    /// <summary>The card's drawing area. Resource tweens bind to it, so they stop with the page.</summary>
    protected Control Stage { get; private set; } = null!;
    protected double Seconds { get; private set; }

    /// <summary>Timing scale relative to the default leg duration, for choreographed sequences.</summary>
    protected double Tempo => Seconds / DefaultSeconds;

    public void Attach(Control stage, GalleryLanguage language = GalleryLanguage.CSharp)
    {
        Language = language;
        Stage = stage;
        Build();
    }

    public void Start(double seconds)
    {
        Seconds = seconds;
        if (Language == GalleryLanguage.CSharp) Animate();
        else
        {
            gdscript = new GalleryGDScript(GallerySource.ForEffect(this, Language));
            Sequence = gdscript.Completion;
            gdscript.Start(Stage, SceneTargets, seconds);
        }
    }

    protected abstract void Build();
    protected abstract void Animate();

    /// <summary>Called after the page is freed, so no node still references these resources.</summary>
    public void ReleaseResources()
    {
        gdscript?.Dispose();
        gdscript = null;
        foreach (var resource in resources) resource.Dispose();
        resources.Clear();
    }

    /// <summary>Observes animation failures, including effects running alongside the main choreography.</summary>
    protected async Task Run(Task animation)
    {
        try { await animation; }
        catch (Exception error) { GD.PushError(error.ToString()); }
    }

    protected T Own<T>(T resource) where T : Resource
    {
        resources.Add(resource);
        return resource;
    }

    /// <summary>A tween-based delay that ends when the stage leaves the tree.</summary>
    protected async Task<bool> Wait(double seconds) =>
        await Stage.TweenFloat(1, seconds, t => t.From = 0).End == Reason.Completed;

    /// <summary>Repeats an async step until its tweens stop, including when their nodes leave the tree.</summary>
    protected async Task Repeat(Func<Task<bool>> step)
    {
        try
        {
            while (await step()) { }
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
        }
    }
}
