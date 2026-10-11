// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
namespace testbed;

/// <summary>One gallery page: a grid of effect cards. Only the selected page exists at a time.</summary>
public abstract partial class GalleryPage : VBoxContainer
{
    private readonly List<Resource> resources = [];
    private GalleryEffect[] effects = [];
    private readonly List<PanelContainer> cards = [];
    private readonly List<StyleBox> cardBoxes = [], stageBoxes = [];
    private readonly List<Control> headers = [], captions = [];
    private readonly List<Button> exitButtons = [];
    private GridContainer grid = null!;
    private HBoxContainer heading = null!;
    private HBoxContainer sourceNavigation = null!;
    private OptionButton examplePicker = null!;
    private GallerySourceView sourceView = null!;
    private Label description = null!;
    private StyleBox soloCard = null!, soloStage = null!;
    private readonly List<Control> frames = [];

    public GalleryLanguage Language { get; set; }
    public IReadOnlyList<GalleryEffect> Effects => effects;

    public int SelectedEffect { get; private set; } = -1;
    public GallerySourceView SourceView => sourceView;

    /// <summary>The example filling the page, or -1.</summary>
    public int SoloEffect { get; private set; } = -1;
    /// <summary>The card stage size that the example filling the page magnifies.</summary>
    public Vector2 SoloSize { get; private set; }
    /// <summary>A card asks to fill the window with its example.</summary>
    public event Action<int>? FullscreenRequested;
    /// <summary>The example filling the window asks to return to the gallery.</summary>
    public event Action? FullscreenExitRequested;
    /// <summary>A card asks to record its example.</summary>
    public event Action<int>? RecordRequested;

    public abstract string Heading { get; }
    public abstract string Description { get; }
    public Task? SequenceTask { get; private set; }

    protected abstract GalleryEffect[] CreateEffects();

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 10);
        heading = this.Add(new HBoxContainer());
        heading.AddThemeConstantOverride("separation", 16);
        heading.AddChild(GalleryTheme.Label(Heading, 24));
        description = heading.Add(GalleryTheme.Label(Description, 14, Palette.Muted));
        description.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        description.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        description.TooltipText = Description;
        description.MouseFilter = MouseFilterEnum.Pass;

        sourceNavigation = heading.Add(new HBoxContainer { Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var back = sourceNavigation.Add(new Button { Text = "‹ All examples" });
        back.Pressed += ShowGallery;
        examplePicker = sourceNavigation.Add(new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        examplePicker.ItemSelected += index => ShowSource((int)index);

        var split = this.Add(new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill });
        sourceView = split.Add(new GallerySourceView { Visible = false });
        grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 14);
        split.AddChild(grid);

        soloCard = Own(new StyleBoxEmpty());
        soloStage = Own(GalleryTheme.Box(Palette.Stage, 0, 0, null, 0, 0));
        effects = CreateEffects();
        grid.Columns = effects.Length == 1 ? 1 : 2;
        for (var i = 0; i < effects.Length; i++)
        {
            examplePicker.AddItem(effects[i].Title);
            effects[i].Attach(AddCard($"{i + 1:00} / {effects[i].Title}", effects[i].Caption, i), Language);
        }
    }

    public void ShowSource(int index)
    {
        if (index < 0 || index >= effects.Length) throw new ArgumentOutOfRangeException(nameof(index));
        LeaveSolo();
        SelectedEffect = index;
        sourceNavigation.Show();
        description.Hide();
        sourceView.Show();
        examplePicker.Select(index);
        grid.Columns = 1;
        grid.SizeFlagsVertical = SizeFlags.ExpandFill;
        for (var i = 0; i < cards.Count; i++)
        {
            cards[i].Visible = i == index;
            headers[i].GetChild<Control>(1).Visible = false;
            FitPreview(frames[i]);
        }
        sourceView.ShowEffect(effects[index], Language);
    }

    public void ShowGallery()
    {
        LeaveSolo();
        SelectedEffect = -1;
        sourceNavigation.Hide();
        description.Show();
        sourceView.Hide();
        grid.Columns = effects.Length == 1 ? 1 : 2;
        grid.SizeFlagsVertical = SizeFlags.ExpandFill;
        foreach (var card in cards) card.Show();
        foreach (var header in headers) header.GetChild<Control>(1).Show();
        foreach (var frame in frames) FitPreview(frame);
    }

    /// <summary>Shows only one example, magnified to fill the page; it keeps playing. Without a
    /// <paramref name="size"/>, the card's current stage size is the layout that it magnifies. Recordings hide the
    /// exit button, so it stays out of the movie.</summary>
    public void ShowSolo(int index, Vector2 size = default, bool exitButton = true)
    {
        if (index < 0 || index >= effects.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (SelectedEffect >= 0) ShowGallery();
        LeaveSolo();
        SoloSize = size == default ? frames[index].GetChild<Control>(0).Size : size;
        SoloEffect = index;
        heading.Hide();
        grid.Columns = 1;
        for (var i = 0; i < cards.Count; i++) cards[i].Visible = i == index;
        headers[index].Hide();
        captions[index].Hide();
        exitButtons[index].Visible = exitButton;
        cards[index].AddThemeStyleboxOverride("panel", soloCard);
        frames[index].GetChild<Control>(0).AddThemeStyleboxOverride("panel", soloStage);
        FitPreview(frames[index]);
    }

    private void LeaveSolo()
    {
        if (SoloEffect < 0) return;
        var index = SoloEffect;
        SoloEffect = -1;
        heading.Show();
        headers[index].Show();
        captions[index].Show();
        exitButtons[index].Hide();
        cards[index].AddThemeStyleboxOverride("panel", cardBoxes[index]);
        frames[index].GetChild<Control>(0).AddThemeStyleboxOverride("panel", stageBoxes[index]);
        FitPreview(frames[index]);
    }

    public void Start(double seconds)
    {
        foreach (var effect in effects) effect.Start(seconds);
        var sequences = effects.Select(e => e.Sequence).OfType<Task>().ToArray();
        SequenceTask = sequences.Length > 0 ? Task.WhenAll(sequences) : null;
    }

    /// <summary>Called after Free(), so children have relinquished their native resource references.</summary>
    public void ReleaseResources()
    {
        sourceView.ReleaseResources();
        foreach (var effect in effects) effect.ReleaseResources();
        foreach (var resource in resources) resource.Dispose();
        resources.Clear();
    }

    /// <summary>Adds a titled card to the grid and returns its stage, the area an effect draws into.</summary>
    private Control AddCard(string title, string caption, int index)
    {
        var panel = grid.Add(new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
        });
        cardBoxes.Add(Own(GalleryTheme.Box(Palette.Surface, 12, 1, null, 12, 10)));
        panel.AddThemeStyleboxOverride("panel", cardBoxes[^1]);
        cards.Add(panel);

        var column = panel.Add(new VBoxContainer());
        column.AddThemeConstantOverride("separation", 8);
        var header = column.Add(new HBoxContainer());
        headers.Add(header);
        var label = header.Add(GalleryTheme.Label(title, 19));
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        var tools = header.Add(new HBoxContainer());
        var record = tools.Add(new Button { Text = "Record", Visible = Gallery.CanRecord, TooltipText = $"Record this example to temp/ at {Gallery.RecordingFps} fps; Esc stops" });
        record.AddThemeFontSizeOverride("font_size", 13);
        record.Pressed += () => RecordRequested?.Invoke(index);
        var fullscreen = tools.Add(new Button { Text = "Fullscreen", TooltipText = "Show only this example; Esc returns" });
        fullscreen.AddThemeFontSizeOverride("font_size", 13);
        fullscreen.Pressed += () => FullscreenRequested?.Invoke(index);
        var source = tools.Add(new Button { Text = Language == GalleryLanguage.CSharp ? "View C#" : "View GDScript", TooltipText = "Read the actual source beside this animation" });
        source.AddThemeFontSizeOverride("font_size", 13);
        source.Pressed += () => ShowSource(index);

        // Drawn clipping keeps viewports inside the rounded inset.
        var frame = column.Add(new Control
        {
            CustomMinimumSize = new Vector2(370, 168), MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
        });
        frames.Add(frame);
        var stage = frame.Add(new Panel
        {
            Name = "PreviewStage",
            ClipChildren = ClipChildrenMode.AndDraw, MouseFilter = MouseFilterEnum.Ignore,
        });
        frame.Resized += () => FitPreview(frame);
        stageBoxes.Add(Own(GalleryTheme.Box(Palette.Stage, 8)));
        stage.AddThemeStyleboxOverride("panel", stageBoxes[^1]);
        // Drawn over the stage while the example fills the window.
        var exit = frame.Add(new Button { Text = "Exit fullscreen", TooltipText = "Esc", Visible = false, Modulate = new Color(1, 1, 1, 0.5f) });
        exit.AddThemeFontSizeOverride("font_size", 13);
        exit.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.KeepSize, 32);
        exit.GrowHorizontal = GrowDirection.Begin;
        exit.Pressed += () => FullscreenExitRequested?.Invoke();
        exitButtons.Add(exit);

        var note = column.Add(GalleryTheme.Label(caption, 13, Palette.Muted));
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        captions.Add(note);
        return stage;
    }

    private T Own<T>(T resource) where T : Resource
    {
        resources.Add(resource);
        return resource;
    }

    private void FitPreview(Control frame)
    {
        // Fill gallery cards; fit source previews at 2:1 without making the layout's
        // minimum size depend on the previous window size.
        var stage = frame.GetChild<Control>(0);
        var size = frame.Size;
        var magnification = 1f;
        if (SelectedEffect >= 0)
        {
            var width = Math.Min(size.X, size.Y * 2);
            size = new Vector2(width, width / 2);
        }
        else if (SoloEffect >= 0 && frames[SoloEffect] == frame && SoloSize.X > 0 && SoloSize.Y > 0)
        {
            // Scaled up uniformly until the card's layout fits; the stage extends to the page's edges around it.
            magnification = Math.Min(size.X / SoloSize.X, size.Y / SoloSize.Y);
            size /= magnification;
        }
        stage.Size = size;
        stage.Scale = Vector2.One * magnification;
        stage.Position = (frame.Size - size * magnification) / 2;
        Magnify(stage, magnification);
    }

    /// <summary>Renders the stage's viewports at the magnified resolution: each one renders the magnification times its
    /// layout size and draws counter-scaled, while 2D content keeps that layout. Gallery viewports are anchored to the
    /// stage; a viewport inside a layout container keeps stretching.</summary>
    private static void Magnify(Control stage, float magnification)
    {
        foreach (var container in Descendants(stage).OfType<SubViewportContainer>())
        {
            if (container.GetChildren().OfType<SubViewport>().FirstOrDefault() is not { } view) continue;
            if (container.GetParentControl() is not { } parent || parent is Container) continue;
            var magnified = magnification != 1;
            if (!magnified && container.Stretch) continue;
            // Unstretched, a container's size follows its viewport, so the layout comes from its anchors and offsets.
            var layout = parent.Size * new Vector2(container.AnchorRight - container.AnchorLeft, container.AnchorBottom - container.AnchorTop)
                + new Vector2(container.OffsetRight - container.OffsetLeft, container.OffsetBottom - container.OffsetTop);
            container.Stretch = !magnified;
            container.Scale = Vector2.One / magnification;
            view.Size2DOverrideStretch = magnified && !view.OwnWorld3D;
            view.Size2DOverride = magnified && !view.OwnWorld3D ? (Vector2I)layout.Round() : Vector2I.Zero;
            if (magnified) view.Size = (Vector2I)(layout * magnification).Round();
        }
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
