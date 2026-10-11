// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System.Text.RegularExpressions;
using Godot;
using Engine = twodog.Engine;

namespace testbed;

internal static class Program
{
    // STA matches how godot.exe runs its main thread on Windows: OLE (drag & drop,
    // IME, native dialogs) fails to initialize on the MTA thread .NET uses by default.
    // No effect on Linux/macOS.
    [STAThread]
    private static void Main(string[] args)
    {
        var options = ParseArguments(args);
        Gallery.CanRecord = true;
        Run(options);
        // A Record button quits the gallery: record in a fresh engine, then reopen the gallery where it was.
        while (Gallery.RequestedRecording is { } recording)
        {
            Gallery.RequestedRecording = null;
            var resume = options with
            {
                Snapshot = null, GallerySnapshots = null, GalleryPage = recording.Page, GallerySource = -1,
                Language = recording.Language, Duration = recording.Duration,
            };
            Run(resume with { Record = recording.Effect });
            Run(resume);
        }
        if (options.RestartCheck) Run(options with { Snapshot = null, GallerySnapshots = null });
    }

    private sealed record LaunchOptions(string[] Forwarded, string? Snapshot, string? GallerySnapshots,
        int GalleryPage, int GallerySource, GalleryLanguage Language, bool RestartCheck, double? Duration, int Record);

    private static LaunchOptions ParseArguments(string[] args)
    {
        var forwarded = new List<string>();
        string? snapshot = null;
        var restartCheck = false;
        string? gallerySnapshots = null;
        var galleryPage = 0;
        var gallerySource = -1;
        var language = GalleryLanguage.CSharp;
        double? duration = null;
        var record = -1;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--snapshot" when i + 1 < args.Length:
                    snapshot = Path.GetFullPath(args[++i]);
                    break;
                case "--gallery-snapshots" when i + 1 < args.Length:
                    gallerySnapshots = Path.GetFullPath(args[++i]);
                    break;
                case "--gallery-page" when i + 1 < args.Length:
                    galleryPage = int.Parse(args[++i]);
                    break;
                case "--gallery-source" when i + 1 < args.Length:
                    gallerySource = int.Parse(args[++i]);
                    break;
                case "--gallery-language" when i + 1 < args.Length:
                    language = args[++i].ToLowerInvariant() switch
                    {
                        "csharp" => GalleryLanguage.CSharp,
                        "gdscript" => GalleryLanguage.GDScript,
                        _ => throw new ArgumentException("--gallery-language must be csharp or gdscript."),
                    };
                    break;
                case "--gallery-duration" when i + 1 < args.Length:
                    duration = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--gallery-record" when i + 1 < args.Length:
                    record = int.Parse(args[++i]);
                    break;
                case "--restart-check":
                    restartCheck = true;
                    break;
                default:
                    forwarded.Add(args[i]);
                    break;
            }
        }
        return new LaunchOptions(forwarded.ToArray(), snapshot, gallerySnapshots, galleryPage, gallerySource, language, restartCheck,
            duration, record);
    }

    private static void Run(LaunchOptions options)
    {
        // The default constructor finds raw project content during development
        // and the exe-adjacent .pck after publish. Arguments are forwarded to Godot.
        using var engine = new Engine("testbed", args: EngineArguments(options));
        engine.Start();
        PrintStartup(engine.Tree);

        var gallery = engine.Tree.CurrentScene as Gallery;
        gallery?.SelectLanguage(options.Language);
        if (options.Duration is { } duration) gallery?.SetLegDuration(duration);
        SelectPage(gallery, options.GallerySnapshots is null ? options.GalleryPage : 0, options.GallerySource);
        if (options.Record >= 0) gallery?.Record(options.Record);
        var capturedPages = 0;

        // Iteration() returns true when Godot wants to quit.
        var frame = 0;
        while (!engine.Iteration())
        {
            frame++;
            if (frame == 20 && options.Snapshot is { } snapshot)
            {
                SaveScreenshot(engine.Tree, snapshot);
                Console.WriteLine($"Saved {snapshot}");
                engine.Tree.Quit();
            }
            if (options.GallerySnapshots is null || frame % 60 != 0 || gallery is null) continue;
            var target = Path.Combine(options.GallerySnapshots, $"{capturedPages + 1:00}.png");
            SaveScreenshot(engine.Tree, target);
            Console.WriteLine($"Captured {Gallery.PageNames[capturedPages]}");
            if (++capturedPages == Gallery.PageNames.Length) engine.Tree.Quit();
            else SelectPage(gallery, capturedPages, options.GallerySource);
        }

        Console.WriteLine("Shutting down...");
    }

    /// <summary>A recording run starts Godot's Movie Maker, which renders every frame at the fixed rate, into ./temp/.
    /// The project's "movie" feature tag sizes the window, and so the movie, at 1920×1080.</summary>
    private static string[] EngineArguments(LaunchOptions options)
    {
        if (options.Record < 0) return options.Forwarded;
        var directory = Directory.CreateDirectory(Path.GetFullPath("temp")).FullName;
        var page = Regex.Replace(Gallery.PageNames[options.GalleryPage].ToLowerInvariant(), "[^a-z0-9]+", "-");
        var name = $"{page}-{options.Record + 1:00}-{options.Language.ToString().ToLowerInvariant()}";
        var movie = Path.Combine(directory, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}.avi");
        Console.WriteLine($"Recording {movie}");
        return [.. options.Forwarded, "--write-movie", movie, "--fixed-fps", Gallery.RecordingFps.ToString()];
    }

    private static void PrintStartup(SceneTree tree)
    {
        if (tree.CurrentScene is { } scene)
            GD.Print($"2dog is running '{scene.Name}'!");
        else
            GD.Print("2dog is running (no run/main_scene set in project.godot).");
        Console.WriteLine("Close the window to quit.");
    }

    private static void SelectPage(Gallery? gallery, int page, int source)
    {
        gallery?.SelectPage(page);
        if (source >= 0) gallery?.CurrentPage?.ShowSource(source);
    }

    private static void SaveScreenshot(SceneTree tree, string path)
    {
        using var image = tree.Root.GetTexture().GetImage();
        if (image.IsEmpty()) throw new InvalidOperationException("Screenshot requires a rendering display driver.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var result = image.SavePng(path);
        if (result != Error.Ok) throw new IOException($"Screenshot failed: {result} ({path})");
    }
}
