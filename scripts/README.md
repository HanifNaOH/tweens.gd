# Repository utilities

Run these from the repository root. PowerShell tools require PowerShell 7; JavaScript
tools require Node.js (CI uses Node 24). Generated files belong in the canonical
addon, never in staged copies.

| Utility | Purpose and inputs | Output / invocation |
| --- | --- | --- |
| `Generate-CSharpDefinitions.ps1` | Build the Roslyn generator, copy its structured definitions and extensions into the addon, and export the shared binding manifest. Requires the .NET SDK. | `./scripts/Generate-CSharpDefinitions.ps1`; writes `addons/tweens_gd/csharp/Generated/` and `tests/conformance/adapters.json`, and updates UIDs. `-Check` verifies without changing generated files. |
| `generate-gdscript-catalog.mjs` | Derive GDScript helpers from the Roslyn binding manifest. Run C# generation first after changing adapters. | `node scripts/generate-gdscript-catalog.mjs`; writes `catalog.gd` and `CATALOG.md`. `--check` verifies freshness. |
| `Generate-AddonUids.ps1` | Create stable `.uid` files for addon scripts/extensions and remove orphaned UIDs. | `./scripts/Generate-AddonUids.ps1`; `-Check` verifies without writing. |
| `Pack-Addon.ps1` | Check generated sources and UIDs, validate addon contents, and build a deterministic installable ZIP. | `./scripts/Pack-Addon.ps1 -Version 0.1.0-pre`; defaults to `artifacts/packages/`. `-OutputDirectory` changes the destination; `-AllowMissingNative` permits a partial local package. |
| `Pack-Tutorial.ps1` | Bundle the tutorial's scenes, scripts, assets, and the same-version addon ZIP as a standalone Godot .NET project. | After packing the addon, run `./scripts/Pack-Tutorial.ps1 -Version 0.1.0-pre`; writes `artifacts/packages/tweens.gd-tutorial.zip`. `-OutputDirectory` selects the addon archive and output directory; `-AllowMissingNative` permits a partial local package. |
| `test-tutorial-web.mjs` | Load all five tutorial lessons in both languages in a browser and exercise C# and GDScript animations; fail on browser or Godot errors. | Install `playwright-core` under `artifacts/browser-test` as shown in the script, publish the tutorial web host, then run `node scripts/test-tutorial-web.mjs tutorial/tutorial.web/AppBundle`. Use `GDSCRIPT_BROWSER=chrome` on CI; the default is Edge. |
| `Test-TutorialPackage.ps1` | Build staged verification tools, import an extracted tutorial with 2dog, and run its tests against the original .NET 8 assembly. Retries only Godot's known native import crash. | Called by `dotnet msbuild build/SmokeTutorial.proj`; takes `-Project` and `-TwoDogVersion`. |
| `calibrate-elastic-easing.mjs` | Reproduce and verify the Elastic overshoot parameters used by C#, C++, and the website. | `node scripts/calibrate-elastic-easing.mjs`; prints parameters and sampled peaks without writing files. |
| `Get-GodotExportTemplates.ps1` | Download official Windows and Web export templates, verify the archive checksum, and reuse verified cached files. | `./scripts/Get-GodotExportTemplates.ps1`; writes `artifacts/godot-templates/`. `-Version` selects Godot (default `4.7.2`); `-KeepArchive` retains the download. |
| `Invoke-GDScriptExportTests.ps1` | Stage the GDScript test project, build Windows and both Web exports, then run the Windows export's suite. Requires native extension builds and downloaded templates. | `./scripts/Invoke-GDScriptExportTests.ps1 -Godot <console-engine-path>`; writes exports and logs under `artifacts/`. See [export tests](../testbed-gdscript/README.md). |
| `Mix-Showreel.ps1` | Put the showreel's music under its newest Movie Maker recording, fading it out at the end. Requires `ffmpeg` on PATH and the track in the ignored `temp/`. | `./scripts/Mix-Showreel.ps1`; writes `temp/showreel-along-the-way.mp4`. `-Record` records a fresh loop first and checks it is complete; `-Video`, `-Music` and `-Output` override the paths. |
| `test-gdscript-web.mjs` | Serve an exported Web suite with isolation headers, run it in a headless browser, and check its reported failures. | Install `playwright-core` as described in [export tests](../testbed-gdscript/README.md), then `node scripts/test-gdscript-web.mjs [gdscript-web-threads]`. Defaults to `gdscript-web` and Edge; `GDSCRIPT_BROWSER` selects another Playwright browser channel. Writes result JSON and browser logs under `artifacts/`. |

Every utility above has a build, check, or documented manual maintenance use. Manual
calibration and export tools are intentionally retained even though CI does not run them.
Website utilities are documented in [docs/scripts/README.md](../docs/scripts/README.md).
Release procedures live in [the authoritative release guide](../docs-internal/RELEASING.md).

## Formatting

Prettier is pinned in `docs/package-lock.json`. After `npm ci --prefix docs`, run:

```powershell
npm run format:scripts --prefix docs
npm run check:script-format --prefix docs
uvx ruff==0.16.10 format docs/scripts
uvx ruff==0.16.10 format --check docs/scripts
```

JavaScript uses two spaces, single quotes, a 100-column target, and LF endings.
Python uses Ruff's standard format. For PowerShell, install PSScriptAnalyzer 1.25.0
once (`Install-Module PSScriptAnalyzer -RequiredVersion 1.25.0 -Scope CurrentUser`),
then run the same formatter and committed settings used for this cleanup:

```powershell
Import-Module PSScriptAnalyzer -RequiredVersion 1.25.0
$settings = './scripts/PowerShellFormatting.psd1'
Get-ChildItem scripts, docs/scripts -Filter '*.ps1' -File | ForEach-Object {
    $source = [IO.File]::ReadAllText($_.FullName)
    $formatted = Invoke-Formatter -ScriptDefinition $source -Settings $settings
    $formatted = $formatted.Replace("`r`n", "`n") -replace '(?m)[ \t]+$', ''
    [IO.File]::WriteAllText($_.FullName, $formatted)
}
```

PowerShell uses four spaces and expands single-line control blocks. Literal embedded
code and SVG text keep their meaningful whitespace; the JavaScript check covers both
script directories without reformatting the website's application source.
