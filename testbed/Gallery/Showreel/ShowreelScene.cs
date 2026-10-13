// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss
using Godot;
namespace testbed;

/// <summary>The root of Showreel.tscn. Inside the gallery it only holds the layout; when Godot runs the scene on its
/// own, the showreel page takes its place and fills the window with the showreel.</summary>
public partial class ShowreelScene : Node3D
{
    [Export] public GalleryLanguage Language { get; set; }

    public override void _Ready()
    {
        if (GetParent() is not Window window) return;
        var tree = GetTree();
        var current = tree.CurrentScene == this;
        var page = new ShowreelPage { Language = Language, SoloOnStart = 0 };
        // The window is still adding its children, so the page joins it once this scene is gone.
        Callable.From(() =>
        {
            window.AddChild(page);
            if (current) tree.CurrentScene = page;
        }).CallDeferred();
        QueueFree();
    }
}
