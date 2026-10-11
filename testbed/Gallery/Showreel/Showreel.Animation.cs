// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using tweens.gd;
using Key = tweens.gd.Tweens.Keyframe;
namespace testbed;

// Scene setup is in Showreel.cs. "Along the Way": a dot travels a page from FROM to the period of TWEENS.GD, and each
// word it passes stands up and acts out its name. Every node plays keyframe definitions on one 55-second loop, timed
// to an 80 bpm track.
public sealed partial class Showreel
{
    private async Task AnimateAsync()
    {
        // The trail keeps each dot it lays for exactly one loop.
        trail.Lifetime = Length * Tempo;
        var route = Route();
        var plays = new List<Group>
        {
            rig.Tween(Rig()), camera.Tween(Lens()), fade.Tween(Fade()),
            // One definition moves both the dot and the emitter that lays its trail.
            dot.Tween(route), trail.Tween(route), trail.Tween(Lay()),
            disc.Tween(Disc()), dust.Tween(Dust()), confetti.Tween(Burst()),
            subtitle.Tween(Timeline(Standing(49.48, Out.Back))),
        };
        plays.AddRange(credit.Select((letter, i) => letter.Tween(Float(letter, i, i, Bars(0)))));
        plays.AddRange(engine.Select((letter, i) => letter.Tween(Float(letter, i, credit.Length + i, Bars(0) + Eighth))));
        for (var g = 0; g < glosses.Length; g++)
        {
            var (words, extra) = glosses[g];
            var at = Glossary[g];
            plays.AddRange(words.Select((word, i) => word.Tween(Printed(word, i, at))));
            plays.AddRange(extra.Select((word, j) => word.Tween(Extra(word, j, at))));
        }
        // Each word stands up with the easing it names.
        plays.AddRange(from.Select((letter, i) => letter.Tween(Timeline(Standing(Bars(2) + 0.08 * i, Out.Back)))));
        plays.AddRange(to.Select((letter, i) => letter.Tween(Timeline(Standing(11 + 0.08 * i, Out.Back)))));
        plays.AddRange(ticks.Select((tick, i) => tick.Tween(Timeline(Standing(Tick(i), Out.Back, 0.35)))));
        plays.AddRange(ease.Select((letter, i) => letter.Tween(Timeline(Standing(14.23 + Beat * i, InOut.Cubic, 0.9)))));
        plays.AddRange(bounce.Select((letter, i) => letter.Tween(Bounce(i))));
        plays.AddRange(elastic.Select((letter, i) => letter.Tween(Elastic(i))));
        plays.AddRange(diamonds.Select((diamond, i) => diamond.Tween(Diamond(i))));
        plays.AddRange(keyframes.Select((letter, i) => letter.Tween(Timeline(Standing(Waypoint(i) + 0.1, Out.Back, 0.4)))));
        plays.AddRange(logo.Select((letter, i) => letter.Tween(Drop(i))));
        plays.AddRange(taglines.Select((line, i) => line.Tween(Timeline(Standing(52.355 + Eighth * i, Out.Back)))));
        plays.AddRange(ripples.Select((ripple, i) => ripple.Tween(Ripple(i))));
        await Task.WhenAll(plays.Select(play => play.End));
    }

    private const double Length = 55;

    /// <summary>Converts seconds on the loop to a percentage stop.</summary>
    private static Percent T(double seconds) => Percent.Of(seconds / Length * 100);

    private Tweens.Keyframes Timeline(List<Key> keys) => new([.. keys],
        options: new TweenOptions { Duration = Length * Tempo, Repeats = TweenOptions.Infinite });

    // The track's grid: 80 bpm in 4/4, its first downbeat at 1.105 s.
    private const double Beat = 0.75, Eighth = Beat / 2;
    /// <summary>The downbeat <paramref name="bars"/> bars after the first one.</summary>
    private static double Bars(int bars) => 1.105 + 4 * Beat * bars;

    // When the dot reaches each ruler tick, BOUNCE letter, ELASTIC letter and keyframe diamond: all on the beat.
    private static double Tick(int i) => Bars(4) + Eighth + Beat * i;
    private static double Landing(int i) => Bars(8) + Beat * i;
    private static double Stretch(int i) => Bars(10) + 3 * Beat + Eighth * i;
    private static double Waypoint(int i) => Bars(12) + Beat * i;

    /// <summary>When a definition is printed, shrinks to its headword, gains new words, and blows away.</summary>
    private readonly record struct Moments(double Print, double Shrink, double Extend, double Blow);
    private static readonly Moments[] Glossary = [
        new(7.48, 10.855, 11.605, Bars(4)), new(14.6, 19.855, 20.605, Bars(7)), new(31.48, 34.855, 35.605, 36.73),
    ];

    /// <summary>Keys that stand a printed node up from the page, hinged at its foot.</summary>
    private static List<Key> Standing(double at, Interpolation ease, double seconds = 0.5) => [
        Key.At(0, rotationDegrees: (-90, 0, 0)), Key.At(T(at), rotationDegrees: (-90, 0, 0)),
        Key.At(T(at + seconds), rotationDegrees: (0, 0, 0), interpolation: ease),
    ];

    /// <summary>The rig looks up at the dot in the sky, follows it down to the page and along the path, leaning into its
    /// moves, then cranes back to reveal the journey and looks back to frame the wordmark.</summary>
    private Tweens.Keyframes Rig() => Timeline([
        Key.At(0, position: (-2.06, 1.6, 0), rotationDegrees: (24, -4, 0)),
        Key.At(T(2.6), position: (-2.06, 1.62, 0), rotationDegrees: (22, 2, 0)),
        Key.At(T(4), position: (-2.06, 1.55, 0), rotationDegrees: (21, 0, 0)),
        Key.At(T(5.2), position: (-1.9, 0.4, 0.1), rotationDegrees: (-22, -10, 0), interpolation: InOut.Cubic),
        Key.At(T(7.1), position: (-1.5, 0.35, 0.2), rotationDegrees: (-24, -9, 0)),
        Key.At(T(8.6), position: (-1.2, 0.35, 0.25), rotationDegrees: (-25, -7, 0)),
        Key.At(T(9.9), position: (-0.8, 0.35, 0.25), rotationDegrees: (-25, -3, -3)),
        Key.At(T(11.3), position: (0, 0.35, 0.3), rotationDegrees: (-25, 0, 2.5)),
        Key.At(T(12.4), position: (0.3, 0.35, 0.3), rotationDegrees: (-26, 0, 0)),
        Key.At(T(13.1), position: (0.7, 0.35, 0.28), rotationDegrees: (-26, 1, 0)),
        Key.At(T(14.6), position: (3, 0.35, 0.25), rotationDegrees: (-27, 3, 0)),
        Key.At(T(16.5), position: (4.6, 0.35, 0.3), rotationDegrees: (-28, 2, -2)),
        Key.At(T(19.5), position: (5.4, 0.35, 0.35), rotationDegrees: (-28, 3, 1.5)),
        Key.At(T(20.8), position: (4.3, 0.3, 0.6), rotationDegrees: (-34, 0, 0)),
        Key.At(T(22.1), position: (4.4, 0.3, 0.6), rotationDegrees: (-34, 0, 0)),
        Key.At(T(24.7), position: (8.8, 0.5, 0.05), rotationDegrees: (-20, -6, -2)),
        Key.At(T(25.4), position: (9.4, 0.55, 0), rotationDegrees: (-20, -6, 0)),
        Key.At(T(28.9), position: (11.2, 0.6, -0.05), rotationDegrees: (-20, -4, 1.5)),
        Key.At(T(30), position: (12.7, 0.4, 0.2), rotationDegrees: (-24, 2, 0)),
        Key.At(T(33.3), position: (15.6, 0.4, 0.45), rotationDegrees: (-26, 2, 0)),
        Key.At(T(35), position: (15.7, 0.4, 0.45), rotationDegrees: (-26, 1, 0)),
        Key.At(T(36.2), position: (16, 0.4, 0.4), rotationDegrees: (-26, 0, 0)),
        Key.At(T(37.1), position: (18.2, 0.35, 0.1), rotationDegrees: (-28, -6, 0)),
        Key.At(T(43.1), position: (23, 0.35, 0), rotationDegrees: (-30, 10, 0)),
        Key.At(T(46.1), position: (27.6, 0.5, -0.3), rotationDegrees: (-18, 0, 0)),
        Key.At(T(49.855), position: (27.6, 0.5, -0.3), rotationDegrees: (-18, 0, 0)),
        Key.At(T(51.355), position: (23, 0.3, -0.4), rotationDegrees: (-34, 36, 0), interpolation: InOut.Sine),
        Key.At(T(51.6), position: (23, 0.3, -0.4), rotationDegrees: (-34, 36, 0)),
        Key.At(T(53.105), position: (27.6, 0.25, 0.25), rotationDegrees: (-16, 0, 0), interpolation: InOut.Cubic),
    ]);

    /// <summary>The camera dollies per beat, punches in on the bounce section, twangs on a rubber band at ELASTIC,
    /// pulls far back for the reveal, and comes in again on the wordmark.</summary>
    private Tweens.Keyframes Lens() => Timeline([
        Key.At(0, z: 3.6, rotationDegrees: (0, 0, 0)), Key.At(T(4), z: 3.6), Key.At(T(5.2), z: 4.2, interpolation: InOut.Cubic),
        Key.At(T(7.1), z: 4.6), Key.At(T(13.5), z: 4.8), Key.At(T(19.5), z: 5.2), Key.At(T(20.8), z: 4.2), Key.At(T(22.1), z: 4.2),
        Key.At(T(24.7), z: 4.8), Key.At(T(Bars(8)), z: 4.8), Key.At(T(25.5), z: 4.3, interpolation: Out.Back),
        Key.At(T(28.9), z: 4.5), Key.At(T(30), z: 4.8),
        Key.At(T(Stretch(0)), z: 5.3, rotationDegrees: (0, 0, 0)), Key.At(T(33.45), z: 5.8, rotationDegrees: (0, 0, 3.5)),
        Key.At(T(35), z: 5.3, rotationDegrees: (0, 0, 0), interpolation: Out.Elastic),
        Key.At(T(37.1), z: 5), Key.At(T(43.1), z: 5.2), Key.At(T(46.1), z: 6), Key.At(T(49.855), z: 6),
        Key.At(T(51.355), z: 11, interpolation: InOut.Sine), Key.At(T(51.6), z: 11), Key.At(T(53.105), z: 5.6, interpolation: InOut.Cubic),
    ]);

    /// <summary>Paper fades in, and fades back over the jump to the loop's first keys.</summary>
    private Tweens.Keyframes Fade() => Timeline([
        Key.At(0, alpha: 1), Key.At(T(0.6), alpha: 0), Key.At(T(54.4), alpha: 0), Key.At(T(54.75), alpha: 1),
    ]);

    /// <summary>A credit letter floats up into view from below and keeps drifting up, then floats away like a released
    /// balloon, turning a little.</summary>
    private Tweens.Keyframes Float(Label3D letter, int i, int n, double at)
    {
        var (x, y) = (letter.Position.X, letter.Position.Y);
        var rise = at + 0.03 * i;
        var release = 5.5 + 0.04 * n;
        var turn = n % 2 == 0 ? 1 : -1;
        return Timeline([
            Key.At(0, x: x, y: y - 2.5, rotationDegrees: (0, 0, 0), transparency: 1),
            Key.At(T(rise), y: y - 2.5, transparency: 1), Key.At(T(rise + 0.4), transparency: 0),
            Key.At(T(rise + 2), y: y, interpolation: Out.Cubic),
            Key.At(T(release), x: x, y: y + 0.25, rotationDegrees: (0, 0, 0), transparency: 0),
            Key.At(T(release + 1.4), x: x + (n % 5 - 2) * 0.3, y: y + 1.25 + n % 3 * 0.35,
                rotationDegrees: (0, 0, (25 + n * 13 % 40) * turn), transparency: 1, interpolation: Out.Quad),
        ]);
    }

    /// <summary>A definition word is printed in turn. The headword and part of speech stay and blow away later; the
    /// rest fade when the definition shrinks.</summary>
    private Tweens.Keyframes Printed(Label3D word, int i, Moments at)
    {
        var (x, z) = (word.Position.X, word.Position.Z);
        var shown = at.Print + 0.07 * i;
        List<Key> keys = [
            Key.At(0, x: x, y: Lift, z: z + 0.12, rotationDegrees: (-90, 0, 0), scale: 1, transparency: 1),
            Key.At(T(shown), z: z + 0.12, transparency: 1),
            Key.At(T(shown + 0.35), z: z, transparency: 0, interpolation: Out.Cubic),
        ];
        if (i < 2) Blow(keys, x, z, i, at.Blow);
        else
        {
            var gone = at.Shrink + 0.02 * (i - 2);
            keys.AddRange([Key.At(T(gone), scale: 1, transparency: 0), Key.At(T(gone + 0.35), scale: 0.8, transparency: 1, interpolation: In.Quad)]);
        }
        return Timeline(keys);
    }

    /// <summary>A new word pops in after the headword, one per 8th, then blows away with it.</summary>
    private Tweens.Keyframes Extra(Label3D word, int j, Moments at)
    {
        var (x, z) = (word.Position.X, word.Position.Z);
        var shown = at.Extend + Eighth * j;
        List<Key> keys = [
            Key.At(0, x: x, y: Lift, z: z, rotationDegrees: (-90, 0, 0), scale: 0.01, transparency: 1),
            Key.At(T(shown), scale: 0.01, transparency: 1),
            Key.At(T(shown + 0.15), transparency: 0), Key.At(T(shown + 0.3), scale: 1, interpolation: Out.Back),
        ];
        Blow(keys, x, z, 2 + j, at.Blow);
        return Timeline(keys);
    }

    /// <summary>Lifts a printed word off the page, and the wind carries it away like an autumn leaf.</summary>
    private static void Blow(List<Key> keys, float x, float z, int n, double at)
    {
        var gust = at + 0.05 * n;
        var turn = n % 2 == 0 ? 1 : -1;
        keys.AddRange([
            Key.At(T(gust), x: x, y: Lift, z: z, rotationDegrees: (-90, 0, 0), transparency: 0),
            Key.At(T(gust + 0.5), x: x + 0.35, y: 0.3, z: z - 0.1, rotationDegrees: (-60, 25 * turn, 40 * turn), interpolation: Out.Quad),
            Key.At(T(gust + 1.5), x: x + 2.2 + 0.3 * (n % 3), y: 0.9 + 0.2 * (n % 2), z: z - 0.4,
                rotationDegrees: (-20, 70 * turn, 160 * turn), transparency: 1, interpolation: In.Quad),
        ]);
    }

    /// <summary>The dot's path over the page.</summary>
    private Tweens.Keyframes Route()
    {
        float start = Middle(from), end = Middle(to), off = bounce[^1].Position.X;
        List<Key> keys = [
            Key.At(0, x: start, z: 0), Key.At(T(8.605), x: start),
            Key.At(T(11.605), x: end, interpolation: InOut.Sine), Key.At(T(12.355), x: end),
            Key.At(T(Tick(0)), x: ticks[0].Position.X, interpolation: InOut.Sine),
            // One InOut.Cubic segment: the dot reaches each tick of the ruler on a beat.
            Key.At(T(Tick(8)), x: ticks[8].Position.X, interpolation: InOut.Cubic), Key.At(T(Bars(7)), x: ticks[8].Position.X),
            // Speeds up into the first hop, which lands on the B on bar 9.
            Key.At(T(Bars(8) - Beat), x: bounce[0].Position.X - 0.6, interpolation: In.Sine),
        ];
        for (var i = 0; i < bounce.Length; i++)
            keys.Add(Key.At(T(Landing(i)), x: bounce[i].Position.X, interpolation: Interpolation.Linear));
        keys.AddRange([
            Key.At(T(Landing(6)), x: off + 1, interpolation: Interpolation.Linear), Key.At(T(29.98), x: off + 1.5, z: 0),
            Key.At(T(Stretch(0)), x: elastic[0].Position.X, z: 0.35),
            Key.At(T(Stretch(6)), x: elastic[6].Position.X, z: 0.35, interpolation: Interpolation.Linear),
        ]);
        // Smooth interpolation curves the path through every diamond.
        for (var i = 0; i < diamonds.Length; i++)
            keys.Add(Key.At(T(Waypoint(i)), x: diamonds[i].Position.X, z: diamonds[i].Position.Z));
        keys.AddRange([
            Key.At(T(Bars(15)), x: period.X, z: 0.4), Key.At(T(48.73), x: period.X, z: 0.4),
            Key.At(T(Bars(16)), x: period.X, z: period.Z, interpolation: Out.Back),
        ]);
        return Timeline(keys);
    }

    /// <summary>The disc above the carrier: it pops into the sky, drops, hops over BOUNCE, squashes and stretches, and
    /// turns into the period.</summary>
    private Tweens.Keyframes Disc()
    {
        var top = cap + Radius;
        List<Key> keys = [
            Key.At(0, y: 1.6, scale: (0.01, 0.01, 0.01), modulate: Ember), Key.At(T(0.25), scale: (0.01, 0.01, 0.01)),
            Key.At(T(0.85), scale: (1, 1, 1), interpolation: Out.Back),
            Key.At(T(2.6), y: 1.66), Key.At(T(Bars(1)), y: 1.6),
            Key.At(T(Bars(1) + Eighth), y: Radius, interpolation: In.Quad), Key.At(T(4.67), y: Radius + 0.3, interpolation: Out.Quad),
            Key.At(T(Bars(1) + Beat), y: Radius, interpolation: In.Quad), Key.At(T(Bars(8) - Beat), y: Radius),
            Key.At(T(Landing(5) + Eighth), y: top + 0.5, interpolation: Out.Quad), Key.At(T(Landing(6)), y: Radius, interpolation: In.Quad),
            Key.At(T(29.79), y: Radius + 0.3, interpolation: Out.Quad), Key.At(T(29.98), y: Radius, interpolation: In.Quad),
            Key.At(T(48.73), y: Radius, scale: (1, 1, 1), modulate: Ember), Key.At(T(49), modulate: GdGreen),
            // Shrinks to the size of the wordmark's period, resting on the page.
            Key.At(T(Bars(16)), y: Radius * 0.45, scale: (0.45, 0.45, 1), interpolation: Out.Back),
        ];
        for (var i = 0; i < bounce.Length; i++)
        {
            keys.Add(Key.At(T(Landing(i) - Eighth), y: top + 0.7, interpolation: Out.Quad));
            keys.Add(Key.At(T(Landing(i)), y: top, interpolation: In.Quad));
        }
        foreach (var landing in new[] { Bars(1) + Eighth, Bars(1) + Beat, Landing(6) }.Concat(bounce.Select((_, i) => Landing(i))))
        {
            keys.Add(Key.At(T(landing), scale: (1, 1, 1)));
            keys.Add(Key.At(T(landing + 0.05), scale: (1.3, 0.72, 1)));
            keys.Add(Key.At(T(landing + 0.13), scale: (0.9, 1.12, 1)));
            keys.Add(Key.At(T(landing + 0.28), scale: (1, 1, 1)));
        }
        return Timeline(keys);
    }

    /// <summary>BOUNCE stands up with a bounce, and each letter squashes under the landing dot.</summary>
    private Tweens.Keyframes Bounce(int i)
    {
        var keys = Standing(Bars(8) - Beat + 0.08 * i, Out.Bounce, 0.6);
        var landing = Landing(i);
        keys.AddRange([
            Key.At(0, scale: (1, 1, 1)), Key.At(T(landing), scale: (1, 1, 1)), Key.At(T(landing + 0.06), scale: (1.15, 0.78, 1)),
            Key.At(T(landing + 0.45), scale: (1, 1, 1), interpolation: Out.Back),
        ]);
        return Timeline(keys);
    }

    /// <summary>ELASTIC stands up elastically, and each letter stretches as the dot passes and springs back.</summary>
    private Tweens.Keyframes Elastic(int i)
    {
        var keys = Standing(Bars(10) + 0.06 * i, Out.Elastic, 0.8);
        var pass = Stretch(i);
        keys.AddRange([
            Key.At(0, scale: (1, 1, 1)), Key.At(T(pass), scale: (1, 1, 1)), Key.At(T(pass + 0.1), scale: (1.6, 0.8, 1)),
            Key.At(T(pass + 1), scale: (1, 1, 1), interpolation: Out.Elastic),
        ]);
        return Timeline(keys);
    }

    /// <summary>A keyframe diamond lights up and stands when the dot passes over it.</summary>
    private Tweens.Keyframes Diamond(int i)
    {
        var at = Waypoint(i);
        var keys = Standing(at, Out.Back, 0.4);
        keys.AddRange([
            Key.At(0, scale: 1, modulate: Sand), Key.At(T(at), scale: 1, modulate: Sand), Key.At(T(at + 0.08), scale: 1.6, modulate: Saffron),
            Key.At(T(at + 0.4), scale: 1, interpolation: Out.Back),
        ]);
        return Timeline(keys);
    }

    /// <summary>A wordmark letter falls onto the page on its 8th and squashes on landing.</summary>
    private Tweens.Keyframes Drop(int i)
    {
        var at = Bars(14) + 3 * Beat + Eighth * i;
        return Timeline([
            Key.At(0, y: 3.5, scale: (0.01, 0.01, 0.01)), Key.At(T(at), y: 3.5, scale: (0.01, 0.01, 0.01)),
            Key.At(T(at + 0.1), scale: (1, 1, 1)),
            Key.At(T(at + Eighth), y: 0, scale: (1, 1, 1), interpolation: In.Quad),
            Key.At(T(at + Eighth + 0.08), scale: (1.18, 0.8, 1)), Key.At(T(at + Eighth + 0.4), scale: (1, 1, 1), interpolation: Out.Back),
        ]);
    }

    // Where the dot lands on the page: its drop, the end of BOUNCE, and the period.
    private (double At, float X, float Z)[] Landings() =>
        [(Bars(1) + Eighth, Middle(from), 0), (Landing(6), bounce[^1].Position.X + 1, 0), (Bars(16), period.X, period.Z)];

    /// <summary>Rings spread over the page from every landing.</summary>
    private Tweens.Keyframes Ripple(int i)
    {
        List<Key> keys = [Key.At(0, x: Middle(from), z: 0, scale: 0.2, transparency: 1)];
        foreach (var (time, x, z) in Landings())
        {
            var at = time + 0.12 * i;
            keys.Add(Key.At(T(at - 0.02), x: x, z: z, scale: 0.2, transparency: 1));
            keys.Add(Key.At(T(at), transparency: 0.2));
            keys.Add(Key.At(T(at + 1.2), scale: 2.6 + 0.6 * i, transparency: 1, interpolation: Out.Cubic));
        }
        return Timeline(keys);
    }

    private static (string, Variant) Ratio(double amount) => ("amount_ratio", amount);
    private static (string, Variant) Speed(double scale) => ("speed_scale", scale);

    /// <summary>The trail lays dots from the dot's first landing until the camera looks back, then fades out. Under the
    /// closing fade, a burst of speed ages every dot out.</summary>
    private Tweens.Keyframes Lay() => Timeline([
        Key.At(0, Ratio(0), Speed(1)), Key.At(T(4.46), Ratio(0)), Key.At(T(4.5), Ratio(1)),
        Key.At(T(51.58), Ratio(1)), Key.At(T(51.6), Ratio(0)),
        Key.At(0, transparency: 0), Key.At(T(51.6), transparency: 0), Key.At(T(52.6), transparency: 1),
        Key.At(T(54.76), Speed(1)), Key.At(T(54.78), Speed(400)), Key.At(T(54.99), Speed(400)), Key.At(100, Speed(1)),
    ]);

    /// <summary>Puffs of paper dust at every landing, and along the wordmark as its letters land.</summary>
    private Tweens.Keyframes Dust()
    {
        List<Key> keys = [Key.At(0, x: Middle(from), z: 0), Key.At(0, Ratio(0))];
        foreach (var (time, x, z) in Landings())
        {
            keys.AddRange([
                Key.At(T(time - 0.03), x: x, z: z), Key.At(T(time - 0.02), Ratio(0)), Key.At(T(time), Ratio(1)),
                Key.At(T(time + 0.1), Ratio(1)), Key.At(T(time + 0.12), Ratio(0)),
            ]);
        }
        keys.AddRange([
            Key.At(T(45.68), x: logo[0].Position.X, z: logo[0].Position.Z), Key.At(T(45.71), Ratio(0)), Key.At(T(45.73), Ratio(1)),
            Key.At(T(48.355), x: logo[^1].Position.X, interpolation: Interpolation.Linear),
            Key.At(T(48.38), Ratio(1)), Key.At(T(48.4), Ratio(0)),
        ]);
        return Timeline(keys);
    }

    /// <summary>Confetti bursts from the wordmark when the dot becomes its period.</summary>
    private Tweens.Keyframes Burst() => Timeline([
        Key.At(0, Ratio(0)), Key.At(T(49.13), Ratio(0)), Key.At(T(49.15), Ratio(1)), Key.At(T(49.5), Ratio(1)), Key.At(T(49.53), Ratio(0)),
    ]);
}
