# testbed

The gallery app: thirteen pages and 37 examples, each written in C# and in GDScript. A switch in the header picks the
implementation that runs, and the source viewer shows the code that is running. Its tests compare the two languages.

The gallery is a .NET app in both modes. Both languages share each example's scene, built by C# code or, for the
showreel, loaded from a `.tscn`, so geometry, materials and initial values are identical; only the animations,
sequencing, callbacks and button handling differ. The non-.NET GDScript test project is `../testbed-gdscript/`.

## Run

GDScript mode needs the GDExtension for your platform; build it first (see `../gdextension/README.md`). From the
repository root:

```powershell
dotnet run --project testbed/testbed.2dog
dotnet run --project testbed/testbed.2dog -- --gallery-language gdscript
dotnet publish testbed/testbed.web   # browser build in testbed/testbed.web/AppBundle; needs the wasm-tools workload
```

| Option | Effect |
| --- | --- |
| `--gallery-language csharp\|gdscript` | Start in that language; the default is C# |
| `--gallery-page <n>` | Open page `n`, zero-based in sidebar order |
| `--gallery-source <n>` | Open the source view of example `n` on that page |
| `--snapshot <file>` | Save a PNG after 20 frames, then quit |
| `--gallery-snapshots <dir>` | Save a PNG of every page, then quit |
| `--restart-check` | After the gallery quits, start a second engine in the same process |
| `--gallery-duration <s>` | Start with this leg duration in seconds |
| `--gallery-record <n>` | Record example `n` of the page into `temp/` with Movie Maker, then quit |

Other arguments go to Godot, such as `--headless`, `--quit-after 12`, `--rendering-method gl_compatibility` or
`--fixed-fps 60`. Snapshots and the Shaders page need a renderer; headless, the Shaders page shows a notice instead.

```powershell
dotnet run --project testbed/testbed.2dog -- --gallery-language gdscript --gallery-page 7 --gallery-source 0 --snapshot artifacts/gdscript-shader.png --rendering-method gl_compatibility
dotnet run --project testbed/testbed.2dog -- --gallery-snapshots artifacts/gallery-csharp --rendering-method gl_compatibility --fixed-fps 60
```

## Use

- The window opens at 1600×900. With every control in the sidebar, the examples get the window's full height.
- The sidebar holds the language switch (**C# · Beta**, **GDScript · Beta**) and the leg duration (0.4–4 s, default
  1.8 s). Changing either restarts the page from its initial state; the page and the open example stay selected.
- The sidebar selects a page, and **Restart page** rebuilds it. A page shows its examples as cards.
- **View C#** or **View GDScript** opens a card's source beside its preview without restarting it. The file menu also
  shows the scene and helper files, and the toolbar copies the file, jumps to the tween code and toggles wrapping.
- **Fullscreen** shows only that example, scaled up uniformly until its card's layout fits the screen and rendered at
  screen resolution, and makes the window fullscreen without restarting it. **Exit fullscreen** or Esc returns.
- **Record** (desktop host only) restarts the gallery in Godot's Movie Maker mode and records that example, filling a
  1920×1080 window, into `temp/` as an AVI at 60 fps. Every frame advances exactly 1/60 s however long it takes to
  render. The showreel records one loop and other examples 10 s; Esc stops early. The gallery then reopens.
- The Easing page composes an In and an Out curve with skew, blend method and join width; None leaves a single curve.
  Amber and blue are the two halves, mint is the result.
- The Colors page compares sRGB, linear RGB and OKLab, each with straight and premultiplied alpha. Press **50%**
  or scrub to pause; **Animate** resumes. The tint-and-fade column treats RGB and opacity independently.
- The Keyframes page plays one sparse definition on three flyers with different captured starting positions.
  Position, scale, rotation and color share its duration, ping-pong and repeats.
- The Layout transitions page reflows four Container-managed slots through four patterns. Each keeps its `Visual`
  child under an independent FLIP transition in C# or GDScript.
- The Showreel page is a 55-second loop at the default leg duration, timed to an 80 bpm track that is not part of
  the repository. A dot crosses a page of printed words, and each word stands up with the easing it names. Every
  animated node plays keyframe definitions spanning the loop; the dot and the emitter laying its trail share one. GPU
  particles emit while keyframes raise their amount ratio.

## Layout

- `Gallery.cs` is the shell that `main.tscn` runs: header, sidebar and the current page.
- `Gallery/` holds the shared host: `GalleryPage` (a page of cards), `GalleryEffect` (one example's scene,
  animation and resources), the source viewer, theme and palette.
- `Gallery/<Page>/` holds a page's `<Page>Page.cs` and, per example, `<Example>.cs` (the scene, plus `SceneTargets`,
  the nodes handed to GDScript) and `<Example>.Animation.cs` (the C# animation).
- `Gallery/Showreel/Showreel.tscn` is the showreel's authored scene: page, stations, words, dot, effects and
  environment. Its `Lettering` and `Definition` nodes lay out their letters and words in the editor as well, and
  `Showreel.cs` finds the nodes the animation moves.
- `Gallery/GDScript/<Example>.gd` is the GDScript animation. It extends `GalleryAnimation.gd`, which holds the shared
  lifecycle and bindings, and carries the small helpers it uses (such as `cycle` or `options`) so that the file shows
  everything that moves.
- The build embeds the gallery sources. GDScript mode compiles the embedded `.gd` text, so the viewer shows exactly
  what runs; no C# tween calls drive GDScript motion.
- C# uses `../csharp/tweens.gd.csproj`. GDScript uses the addon, which the build copies from `../addons/tweens_gd/`
  into the ignored `addons/`.
- `testbed.2dog/` is the desktop host, `testbed.web/` the browser host and `testbed.tests/` the tests.

## Add an example

1. In the page folder, add `<Example>.cs` and `<Example>.Animation.cs`, and list the effect in the page's
   `CreateEffects`.
2. Add `Gallery/GDScript/<Example>.gd` with the same motion, animating only the nodes in `SceneTargets`.
3. In `testbed.tests/Integration/`, add the example to the teardown test in `GalleryLifetimeTests.cs` and raise the
   example count in `GallerySourceTests.cs`. The language comparison finds it on its own.

## Test

```powershell
dotnet test testbed/testbed.tests/testbed.tests.csproj -c Release
dotnet test testbed/testbed.tests/testbed.tests.csproj -c Debug --filter "FullyQualifiedName~GalleryLanguageTests"
dotnet test testbed/testbed.tests/testbed.tests.csproj -c Release -p:RenderingTests=true
```

Run each command on its own: they build different tests into the same output.

- **Headless** tests run both languages side by side and sample them at the same times. They also cover teardown,
  language and page switching, the composer controls, button clicks and that the viewer shows the compiled source.
- **Debug** turns GDScript warnings into errors, so this run fails on a warning in any example.
- **Rendering** tests play every page in both languages, compare the shader examples and check the source layout at
  several window sizes.

Two differences between the languages are expected. C# eases with single precision and GDScript with double, so a
value just below an Expo or Elastic endpoint can round onto it in C#. On the easing race's 240-pixel track that moves
a sample by 0.1171875 pixels, and the comparison allows exactly that. Confetti trajectories are random, so the
comparison skips them.
