# tweens_gd GDExtension

The engine behind the GDScript API: definitions, handles, groups, schedulers, the
automatic runner, easing, interpolation and FX, in C++ against
[godot-cpp](https://github.com/godotengine/godot-cpp) 10 (`api_version` 4.4).
`addons/tweens_gd/tweens.gd` and the generated catalog stay GDScript; so do the
adapter scripts, which are the extension point for custom storage.

## Build

Needs Python with [SCons](https://scons.org/) and a C++ compiler for the target
(MSVC or MinGW on Windows, GCC or Clang on Linux, Xcode on macOS/iOS, the Android
NDK, Emscripten 4.0.11 for the Web). From the repository root:

```sh
git submodule update --init --recursive
scons -C gdextension target=template_debug
scons -C gdextension target=template_release
```

The editor and debug exports load `template_debug`, release exports
`template_release`. Pass `platform=`, `arch=` and, for the Web, `threads=no` to
cross-build. `addons/tweens_gd/tweens_gd.gdextension` maps feature tags to library
filenames, and builds write those files to `addons/tweens_gd/bin/`.

CI (`.github/workflows/gdextension.yml`) builds Windows x86_64/x86_32/arm64,
Linux x86_64/x86_32/arm64, macOS universal, iOS arm64, Android
arm64/arm32/x86_64/x86_32 and Web wasm32 with and without threads, then runs the
GDScript suite on Linux and macOS. Branch pushes reuse a cached build when the
sources in `gdextension/`, the godot-cpp commit and the workflow are unchanged,
and then test on Linux only; releases and manual runs always build.

`build_profile.json` limits the generated bindings to the engine classes the
sources use. Add a class there when the code starts using a new one.

## Editor documentation

`doc_classes/` contains Godot's XML class reference, with BBCode descriptions and
examples. Editor/debug builds embed it through `GodotCPPDocData`; release exports
omit the help data. Script factories and adapters use GDScript `##` comments.

To refresh native signatures after changing bindings, stage the addon and run
Godot's documentation tool against the test project, then review the XML changes:

```sh
godot --headless --path testbed-gdscript --doctool /absolute/path/to/gdextension --gdextension-docs
```

Keep the shared terms **definition**, **playback handle**, **group**, and
**Chain** aligned with the C# XML documentation. A Chain links a flat list of
definitions on one target; a group is a parallel step. Ordinary awaits coordinate
completion and game logic without inheriting frame time. Explain language
differences at the relevant member.
