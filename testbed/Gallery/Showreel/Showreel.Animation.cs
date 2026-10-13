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

// The scene is Showreel.tscn. "Along the Way": a dot travels a page from FROM to the period of TWEENS.GD, and each
// word it passes stands up and acts out its name. Every node plays keyframe definitions on one 55-second loop, timed
// to an 80 bpm track.
public sealed partial class Showreel
{
    private async Task AnimateAsync()
    {
        // Scenes share the loop's clock, so their lead-ins and exits can overlap. Start them together;
        // awaiting one scene before starting the next would wait for its infinite loop to end.
        // Edit a cue's at value to move its lettering, definition and punch line together.
        // Camera, dot and landing effects are choreographed separately in JourneyAsync.
        await Task.WhenAll(
            JourneyAsync(),
            OpeningCreditAsync(at: Bars(0)),
            FromToAsync(at: Bars(2)),
            EaseAsync(at: Tick(0)),
            BounceAsync(at: 21.98),
            ElasticAsync(at: 31.855),
            KeyframesAsync(at: Waypoint(0)),
            WordmarkAsync(at: Bars(15) - Eighth),
            ClosingAsync(at: Bars(16) + 2 * Beat));
    }

    /// <summary>The camera, travelling dot and landing effects run across scene boundaries.</summary>
    private async Task JourneyAsync()
    {
        // The trail keeps each dot it lays for exactly one loop.
        trail.Lifetime = Length * Tempo;
        var route = Route();
        await Task.WhenAll(
            PlayAsync(
                rig.Tween(Rig()), camera.Tween(Lens()), fade.Tween(Fade()),
                // One definition moves both the dot and the emitter that lays its trail.
                dot.Tween(route), trail.Tween(route), trail.Tween(Lay()),
                disc.Tween(Disc()), rim.Tween(Rim()), leaves.Tween(Toss()), impact.Tween(Impact())),
            PlayAsync(ripples.Select((ripple, i) => ripple.Tween(Ripple(i)))));
    }

    private async Task OpeningCreditAsync(double at)
    {
        var shift = at - Bars(0);
        await Task.WhenAll(
            PlayAsync(godot.Tween(Float(godot, 0, credit.Length, Bars(0) + Eighth, shift))),
            PlayAsync(credit.Select((letter, i) => letter.Tween(Float(letter, i, i, Bars(0), shift)))));
    }

    private async Task FromToAsync(double at)
    {
        var shift = at - Bars(2);
        await Task.WhenAll(
            DefinitionAsync(0, shift),
            PlayAsync(from.Select((letter, i) => letter.Tween(Timeline(Standing(Bars(2) + 0.08 * i, Out.Back), shift)))),
            PlayAsync(to.Select((letter, i) => letter.Tween(Timeline(Standing(Bars(3) + Eighth + 0.08 * i, Out.Back), shift)))));
    }

    private async Task EaseAsync(double at)
    {
        var shift = at - Tick(0);
        await Task.WhenAll(
            DefinitionAsync(1, shift),
            PlayAsync(ticks.Select((tick, i) => tick.Tween(Timeline(Standing(Tick(i), Out.Back, 0.35), shift)))),
            PlayAsync(ease.Select((letter, i) => letter.Tween(Timeline(Standing(Tick(i + 1), InOut.Cubic, 0.9), shift)))));
    }

    private async Task BounceAsync(double at)
    {
        var shift = at - 21.98;
        await Task.WhenAll(DefinitionAsync(2, shift), PlayAsync(bounce.Select((letter, i) => letter.Tween(Bounce(i, shift)))));
    }

    private async Task ElasticAsync(double at)
    {
        var shift = at - 31.855;
        await Task.WhenAll(DefinitionAsync(3, shift), PlayAsync(elastic.Select((letter, i) => letter.Tween(Elastic(i, shift)))));
    }

    private async Task KeyframesAsync(double at)
    {
        var shift = at - Waypoint(0);
        await Task.WhenAll(
            DefinitionAsync(4, shift),
            PlayAsync(diamonds.Select((diamond, i) => diamond.Tween(Diamond(i, shift)))),
            PlayAsync(keyframes.Select((letter, i) => letter.Tween(Timeline(Standing(Waypoint(i) + 0.1, Out.Back, 0.4), shift)))));
    }

    private async Task WordmarkAsync(double at)
    {
        var shift = at - (Bars(15) - Eighth);
        await Task.WhenAll(
            PlayAsync(logo.Select((letter, i) => letter.Tween(Drop(i, shift)))),
            PlayAsync(confetti.Tween(Burst(shift)), subtitle.Tween(Timeline(Standing(49.48, Out.Back), shift))));
    }

    private async Task ClosingAsync(double at) => await PlayAsync(
        taglines.Select((line, i) => line.Tween(Timeline(Standing(Bars(16) + 2 * Beat + Eighth * i, Out.Back), at - (Bars(16) + 2 * Beat)))));

    /// <summary>Print a definition, shorten it to a punch line, then blow it away.</summary>
    private async Task DefinitionAsync(int station, double shift)
    {
        var (words, extra) = glosses[station];
        var at = Glossary[station];
        await Task.WhenAll(
            PlayAsync(words.Select((word, i) => word.Tween(Printed(word, i, at, shift)))),
            PlayAsync(extra.Select((word, j) => word.Tween(Extra(word, j, at, shift)))));
    }

    private static Task PlayAsync(params Group[] plays) => PlayAsync((IEnumerable<Group>)plays);

    private static async Task PlayAsync(IEnumerable<Group> plays) => await Task.WhenAll(plays.Select(play => play.End));

    private const double Length = 55;

    /// <summary>Converts seconds on the loop to a percentage stop.</summary>
    private static Percent T(double seconds) => Percent.Of(seconds / Length * 100);

    // A cue shifts the whole scene. Delay moves it later; offset starts it further into its first loop.
    private Tweens.Keyframes Timeline(List<Key> keys, double shift = 0) => new([.. keys],
        options: new TweenOptions
        {
            Duration = Length * Tempo, Repeats = TweenOptions.Infinite,
            Delay = Math.Max(0, shift) * Tempo, Offset = Math.Max(0, -shift) * Tempo, Fill = FillMode.Both,
        });

    // The track's grid: 80 bpm in 4/4, its first downbeat at 1.105 s.
    private const double Beat = 0.75, Eighth = Beat / 2;
    /// <summary>The downbeat <paramref name="bars"/> bars after the first one.</summary>
    private static double Bars(int bars) => 1.105 + 4 * Beat * bars;

    // When the dot reaches each ruler tick, BOUNCE letter, ELASTIC letter and keyframe diamond.
    private static double Tick(int i) => Bars(4) + 3 * Eighth + Beat * i;
    // The hops land 0.6 s after the bar; the hop from U to N is half as long, for the track's double bass drum hit.
    private static double Landing(int i) => Bars(8) + 0.6 + Beat * i - (i > 2 ? Eighth : 0);
    private static readonly double Takeoff = Landing(0) - Beat;
    private static double Stretch(int i) => Bars(10) + 3 * Beat + Eighth * i;
    private static double Waypoint(int i) => Bars(12) + 3 * Eighth + Beat * i;

    /// <summary>When a definition is printed, shrinks to its headword, gains new words, and blows away.</summary>
    private readonly record struct Moments(double Print, double Shrink, double Extend, double Blow);
    private static readonly Moments[] Glossary = [
        new(7.48, 10.855, 11.605, Bars(4) + Beat), new(15.35, 18.23, 18.605, 21.355),
        new(21.98, Landing(5) - Eighth, Landing(5), Bars(10) + Beat), new(31.855, 34.48, 34.855, 37.855), new(38.98, 41.98, 42.355, 44.98),
    ];

    /// <summary>Keys that stand a printed node up from the page, hinged at its foot.</summary>
    private static List<Key> Standing(double at, Interpolation ease, double seconds = 0.5) => [
        Key.At(0, rotationDegrees: (-90, 0, 0)), Key.At(T(at), rotationDegrees: (-90, 0, 0)),
        Key.At(T(at + seconds), rotationDegrees: (0, 0, 0), interpolation: ease),
    ];

    /// <summary>Where the camera looks at a moment: the rig sits on the target and turns, and the camera backs away
    /// from it by the distance. Target and turn are relative to a station, so each scene is framed as if unturned.</summary>
    private readonly record struct Shot(double At, int Station, (double X, double Y, double Z) Target,
        (double X, double Y, double Z) Turn, double Distance, Interpolation? Ease = null);

    // Each station word gets a close-up as it stands, each definition a wider frame, and each punch line a close one.
    // Moves take a second or more and start early, so the camera arrives as things happen.
    private static readonly Shot[] Shots = [
        // Up at the dot in the sky, then down to the page with it.
        new(0, 0, (-2.06, 2.1, 0), (25, -4, 0), 4.4),
        new(2.6, 0, (-2.06, 2.15, 0), (23, 2, 0), 4.4),
        new(4, 0, (-2.06, 2.1, 0), (22, 0, 0), 4.4),
        new(5.2, 0, (-1.9, 0.4, 0.1), (-22, -10, 0), 4.2, InOut.Cubic),
        // FROM, the definition, TO, and the punch line.
        new(6.3, 0, (-2, 0.32, -0.45), (-14, -6, 0), 3),
        new(7.1, 0, (-2.06, 0.34, -0.6), (-15, -4, 0), 2.4),
        new(7.6, 0, (-2, 0.34, -0.6), (-15, -3, 0), 2.4),
        new(8.9, 0, (-0.3, 0.1, 0.5), (-30, -4, 0), 5),
        new(9.5, 0, (-0.1, 0.1, 0.5), (-30, -2, 0), 5),
        new(10.6, 0, (1.1, 0.25, -0.35), (-22, 2, 0), 3.4),
        new(10.9, 0, (1.15, 0.25, -0.35), (-22, 2, 0), 3.3),
        new(12.15, 0, (-0.6, 0, 0.3), (-34, 0, 0), 3.9),
        new(13.75, 0, (-0.45, 0, 0.3), (-34, 1, 0), 3.8),
        // EASE as it stands letter by letter, then one slow pull back to the definition, and the punch line.
        new(14.95, 1, (4.7, 0.3, -1), (-12, 1, 0), 2.8),
        new(15.85, 1, (5, 0.3, -1), (-12, 0, 0), 2.8),
        new(17.25, 1, (5.6, 0, 0.4), (-30, 2, 0), 5),
        new(18.15, 1, (5.7, 0, 0.4), (-30, 2, 0), 5),
        new(18.9, 1, (5.1, 0, 0.1), (-34, 0, 0), 4.2),
        new(21.25, 1, (5.2, 0, 0.1), (-34, 1, 0), 4.05),
        // The BOUNCE definition, BOUNCE from near the ground, and the punch line as the dot leaves.
        new(22.35, 2, (10.6, 0, 0.6), (-30, 0, 0), 5),
        new(24.2, 2, (10.7, 0, 0.6), (-30, 0, 0), 4.8),
        new(25.4, 2, (10.5, 0.48, 0), (4, -3, 0), 3.5),
        new(Landing(0), 2, (10.5, 0.48, 0), (4, -2.5, 0), 3.5),
        new(Landing(0) + 0.4, 2, (10.5, 0.48, 0), (4, -2, 0), 3.15, Out.Back),
        new(28.5, 2, (10.9, 0.48, 0), (4, 1, 0), 3.3),
        new(29.6, 2, (10.5, 0, 0.4), (-34, 0, 0), 3.9),
        new(31.75, 2, (10.65, 0, 0.4), (-34, 1, 0), 3.8),
        // ELASTIC, the definition, and the punch line.
        new(32.75, 3, (15.3, 0.3, -0.4), (-12, 0, 0), 3.2),
        new(33.15, 3, (15.4, 0.3, -0.4), (-12, 0, 0), 3.2),
        new(33.95, 3, (16.1, 0, 0.5), (-28, 0, 0), 5),
        new(34.55, 3, (16.2, 0, 0.5), (-28, 0, 0), 5),
        new(35.45, 3, (15.6, 0, 0.2), (-34, 0, 0), 3.9),
        new(37.75, 3, (15.75, 0, 0.2), (-34, 1, 0), 3.8),
        // Along KEYFRAMES as its letters stand, the definition, the punch line, and on to the wordmark.
        new(38.9, 4, (20, 0.25, -0.2), (-24, -8, 0), 3),
        new(40.45, 4, (21, 0.25, -0.2), (-24, -2, 0), 3),
        new(41.15, 4, (21.6, 0, 0.6), (-30, 0, 0), 4.8),
        new(41.9, 4, (21.7, 0, 0.6), (-30, 1, 0), 4.8),
        new(42.75, 4, (21.25, 0, 0.3), (-34, 0, 0), 3.9),
        new(44.85, 4, (21.4, 0, 0.3), (-34, 1, 0), 3.8),
        new(46.35, 5, (27.6, 0.5, -0.3), (-18, 0, 0), 6.2),
        new(49.855, 5, (27.6, 0.45, -0.3), (-18, 0, 0), 5.8),
        // Settle on the wordmark with the subtitle and taglines, and drift in until the loop ends.
        new(51.6, 5, (27.6, 0.25, 0.25), (-16, 0, 0), 5.6, InOut.Sine),
        new(54.75, 5, (27.6, 0.25, 0.2), (-16, 0, 0), 5.4),
    ];

    private static Vector3 Vector((double X, double Y, double Z) v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private Tweens.Keyframes Rig() => Timeline([
        .. Shots.Select(shot =>
        {
            var station = stations[shot.Station];
            var turn = Vector(shot.Turn) + new Vector3(0, station.RotationDegrees.Y, 0);
            return Key.At(T(shot.At), position: station.Transform * Vector(shot.Target), rotationDegrees: turn, interpolation: shot.Ease);
        }),
    ]);

    private Tweens.Keyframes Lens() => Timeline([.. Shots.Select(shot => Key.At(T(shot.At), z: shot.Distance, interpolation: shot.Ease))]);

    /// <summary>Paper fades in, and fades back over the jump to the loop's first keys.</summary>
    private Tweens.Keyframes Fade() => Timeline([
        Key.At(0, alpha: 1), Key.At(T(0.6), alpha: 0), Key.At(T(54.4), alpha: 0), Key.At(T(54.75), alpha: 1),
    ]);

    /// <summary>A piece of the credit floats up into view from below and keeps drifting up, then floats away like a
    /// released balloon, turning a little.</summary>
    private Tweens.Keyframes Float(Node3D piece, int i, int n, double at, double shift)
    {
        var (x, y) = (piece.Position.X, piece.Position.Y);
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
        ], shift);
    }

    /// <summary>A definition word is printed in turn. The headword and part of speech stay and blow away later; the
    /// rest fade when the definition shrinks.</summary>
    private Tweens.Keyframes Printed(Label3D word, int i, Moments at, double shift)
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
        return Timeline(keys, shift);
    }

    /// <summary>A new word pops in after the headword, one per 8th, then blows away with it.</summary>
    private Tweens.Keyframes Extra(Label3D word, int j, Moments at, double shift)
    {
        var (x, z) = (word.Position.X, word.Position.Z);
        var shown = at.Extend + Eighth * j;
        List<Key> keys = [
            Key.At(0, x: x, y: Lift, z: z, rotationDegrees: (-90, 0, 0), scale: 0.01, transparency: 1),
            Key.At(T(shown), scale: 0.01, transparency: 1),
            Key.At(T(shown + 0.15), transparency: 0), Key.At(T(shown + 0.3), scale: 1, interpolation: Out.Back),
        ];
        Blow(keys, x, z, 2 + j, at.Blow);
        return Timeline(keys, shift);
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

    /// <summary>Halfway along a smooth bend from leaving one station to entering the next.</summary>
    private Vector3 Bend(int station, Vector3 from, Vector3 to)
        => (from + to) / 2 + (stations[station].Basis.X - stations[station + 1].Basis.X) * from.DistanceTo(to) / 8;

    /// <summary>The dot's path over the page: straight along each station, curving from one to the next.</summary>
    private Tweens.Keyframes Route()
    {
        List<Key> keys = [];
        void Visit(double at, Vector3 point, Interpolation? ease = null) => keys.Add(Key.At(T(at), x: point.X, z: point.Z, interpolation: ease));
        Vector3 start = On(0, new Vector3(Middle(from), 0, 0)), end = On(0, new Vector3(Middle(to), 0, 0));
        var ruler = ticks.Select(tick => On(1, new Vector3(Page(tick).X, 0, 0))).ToArray();
        Visit(0, start);
        Visit(8.605, start);
        Visit(11.605, end, InOut.Sine);
        Visit(12.355, end);
        // Curves over to the ruler and waits at its start.
        Visit((12.355 + Tick(0) - Eighth) / 2, Bend(0, end, ruler[0]));
        Visit(Tick(0) - Eighth, ruler[0]);
        Visit(Tick(0), ruler[0]);
        // One InOut.Cubic segment: the dot reaches each tick of the ruler on a beat.
        Visit(Tick(8), ruler[8], InOut.Cubic);
        Visit(Bars(7) + Beat, ruler[8]);
        // Speeds up into the first hop.
        var liftOff = On(2, new Vector3(Page(bounce[0]).X - 0.6f, 0, 0));
        Visit((Bars(7) + Beat + Takeoff) / 2, Bend(1, ruler[8], liftOff));
        Visit(Takeoff, liftOff);
        for (var i = 0; i < bounce.Length; i++)
            Visit(Landing(i), On(2, new Vector3(Page(bounce[i]).X, 0, 0)), Interpolation.Linear);
        float off = Page(bounce[^1]).X;
        Visit(Landing(6), On(2, new Vector3(off + 1, 0, 0)), Interpolation.Linear);
        var rebound = On(2, new Vector3(off + 1.5f, 0, 0));
        Visit(Landing(6) + Eighth, rebound);
        Vector3 into = On(3, new Vector3(Page(elastic[0]).X, 0, 0.35f)), past = On(3, new Vector3(Page(elastic[^1]).X, 0, 0.35f));
        Visit((Landing(6) + Eighth + Stretch(0)) / 2, Bend(2, rebound, into));
        Visit(Stretch(0), into);
        Visit(Stretch(6), past, Interpolation.Linear);
        // Smooth interpolation curves the path through every diamond.
        var waypoints = diamonds.Select(diamond => On(4, Page(diamond))).ToArray();
        Visit((Stretch(6) + Waypoint(0)) / 2, Bend(3, past, waypoints[0]));
        for (var i = 0; i < diamonds.Length; i++) Visit(Waypoint(i), waypoints[i]);
        var front = On(5, new Vector3(period.X, 0, 0.25f));
        Visit((Waypoint(8) + Bars(15)) / 2, Bend(4, waypoints[^1], front));
        Visit(Bars(15), front);
        Visit(48.73, front);
        Visit(Bars(16), On(5, period), Out.Back);
        return Timeline(keys);
    }

    /// <summary>The disc above the carrier: it pops into the sky, drops, hops over BOUNCE, squashes and stretches, and
    /// turns into the period.</summary>
    private Tweens.Keyframes Disc()
    {
        var top = cap + Radius;
        List<Key> keys = [
            Key.At(0, y: 1.6, scale: (0.01, 0.01, 0.01), modulate: TweensBlue), Key.At(T(0.25), scale: (0.01, 0.01, 0.01)),
            Key.At(T(0.85), scale: (1, 1, 1), interpolation: Out.Back),
            Key.At(T(2.6), y: 1.66), Key.At(T(Bars(1)), y: 1.6),
            Key.At(T(Bars(1) + Eighth), y: Radius, interpolation: In.Quad), Key.At(T(4.67), y: Radius + 0.3, interpolation: Out.Quad),
            Key.At(T(Bars(1) + Beat), y: Radius, interpolation: In.Quad), Key.At(T(Takeoff), y: Radius),
            Key.At(T(Landing(5) + Eighth), y: top + 0.5, interpolation: Out.Quad), Key.At(T(Landing(6)), y: Radius, interpolation: In.Quad),
            Key.At(T(Landing(6) + 0.185), y: Radius + 0.3, interpolation: Out.Quad),
            Key.At(T(Landing(6) + Eighth), y: Radius, interpolation: In.Quad),
            Key.At(T(48.73), y: Radius, scale: (1, 1, 1), modulate: TweensBlue), Key.At(T(49), modulate: GdGreen),
            // Becomes the wordmark's period.
            Key.At(T(Bars(16)), y: period.Y, scale: periodScale, interpolation: Out.Back),
        ];
        for (var i = 0; i < bounce.Length; i++)
        {
            // A shorter hop rises less, as under gravity.
            var flight = Landing(i) - (i == 0 ? Takeoff : Landing(i - 1));
            keys.Add(Key.At(T(Landing(i) - flight / 2), y: top + 0.7 * flight * flight / (Beat * Beat), interpolation: Out.Quad));
            keys.Add(Key.At(T(Landing(i)), y: top, interpolation: In.Quad));
        }
        // Rolling over the page, it stretches as it speeds up and squeezes as it slows down.
        keys.AddRange([.. Roll(8.605, 11.605), .. Roll(12.355, Tick(0) - Eighth), .. Roll(Tick(0), Tick(8)), .. Stop(Bars(15), 1.5)]);
        // It stays stretched into the first hop and rounds out in the air.
        keys.AddRange([
            Key.At(T(Bars(7) + Beat), scale: (1, 1, 1)), Key.At(T(Takeoff - 0.225), scale: Stretched),
            Key.At(T(Takeoff), scale: Stretched), Key.At(T(Takeoff + 0.245), scale: (1, 1, 1)),
        ]);
        foreach (var landing in new[] { Bars(1) + Eighth, Bars(1) + Beat, Landing(6) }.Concat(bounce.Select((_, i) => Landing(i))))
        {
            keys.Add(Key.At(T(landing), scale: (1, 1, 1)));
            keys.Add(Key.At(T(landing + 0.05), scale: (1.3, 0.72, 1)));
            keys.Add(Key.At(T(landing + 0.13), scale: (0.9, 1.12, 1)));
            keys.Add(Key.At(T(landing + 0.28), scale: (1, 1, 1)));
        }
        return Timeline(keys);
    }

    /// <summary>The rim stays a speck behind the disc until it swells to the wordmark's outline in the period.</summary>
    private Tweens.Keyframes Rim() => Timeline([
        Key.At(0, scale: 0.01), Key.At(T(48.73), scale: 0.01), Key.At(T(Bars(16)), scale: rimScale, interpolation: Out.Back),
    ]);

    private static readonly Vec Stretched = (1.1, 0.92, 1), Squeezed = (0.92, 1.08, 1);

    /// <summary>A roll from rest to rest: the disc stretches along the page while it speeds up and squeezes while it
    /// slows down.</summary>
    private static List<Key> Roll(double start, double end)
    {
        var span = end - start;
        return [Key.At(T(start), scale: (1, 1, 1)), Key.At(T(start + 0.25 * span), scale: Stretched), .. Stop(end, 0.5 * span)];
    }

    /// <summary>The disc squeezes while it brakes and wobbles round again once it stops.</summary>
    private static List<Key> Stop(double at, double braking) => [
        Key.At(T(at - braking), scale: (1, 1, 1)), Key.At(T(at - 0.4 * braking), scale: Squeezed),
        Key.At(T(at + 0.15), scale: (1.03, 0.98, 1)), Key.At(T(at + 0.35), scale: (1, 1, 1)),
    ];

    /// <summary>BOUNCE stands up with a bounce, and each letter squashes under the landing dot.</summary>
    private Tweens.Keyframes Bounce(int i, double shift)
    {
        var keys = Standing(Takeoff + 0.08 * i, Out.Bounce, 0.6);
        var landing = Landing(i);
        keys.AddRange([
            Key.At(0, scale: (1, 1, 1)), Key.At(T(landing), scale: (1, 1, 1)), Key.At(T(landing + 0.06), scale: (1.15, 0.78, 1)),
            Key.At(T(landing + 0.45), scale: (1, 1, 1), interpolation: Out.Back),
        ]);
        return Timeline(keys, shift);
    }

    /// <summary>ELASTIC stands up elastically, and each letter stretches as the dot passes and springs back.</summary>
    private Tweens.Keyframes Elastic(int i, double shift)
    {
        var keys = Standing(Bars(10) + 3 * Eighth + 0.06 * i, Out.Elastic, 0.8);
        var pass = Stretch(i);
        keys.AddRange([
            Key.At(0, scale: (1, 1, 1)), Key.At(T(pass), scale: (1, 1, 1)), Key.At(T(pass + 0.1), scale: (1.6, 0.8, 1)),
            Key.At(T(pass + 1), scale: (1, 1, 1), interpolation: Out.Elastic),
        ]);
        return Timeline(keys, shift);
    }

    /// <summary>A keyframe diamond lights up and stands when the dot passes over it.</summary>
    private Tweens.Keyframes Diamond(int i, double shift)
    {
        var at = Waypoint(i);
        var keys = Standing(at, Out.Back, 0.4);
        keys.AddRange([
            Key.At(0, scale: 1, modulate: Sand), Key.At(T(at), scale: 1, modulate: Sand), Key.At(T(at + 0.08), scale: 1.6, modulate: Saffron),
            Key.At(T(at + 0.4), scale: 1, interpolation: Out.Back),
        ]);
        return Timeline(keys, shift);
    }

    /// <summary>A wordmark letter falls onto the page on its 8th, the first on bar 15, and squashes on landing.</summary>
    private Tweens.Keyframes Drop(int i, double shift)
    {
        var at = Bars(15) + Eighth * (i - 1);
        return Timeline([
            Key.At(0, y: 3.5, scale: (0.01, 0.01, 0.01)), Key.At(T(at), y: 3.5, scale: (0.01, 0.01, 0.01)),
            Key.At(T(at + 0.1), scale: (1, 1, 1)),
            Key.At(T(at + Eighth), y: 0, scale: (1, 1, 1), interpolation: In.Quad),
            Key.At(T(at + Eighth + 0.08), scale: (1.18, 0.8, 1)), Key.At(T(at + Eighth + 0.4), scale: (1, 1, 1), interpolation: Out.Back),
        ], shift);
    }

    // Where the dot lands on the page: its drop, the end of BOUNCE, and the period.
    private (double At, Vector3 Spot)[] Landings() => [
        (Bars(1) + Eighth, On(0, new Vector3(Middle(from), 0, 0))), (Landing(6), On(2, new Vector3(Page(bounce[^1]).X + 1, 0, 0))),
        (Bars(16), On(5, period)),
    ];

    /// <summary>Rings spread over the page from every landing.</summary>
    private Tweens.Keyframes Ripple(int i)
    {
        var first = Landings()[0].Spot;
        List<Key> keys = [Key.At(0, x: first.X, z: first.Z, scale: 0.2, transparency: 1)];
        foreach (var (time, spot) in Landings())
        {
            var at = time + 0.12 * i;
            keys.Add(Key.At(T(at - 0.02), x: spot.X, z: spot.Z, scale: 0.2, transparency: 1));
            keys.Add(Key.At(T(at), transparency: 0.2));
            keys.Add(Key.At(T(at + 1.2), scale: 2.6 + 0.6 * i, transparency: 1, interpolation: Out.Cubic));
        }
        return Timeline(keys);
    }

    private static (string, Variant) Ratio(double amount) => ("amount_ratio", amount);
    private static (string, Variant) Speed(double scale) => ("speed_scale", scale);

    /// <summary>The trail lays dots from the dot's first landing and fades out before the wordmark appears. Under the
    /// closing fade, a burst of speed ages every dot out.</summary>
    private Tweens.Keyframes Lay() => Timeline([
        Key.At(0, Ratio(0), Speed(1)), Key.At(T(4.46), Ratio(0)), Key.At(T(4.5), Ratio(1)),
        Key.At(T(45.655), Ratio(1)), Key.At(T(45.675), Ratio(0)),
        Key.At(0, transparency: 0), Key.At(T(44.875), transparency: 0), Key.At(T(45.675), transparency: 1),
        Key.At(T(54.76), Speed(1)), Key.At(T(54.78), Speed(400)), Key.At(T(54.99), Speed(400)), Key.At(100, Speed(1)),
    ]);

    /// <summary>Every touchdown of the disc and the height it lands at: its drop and rebound, each hop onto a BOUNCE
    /// letter, and the landing after BOUNCE and its rebound.</summary>
    private (double At, float Height)[] Contacts() => [
        (Bars(1) + Eighth, 0), (Bars(1) + Beat, 0), .. bounce.Select((_, i) => (Landing(i), cap)), (Landing(6), 0), (Landing(6) + Eighth, 0),
    ];

    /// <summary>A spray of small leaves at every touchdown of the disc.</summary>
    private Tweens.Keyframes Impact()
    {
        List<Key> keys = [Key.At(0, y: 0.02), Key.At(0, Ratio(0))];
        foreach (var (at, height) in Contacts())
        {
            keys.AddRange([
                Key.At(T(at - 0.04), y: height + 0.02), Key.At(T(at - 0.01), Ratio(0)), Key.At(T(at), Ratio(1)),
                Key.At(T(at + 0.04), Ratio(1)), Key.At(T(at + 0.05), Ratio(0)),
            ]);
        }
        return Timeline(keys);
    }

    /// <summary>Leaves thrown into the air at every landing on the page, and along the wordmark as its letters land.</summary>
    private Tweens.Keyframes Toss()
    {
        var first = Landings()[0].Spot;
        List<Key> keys = [Key.At(0, x: first.X, z: first.Z), Key.At(0, Ratio(0))];
        void Throw(double at, Vector3 spot, double open) => keys.AddRange([
            Key.At(T(at - 0.03), x: spot.X, z: spot.Z), Key.At(T(at - 0.02), Ratio(0)), Key.At(T(at), Ratio(1)),
            Key.At(T(at + open), Ratio(1)), Key.At(T(at + open + 0.02), Ratio(0)),
        ]);
        foreach (var (time, spot) in Landings()) Throw(time, spot, 0.1);
        // A smaller throw as each wordmark letter lands, just in front of it, so the leaves fly up before the letters.
        for (var i = 0; i < logo.Length; i++) Throw(Bars(15) + Eighth * i, On(5, Page(logo[i]) + Vector3.Back * 0.3f), 0.05);
        return Timeline(keys);
    }

    /// <summary>Confetti bursts from the wordmark when the dot becomes its period.</summary>
    private Tweens.Keyframes Burst(double shift) => Timeline([
        Key.At(0, Ratio(0)), Key.At(T(49.13), Ratio(0)), Key.At(T(49.15), Ratio(1)), Key.At(T(49.5), Ratio(1)), Key.At(T(49.53), Ratio(0)),
    ], shift);
}
