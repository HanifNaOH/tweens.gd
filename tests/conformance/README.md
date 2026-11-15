# Shared behavioral fixtures

`timelines.json` contains language-neutral samples extracted from the C# playback
tests and easing equations. Both the C# unit suite and the actual GDScript suite
consume this file. Deltas are incremental seconds; optional `local_time` samples
the clock at that absolute timeline time before the first update. State numbers
are delayed (0), playing (1), interval (2), completed (3).
The tolerance accommodates C#'s single-precision easing/progress values.

These fixtures establish timing/easing agreement, not full API or export parity.
Engine lifetime, callbacks and property writes have separate integration tests.

`groups.json` also permutes an already-cancelled, running, and failed member to
check that construction collects the failure once. `endpoints.json` includes
wide scalar and vector interpolation samples near opposite finite limits.

`easing.json` supplies composed In/Out samples at the blend boundaries, inside
the transition, with missing legs and with a linear In/Out split. The neutral split is 0.5; 0 and 1 select the full Out and In profiles. `blend` gives the centered
window, and the optional `blendType` the method (default: Makima). Non-polynomial
samples also exercise the analytic derivatives used to construct the cubic join. C#, native GDScript, and the
website's `npm run check:easing` consume the same samples. Each implementation
also checks every family pairing. Bare calibrated names alias their 30% variants. Elastic relaxes
solo damping after the main swing, so a solo leg settles near 90% of its duration; peak strengths are preserved.
`node scripts/calibrate-elastic-easing.mjs` reproduces its calibrated parameters. Back/Elastic fixtures cover aliases, 10%–50%
variants, and mixed joins; independent peak tests verify the named overshoot
against the full tween range for solo curves and matching pairs. Bounce fixtures
cover the same 10%–50% variants; separate tests find the first rebound depth,
check the two shrinking rebounds, and verify bounds and mirror symmetry. Jump checks find three diminishing
peaks above the target and verify the intervening landings, mirror symmetry,
full-range percentages, and signed flag combinations. Legacy curves remain covered by `timelines.json`.

`fx.json` supplies shared samples of the FX shake signal, including negative noise
coordinates, signed seed extremes and the periodic lattice boundary. Noise is
quintic-interpolated value noise with a fixed 32-bit lattice hash. Both languages
check these samples, plus continuity, bounds, endpoints and sampling-order
independence in their FX suites. It requires no native noise resource.
