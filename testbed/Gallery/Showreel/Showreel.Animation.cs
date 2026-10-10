// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using tweens.gd;
using Key = tweens.gd.Tweens.Keyframe;
namespace testbed;

// Scene setup is in Showreel.cs. Every node plays one definition that spans the whole loop.
public sealed partial class Showreel
{
    private async Task AnimateAsync()
    {
        var bar = Bars();
        var captions = Captions();
        var plays = new List<Group> { rig.Tween(Rig()), camera.Tween(Lens()), hero.Tween(Hero()), flash.Tween(Flashes()), fade.Tween(Fade()) };
        plays.AddRange(bars.Select(node => node.Tween(bar)));
        plays.AddRange(letters.Select((letter, i) => letter.Tween(Letter(i, letter.Position, letter.Modulate))));
        plays.AddRange(words.Select((word, i) => word.Tween(captions[i])));
        plays.Add(ticker.Tween(Ticker()));
        plays.AddRange(shockwaves.Select((ring, i) => ring.Tween(Shockwave(i))));
        plays.AddRange([stream.Tween(Flow()), wind.Tween(Wind()), sparks.Tween(Sparks())]);
        plays.AddRange(streaks.Select((streak, i) => streak.Tween(Pass(0.8 + i * 0.618034 % 1, i * 0.381966 % 1))));
        plays.AddRange(chunks.Select((chunk, i) => chunk.Tween(Pass(2.6 + i * 0.618034 % 1 * 1.4, i * 0.381966 % 1 * 2,
            new Vector3(90 * (1 + i % 3), 120, 60 * (i % 2))))));
        plays.AddRange(wipes.Select((wipe, i) => wipe.Tween(Wipe(i))));
        await Task.WhenAll(plays.Select(play => play.End));
    }

    private const double Length = 20;

    /// <summary>Converts seconds on the loop to a percentage stop.</summary>
    private static Percent T(double seconds) => Percent.Of(seconds / Length * 100);

    private Tweens.Keyframes Timeline(List<Key> keys, Interpolation interpolation = default) => new([.. keys],
        interpolation: interpolation, options: new TweenOptions { Duration = Length * Tempo, Repeats = TweenOptions.Infinite });

    /// <summary>The rig orbits: pitch, yaw and roll. Whip pans ease in and out between held framings.</summary>
    private Tweens.Keyframes Rig() => Timeline([
        Key.At(0, position: (-2, 0.5, 0), rotationDegrees: (-35, -120, 0)),
        Key.At(T(2.6), position: (0, 0, 0), rotationDegrees: (-8, -12, -5)),
        Key.At(T(4.6), rotationDegrees: (-5, 6, 3)),
        Key.At(T(5), rotationDegrees: (-6, 10, 0)),
        Key.At(T(5.5), rotationDegrees: (-12, 90, 0), interpolation: InOut.Expo),
        Key.At(T(6.6), rotationDegrees: (-10, 98, 2)),
        Key.At(T(6.9), rotationDegrees: (-12, 180, 0), interpolation: InOut.Expo),
        Key.At(T(7.9), rotationDegrees: (-10, 188, -2)),
        Key.At(T(8.2), rotationDegrees: (-12, 270, 0), interpolation: InOut.Expo),
        Key.At(T(9.2), rotationDegrees: (-10, 278, 2)),
        Key.At(T(9.5), rotationDegrees: (-12, 360, 0), interpolation: InOut.Expo),
        Key.At(T(10.4), rotationDegrees: (-10, 370, 0)),
        Key.At(T(11.4), position: (0, 0.3, 0), rotationDegrees: (-30, 420, 12)),
        Key.At(T(13.3), rotationDegrees: (-14, 470, -6)),
        Key.At(T(14.6), rotationDegrees: (-10, 500, 0)),
        Key.At(T(15.4), position: (0, 0.1, 0), rotationDegrees: (-4, 720, 0), interpolation: InOut.Expo),
        Key.At(T(18.4), rotationDegrees: (-2, 726, 0)),
        Key.At(100, rotationDegrees: (-6, 740, 4)),
    ]);

    /// <summary>The camera dollies along the rig's Z axis, punches its field of view and shakes on impacts.</summary>
    private Tweens.Keyframes Lens()
    {
        List<Key> keys = [
            Key.At(0, z: 10), Key.At(T(2.6), z: 6.5), Key.At(T(5), z: 6), Key.At(T(5.5), z: 4.6), Key.At(T(10.4), z: 4.6),
            Key.At(T(11.4), z: 6.5), Key.At(T(13.3), z: 3.4), Key.At(T(14.6), z: 5), Key.At(T(15.4), z: 7.5),
            Key.At(T(19.2), z: 7.2), Key.At(100, z: 6.4),
            Key.At(0, Fov(60)), Key.At(T(2.6), Fov(44)), Key.At(T(11.4), Fov(50)), Key.At(T(13.3), Fov(72)),
            Key.At(T(15.4), Fov(40)), Key.At(100, Fov(30)),
        ];
        foreach (var (at, fov, shake) in new[] { (3.2, 44.0, 0.1), (13.4, 72.0, 0.25), (17.0, 40.0, 0.08), (17.5, 38.0, 0.08), (18.0, 36.0, 0.12) })
        {
            keys.Add(Key.At(T(at), Fov(fov)));
            keys.Add(Key.At(T(at + 0.1), Fov(fov - 8)));
            keys.Add(Key.At(T(at + 0.4), Fov(fov - 2)));
            double[] offsets = [0, 1, -0.8, 0.6, -0.4, 0.2, 0];
            for (var i = 0; i < offsets.Length; i++)
                keys.Add(Key.At(T(at + i * 0.04), ("h_offset", offsets[i] * shake), ("v_offset", offsets[^(i + 1)] * shake)));
        }
        return Timeline(keys);
    }

    private static (string, Variant) Fov(double degrees) => ("fov", degrees);

    /// <summary>A white flash on every impact and whip landing.</summary>
    private Tweens.Keyframes Flashes()
    {
        List<Key> keys = [Key.At(0, alpha: 0)];
        foreach (var (at, strength) in new[] { (3.2, 0.9), (5.0, 0.5), (5.5, 0.25), (6.9, 0.25), (8.2, 0.25), (9.5, 0.25),
            (13.4, 1.0), (15.4, 0.7), (17.0, 0.35), (17.5, 0.35), (18.0, 0.45) })
        {
            keys.Add(Key.At(T(at - 0.04), alpha: 0));
            keys.Add(Key.At(T(at), alpha: strength));
            keys.Add(Key.At(T(at + 0.4), alpha: 0, interpolation: Out.Cubic));
        }
        return Timeline(keys, Interpolation.Linear);
    }

    /// <summary>Fades in from black and back out, hiding the jump to the loop's first keys.</summary>
    private Tweens.Keyframes Fade() => Timeline([
        Key.At(0, alpha: 1), Key.At(T(0.8), alpha: 0), Key.At(T(19.2), alpha: 0), Key.At(100, alpha: 1),
    ]);

    /// <summary>Both letterbox bars play this one definition during the hero shot.</summary>
    private Tweens.Keyframes Bars() => Timeline([
        Key.At(0, scale: (1, 0)), Key.At(T(10.2), scale: (1, 0)),
        Key.At(T(10.8), scale: (1, 1), interpolation: Out.Cubic), Key.At(T(14.8), scale: (1, 1)),
        Key.At(T(15.3), scale: (1, 0), interpolation: In.Cubic),
    ]);

    /// <summary>Places a letter on the title's baseline, shifted by (x, y), tilted by degrees and turned.</summary>
    private static Key Pose(double at, Vector3 home, (double X, double Y, double Tilt, Vector3 Turn) pose,
        double? scale = null, Interpolation? interpolation = null) => Key.At(T(at),
        position: home + new Vector3((float)pose.X, (float)(pose.Y + home.X * Math.Tan(Mathf.DegToRad(pose.Tilt))), 0),
        rotationDegrees: pose.Turn + new Vector3(0, 0, (float)pose.Tilt), scale: scale, interpolation: interpolation);

    /// <summary>Each letter flies in, slides across the frame with a turn, explodes past the camera, slams back from
    /// behind it and snakes between framings on the title beats.</summary>
    private Tweens.Keyframes Letter(int i, Vector3 home, Color color)
    {
        var scatter = (Math.Cos(i * 2.4) * 7, Math.Sin(i * 1.9) * 3 + 1, -6.0 - i % 3 * 3);
        var spin = new Vector3(i * 67 % 360 - 180, i * 131 % 360 - 180, i * 53 % 90 - 45);
        var blast = new Vector3(home.X * 7, i % 2 == 0 ? 3 : -2.5f, 4.5f);
        var behind = new Vector3(home.X * 2.5f, home.Y, 11);
        var lowLeft = (-1.4, -0.55, -8.0, Vector3.Zero);
        var highRight = (1.3, 0.5, 6.0, new Vector3(0, 360, 0));
        // Each letter trails its left neighbour, so the title snakes from pose to pose.
        double land = 1 + 0.15 * i, slam = 15.3 + 0.06 * i, lag = 0.03 * i;
        // Transparency fades the outline with the glyphs; modulate only tints.
        List<Key> keys = [
            Key.At(0, position: scatter, rotationDegrees: spin, scale: 0.4, modulate: color, transparency: 1),
            Key.At(T(land - 0.75), position: scatter, rotationDegrees: spin, scale: 0.4, transparency: 1),
            Key.At(T(land - 0.55), transparency: 0),
            Pose(land, home, lowLeft, scale: 1, interpolation: Out.Back),
            Key.At(T(3.2), scale: 1, modulate: color),
            Key.At(T(3.3 + lag), scale: 1.45, modulate: Yellow),
            Pose(3.45 + lag, home, lowLeft),
            Key.At(T(3.75 + lag), scale: 1, interpolation: Out.Back),
            Key.At(T(4.2), modulate: color),
            Pose(4.45 + lag, home, highRight, interpolation: Out.Back),
            Pose(4.95, home, highRight),
            Key.At(T(5.35), transparency: 0),
            Key.At(T(5.6), position: blast, rotationDegrees: spin * 2, transparency: 1, interpolation: Out.Quad),
            Key.At(T(slam - 0.4), transparency: 1),
            Key.At(T(slam - 0.35), position: behind, rotationDegrees: new Vector3(0, 0, (i - 4) * 25), transparency: 0),
            Key.At(T(16.9), modulate: color),
            Key.At(T(18.8), modulate: color),
        ];
        // The slam lands tilted on the lower left; each beat punches, then slides and twists to the next framing.
        (double, double, double, Vector3)[] poses = [
            (-1.5, -0.6, -10, Vector3.Zero), (1.4, 0.5, 8, new(0, 360, 0)), (-0.9, 0.55, -6, new(360, 360, 0)), (0, 0, 0, new(360, 360, 0)),
        ];
        Color[] tints = [Cyan, Yellow, Pink];
        keys.Add(Pose(slam, home, poses[0], interpolation: Out.Back));
        for (var beat = 0; beat < tints.Length; beat++)
        {
            var at = 17 + 0.5 * beat + lag;
            keys.Add(Pose(at, home, poses[beat], scale: 1));
            keys.Add(Key.At(T(at + 0.08), scale: 1.45, modulate: tints[beat]));
            keys.Add(Pose(at + 0.35, home, poses[beat + 1], scale: 1, interpolation: Out.Back));
        }
        return Timeline(keys);
    }

    /// <summary>One word per whip pan. Each crosses the frame in the way its channel names.</summary>
    private Tweens.Keyframes[] Captions() => [
        // POSITION skids in along the bottom, leans as it brakes, then dashes out to the upper right.
        Timeline([
            Key.At(0, position: (-7, -0.9, 0), rotationDegrees: (0, 0, 0), transparency: 1),
            Key.At(T(5.25), position: (-7, -0.9, 0), transparency: 1), Key.At(T(5.3), transparency: 0),
            Key.At(T(5.6), position: (-1.5, -0.9, 0), rotationDegrees: (0, 0, 10), interpolation: Out.Back),
            Key.At(T(5.9), rotationDegrees: (0, 0, 0)), Key.At(T(6.45), position: (-1.1, -0.85, 0)), Key.At(T(6.75), transparency: 0),
            Key.At(T(6.8), position: (8, 1.2, 0), rotationDegrees: (0, 0, -12), interpolation: In.Back),
            Key.At(T(6.85), transparency: 1),
        ]),
        // ROTATION corkscrews down from the upper right, then barrel-rolls out to the lower left.
        Timeline([
            Key.At(0, position: (4.5, 1.6, 0), rotationDegrees: (0, -270, 40), transparency: 1),
            Key.At(T(6.75), position: (4.5, 1.6, 0), rotationDegrees: (0, -270, 40), transparency: 1),
            Key.At(T(6.8), transparency: 0),
            Key.At(T(7.2), position: (1.1, 0.8, 0), rotationDegrees: (0, 0, -6), interpolation: Out.Back),
            Key.At(T(7.85), position: (0.6, 0.65, 0), rotationDegrees: (0, 0, -2)), Key.At(T(8.1), transparency: 0),
            Key.At(T(8.15), position: (-6, -1.4, 0), rotationDegrees: (360, 0, 30), interpolation: In.Cubic),
            Key.At(T(8.2), transparency: 1),
        ]),
        // SCALE pops in on the upper left and hops across the frame, swelling on every landing.
        Timeline([
            Key.At(0, position: (-2.3, 0.75, 0), rotationDegrees: (0, 0, 0), scale: 0.01),
            Key.At(T(8.1), scale: 0.01), Key.At(T(8.3), scale: 1.5, interpolation: Out.Quad), Key.At(T(8.45), scale: 1),
            Key.At(T(8.55), position: (-2.3, 0.75, 0), rotationDegrees: (0, 0, 0)),
            Key.At(T(8.8), position: (0, -0.55, 0), rotationDegrees: (0, 0, -10), scale: 1.35, interpolation: Out.Back),
            Key.At(T(8.95), scale: 1), Key.At(T(9), position: (0, -0.55, 0), rotationDegrees: (0, 0, 0)),
            Key.At(T(9.25), position: (2.2, 0.6, 0), rotationDegrees: (0, 0, 8), scale: 1.35, interpolation: Out.Back),
            Key.At(T(9.35), scale: 1), Key.At(T(9.5), scale: 0.01, interpolation: In.Back),
        ]),
        // COLOR flips up from the lower right and cycles its tint as it slides out to the left.
        Timeline([
            Key.At(0, position: (6, -1, 0), rotationDegrees: (-90, 0, 0), modulate: Pink, transparency: 1),
            Key.At(T(9.35), position: (6, -1, 0), rotationDegrees: (-90, 0, 0), transparency: 1), Key.At(T(9.4), transparency: 0),
            Key.At(T(9.7), position: (1.4, -0.9, 0), rotationDegrees: (0, 0, 0), interpolation: Out.Back),
            Key.At(T(9.75), modulate: Pink), Key.At(T(10), modulate: Yellow), Key.At(T(10.25), modulate: Cyan),
            Key.At(T(10.5), position: (0.3, -0.75, 0), modulate: Violet), Key.At(T(10.75), modulate: Colors.White),
            Key.At(T(10.8), transparency: 0),
            Key.At(T(10.85), position: (-7, -0.4, 0), rotationDegrees: (0, 0, 15), interpolation: In.Back),
            Key.At(T(10.9), transparency: 1),
        ]),
    ];

    /// <summary>Slides in at the top of the frame, flips down to the bottom for the punch, then shoots off to the left.</summary>
    private Tweens.Keyframes Ticker() => Timeline([
        Key.At(0, position: (5, 0.9, -3), rotationDegrees: (0, 0, 0), scale: 1, transparency: 1),
        Key.At(T(10.9), position: (5, 0.9, -3), transparency: 1), Key.At(T(10.95), transparency: 0),
        Key.At(T(11.35), position: (0.9, 0.9, -3), interpolation: Out.Back),
        Key.At(T(12.5), position: (0.4, 0.9, -3), rotationDegrees: (0, 0, 0)),
        Key.At(T(12.95), position: (-0.9, -0.95, -3), rotationDegrees: (360, 0, 0), interpolation: InOut.Back),
        Key.At(T(13.4), scale: 1), Key.At(T(13.48), scale: 1.4), Key.At(T(13.8), scale: 1, interpolation: Out.Back),
        Key.At(T(14.4), position: (-1.2, -0.95, -3), rotationDegrees: (360, 0, 0)), Key.At(T(14.7), transparency: 0),
        Key.At(T(14.75), position: (-6, -0.95, -3), rotationDegrees: (360, 0, -20), interpolation: In.Back),
        Key.At(T(14.8), transparency: 1),
    ]);

    /// <summary>The hero rises through the floor, swallows the stream, punches and vanishes.</summary>
    private Tweens.Keyframes Hero() => Timeline([
        Key.At(0, position: (0, -6, 0), rotationDegrees: (0, 0, 0), scale: 0.01),
        Key.At(T(10.2), position: (0, -6, 0), scale: 0.01),
        Key.At(T(11.3), position: (0, 0.3, 0), rotationDegrees: (20, 60, 0), scale: 1.4, interpolation: Out.Back),
        Key.At(T(13.25), rotationDegrees: (25, 110, 10), scale: 1.1),
        Key.At(T(13.4), scale: 2.2, interpolation: Out.Quad),
        Key.At(T(13.8), scale: 1.4, interpolation: Out.Back),
        Key.At(T(14.5), rotationDegrees: (35, 160, 0), scale: 1.6),
        Key.At(T(14.85), scale: 0.01, interpolation: In.Back),
    ]);

    /// <summary>Rings expand and fade on the hero's punch, then turn to face the camera for the title beats.</summary>
    private Tweens.Keyframes Shockwave(int i)
    {
        List<Key> keys = [
            Key.At(0, rotationDegrees: (i * 30 - 30, 0, i * 20), scale: 0.2, transparency: 1),
            Key.At(T(14.6), rotationDegrees: (i * 30 - 30, 0, i * 20)),
            Key.At(T(16.4), rotationDegrees: (90, 0, 0)),
        ];
        foreach (var (at, size) in new[] { (13.4, 8.0), (17.0, 6.0), (18.0, 7.0) })
        {
            var begin = at + 0.1 * i;
            keys.Add(Key.At(T(begin - 0.02), scale: 0.2, transparency: 1));
            keys.Add(Key.At(T(begin), transparency: 0.1));
            keys.Add(Key.At(T(begin + 0.9), scale: size, transparency: 1, interpolation: Out.Cubic));
        }
        return Timeline(keys);
    }
    /// <summary>The stream's direction: right to left behind the title, rising during the whip pans, into the screen
    /// toward the hero, out at the camera on its punch, and upward behind the finale. It turns under the flashes.</summary>
    private Tweens.Keyframes Flow() => Timeline([
        Key.At(0, rotationDegrees: (0, 0, 180)), Key.At(T(4.95), rotationDegrees: (0, 0, 180)),
        Key.At(T(5.1), rotationDegrees: (0, 0, 15)), Key.At(T(10), rotationDegrees: (0, 0, 15)),
        Key.At(T(10.3), rotationDegrees: (0, 90, 0)), Key.At(T(13.35), rotationDegrees: (0, 90, 0)),
        Key.At(T(13.45), rotationDegrees: (0, -90, 0)), Key.At(T(15.25), rotationDegrees: (0, -90, 0)),
        Key.At(T(15.45), rotationDegrees: (0, 0, 90)),
    ]);

    /// <summary>One trip along the stream: fade in, cross, fade out and return unseen. It repeats after its own delay.</summary>
    private Tweens.Keyframes Pass(double seconds, double delay, Vector3 tumble = default)
    {
        List<Key> keys = [
            Key.At(0, x: -16, transparency: 1), Key.At(8, transparency: 0), Key.At(84, transparency: 0),
            Key.At(92, x: 16, transparency: 1), Key.At(100, x: -16),
        ];
        if (tumble != Vector3.Zero)
            keys.AddRange([Key.At(0, rotationDegrees: Vector3.Zero), Key.At(92, rotationDegrees: tumble), Key.At(100, rotationDegrees: Vector3.Zero)]);
        return new([.. keys], interpolation: Interpolation.Linear,
            options: new TweenOptions { Duration = seconds * Tempo, Delay = delay * Tempo, Repeats = TweenOptions.Infinite });
    }

    private static (string, Variant) Ratio(double amount) => ("amount_ratio", amount);

    /// <summary>The wind thickens for the swarm and the warp, then eases for the finale.</summary>
    private Tweens.Keyframes Wind() => Timeline([
        Key.At(0, Ratio(0.3)), Key.At(T(4.9), Ratio(0.3)), Key.At(T(5.1), Ratio(0.6)), Key.At(T(10), Ratio(0.6)),
        Key.At(T(10.3), Ratio(1)), Key.At(T(15.25), Ratio(1)), Key.At(T(15.45), Ratio(0.5)),
    ]);

    /// <summary>A short burst of sparks at every impact: the title punch, the blast, the hero and the title beats.</summary>
    private Tweens.Keyframes Sparks()
    {
        List<Key> keys = [Key.At(0, position: (-1.4, -0.4, 0)), Key.At(0, Ratio(0))];
        foreach (var (at, x, y) in new[] { (3.2, -1.4, -0.4), (5.0, 1.3, 0.65), (13.4, 0.0, 0.3), (15.4, -1.5, -0.45),
            (17.0, -1.5, -0.45), (17.5, 1.4, 0.65), (18.0, -0.9, 0.7) })
        {
            keys.Add(Key.At(T(at), position: (x, y, 0)));
            keys.Add(Key.At(T(at - 0.02), Ratio(0)));
            keys.Add(Key.At(T(at), Ratio(1)));
            keys.Add(Key.At(T(at + 0.12), Ratio(1)));
            keys.Add(Key.At(T(at + 0.14), Ratio(0)));
        }
        return Timeline(keys);
    }

    /// <summary>Flat bars sweep across the lens at each act change, alternating direction.</summary>
    private Tweens.Keyframes Wipe(int i)
    {
        var lag = 0.05 * i;
        return Timeline([
            Key.At(0, x: -4), Key.At(T(4.95 + lag), x: -4), Key.At(T(5.35 + lag), x: 4, interpolation: InOut.Cubic),
            Key.At(T(10.05 + lag), x: 4), Key.At(T(10.45 + lag), x: -4, interpolation: InOut.Cubic),
            Key.At(T(14.95 + lag), x: -4), Key.At(T(15.35 + lag), x: 4, interpolation: InOut.Cubic),
            Key.At(T(19.9), x: 4), Key.At(100, x: -4),
        ]);
    }
}
