# Releasing tweens.gd

This is the authoritative release guide. Keep release procedures in
`docs-internal/RELEASING.md` and link here from other documentation; do not maintain
another copy. All commands below run from the repository root unless stated otherwise.

The primary distribution is a unified Godot Asset Store addon containing C# and
GDScript sources plus prebuilt GDExtension libraries. NuGet provides the compiled
C# API as an alternative. The GitHub release publishes these artifacts:

| Artifact | Release asset |
| --- | --- |
| Unified addon | `tweens.gd-<version>.zip` |
| Standalone tutorial project | `tweens.gd-tutorial.zip` |
| C# package | `tweens.gd.<version>.nupkg` |
| C# symbols | `tweens.gd.<version>.snupkg` |

## Prepare the release

1. Choose a tag such as `v0.1.3-beta`. Tags must contain three numeric version
   components and may have a SemVer prerelease suffix; build metadata is not
   accepted. The tag supplies the version for all release artifacts. Leave the
   project file's local development version unchanged. The release title matches
   the tag exactly. We do not publish GitHub prereleases: publish every GitHub
   release as a full release and mark the newest release as **Latest**, even when
   its tag has a `-beta` or other SemVer prerelease suffix. The suffix still applies
   to the NuGet package version.
2. Review changes since the previous release and write `.github/releases/<tag>.md`.
   These committed files are the versioned changelog and the source of GitHub
   release notes. Lead with user-visible changes, then documentation improvements,
   download names and a comparison link.
3. Regenerate affected addon files, run the checks below and confirm NuGet Trusted
   Publishing is configured before tagging.
4. Commit implementation changes, generated files and release notes. Ensure CI
   passes for the intended release commit.

Validate the chosen tag and the release script's failure handling locally:

```powershell
$tag = 'v0.1.3-beta'
./.github/scripts/Get-ReleaseVersion.ps1 -Tag $tag
./.github/scripts/Test-ReleasePublishing.ps1
```

## Generate and verify

`addons/tweens_gd/` is the complete installable addon and the canonical source for
both languages. GDScript entry points live at its root; C# sources live in
`csharp/`, with prepared definitions in `csharp/Generated/`. The NuGet project
compiles the same runtime sources and regenerates definitions during compilation.

After changing adapters, configuration properties or the Roslyn generator, run:

```powershell
./scripts/Generate-CSharpDefinitions.ps1
node scripts/generate-gdscript-catalog.mjs
```

After adding or removing addon `.gd`, `.cs` or `.gdextension` files, also run
`./scripts/Generate-AddonUids.ps1`. Commit generated sources and UIDs with the
implementation. Packing checks both catalogs and addon UIDs and fails on stale output.

Only the library's generated definitions ship as source. Each consuming project's
Godot.NET.Sdk generates Godot's own glue for its assembly and script paths; the
addon does not bundle that glue or an analyzer. Each C# source declares its own
imports and nullable context.

For local verification, use .NET 10, Node.js, PowerShell 7, uv and a C++ compiler.
Godot is supplied through 2dog; no local Godot editor installation is needed.
Build the native engine against the pinned `gdextension/godot-cpp` submodule:

```powershell
git submodule update --init --recursive
uv run --with scons scons -C gdextension target=template_debug
uv run --with scons scons -C gdextension target=template_release
```

Builds write to the ignored `addons/tweens_gd/bin/`, with filenames defined by
`addons/tweens_gd/tweens_gd.gdextension`. A local checkout needs libraries for its
own platform; CI builds every supported platform. See
[the native engine README](../gdextension/README.md) for build details.

Run the relevant library, gallery, GDScript and documentation checks.
[The CI workflow](../.github/workflows/ci.yml) defines the full check sequence,
including rendering tests and platform coverage. Useful local checks are:

```powershell
dotnet build tweens.gd.slnx -c Release
dotnet msbuild build/Coverage.proj
dotnet test tests/tweens.gd.tests/tweens.gd.tests.csproj -c Release -p:LibraryFramework=net8.0
dotnet test testbed/testbed.tests/testbed.tests.csproj -c Release
dotnet run --project testbed-gdscript/host -c Release
dotnet run --project testbed-gdscript/host -c Debug
dotnet run --project testbed-gdscript/host -c Release -- --lifecycle
```

`build/Coverage.proj` runs Headless and Lifecycle in separate processes, merges
coverage and enforces 99% line / 95% branch coverage on hand-written library
sources. Generated definitions are reported separately. Add `-p:Rendering=true`
to include the GPU suite, or `-p:NoBuild=true` after building each selected suite.
Reports and TRX files are isolated per run under `artifacts/coverage/`.

For documentation checks, follow [docs/README.md](../docs/README.md).

## Check the packages locally

Use the same version for the addon, tutorial and NuGet packages:

```powershell
$version = '0.1.3-beta'
./scripts/Pack-Addon.ps1 -Version $version
./scripts/Pack-Tutorial.ps1 -Version $version
dotnet pack csharp/tweens.gd.csproj -c Release -p:Version=$version -o artifacts/packages
dotnet msbuild build/Smoke.proj -p:Version=$version
dotnet msbuild build/SmokeTutorial.proj
```

The addon ZIP is rooted at `addons/tweens_gd/` and includes both languages,
generated definitions, native libraries, documentation and licenses. It contains
an addon-local `.gitignore` that keeps bundled files visible despite common
project ignore rules such as `[Bb]in/` and `*.dll`. This file is added only when
packing; native build outputs in the repository stay ignored. The ZIP contains
no build projects or tools that users must install. Packing requires every native
library named in the manifest. For local testing when libraries for other
platforms are unavailable, pass `-AllowMissingNative` to both `Pack-Addon.ps1`
and `Pack-Tutorial.ps1`. Release archives must be complete and come from CI.

`build/Smoke.proj` extracts the actual ZIP into fresh consumers. It checks Debug
and Release source compilation in a `net8.0` Godot.NET.Sdk project with warnings
as errors, implicit imports disabled and nullable disabled. A separate 2dog host
imports the addon and runs independent C# and GDScript automatic-playback tests.
Engine errors, failed assertions, missing completion and timeouts fail the build.
The target also verifies NuGet metadata and symbols for `net8.0` and `net10.0`,
and compiles the documented mixed install with bundled C# sources excluded and
an isolated local package cache. Fixtures live in `tests/smoke/`; staged consumers
live in `artifacts/smoke/`.

The separate `tweens.gd-tutorial.zip` is an importable Godot .NET project rooted
at `project.godot`. It bundles the contents of the same-version addon ZIP,
tutorial scenes and scripts, themes, artwork, fonts, licenses and export presets
for Web (extensions enabled, threads disabled), Windows, Linux, macOS, Android
and iOS. Its standalone
`tutorial.csproj` targets .NET 8 and embeds lesson sources for the code panels; it has no repository
references, 2dog packages, hosts, tests, build outputs or imported caches. Users
need Godot .NET 4.7.2 and the .NET 8 SDK or later, then import, build and run.
`build/SmokeTutorial.proj` extracts the archive into a fresh project, checks
Debug and Release compilation, and runs the headless tutorial page and lesson
tests against the extracted project in both languages.

## Publish to GitHub and NuGet

Create and push the release tag on the checked commit. The
[Release workflow](../.github/workflows/release.yml) then:

1. Validates the tag and runs the full CI pipeline, including native builds,
   package verification and smoke tests of fresh consumers.
2. Creates the GitHub release and uploads the addon ZIP, tutorial ZIP, `.nupkg`
   and `.snupkg`.
3. Publishes the C# package and symbols to NuGet.org using Trusted Publishing.

Publication proceeds automatically once checks succeed. The workflow requires
nonempty hand-written notes in `.github/releases/<tag>.md`; it does not fall back
to generated notes. Prepare notes before tagging. A separate GitHub draft is not
part of this workflow because the release-by-tag lookup does not find unpublished
drafts. Reruns replace existing release assets without overwriting edited notes,
and skip NuGet package versions already published.

After publication, verify that the GitHub release is not marked **Pre-release**
and is marked **Latest**. The current workflow derives the prerelease flag from
the tag suffix, so clear it and mark the release latest when necessary:

```powershell
gh release edit $tag --repo outfox/tweens.gd --prerelease=false --latest
```

This keeps stable latest-release download links working. For the tutorial, use
`https://github.com/outfox/tweens.gd/releases/latest/download/tweens.gd-tutorial.zip`.
Keep the asset filename unchanged across releases; the URL order is
`releases/latest/download/<asset-name>`.

Only the NuGet publishing job can request a GitHub OIDC token. `NuGet/login`
exchanges it for a short-lived API key immediately before the push. No long-lived
NuGet API key or opt-in publishing variable is required.

## Update the Godot Asset Store

After the GitHub release succeeds, update the existing **tweens.gd** listing at
[store.godotengine.org](https://store.godotengine.org/):

1. In **Versions**, upload the release's unified `tweens.gd-<version>.zip`, add the
   version's changelog and set the supported Godot version to **4.7.2**.
2. State that C# requires Godot .NET 4.7.2 and .NET 8 or later, while GDScript
   requires no .NET runtime. Users choosing NuGet alongside the GDScript addon
   must exclude `addons/tweens_gd/csharp/**/*.cs` from compilation.
3. Keep the asset type **Addon**, the MIT license and both language tags. Review
   required metadata and submit the version for review.

Store upload and review are manual steps. The Store hosts uploaded version files;
use the existing listing rather than creating an Asset Library submission. See
[the official Store submission guide](https://docs.godotengine.org/en/latest/community/asset_store/submitting_to_asset_store.html).

## Configure NuGet Trusted Publishing

1. Set the GitHub repository secret `NUGET_USER` to the NuGet.org **username** of
   the user who created the policy, rather than an email address or API key.
2. Create a NuGet.org Trusted Publishing policy with these values:

   | Field | Value |
   | --- | --- |
   | Repository owner | `outfox` |
   | Repository | `tweens.gd` |
   | Workflow file | `release.yml` |
   | Environment | Leave empty; the workflow uses no environment |
   | Package glob | `tweens.gd` |

3. Select the policy owner that owns the package. Allow publishing new package
   versions, and allow publishing new packages if this is the first NuGet release.
   A policy for another repository or workflow does not match this workflow.

See [NuGet's Trusted Publishing documentation](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
for policy ownership and activation details.

## Retry NuGet publication

If NuGet publication fails after the GitHub release was created, fix the
publishing configuration and run the **Release** workflow manually on `main` with
its `tag` input, for example `v0.1.3-beta`.

This path validates the tag's version and downloads its exact `.nupkg` and
`.snupkg` release assets. Both files must be present and nonempty before NuGet
login. It publishes those existing packages without rebuilding source or changing
the release tag or notes. Use a tag-driven workflow rerun for build or GitHub
release failures; the manual path only retries NuGet publication.
