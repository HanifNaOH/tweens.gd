// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using tweens.gd;

namespace testbed;

/// <summary>Gallery shell: header, page navigation and the current page. Navigation frees the previous page.</summary>
public partial class Gallery : Control
{
    private static readonly (string Name, Func<GalleryPage> Create)[] Pages =
    [
        ("Squash & stretch", () => new SquashPage()),
        ("Choreography", () => new ChoreographyPage()),
        ("Motion & paths", () => new MotionPage()),
        ("Interface", () => new InterfacePage()),
        ("Drawing & particles", () => new DrawingPage()),
        ("3D", () => new SpatialPage()),
        ("Materials", () => new MaterialsPage()),
        ("Shaders", () => new ShadersPage()),
        ("Easing", () => new EasingPage()),
        ("Colors", () => new ColorsPage()),
        ("Keyframes", () => new KeyframesPage()),
        ("Layout transitions", () => new LayoutTransitionsPage()),
        ("Showreel", () => new ShowreelPage()),
    ];

    public static readonly string[] PageNames = Pages.Select(p => p.Name).ToArray();

    private readonly List<Button> navigation = [];
    private readonly List<Resource> themeResources = [];
    private VBoxContainer? content;
    private Label durationLabel = null!;
    private HSlider duration = null!;
    private OptionButton languagePicker = null!;
    private MarginContainer margin = null!;
    private Control sidebar = null!;
    private DisplayServer.WindowMode windowed = DisplayServer.WindowMode.Windowed;
    public GalleryLanguage Language { get; private set; }
    private GalleryPage? page;
    private EasingSelection easingSelection = EasingSelection.Default;
    private int revision;

    public int SelectedPage { get; private set; }
    public GalleryPage? CurrentPage => page;

    public override void _Ready()
    {
        if (GalleryEffect.Supports2DMsaa) GetTree().Root.Msaa2D = Viewport.Msaa.Msaa4X;
        BuildControls();
        SelectPage(0);
    }

    private void BuildControls()
    {
        Theme = GalleryTheme.Build(themeResources);
        var background = new ColorRect { Color = Palette.Background, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        margin = new MarginContainer();
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetChrome(true);

        // The sidebar holds every control, so the examples get the window's full height.
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 16);
        margin.AddChild(body);
        sidebar = body.Add(BuildSidebar());
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body.AddChild(scroll);
        content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(content);
    }

    /// <summary>The title, settings and page navigation. It scrolls when the window is shorter than its contents.</summary>
    private ScrollContainer BuildSidebar()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var sidebar = scroll.Add(new VBoxContainer { CustomMinimumSize = new Vector2(232, 0) });
        sidebar.AddThemeConstantOverride("separation", 4);
        sidebar.AddChild(GalleryTheme.Label("tweens.gd / testbed", 22));
        sidebar.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        languagePicker = sidebar.Add(new OptionButton
        {
            Name = "LanguageSwitch", CustomMinimumSize = new Vector2(0, 34),
            TooltipText = "Switch the running tween implementation and restart this page",
        });
        languagePicker.AddItem("C# · Beta");
        languagePicker.AddItem("GDScript · Beta");
        languagePicker.Select((int)Language);
        languagePicker.ItemSelected += index => SelectLanguage((GalleryLanguage)index);

        var timing = sidebar.Add(new HBoxContainer());
        var timingLabel = timing.Add(GalleryTheme.Label("Leg duration", 16, Palette.Muted));
        timingLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        durationLabel = timing.Add(GalleryTheme.Label("1.8 s"));
        duration = sidebar.Add(new HSlider { MinValue = 0.4, MaxValue = 4, Step = 0.1, Value = 1.8, CustomMinimumSize = new Vector2(0, 30) });
        duration.ValueChanged += seconds =>
        {
            durationLabel.Text = $"{seconds:0.0} s";
            RestartPage();
        };
        var restart = sidebar.Add(new Button { Text = "Restart page", CustomMinimumSize = new Vector2(0, 34) });
        restart.Pressed += RestartPage;
        sidebar.AddChild(GalleryTheme.Label("Settings apply to the current page.", 13, Palette.Muted));
        sidebar.AddChild(new HSeparator { CustomMinimumSize = new Vector2(0, 20) });

        sidebar.AddChild(GalleryTheme.Label("EXAMPLES", 12, Palette.Muted));

        var idle = GalleryTheme.Box(Colors.Transparent, 8, 0, null, 14, 8);
        var hover = GalleryTheme.Box(Palette.Raised, 8, 0, null, 14, 8);
        var selected = GalleryTheme.Box(Palette.Selected, 8, 0, Palette.Mint, 14, 8);
        selected.BorderWidthLeft = 3;
        themeResources.AddRange([idle, hover, selected]);

        for (var i = 0; i < PageNames.Length; i++)
        {
            var index = i;
            var button = new Button
            {
                Text = $"{i + 1:00}  {PageNames[i]}", ToggleMode = true, FocusMode = FocusModeEnum.None,
                Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 36),
            };
            button.AddThemeStyleboxOverride("normal", idle);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", selected);
            button.AddThemeStyleboxOverride("hover_pressed", selected);
            button.AddThemeFontSizeOverride("font_size", 14);
            button.Pressed += () => SelectPage(index);
            sidebar.AddChild(button);
            navigation.Add(button);
        }

        return scroll;
    }

    /// <summary>Whether one example of the current page fills the window.</summary>
    public bool IsFullscreen => page?.SoloEffect >= 0;

    /// <summary>Shows only one example of the current page, magnified, and makes the window fullscreen.</summary>
    public void EnterFullscreen(int effect) => Fill(effect, recording: false);

    private void Fill(int effect, bool recording)
    {
        if (page is null) return;
        // The page measures the card before the chrome hides, so the example keeps its layout.
        page.ShowSolo(effect, exitButton: !recording);
        SetChrome(false);
        // A movie keeps the window size it started with, so recordings leave the window as it is.
        if (recording) return;
        windowed = DisplayServer.WindowGetMode();
        if (windowed != DisplayServer.WindowMode.Fullscreen) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
    }

    public void ExitFullscreen()
    {
        if (!IsFullscreen) return;
        // Leaving a recording ends it; quitting lets Movie Maker finish the file.
        if (recordFrames >= 0)
        {
            GetTree().Quit();
            return;
        }
        page!.ShowGallery();
        SetChrome(true);
        if (DisplayServer.WindowGetMode() != windowed) DisplayServer.WindowSetMode(windowed);
    }

    private void SetChrome(bool visible)
    {
        if (sidebar is not null) sidebar.Visible = visible;
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, visible ? 16 : 0);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!IsFullscreen || !input.IsActionPressed("ui_cancel")) return;
        ExitFullscreen();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Frames per second of a recording; each frame advances the examples by exactly one frame's time.</summary>
    public const int RecordingFps = 60;
    /// <summary>Set by a host that can restart into a recording run. It shows each card's Record button.</summary>
    public static bool CanRecord { get; set; }
    /// <summary>The recording that a Record button asked for. The host consumes it after the gallery quits.</summary>
    public static GalleryRecording? RequestedRecording { get; set; }
    private int recordEffect = -1;
    private bool recordPending;
    private long recordFrames = -1;

    /// <summary>Godot writes movies only from startup, so the gallery quits and the host restarts it to record.</summary>
    public void RequestRecording(int effect)
    {
        if (page is null) return;
        RequestedRecording = new GalleryRecording(SelectedPage, effect, Language, duration.Value);
        GetTree().Quit();
    }

    /// <summary>Records one example in a Movie Maker run: once its page starts, the example fills the window without
    /// controls, and the gallery quits after the example's recording length so Godot finishes the file.</summary>
    public void Record(int effect) => recordEffect = effect;

    /// <summary>Sets the leg duration for the next page start, without restarting the current page.</summary>
    public void SetLegDuration(double seconds)
    {
        duration.SetValueNoSignal(seconds);
        durationLabel.Text = $"{duration.Value:0.0} s";
    }

    public override void _Process(double delta)
    {
        if (recordPending)
        {
            recordPending = false;
            Fill(recordEffect, recording: true);
            recordFrames = (long)Math.Round(page!.Effects[recordEffect].RecordingSeconds * RecordingFps);
        }
        else if (recordFrames > 0 && --recordFrames == 0) GetTree().Quit();
    }

    public void SelectPage(int index)
    {
        if (index < 0 || index >= Pages.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (!IsInsideTree() || content is null) return;
        ExitFullscreen();
        Load(index);
    }

    private void Load(int index)
    {
        if (!IsInsideTree() || content is null) return;
        DestroyPage();
        SelectedPage = index;
        for (var i = 0; i < navigation.Count; i++) navigation[i].SetPressedNoSignal(i == index);

        page = Pages[index].Create();
        if (page is EasingPage easingPage) easingPage.Selection = easingSelection;
        page.Language = Language;
        page.FullscreenRequested += EnterFullscreen;
        page.RecordRequested += RequestRecording;
        page.FullscreenExitRequested += ExitFullscreen;
        content.AddChild(page);

        // Start once the page's layout has settled. Bound to this node, so Godot drops the call if the gallery is
        // freed first.
        CallDeferred(MethodName.StartSelectedPage, ++revision);
    }

    /// <summary>Starts the current page, unless a newer selection superseded <paramref name="selection"/>.</summary>
    private void StartSelectedPage(int selection)
    {
        if (!IsInsideTree() || selection != revision || page is null) return;
        try
        {
            page.Start(duration.Value);
            // A recording starts once the page is laid out, so the card it magnifies has its size.
            recordPending = recordEffect >= 0;
        }
        catch (Exception error)
        {
            DestroyPage();
            GD.PushError(error.ToString());
        }
    }

    public void SelectLanguage(GalleryLanguage language)
    {
        if (!Enum.IsDefined(language)) throw new ArgumentOutOfRangeException(nameof(language));
        if (Language == language) return;
        Language = language;
        languagePicker?.Select((int)language);
        RestartPage();
    }

    public void RestartPage()
    {
        if (content is null) return;
        var selectedEffect = page?.SelectedEffect ?? -1;
        var solo = page?.SoloEffect ?? -1;
        var soloSize = page?.SoloSize ?? default;
        // A restart keeps the open source view, or the example filling the window.
        Load(SelectedPage);
        if (selectedEffect >= 0) page?.ShowSource(selectedEffect);
        else if (solo >= 0) page?.ShowSolo(solo, soloSize);
    }

    private void DestroyPage()
    {
        if (page is null) return;
        if (page is EasingPage) easingSelection = ((EasingComposer)page.Effects[0]).Selection;
        page.Free();
        page = null;
    }

    public override void _ExitTree()
    {
        // Children are still valid during tree exit; release page-owned resources after their deletion.
        DestroyPage();
        foreach (var resource in themeResources) resource.Dispose();
        themeResources.Clear();
    }
}

/// <summary>An example to record: its page, its index on the page, and the settings it plays with.</summary>
public sealed record GalleryRecording(int Page, int Effect, GalleryLanguage Language, double Duration);
