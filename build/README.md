# CI targets

GitHub Actions owns build ordering and timeouts. These MSBuild entry points also
work locally with the repository's .NET SDK:

```powershell
dotnet msbuild build/Coverage.proj
dotnet msbuild build/Smoke.proj -p:Version=0.1.0-pre
dotnet msbuild build/SmokeTutorial.proj
```

Pack the addon ZIP and NuGet packages into `artifacts/packages/` before running
`Smoke.proj` (see [the authoritative release guide](../docs-internal/RELEASING.md)).
Smoke tests always extract fresh consumers from those artifacts. The addon ZIP
is also checked in a fresh Git repository with common directory and
binary ignore patterns; every bundled file must remain visible to Git.
The plain Godot SDK fixture checks source-install
compilation; its separate 2dog host imports through `TwoDogImportGodotProject` and
runs both languages. The NuGet fixture restores the exact local version into an
isolated cache. Fixture props/targets block accidental repository settings.

`SmokeTutorial.proj` extracts `tweens.gd-tutorial.zip`, builds its standalone
Godot project in Debug and Release for .NET 8, and runs the tutorial's headless
page and lesson tests in both languages against the extracted copy. Verification
tools are staged separately and do not ship in the archive.

Coverage defaults to Release with 99% line and 95% branch minimums for library
sources. `Configuration`, `MinimumLine`, `MinimumBranch`, `Rendering`, and `NoBuild`
can be supplied with `-p:`. `NoBuild=true` requires each selected suite to have
already been built. Each invocation writes a new run directory, so earlier
reports cannot improve the current result. To recheck a run, use
`-t:Report -p:RunDirectory=<absolute-run-directory>`.

The remaining release publishing scripts retain their failure-handling checks.
Addon generation/packing scripts retain the deterministic archive and generated
source checks. The optional standard-Godot export scripts and browser harness
exercise non-.NET Windows/Web exports locally; CI uses only NuGet-delivered 2dog.
