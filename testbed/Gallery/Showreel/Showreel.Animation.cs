// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using tweens.gd;
using Key = tweens.gd.Tweens.Keyframe;
namespace testbed;

// Scene setup is in Showreel.cs. A dot crosses the page from FROM to the period of TWEENS.GD, and each word it passes
// stands up and acts out its name. Every node plays keyframe definitions on one 20-second loop.
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
            subtitle.Tween(Timeline(Standing(17.7, Out.Back))),
        };
        // Each word stands up with the easing it names.
        plays.AddRange(from.Select((letter, i) => letter.Tween(Timeline(Standing(1 + 0.08 * i, Out.Back)))));
        plays.AddRange(to.Select((letter, i) => letter.Tween(Timeline(Standing(3 + 0.08 * i, Out.Back)))));
        plays.AddRange(ticks.Select((tick, i) => tick.Tween(Timeline(Standing(Tick(i), Out.Back, 0.35)))));
        plays.AddRange(ease.Select((letter, i) => letter.Tween(Timeline(Standing(4.5 + 0.35 * i, InOut.Cubic, 0.9)))));
        plays.AddRange(bounce.Select((letter, i) => letter.Tween(Bounce(i))));
        plays.AddRange(elastic.Select((letter, i) => letter.Tween(Elastic(i))));
        plays.AddRange(diamonds.Select((diamond, i) => diamond.Tween(Diamond(i))));
        plays.AddRange(keyframes.Select((letter, i) => letter.Tween(Timeline(Standing(Waypoint(i) + 0.1, Out.Back, 0.4)))));
        plays.AddRange(logo.Select((letter, i) => letter.Tween(Drop(i))));
        plays.AddRange(ripples.Select((ripple, i) => ripple.Tween(Ripple(i))));
        await Task.WhenAll(plays.Select(play => play.End));
    }

    private const double Length = 20;

    /// <summary>Converts seconds on the loop to a percentage stop.</summary>
    private static Percent T(double seconds) => Percent.Of(seconds / Length * 100);

    private Tweens.Keyframes Timeline(List<Key> keys) => new([.. keys],
        options: new TweenOptions { Duration = Length * Tempo, Repeats = TweenOptions.Infinite });

    // When the dot reaches each ruler tick, BOUNCE letter, ELASTIC letter and keyframe diamond.
    private static double Tick(int i) => 4.4 + 0.3 * i;
    private static double Landing(int i) => 7.75 + 0.45 * i;
    private static double Stretch(int i) => 11.3 + 0.18 * i;
    private static double Waypoint(int i) => 12.9 + 0.3 * i;

    /// <summary>Keys that stand a printed node up from the page, hinged at its foot.</summary>
    private static List<Key> Standing(double at, Interpolation ease, double seconds = 0.5) => [
        Key.At(0, rotationDegrees: (-90, 0, 0)), Key.At(T(at), rotationDegrees: (-90, 0, 0)),
        Key.At(T(at + seconds), rotationDegrees: (0, 0, 0), interpolation: ease),
    ];

    /// <summary>The rig glides along the journey, turning gently, then cranes back to reveal it.</summary>
    private Tweens.Keyframes Rig() => Timeline([
        Key.At(0, position: (-1.4, 0.4, 0), rotationDegrees: (-20, -18, 0)),
        Key.At(T(1.8), position: (-1.1, 0.4, 0), rotationDegrees: (-22, -12, 0)),
        Key.At(T(3.7), position: (-0.2, 0.35, -0.3), rotationDegrees: (-25, -4, 0)),
        Key.At(T(6.8), position: (5, 0.35, -0.5), rotationDegrees: (-28, 6, 0)),
        Key.At(T(9.6), position: (10.4, 0.6, -0.1), rotationDegrees: (-20, -6, 0)),
        Key.At(T(12.3), position: (15.6, 0.4, -0.2), rotationDegrees: (-25, 8, 0)),
        Key.At(T(15), position: (21.2, 0.35, -0.3), rotationDegrees: (-30, -5, 0)),
        Key.At(T(17.4), position: (27.6, 0.5, -0.3), rotationDegrees: (-18, 0, 0)),
        Key.At(T(17.7), position: (27.6, 0.5, -0.3), rotationDegrees: (-18, 0, 0)),
        Key.At(T(19.1), position: (23, 0.3, -0.4), rotationDegrees: (-34, 36, 0), interpolation: InOut.Sine),
    ]);

    /// <summary>The camera dollies out a little per station, then far back for the reveal.</summary>
    private Tweens.Keyframes Lens() => Timeline([
        Key.At(0, z: 3.8), Key.At(T(1.8), z: 4), Key.At(T(3.7), z: 4.6), Key.At(T(6.8), z: 5), Key.At(T(9.6), z: 4.8),
        Key.At(T(12.3), z: 4.8), Key.At(T(15), z: 5), Key.At(T(17.4), z: 6), Key.At(T(17.7), z: 6),
        Key.At(T(19.1), z: 11, interpolation: InOut.Sine),
    ]);

    /// <summary>Paper fades in, and fades back over the jump to the loop's first keys.</summary>
    private Tweens.Keyframes Fade() => Timeline([
        Key.At(0, alpha: 1), Key.At(T(0.7), alpha: 0), Key.At(T(19.45), alpha: 0), Key.At(T(19.85), alpha: 1),
    ]);

    /// <summary>The dot's path over the page.</summary>
    private Tweens.Keyframes Route()
    {
        float start = Middle(from), end = Middle(to), off = bounce[^1].Position.X;
        List<Key> keys = [
            Key.At(0, x: start, z: 0), Key.At(T(1.8), x: start),
            Key.At(T(3.4), x: end, interpolation: InOut.Sine), Key.At(T(3.7), x: end),
            Key.At(T(Tick(0)), x: ticks[0].Position.X, interpolation: InOut.Sine),
            // One InOut.Cubic segment: the dot reaches each tick of the ruler at equal time steps.
            Key.At(T(Tick(8)), x: ticks[8].Position.X, interpolation: InOut.Cubic),
            Key.At(T(7.3), x: bounce[0].Position.X - 0.9),
        ];
        for (var i = 0; i < bounce.Length; i++)
            keys.Add(Key.At(T(Landing(i)), x: bounce[i].Position.X, interpolation: Interpolation.Linear));
        keys.AddRange([
            Key.At(T(10.45), x: off + 1, interpolation: Interpolation.Linear), Key.At(T(10.75), x: off + 1.5, z: 0),
            Key.At(T(Stretch(0)), x: elastic[0].Position.X, z: 0.35),
            Key.At(T(Stretch(6)), x: elastic[6].Position.X, z: 0.35, interpolation: Interpolation.Linear),
        ]);
        // Smooth interpolation curves the path through every diamond.
        for (var i = 0; i < diamonds.Length; i++)
            keys.Add(Key.At(T(Waypoint(i)), x: diamonds[i].Position.X, z: diamonds[i].Position.Z));
        keys.AddRange([
            Key.At(T(16.8), x: period.X, z: 0.4), Key.At(T(17), x: period.X, z: 0.4),
            Key.At(T(17.4), x: period.X, z: period.Z, interpolation: Out.Back),
        ]);
        return Timeline(keys);
    }

    /// <summary>The disc above the carrier: drop, hops over BOUNCE, squash and stretch, and its turn into the period.</summary>
    private Tweens.Keyframes Disc()
    {
        var top = cap + Radius;
        List<Key> keys = [
            Key.At(0, y: 3.2, scale: (1, 1, 1), modulate: Ember), Key.At(T(0.3), y: 3.2),
            Key.At(T(0.9), y: Radius, interpolation: In.Quad), Key.At(T(1.12), y: Radius + 0.35, interpolation: Out.Quad),
            Key.At(T(1.35), y: Radius, interpolation: In.Quad), Key.At(T(7.3), y: Radius),
            Key.At(T(10.22), y: top + 0.5, interpolation: Out.Quad), Key.At(T(10.45), y: Radius, interpolation: In.Quad),
            Key.At(T(10.6), y: Radius + 0.3, interpolation: Out.Quad), Key.At(T(10.75), y: Radius, interpolation: In.Quad),
            Key.At(T(17), y: Radius, scale: (1, 1, 1), modulate: Ember), Key.At(T(17.3), modulate: GdGreen),
            // Shrinks to the size of the wordmark's period, resting on the page.
            Key.At(T(17.4), y: Radius * 0.45, scale: (0.45, 0.45, 1), interpolation: Out.Back),
        ];
        for (var i = 0; i < bounce.Length; i++)
        {
            keys.Add(Key.At(T(Landing(i) - 0.225), y: top + 0.7, interpolation: Out.Quad));
            keys.Add(Key.At(T(Landing(i)), y: top, interpolation: In.Quad));
        }
        foreach (var landing in new[] { 0.9, 1.35, 10.45 }.Concat(bounce.Select((_, i) => Landing(i))))
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
        var keys = Standing(7 + 0.08 * i, Out.Bounce, 0.6);
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
        var keys = Standing(10.4 + 0.06 * i, Out.Elastic, 0.8);
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

    /// <summary>A wordmark letter falls onto the page and squashes on landing.</summary>
    private Tweens.Keyframes Drop(int i)
    {
        var at = 15.7 + 0.1 * i;
        return Timeline([
            Key.At(0, y: 3.5, scale: (0.01, 0.01, 0.01)), Key.At(T(at), y: 3.5, scale: (0.01, 0.01, 0.01)),
            Key.At(T(at + 0.1), scale: (1, 1, 1)),
            Key.At(T(at + 0.5), y: 0, scale: (1, 1, 1), interpolation: In.Quad),
            Key.At(T(at + 0.58), scale: (1.18, 0.8, 1)), Key.At(T(at + 0.9), scale: (1, 1, 1), interpolation: Out.Back),
        ]);
    }

    // Where the dot lands on the page: its drop, the end of BOUNCE, and the period.
    private (double At, float X, float Z)[] Landings() =>
        [(0.9, Middle(from), 0), (10.45, bounce[^1].Position.X + 1, 0), (17.4, period.X, period.Z)];

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

    /// <summary>The trail lays dots once the dot lands. Under the closing fade, a burst of speed ages every dot out.</summary>
    private Tweens.Keyframes Lay() => Timeline([
        Key.At(0, Ratio(0), Speed(1)), Key.At(T(0.88), Ratio(0)), Key.At(T(0.92), Ratio(1)),
        Key.At(T(19.4), Ratio(1)), Key.At(T(19.42), Ratio(0)),
        Key.At(T(19.86), Speed(1)), Key.At(T(19.9), Speed(400)), Key.At(T(19.98), Speed(400)), Key.At(100, Speed(1)),
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
            Key.At(T(16.15), x: logo[0].Position.X, z: logo[0].Position.Z), Key.At(T(16.18), Ratio(0)), Key.At(T(16.2), Ratio(1)),
            Key.At(T(16.92), x: logo[^1].Position.X, interpolation: Interpolation.Linear),
            Key.At(T(16.95), Ratio(1)), Key.At(T(16.97), Ratio(0)),
        ]);
        return Timeline(keys);
    }

    /// <summary>Confetti bursts from the wordmark when the dot becomes its period.</summary>
    private Tweens.Keyframes Burst() => Timeline([
        Key.At(0, Ratio(0)), Key.At(T(17.42), Ratio(0)), Key.At(T(17.45), Ratio(1)), Key.At(T(17.8), Ratio(1)), Key.At(T(17.83), Ratio(0)),
    ]);
}
