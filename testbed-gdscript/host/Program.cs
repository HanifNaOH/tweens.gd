// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss
using Godot;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Engine.Start changes cwd to the Godot project; resolve caller paths first.
        var outputIndex = Array.IndexOf(args, "--benchmark-output");
        var benchmarkOutput = outputIndex >= 0 && outputIndex + 1 < args.Length
            ? Path.GetFullPath(args[outputIndex + 1]) : null;
        var directory = Directory.GetCurrentDirectory();
        var cycles = args.Contains("--lifecycle") || args.Contains("--restart-probe") ? 3 : 1;
        if (cycles == 1) return Run(args, benchmarkOutput);
        for (var cycle = 0; cycle < cycles; cycle++)
        {
            Directory.SetCurrentDirectory(directory);
            var result = 1;
            var thread = new Thread(() => result = Run(args, benchmarkOutput));
            if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (result != 0) return result;
        }
        return 0;
    }

    private static int Run(string[] args, string? benchmarkOutput)
    {
        var engineArgs = args.Contains("--rendering")
            ? new[] { "--rendering-method", "gl_compatibility", "--audio-driver", "Dummy", "--minimized", "--resolution", "64x64", "--fixed-fps", "60" }
            : new[] { "--headless", "--fixed-fps", "60" };
        if (args.Contains("--restart-probe")) engineArgs = [.. engineArgs, "res://restart_probe.tscn"];
        using var engine = new twodog.Engine("gdscript.2dog", args: engineArgs);
        engine.Start();
        try { return RunTests(engine, args, benchmarkOutput); }
        finally
        {
            // Finalize wrappers while their engine is alive; never let a later engine reuse their native addresses.
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private static int RunTests(twodog.Engine engine, string[] args, string? benchmarkOutput)
    {
        if (args.Contains("--restart-probe"))
        {
            using var method = new StringName("ping");
            var result = engine.Tree.CurrentScene.Call(method).AsInt32();
            Console.WriteLine($"GDScript restart probe: {result}");
            return result == 42 ? 0 : 1;
        }
        // Compile every script, so each failure prints its own parser diagnostics rather than only the first.
        var root = ProjectSettings.GlobalizePath("res://");
        var failed = 0;
        foreach (var source in Directory.EnumerateFiles(root, "*.gd", SearchOption.AllDirectories))
        {
            var path = "res://" + Path.GetRelativePath(root, source).Replace('\\', '/');
            if (path.StartsWith("res://.godot/", StringComparison.Ordinal)) continue;
            using var script = ResourceLoader.Load<GDScript>(path);
            if (script is not null && script.CanInstantiate()) continue;
            Console.Error.WriteLine($"GDScript failed to compile: {path}");
            failed++;
        }
        if (failed != 0) return 1;
        var tests = engine.Tree.CurrentScene;
        if (tests is null || !tests.HasMethod("run_tests"))
        {
            Console.Error.WriteLine("GDScript suite failed to load (check parser errors above).");
            return 1;
        }
        using var runTests = new StringName("run_tests");
        tests.Set("trace_runs", args.Contains("--lifecycle"));
        tests.Call(runTests);
        for (var frame = 0; frame < 240 && !tests.Get("finished").AsBool(); frame++)
            engine.Iteration();
        if (!tests.Get("finished").AsBool())
        {
            Console.Error.WriteLine("GDScript suite did not finish within 240 frames.");
            return 1;
        }
        using var failures = tests.Get("failures").AsGodotArray();
        foreach (var failure in failures) Console.Error.WriteLine(failure.AsString());
        Console.WriteLine($"GDScript: {tests.Get("checks").AsInt32()} checks, {failures.Count} failures.");
        if (args.Contains("--benchmark") && failures.Count == 0)
        {
            var json = tests.Call("benchmark").AsString();
            if (string.IsNullOrEmpty(json))
            {
                Console.Error.WriteLine("GDScript benchmark failed; see script errors above.");
                return 1;
            }
            Console.WriteLine(json);
            if (benchmarkOutput is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(benchmarkOutput)!);
                File.WriteAllText(benchmarkOutput, json);
            }
        }
        return failures.Count == 0 ? 0 : 1;
    }
}
