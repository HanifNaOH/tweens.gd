// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using Godot;
using tweens.gd;
using twodog.Testing;
using twodog.Testing.Xunit;

namespace testbed.Tests.Integration;


[Collection<HeadlessCollection>]
public class GalleryLifetimeTests
{
    private readonly HeadlessFixture godot;

    public GalleryLifetimeTests(HeadlessFixture godot)
    {
        this.godot = godot;
        // The fixture boots the main scene. Remove it before checking the shared scheduler.
        Pump();
        if (godot.Tree.CurrentScene is Gallery gallery) gallery.Free();
    }

    private void Pump() { for (var i = 0; i < 3; i++) godot.Engine.Iteration(); }

    [Theory]
    [InlineData(nameof(BouncingBall))] [InlineData(nameof(SlimeHop))] [InlineData(nameof(JellyCube))]
    [InlineData(nameof(JellyButton))] [InlineData(nameof(AsyncDelivery))]
    [InlineData(nameof(SquashWave))] [InlineData(nameof(EasingRace))] [InlineData(nameof(Spirograph))]
    [InlineData(nameof(CombinedTransforms))] [InlineData(nameof(CameraPan))] [InlineData(nameof(CurveFollower2D))]
    [InlineData(nameof(TextReveal))] [InlineData(nameof(ScrollingList))] [InlineData(nameof(RangeMeter))]
    [InlineData(nameof(OffsetTransforms))] [InlineData(nameof(GlowingRibbon))] [InlineData(nameof(LightSweep))]
    [InlineData(nameof(ColorInterpolation))]
    [InlineData(nameof(KeyframeFlight))] [InlineData(nameof(LayoutMorph))] [InlineData(nameof(Showreel))]
    [InlineData(nameof(ParticleStream))] [InlineData(nameof(PolygonEchoes))] [InlineData(nameof(CameraLens))]
    [InlineData(nameof(CurveFollower3D))] [InlineData(nameof(ParentedRotation))] [InlineData(nameof(Spotlight))]
    [InlineData(nameof(AlbedoFade))] [InlineData(nameof(EmissionPulse))] [InlineData(nameof(SharedMaterial))]
    [InlineData(nameof(UvScroll))]
    [InlineData(nameof(CardDeal))]
    public void FreeingTheStageSettlesAnimationTasksThroughoutTheirPhases(string name)
    {
        // Sample launch, flight, impact, recovery, and subsequent repeats, including awaited button phases.
        for (var updates = 0; updates <= 100; updates += 4)
        {
            var stage = new Control { Size = new Vector2(600, 300) };
            godot.Tree.Root.AddChild(stage);
            var type = typeof(GalleryEffect).Assembly.GetType("testbed." + name)!;
            var effect = (GalleryEffect)Activator.CreateInstance(type)!;
            effect.Attach(stage);
            var scheduler = TweenRuntime.GetRunner(stage).Scheduler;
            try
            {
                effect.Start(1.8);
                if (effect is JellyButton)
                    effect.SceneTargets["button"].As<Button>().EmitSignal(BaseButton.SignalName.Pressed);
                var sequence = Assert.IsAssignableFrom<Task>(effect.Sequence);
                for (var i = 0; i < updates; i++) scheduler.Update(0.05);
                Assert.False(sequence.IsFaulted);
                stage.Free();
                if (name == nameof(LayoutMorph))
                    for (var frame = 0; frame < 3 && !sequence.IsCompleted; frame++) godot.Engine.Iteration();
                Assert.True(sequence.IsCompletedSuccessfully);
                scheduler.Update(5);
                Assert.Equal(0, scheduler.ActiveCount);
            }
            finally
            {
                if (GodotObject.IsInstanceValid(stage)) stage.Free();
                effect.ReleaseResources();
            }
        }
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }

    [Fact]
    public void DeliveryTaskIncludesTheFinalStatusAnimation()
    {
        var stage = new Control { Size = new Vector2(600, 300) };
        godot.Tree.Root.AddChild(stage);
        var effect = new AsyncDelivery();
        effect.Attach(stage);
        var scheduler = TweenRuntime.GetRunner(stage).Scheduler;
        try
        {
            effect.Start(1);
            var sequence = Assert.IsAssignableFrom<Task>(effect.Sequence);
            for (var i = 0; i < 41; i++) scheduler.Update(0.05);
            Assert.True(scheduler.ActiveCount > 0);
            Assert.False(sequence.IsCompleted);
            for (var i = 0; i < 11; i++) scheduler.Update(0.05);
            Assert.True(sequence.IsCompletedSuccessfully);
            Assert.Equal(0, scheduler.ActiveCount);
        }
        finally { stage.Free(); effect.ReleaseResources(); }
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }

    [Fact]
    public void PathTweenGroupPreservesTrailingTimingAndPingPong()
    {
        var stage = new Control { Size = new Vector2(600, 300) };
        godot.Tree.Root.AddChild(stage);
        var effect = new CurveFollower2D();
        effect.Attach(stage);
        var view = stage.GetChild<SubViewportContainer>(0).GetChild<SubViewport>(0);
        var followers = view.GetChildren().OfType<Path2D>().Single().GetChildren().OfType<PathFollow2D>().ToArray();
        var leader = followers[^1];
        var scheduler = TweenRuntime.GetRunner(stage).Scheduler;
        try
        {
            effect.Start(1);
            scheduler.Update(0.17);
            Assert.True(leader.ProgressRatio > followers[0].ProgressRatio);
            Assert.True(followers[0].ProgressRatio > followers[1].ProgressRatio);
            Assert.True(followers[1].ProgressRatio > 0);
            Assert.Equal(0, followers[2].ProgressRatio);

            scheduler.Update(0.33);
            Assert.InRange(leader.ProgressRatio, 0.499f, 0.501f);
            scheduler.Update(0.5);
            Assert.InRange(leader.ProgressRatio, 0.999f, 1);
            scheduler.Update(0.65);
            Assert.InRange(leader.ProgressRatio, 0.499f, 0.501f);
        }
        finally { stage.Free(); effect.ReleaseResources(); }
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }

    [Fact]
    public void GalleryNavigationAndRestartDisposeThePreviousAnimation()
    {
        var gallery = GD.Load<PackedScene>("res://main.tscn").Instantiate<Gallery>();
        godot.Tree.Root.AddChild(gallery);
        try
        {
            Pump();
            for (var round = 0; round < 2; round++)
            for (var index = 0; index < Gallery.PageNames.Length; index++)
            {
                var old = gallery.CurrentPage!;
                var sequence = old.SequenceTask;
                gallery.SelectPage(index); Pump();
                Assert.False(GodotObject.IsInstanceValid(old));
                if (sequence is not null) Assert.True(sequence.IsCompletedSuccessfully);
                Assert.Equal(index, gallery.SelectedPage);

                var current = gallery.CurrentPage!;
                var animation = current.SequenceTask;
                gallery.RestartPage(); Pump();
                Assert.False(GodotObject.IsInstanceValid(current));
                if (animation is not null) Assert.True(animation.IsCompletedSuccessfully);
                if (gallery.CurrentPage is not ShadersPage)
                    Assert.False(Assert.IsAssignableFrom<Task>(gallery.CurrentPage!.SequenceTask).IsCompleted);
            }
            gallery.SelectPage(1); gallery.SelectPage(4); gallery.SelectPage(0); Pump();
            Assert.Equal(0, gallery.SelectedPage);
            Assert.False(gallery.CurrentPage!.SequenceTask!.IsCompleted);
            Assert.Throws<ArgumentOutOfRangeException>(() => gallery.SelectPage(99));
        }
        finally { gallery.Free(); }
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }
    [Fact]
    public void FreeingBeforeThePageStartsDropsTheDeferredStart()
    {
        var gallery = new Gallery(); godot.Tree.Root.AddChild(gallery);
        gallery.Free();
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }
    [Fact]
    public void LeavingDuringAnAwaitedSequenceSettlesItAndDisposesThePage()
    {
        var gallery = new Gallery(); godot.Tree.Root.AddChild(gallery); Pump();
        var page = gallery.CurrentPage!; var sequence = page.SequenceTask!;
        gallery.Free();
        Assert.True(sequence.IsCompletedSuccessfully);
        Assert.False(GodotObject.IsInstanceValid(page));
    }
}
