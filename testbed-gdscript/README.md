# GDScript conformance project

The project runs the GDScript API and its GDExtension, with no reference to the C#
tween library. The addon itself has no .NET dependency; 2dog is only the development
test host. Build the extension for your platform first
([gdextension/README.md](../gdextension/README.md)).

From the repository root:

```powershell
scons -C gdextension target=template_debug
scons -C gdextension target=template_release
dotnet run --project testbed-gdscript/host -c Release
dotnet run --project testbed-gdscript/host -c Debug
dotnet run --project testbed-gdscript/host -c Debug -- --rendering
dotnet run --project testbed-gdscript/host -c Release -- --lifecycle
dotnet test tests/tweens.gd.tests/tweens.gd.tests.csproj -c Release
```

The host build copies the current addon, including its `bin/` libraries, and shared
fixtures into ignored project directories before 2dog's required MSBuild import. The launcher checks every addon script compiles, runs the tests, and
exits nonzero for assertions, captured Godot script errors, or a 240-frame timeout.
Shared timing, easing and group completion/overshoot fixtures also run through the
C# implementation in the library test suite. Group tests additionally cover shared
controls, overlapping groups, already-settled/rejected members, cancellation during
callbacks, owner/target lifetime, mixed clocks, error aggregation and reference cleanup.
The generated catalog exercises every concrete C# property/value adapter against
native Godot properties. Custom adapters, multi-starts, cancellable waits and shader
metadata/bindings have dedicated suites. `--rendering` opens a minimized OpenGL
window and also checks shader defaults, instance uniforms and a rendered pixel.
`--lifecycle` runs the full suite through three engine starts/stops in one process.

The addon copies Curve points, tangents, bounds and bake resolution explicitly, because
2dog 4.7.2.91 crashed on `Curve.duplicate()` after an engine restart. The regular and
restart suites cover that workaround.
`dotnet run --project testbed-gdscript/host -c Release -- --restart-probe` runs the small
no-addon reproduction in `restart_probe.gd`; it is a manual diagnostic, not a CI test, and
passes on 2dog 4.7.2.109. The project uses the newest 2dog through `testbed/Directory.Build.props`.

To use an installed standard Godot executable instead:

```powershell
dotnet msbuild testbed-gdscript/host/gdscript.2dog.csproj -t:StageGDScript
godot --headless --path testbed-gdscript -- --run-tests
```

Standard non-.NET Godot 4.7.2 is also validated on Windows and Linux; CI also runs the suite
with the official Linux and macOS builds. The project can be opened in the Godot editor; use
`--run-tests` in the run arguments. The 2dog build imports through its packaged
editor library; it does not need an installed Godot executable.

## Export and packaging validation

The committed Windows and Web test presets use matching official templates under
`artifacts/godot-templates/`. The Web presets use the `dlink` templates, which can
load GDExtensions: one single-threaded, one with thread support. The Web exports need
the release Web libraries, built with Emscripten 4.0.11 (emsdk) on `PATH`. Export with
the installed standard Godot editor:

```powershell
scons -C gdextension platform=web target=template_release threads=no
scons -C gdextension platform=web target=template_release threads=yes
./scripts/Get-GodotExportTemplates.ps1
./scripts/Invoke-GDScriptExportTests.ps1 -Godot C:/Tools/godot/Godot_v4.7.2-stable_win64_console.exe
npm.cmd install --prefix artifacts/browser-test --no-audit --no-fund playwright-core@1.56.1
node scripts/test-gdscript-web.mjs
node scripts/test-gdscript-web.mjs gdscript-web-threads
./scripts/Pack-Addon.ps1 -AllowMissingNative # Partial: only the libraries built here.
```

Template downloads are verified against the official SHA512 list. The export
script builds the three release exports and runs the Windows executable headlessly.
The Web harness starts a temporary localhost server and isolated headless Edge,
runs the exported suite, records its result, and closes both. Set `GDSCRIPT_BROWSER`
to another installed Playwright Chromium channel to test it. Worker-thread
rejection tests are skipped on the Web; core, group, catalog, custom-adapter and
rendering tests still run.

Local validation used Godot 4.7.2 and Edge 154 on Windows. Wider browser/device
coverage and performance budgets remain separate follow-ups. No benchmark rerun is
part of the parity work. The addon ZIP contains only `addons/tweens_gd/`, including
source, the helper catalog, the GDExtension libraries and licenses. Locally,
`-AllowMissingNative` packs a partial archive with only the libraries built here;
release archives come from CI, which has every library. CI checks generated files and uploads the
ZIP beside the C# packages for the existing release workflow.

## Baseline benchmark

```powershell
dotnet run --project testbed-gdscript/host -c Release -- --benchmark --benchmark-output artifacts/gdscript-benchmark.json
```

Measures value callbacks, Node2D position/color and material roughness at 100,
1,000 and 10,000 concurrent tweens. Both implementations receive identical manual
linear deltas: 30 warmup updates, then 120 samples. Godot uses one parallel native
Tween, stepped with `custom_step()`; the addon uses one manual scheduler. Target
creation is excluded from creation time. The JSON records engine/CPU/build details,
creation/disposal time, median and p95 update times in microseconds.

This isolates simple steady-state updates. It does not compare equivalent lifetime
guarantees, C# performance, allocation counts, repeated churn, full-frame rendering,
shader workloads or WASM. Do not infer a supported tween count from this baseline.
