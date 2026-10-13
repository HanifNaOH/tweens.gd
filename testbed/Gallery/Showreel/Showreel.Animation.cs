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
    private enum Scene { Opening, FromTo, Ease, Bounce, Elastic, Keyframes, Wordmark, Closing }

    // The cue sheet: when each scene starts on the track. Times in a scene count from its cue, so moving a cue moves the
    // scene with its camera shots and the dot's path through it. The opening is tied to the start of the loop.
    private readonly Dictionary<Scene, double> cues = new()
    {
        [Scene.FromTo] = Bars(2), [Scene.Ease] = Bars(4) + 3 * Eighth, [Scene.Bounce] = Bars(8), [Scene.Elastic] = Bars(10),
        [Scene.Keyframes] = Bars(12) + 3 * Eighth, [Scene.Wordmark] = Bars(15), [Scene.Closing] = Bars(16) + 2 * Beat,
    };

    // Each station word gets a close-up as it stands, each definition a wider frame, and each punch line a close one
    // that drifts after the leaving dot. Moves take a second or more and start early, so the camera arrives as things
    // happen. A shot with a scene is timed from its cue; After follows the shot before it by the seconds of the move.
    private static readonly Shot[] Shots = [
        // Up at the dot in the sky, then down to the page with it.
        new(Scene.Opening, 0, (-2.06, 2.1, 0), (25, -4, 0), 4.4),
        After(2.6, (-2.06, 2.15, 0), (23, 2, 0), 4.4),
        After(1.4, (-2.06, 2.1, 0), (22, 0, 0), 4.4),
        After(1.2, (-1.9, 0.4, 0.1), (-22, -10, 0), 4.2),
        // FROM, the definition, and the punch line, held until its words have nearly blown away.
        new(Scene.FromTo, -0.805, (-2, 0.32, -0.45), (-14, -6, 0), 3),
        After(0.8, (-2.06, 0.34, -0.6), (-15, -4, 0), 2.4),
        After(0.5, (-2, 0.34, -0.6), (-15, -3, 0), 2.4),
        After(1.8, (-0.3, 0.1, 0.5), (-30, -4, 0), 5),
        After(1.1, (-0.1, 0.1, 0.5), (-30, -2, 0), 5),
        After(1.25, (-0.6, 0, 0.3), (-34, 0, 0), 3.9),
        After(4.15, (0.4, 0.1, 0.3), (-34, 1, 0), 4.6),
        // A slow pan over to EASE and its definition as its last letter stands, and the punch line.
        new(Scene.Ease, 3.17, (5.6, 0, 0.4), (-30, 2, 0), 5),
        After(0.9, (5.7, 0, 0.4), (-30, 2, 0), 5),
        After(0.75, (5.1, 0, 0.1), (-34, 0, 0), 4.2),
        After(2.35, (5.2, 0, 0.1), (-34, 1, 0), 4.05),
        // The BOUNCE definition, BOUNCE from near the ground, and the punch line as the dot leaves.
        new(Scene.Bounce, -2.755, (10.6, 0, 0.6), (-30, 0, 0), 5),
        After(1.85, (10.7, 0, 0.6), (-30, 0, 0), 4.8),
        After(1.2, (10.5, 0.48, 0), (4, -3, 0), 3.5),
        new(Scene.Bounce, Hop, (10.5, 0.48, 0), (4, -2.5, 0), 3.5),
        After(0.4, (10.5, 0.48, 0), (4, -2, 0), 3.15),
        After(2.395, (10.9, 0.48, 0), (4, 1, 0), 3.3),
        After(1.1, (10.5, 0, 0.4), (-34, 0, 0), 3.9),
        After(2.15, (11.6, 0, 0.4), (-34, 1, 0), 3.8),
        // ELASTIC, the definition, and the punch line.
        new(Scene.Elastic, 1.645, (15.3, 0.3, -0.4), (-12, 0, 0), 3.2),
        After(0.4, (15.4, 0.3, -0.4), (-12, 0, 0), 3.2),
        After(0.8, (16.1, 0, 0.5), (-28, 0, 0), 5),
        After(0.6, (16.2, 0, 0.5), (-28, 0, 0), 5),
        After(0.9, (15.6, 0, 0.2), (-34, 0, 0), 3.9),
        After(2.3, (16.5, 0, 0.2), (-34, 1, 0), 3.8),
        // Along KEYFRAMES as its letters stand, the definition, the punch line, and on to the wordmark.
        new(Scene.Keyframes, 0.67, (20, 0.25, -0.2), (-24, -8, 0), 3),
        After(1.55, (21, 0.25, -0.2), (-24, -2, 0), 3),
        After(0.7, (21.6, 0, 0.6), (-30, 0, 0), 4.8),
        After(0.75, (21.7, 0, 0.6), (-30, 1, 0), 4.8),
        After(0.85, (21.25, 0, 0.3), (-34, 0, 0), 3.9),
        After(2.1, (22.2, 0, 0.3), (-34, 1, 0), 3.8),
        new(Scene.Wordmark, 0.245, (27.6, 0.5, -0.3), (-18, 0, 0), 6.2),
        After(3.505, (27.6, 0.45, -0.3), (-18, 0, 0), 5.8),
        // Settle on the wordmark with the subtitle and taglines, and drift in until the loop ends.
        new(Scene.Closing, 0.995, (27.6, 0.25, 0.25), (-16, 0, 0), 5.6),
        After(3.15, (27.6, 0.25, 0.2), (-16, 0, 0), 5.4),
    ];

    private double Cue(Scene scene) => scene == Scene.Opening ? 0 : cues[scene];

    /// <summary>The opening shares the first station with FROM TO, and the closing the last with the wordmark.</summary>
    private static int Station(Scene scene) => Math.Clamp((int)scene - 1, 0, 5);

    private async Task AnimateAsync()
    {
        // Start every scene before awaiting any, so a bad key throws here. A scene task that failed later would only
        // report once the infinite loops of all the others end.
        Group[] plays = [
            .. Journey(), .. OpeningCredit(), .. FromTo(), .. EaseScene(), .. BounceScene(), .. ElasticScene(),
            .. KeyframesScene(), .. Wordmark(), .. Closing(),
        ];
        await Task.WhenAll(plays.Select(play => play.End));
    }

    /// <summary>The camera, travelling dot and landing effects run across scene boundaries.</summary>
    private Group[] Journey()
    {
        // The trail keeps each dot it lays for exactly one loop.
        trail.Lifetime = Length * Tempo;
        var route = Route();
        return [
            fade.Tween(Fade()), rig.Tween(Rig()), lens.Tween(Lens()),
            // One definition moves both the dot and the emitter that lays its trail.
            dot.Tween(route), trail.Tween(route), trail.Tween(Lay()),
            disc.Tween(Disc()), rim.Tween(Rim()), leaves.Tween(Toss()), impact.Tween(Impact()),
            .. ripples.Select((ripple, i) => ripple.Tween(Ripple(i))),
        ];
    }

    private Group[] OpeningCredit() => [
        godot.Tween(Float(godot, 0, credit.Length, Bars(0) + Eighth)),
        .. credit.Select((letter, i) => letter.Tween(Float(letter, i, i, Bars(0)))),
    ];

    private Group[] FromTo()
    {
        var at = Cue(Scene.FromTo);
        return [
            .. Gloss(Scene.FromTo, new(at + Eighth, at + Bar, at + Bar + Beat, at + 2 * Bar + 2 * Beat)),
            .. from.Select((letter, i) => letter.Tween(Timeline(Standing(at + 0.08 * i, Out.Back)))),
            .. to.Select((letter, i) => letter.Tween(Timeline(Standing(at + Bar + Eighth + 0.08 * i, Out.Back)))),
        ];
    }

    private Group[] EaseScene()
    {
        var at = Cue(Scene.Ease);
        return [
            .. Gloss(Scene.Ease, new(at + 1.12, at + 4, at + 4 + Eighth, at + 2 * Bar + Beat + Eighth)),
            .. ticks.Select((tick, i) => tick.Tween(Timeline(Standing(Tick(i), Out.Back, 0.35)))),
            .. ease.Select((letter, i) => letter.Tween(Timeline(Standing(Tick(i + 1), InOut.Cubic, 0.9)))),
        ];
    }

    private Group[] BounceScene()
    {
        var at = Cue(Scene.Bounce);
        return [
            .. Gloss(Scene.Bounce, new(at - 3.125, Landing(5) - Eighth, Landing(5), at + 2 * Bar + Beat)),
            .. bounce.Select((letter, i) => letter.Tween(Bounce(i))),
        ];
    }

    private Group[] ElasticScene()
    {
        var at = Cue(Scene.Elastic);
        return [
            .. Gloss(Scene.Elastic, new(at + Beat, at + Bar + Eighth, at + Bar + Beat, at + 2 * Bar + Beat)),
            .. elastic.Select((letter, i) => letter.Tween(Elastic(i))),
        ];
    }

    private Group[] KeyframesScene()
    {
        var at = Cue(Scene.Keyframes);
        return [
            .. Gloss(Scene.Keyframes, new(at + Beat, at + Bar + Beat, at + Bar + Beat + Eighth, at + 2 * Bar + Beat)),
            .. diamonds.Select((diamond, i) => diamond.Tween(Diamond(i))),
            .. keyframes.Select((letter, i) => letter.Tween(Timeline(Standing(Waypoint(i) + 0.1, Out.Back, 0.4)))),
        ];
    }

    private Group[] Wordmark() => [
        .. logo.Select((letter, i) => letter.Tween(Drop(i))),
        confetti.Tween(Burst()), subtitle.Tween(Timeline(Standing(Dotted + Eighth, Out.Back))),
    ];

    private Group[] Closing() => [
        .. taglines.Select((line, i) => line.Tween(Timeline(Standing(Cue(Scene.Closing) + Eighth * i, Out.Back)))),
    ];

    /// <summary>Print a definition, shorten it to a punch line, then blow it away.</summary>
    private Group[] Gloss(Scene scene, Moments at)
    {
        if (!(at.Print < at.Shrink && at.Shrink <= at.Extend && at.Extend < at.Blow))
            throw new ArgumentException($"The {scene} definition must print, shrink, extend and blow away in that order.");
        var (words, extra) = glosses[Station(scene)];
        return [
            .. words.Select((word, i) => word.Tween(Printed(word, i, at))),
            .. extra.Select((word, j) => word.Tween(Extra(word, j, at))),
        ];
    }

    private const double Length = 55;

    /// <summary>Converts seconds on the loop to a percentage stop.</summary>
    private static Percent T(double seconds) => seconds is >= 0 and <= Length ? Percent.Of(seconds / Length * 100)
        : throw new ArgumentOutOfRangeException(nameof(seconds), $"A key at {seconds:0.###} s falls outside the {Length} s loop.");

    private Tweens.Keyframes Timeline(List<Key> keys) => new([.. keys],
        options: new TweenOptions { Duration = Length * Tempo, Repeats = TweenOptions.Infinite });

    // The track's grid: 80 bpm in 4/4, its first downbeat at 1.105 s.
    private const double Beat = 0.75, Eighth = Beat / 2, Bar = 4 * Beat;
    /// <summary>The downbeat <paramref name="bars"/> bars after the first one.</summary>
    private static double Bars(double bars) => 1.105 + Bar * bars;

    // When the dot reaches each ruler tick, BOUNCE letter, ELASTIC letter and keyframe diamond.
    private double Tick(int i) => Cue(Scene.Ease) + Beat * i;
    // The hops land 0.6 s after the bar; the hop from U to N is half as long, for the track's double bass drum hit.
    private const double Hop = 0.6;
    private double Landing(int i) => Cue(Scene.Bounce) + Hop + Beat * i - (i > 2 ? Eighth : 0);
    private double Takeoff => Landing(0) - Beat;
    private double Stretch(int i) => Cue(Scene.Elastic) + 3 * Beat + Eighth * i;
    private double Waypoint(int i) => Cue(Scene.Keyframes) + Beat * i;
    // When the dot walks from FROM to TO, runs up to BOUNCE, stops in front of the wordmark, and becomes its period.
    private double Walk => Cue(Scene.FromTo) + 2 * Beat;
    private double RunUp => Cue(Scene.Bounce) - 3 * Beat;
    private double Parked => Cue(Scene.Wordmark) + 2 * Beat;
    private double Dotted => Cue(Scene.Wordmark) + Bar;

    /// <summary>When a definition is printed, shrinks to its headword, gains new words, and blows away.</summary>
    private readonly record struct Moments(double Print, double Shrink, double Extend, double Blow);

    /// <summary>Keys that stand a printed node up from the page, hinged at its foot.</summary>
    private static List<Key> Standing(double at, Interpolation ease, double seconds = 0.5) => [
        Key.At(0, rotationDegrees: (-90, 0, 0)), Key.At(T(at), rotationDegrees: (-90, 0, 0)),
        Key.At(T(at + seconds), rotationDegrees: (0, 0, 0), interpolation: ease),
    ];

    /// <summary>Where the camera looks: the rig sits on the target and turns, and the camera backs away from it by the
    /// distance. Target and turn are relative to the scene's station, so each scene is framed as if unturned. At counts
    /// seconds from the scene's cue, or without a scene, from the shot before. The camera glides into a shot unless it
    /// names another ease.</summary>
    private readonly record struct Shot(Scene? Scene, double At, (double X, double Y, double Z) Target,
        (double X, double Y, double Z) Turn, double Distance, Interpolation? Ease = null);

    /// <summary>A shot in the same scene as the one before it, <paramref name="seconds"/> later.</summary>
    private static Shot After(double seconds, (double X, double Y, double Z) target, (double X, double Y, double Z) turn,
        double distance, Interpolation? ease = null) => new(null, seconds, target, turn, distance, ease);

    /// <summary>Each shot's scene and time on the loop; every shot must come after the one before it.</summary>
    private (Scene Scene, double At)[] Timed()
    {
        var timed = new (Scene Scene, double At)[Shots.Length];
        for (var i = 0; i < Shots.Length; i++)
        {
            var shot = Shots[i];
            if (shot.Scene is not { } scene)
            {
                if (i == 0 || shot.At <= 0)
                    throw new InvalidOperationException($"A shot must follow the one before it by a positive time, not {shot.At:0.###} s.");
                timed[i] = (timed[i - 1].Scene, timed[i - 1].At + shot.At);
                continue;
            }
            timed[i] = (scene, Cue(scene) + shot.At);
            if (i > 0 && timed[i].At <= timed[i - 1].At)
            {
                throw new InvalidOperationException($"The shots before the {scene} shot at {shot.At:0.###} s from its cue run past " +
                    $"it; shorten them by more than {timed[i - 1].At - timed[i].At:0.###} s.");
            }
        }
        return timed;
    }

    // Each move starts and ends at rest without a jolt: velocity and acceleration are zero at every shot.
    private static readonly Interpolation Glide = InOut.SmootherStep;

    /// <summary>How the camera moves into a shot; the loop's first shot has no move into it.</summary>
    private static Interpolation? Move(Shot shot, double at) => at > 0 ? shot.Ease ?? Glide : null;

    private static Vector3 Vector((double X, double Y, double Z) v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private Tweens.Keyframes Rig() => Timeline([
        .. Timed().Select((timed, i) =>
        {
            var shot = Shots[i];
            var station = stations[Station(timed.Scene)];
            var turn = Vector(shot.Turn) + new Vector3(0, station.RotationDegrees.Y, 0);
            return Key.At(T(timed.At), position: station.Transform * Vector(shot.Target), rotationDegrees: turn, interpolation: Move(shot, timed.At));
        }),
    ]);

    private Tweens.Keyframes Lens() =>
        Timeline([.. Timed().Select((timed, i) => Key.At(T(timed.At), z: Shots[i].Distance, interpolation: Move(Shots[i], timed.At)))]);

    /// <summary>Paper fades in, and fades back over the jump to the loop's first keys.</summary>
    private Tweens.Keyframes Fade() => Timeline([
        Key.At(0, alpha: 1), Key.At(T(0.6), alpha: 0), Key.At(T(54.4), alpha: 0), Key.At(T(54.75), alpha: 1),
    ]);

    /// <summary>A piece of the credit floats up into view from below and keeps drifting up, then floats away like a
    /// released balloon, turning a little.</summary>
    private Tweens.Keyframes Float(Node3D piece, int i, int n, double at)
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

    /// <summary>Halfway along a smooth bend from leaving one station to entering the next.</summary>
    private Vector3 Bend(int station, Vector3 from, Vector3 to)
        => (from + to) / 2 + (stations[station].Basis.X - stations[station + 1].Basis.X) * from.DistanceTo(to) / 8;

    /// <summary>The dot's path over the page: straight along each station, curving from one to the next.</summary>
    private Tweens.Keyframes Route()
    {
        List<Key> keys = [];
        var last = double.NegativeInfinity;
        void Visit(double at, Vector3 point, Interpolation? ease = null)
        {
            if (at <= last) throw new InvalidOperationException($"The dot's path goes back in time at {at:0.###} s.");
            last = at;
            keys.Add(Key.At(T(at), x: point.X, z: point.Z, interpolation: ease));
        }

        Vector3 start = On(0, new Vector3(Middle(from), 0, 0)), end = On(0, new Vector3(Middle(to), 0, 0));
        var ruler = ticks.Select(tick => On(1, new Vector3(Page(tick).X, 0, 0))).ToArray();
        Visit(0, start);
        Visit(Walk, start);
        Visit(Walk + Bar, end, InOut.Sine);
        Visit(Walk + Bar + Beat, end);
        // Curves over to the ruler and waits at its start.
        Visit((Walk + Bar + Beat + Tick(0) - Eighth) / 2, Bend(0, end, ruler[0]));
        Visit(Tick(0) - Eighth, ruler[0]);
        Visit(Tick(0), ruler[0]);
        // One InOut.Cubic segment: the dot reaches each tick of the ruler on a beat.
        Visit(Tick(8), ruler[8], InOut.Cubic);
        Visit(RunUp, ruler[8]);
        // Speeds up into the first hop.
        var liftOff = On(2, new Vector3(Page(bounce[0]).X - 0.6f, 0, 0));
        Visit((RunUp + Takeoff) / 2, Bend(1, ruler[8], liftOff));
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
        Visit((Waypoint(8) + Parked) / 2, Bend(4, waypoints[^1], front));
        Visit(Parked, front);
        Visit(Dotted - Eighth, front);
        Visit(Dotted, On(5, period), Out.Back);
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
            Key.At(T(Dotted - Eighth), y: Radius, scale: (1, 1, 1), modulate: TweensBlue), Key.At(T(Dotted - 0.105), modulate: GdGreen),
            // Becomes the wordmark's period.
            Key.At(T(Dotted), y: period.Y, scale: periodScale, interpolation: Out.Back),
        ];
        for (var i = 0; i < bounce.Length; i++)
        {
            // A shorter hop rises less, as under gravity.
            var flight = Landing(i) - (i == 0 ? Takeoff : Landing(i - 1));
            keys.Add(Key.At(T(Landing(i) - flight / 2), y: top + 0.7 * flight * flight / (Beat * Beat), interpolation: Out.Quad));
            keys.Add(Key.At(T(Landing(i)), y: top, interpolation: In.Quad));
        }
        // Rolling over the page, it stretches as it speeds up and squeezes as it slows down.
        keys.AddRange([
            .. Roll(Walk, Walk + Bar), .. Roll(Walk + Bar + Beat, Tick(0) - Eighth), .. Roll(Tick(0), Tick(8)),
            .. Stop(Parked, 1.5),
        ]);
        // It stays stretched into the first hop and rounds out in the air.
        keys.AddRange([
            Key.At(T(RunUp), scale: (1, 1, 1)), Key.At(T(Takeoff - 0.225), scale: Stretched),
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
        Key.At(0, scale: 0.01), Key.At(T(Dotted - Eighth), scale: 0.01), Key.At(T(Dotted), scale: rimScale, interpolation: Out.Back),
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
    private Tweens.Keyframes Bounce(int i)
    {
        var keys = Standing(Takeoff + 0.08 * i, Out.Bounce, 0.6);
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
        var keys = Standing(Cue(Scene.Elastic) + 3 * Eighth + 0.06 * i, Out.Elastic, 0.8);
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

    /// <summary>A wordmark letter falls onto the page on its 8th, the first on the wordmark's cue, and squashes on
    /// landing.</summary>
    private Tweens.Keyframes Drop(int i)
    {
        var at = Cue(Scene.Wordmark) + Eighth * (i - 1);
        return Timeline([
            Key.At(0, y: 3.5, scale: (0.01, 0.01, 0.01)), Key.At(T(at), y: 3.5, scale: (0.01, 0.01, 0.01)),
            Key.At(T(at + 0.1), scale: (1, 1, 1)),
            Key.At(T(at + Eighth), y: 0, scale: (1, 1, 1), interpolation: In.Quad),
            Key.At(T(at + Eighth + 0.08), scale: (1.18, 0.8, 1)), Key.At(T(at + Eighth + 0.4), scale: (1, 1, 1), interpolation: Out.Back),
        ]);
    }

    // Where the dot lands on the page: its drop, the end of BOUNCE, and the period.
    private (double At, Vector3 Spot)[] Landings() => [
        (Bars(1) + Eighth, On(0, new Vector3(Middle(from), 0, 0))), (Landing(6), On(2, new Vector3(Page(bounce[^1]).X + 1, 0, 0))),
        (Dotted, On(5, period)),
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
    private Tweens.Keyframes Lay()
    {
        var gone = Cue(Scene.Wordmark) - 0.43;
        return Timeline([
            Key.At(0, Ratio(0), Speed(1)), Key.At(T(4.46), Ratio(0)), Key.At(T(4.5), Ratio(1)),
            Key.At(T(gone - 0.02), Ratio(1)), Key.At(T(gone), Ratio(0)),
            Key.At(0, transparency: 0), Key.At(T(gone - 0.8), transparency: 0), Key.At(T(gone), transparency: 1),
            Key.At(T(54.76), Speed(1)), Key.At(T(54.78), Speed(400)), Key.At(T(54.99), Speed(400)), Key.At(100, Speed(1)),
        ]);
    }

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
        for (var i = 0; i < logo.Length; i++) Throw(Cue(Scene.Wordmark) + Eighth * i, On(5, Page(logo[i]) + Vector3.Back * 0.3f), 0.05);
        return Timeline(keys);
    }

    /// <summary>Confetti bursts from the wordmark when the dot becomes its period.</summary>
    private Tweens.Keyframes Burst() => Timeline([
        Key.At(0, Ratio(0)), Key.At(T(Dotted + 0.025), Ratio(0)), Key.At(T(Dotted + 0.045), Ratio(1)),
        Key.At(T(Dotted + 0.395), Ratio(1)), Key.At(T(Dotted + 0.425), Ratio(0)),
    ]);
}
