// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using Godot;
using twodog.Testing;
using twodog.Testing.Xunit;
using FileAccess = Godot.FileAccess;

namespace testbed.Tests.Integration;

[Collection<HeadlessCollection>]
public class GallerySourceTests(HeadlessFixture godot)
{
    private void Pump() { for (var i = 0; i < 3; i++) godot.Engine.Iteration(); }

    [Fact]
    public void EveryEffectEmbedsItsAnimationPartialAndCompanionWithoutTrackingInTheAnimation()
    {
        var types = typeof(GalleryEffect).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(GalleryEffect)) && !t.IsAbstract && !t.IsNested).ToArray();
        Assert.Equal(36, types.Length);
        foreach (var type in types)
        {
            var effect = (GalleryEffect)Activator.CreateInstance(type)!;
            var animation = GallerySource.ForEffect(effect);
            var gdscript = GallerySource.ForEffect(effect, GalleryLanguage.GDScript);
            var setup = GallerySource.ForSetup(effect);
            Assert.EndsWith(type.Name + ".Animation.cs", animation.Path);
            Assert.EndsWith(type.Name + ".cs", setup.Path);
            Assert.Contains("public sealed partial class " + type.Name, animation.Text);
            Assert.Contains("public sealed partial class " + type.Name, setup.Text);
            Assert.Contains(".Tween", animation.Text);
            Assert.Contains("async Task", animation.Text);
            foreach (var hidden in new[] { "Play(", "Keep(", "TrackTweens(", "Generation", "protected override void Build(", "IEnumerable<TweenInstance>", "yield return" })
                Assert.DoesNotContain(hidden, animation.Text);
            Assert.EndsWith(type.Name + ".gd", gdscript.Path);
            Assert.Contains("func animate()", gdscript.Text);
            Assert.Contains("Tweens.", gdscript.Text);
            using var compiled = new GalleryGDScript(gdscript);
            foreach (var source in new[] { animation, setup, gdscript })
                Assert.Equal(FileAccess.GetFileAsString("res://" + source.Path).Replace("\r\n", "\n"), source.Text);
        }
    }

    [Fact]
    public void EveryCardDisplaysTheExactCompiledSourceWithoutRestartingPlayback()
    {
        var gallery = new Gallery();
        godot.Tree.Root.AddChild(gallery);
        try
        {
            for (var index = 0; index < Gallery.PageNames.Length; index++)
            {
                gallery.SelectPage(index); Pump();
                var page = gallery.CurrentPage!;
                var buttons = Descendants(page).OfType<Button>().Where(b => b.Text == "View C#").ToArray();
                Assert.NotEmpty(buttons);
                var sequence = page.SequenceTask;
                for (var effect = 0; effect < buttons.Length; effect++)
                {
                    buttons[effect].EmitSignal(BaseButton.SignalName.Pressed);
                    var view = page.SourceView;
                    Assert.Equal(effect, page.SelectedEffect);
                    Assert.True(view.Visible);
                    Assert.False(view.Code.Editable);
                    Assert.True(view.Code.GuttersDrawLineNumbers);
                    Assert.IsType<CodeHighlighter>(view.Code.SyntaxHighlighter);
                    Assert.Equal(FileAccess.GetFileAsString("res://" + view.Source.Path).Replace("\r\n", "\n"), view.Code.Text);
                    Assert.True(view.Source.TweenLine > 0);
                    Assert.Equal(view.Source.TweenLine, view.Code.GetCaretLine());
                    Assert.Same(sequence, page.SequenceTask);
                }
                page.ShowGallery();
                Assert.Equal(-1, page.SelectedEffect);
                Assert.False(page.SourceView.Visible);
                Assert.All(buttons, button => Assert.True(button.IsVisibleInTree()));
            }
        }
        finally { gallery.Free(); }
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }

    [Fact]
    public void FullscreenShowsOnlyThePlayingExampleUntilEscapeWithoutRestartingIt()
    {
        var gallery = new Gallery();
        godot.Tree.Root.AddChild(gallery);
        gallery.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Pump();
        try
        {
            var page = gallery.CurrentPage!;
            Assert.False(page.Standalone);
            var sequence = page.SequenceTask;
            var fullscreen = Descendants(page).OfType<Button>().Where(b => b.Text == "Fullscreen").ToArray();
            Assert.Equal(page.Effects.Count, fullscreen.Length);
            // Only a host that can restart into Movie Maker shows Record buttons.
            Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => b.Text == "Record" && b.Visible);
            fullscreen[1].EmitSignal(BaseButton.SignalName.Pressed);
            Pump();
            Assert.True(gallery.IsFullscreen);
            Assert.Equal(1, page.SoloEffect);
            Assert.Same(sequence, page.SequenceTask);
            Assert.Single(Stages(gallery));
            Assert.Equal("Exit fullscreen", Assert.Single(Descendants(gallery).OfType<Button>(), b => b.IsVisibleInTree()).Text);

            // A restart rebuilds the page and keeps the same example filling the window.
            gallery.RestartPage(); Pump();
            Assert.Equal(1, gallery.CurrentPage!.SoloEffect);
            Assert.Single(Stages(gallery));

            gallery.GetViewport().PushInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
            Pump();
            Assert.False(gallery.IsFullscreen);
            Assert.Equal(-1, gallery.CurrentPage!.SoloEffect);
            Assert.Equal(gallery.CurrentPage.Effects.Count, Stages(gallery).Count());
            Assert.All(Descendants(gallery.CurrentPage).OfType<Button>().Where(b => b.Text == "Fullscreen"), b => Assert.True(b.IsVisibleInTree()));
        }
        finally { gallery.Free(); }
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }

    [Theory]
    [InlineData(GalleryLanguage.CSharp)] [InlineData(GalleryLanguage.GDScript)]
    public void APageUnderTheRootWindowPlaysStandaloneWithItsOwnFullscreen(GalleryLanguage language)
    {
        var page = new SquashPage { Language = language };
        godot.Tree.Root.AddChild(page);
        Pump();
        Task sequence;
        try
        {
            Assert.True(page.Standalone);
            Assert.NotNull(page.Theme);
            Assert.Equal(new Vector4(0, 0, 1, 1), new Vector4(page.AnchorLeft, page.AnchorTop, page.AnchorRight, page.AnchorBottom));
            Assert.Equal(Vector4.Zero, new Vector4(page.OffsetLeft, page.OffsetTop, page.OffsetRight, page.OffsetBottom));
            sequence = Assert.IsAssignableFrom<Task>(page.SequenceTask);
            Assert.False(sequence.IsCompleted);
            Assert.All(page.Effects, effect => Assert.Equal(language, effect.Language));
            Assert.DoesNotContain(Descendants(page).OfType<Button>(), b => b.Text == "Record" && b.Visible);

            var fullscreen = Descendants(page).OfType<Button>().Where(b => b.Text == "Fullscreen").ToArray();
            fullscreen[1].EmitSignal(BaseButton.SignalName.Pressed);
            Pump();
            Assert.Equal(1, page.SoloEffect);
            Assert.Single(Stages(page));
            Assert.Same(sequence, page.SequenceTask);

            page.GetViewport().PushInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
            Pump();
            Assert.Equal(-1, page.SoloEffect);
            Assert.Equal(page.Effects.Count, Stages(page).Count());
        }
        finally { page.Free(); }
        Assert.True(sequence.IsCompletedSuccessfully);
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }

    [Fact]
    public void RunningTheShowreelSceneOnItsOwnPlaysTheShowreelFillingThePage()
    {
        var scene = GD.Load<PackedScene>("res://Gallery/Showreel/Showreel.tscn").Instantiate();
        godot.Tree.Root.AddChild(scene);
        Pump();
        var page = Assert.Single(godot.Tree.Root.GetChildren().OfType<ShowreelPage>());
        try
        {
            Assert.False(GodotObject.IsInstanceValid(scene));
            Assert.True(page.Standalone);
            Assert.Equal(0, page.SoloEffect);
            Assert.False(Assert.IsAssignableFrom<Task>(page.SequenceTask).IsCompleted);
        }
        finally { page.Free(); }
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }

    private static IEnumerable<Control> Stages(Node gallery) =>
        Descendants(gallery).OfType<Control>().Where(c => c.Name == "PreviewStage" && c.IsVisibleInTree());

    [Fact]
    public void HelpersAndSourceSelectionSurviveRestartAndDisposeWithThePage()
    {
        var gallery = new Gallery();
        godot.Tree.Root.AddChild(gallery); Pump();
        try
        {
            gallery.CurrentPage!.ShowSource(1);
            var view = gallery.CurrentPage.SourceView;
            var files = Descendants(view).OfType<OptionButton>().Single();
            Assert.Equal("Animation", files.GetItemText(0));
            Assert.Equal("Scene & playback", files.GetItemText(1));
            var animationPath = view.Source.Path;
            for (var i = 1; i < files.ItemCount; i++)
            {
                files.Select(i);
                files.EmitSignal(OptionButton.SignalName.ItemSelected, i);
                Assert.Equal(FileAccess.GetFileAsString("res://" + view.Source.Path).Replace("\r\n", "\n"), view.Code.Text);
                Assert.Equal(Math.Max(0, view.Source.TweenLine), view.Code.GetCaretLine());
                if (i == 1) Assert.Equal(animationPath.Replace(".Animation.cs", ".cs"), view.Source.Path);
            }
            gallery.RestartPage(); Pump();
            Assert.False(GodotObject.IsInstanceValid(view));
            Assert.Equal(1, gallery.CurrentPage!.SelectedEffect);
            Assert.True(gallery.CurrentPage.SourceView.Visible);
            gallery.SelectPage(2); Pump();
            Assert.Equal(-1, gallery.CurrentPage!.SelectedEffect);
        }
        finally { gallery.Free(); }
        Pump();
        Assert.Empty(godot.Errors.Drain());
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
