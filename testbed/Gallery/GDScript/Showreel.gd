# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
extends "res://Gallery/GDScript/GalleryAnimation.gd"
# The scene is Showreel.tscn. "Along the Way": a dot travels a page from FROM to the period of TWEENS.GD, and each
# word it passes stands up and acts out its name. Every node plays keyframe definitions on one 55-second loop, timed
# to an 80 bpm track.

enum Scene { OPENING, FROM_TO, EASE, BOUNCE, ELASTIC, KEYFRAMES, WORDMARK, CLOSING }

const LENGTH = 55.0
const RADIUS = 0.14
const LIFT = 0.005
const SAFFRON = Color("f0a830")
const SAND = Color("e9c4a4")
const TWEENS_BLUE = Color("67a2dd")
const GD_GREEN = Color("9bd69c")
# The track's grid: 80 bpm in 4/4, its first downbeat at 1.105 s.
const BEAT = 0.75
const EIGHTH = BEAT / 2
const BAR = 4 * BEAT
# The hops land 0.6 s after the bar; the hop from U to N is half as long, for the track's double bass drum hit.
const HOP = 0.6
const STRETCHED = Vector3(1.1, 0.92, 1)
const SQUEEZED = Vector3(0.92, 1.08, 1)

# The cue sheet: when each scene starts on the track. Times in a scene count from its cue, so moving a cue moves the
# scene with its camera shots and the dot's path through it. The opening is tied to the start of the loop.
var cues := {
	Scene.FROM_TO: bars(2), Scene.EASE: bars(4) + 3 * EIGHTH, Scene.BOUNCE: bars(8), Scene.ELASTIC: bars(10),
	Scene.KEYFRAMES: bars(12) + 3 * EIGHTH, Scene.WORDMARK: bars(15), Scene.CLOSING: bars(16) + 2 * BEAT,
}

func cue(scene: Scene) -> float:
	if scene == Scene.OPENING: return 0.0
	return cues[scene]

## The opening shares the first station with FROM TO, and the closing the last with the wordmark.
static func station(scene: Scene) -> int:
	return clampi(scene - 1, 0, 5)

func animate() -> void:
	# Start every scene before waiting for any of its infinite tweens.
	var scenes: Array[Array] = [
		journey(), opening_credit(), from_to(), ease_scene(), bounce_scene(), elastic_scene(), keyframes_scene(),
		wordmark(), closing(),
	]
	for scene in scenes:
		for play: TweensGdGroup in scene: await play.wait()

## The camera, travelling dot and landing effects run across scene boundaries.
func journey() -> Array[TweensGdGroup]:
	# The trail keeps each dot it lays for exactly one loop.
	targets.trail.lifetime = LENGTH * tempo
	var path := route()
	var plays: Array[TweensGdGroup] = [
		Tweens.animate(targets.fade, fade()),
		Tweens.animate(targets.rig, rig()),
		Tweens.animate(targets.camera, lens()),
		# One definition moves both the dot and the emitter that lays its trail.
		Tweens.animate(targets.dot, path),
		Tweens.animate(targets.trail, path),
		Tweens.animate(targets.trail, lay()),
		Tweens.animate(targets.disc, disc()),
		Tweens.animate(targets.rim, rim()),
		Tweens.animate(targets.leaves, toss()),
		Tweens.animate(targets.impact, impact()),
	]
	for i in targets.ripples.size(): plays.append(Tweens.animate(targets.ripples[i], ripple(i)))
	return plays

func opening_credit() -> Array[TweensGdGroup]:
	var credit: Array = targets.credit
	var plays: Array[TweensGdGroup] = [
		Tweens.animate(targets.godot, float_up(targets.godot, 0, credit.size(), bars(0) + EIGHTH)),
	]
	for i in credit.size(): plays.append(Tweens.animate(credit[i], float_up(credit[i], i, i, bars(0))))
	return plays

func from_to() -> Array[TweensGdGroup]:
	var at := cue(Scene.FROM_TO)
	var plays := gloss(Scene.FROM_TO, moments(at + EIGHTH, at + BAR, at + BAR + BEAT, at + 2 * BAR + 2 * BEAT))
	for i in targets.from.size():
		plays.append(Tweens.animate(targets.from[i], timeline(standing(at + 0.08 * i, Out.BACK))))
	for i in targets.to.size():
		plays.append(Tweens.animate(targets.to[i], timeline(standing(at + BAR + EIGHTH + 0.08 * i, Out.BACK))))
	return plays

func ease_scene() -> Array[TweensGdGroup]:
	var at := cue(Scene.EASE)
	var plays := gloss(Scene.EASE, moments(at + 1.12, at + 4, at + 4 + EIGHTH, at + 2 * BAR + BEAT + EIGHTH))
	for i in targets.ticks.size(): plays.append(Tweens.animate(targets.ticks[i], timeline(standing(tick(i), Out.BACK, 0.35))))
	for i in targets.ease.size():
		plays.append(Tweens.animate(targets.ease[i], timeline(standing(tick(i + 1), InOut.CUBIC, 0.9))))
	return plays

func bounce_scene() -> Array[TweensGdGroup]:
	var at := cue(Scene.BOUNCE)
	var plays := gloss(Scene.BOUNCE, moments(at - 3.125, landing(5) - EIGHTH, landing(5), at + 2 * BAR + BEAT))
	for i in targets.bounce.size(): plays.append(Tweens.animate(targets.bounce[i], bounce(i)))
	return plays

func elastic_scene() -> Array[TweensGdGroup]:
	var at := cue(Scene.ELASTIC)
	var plays := gloss(Scene.ELASTIC, moments(at + BEAT, at + BAR + EIGHTH, at + BAR + BEAT, at + 2 * BAR + BEAT))
	for i in targets.elastic.size(): plays.append(Tweens.animate(targets.elastic[i], elastic(i)))
	return plays

func keyframes_scene() -> Array[TweensGdGroup]:
	var at := cue(Scene.KEYFRAMES)
	var plays := gloss(Scene.KEYFRAMES, moments(at + BEAT, at + BAR + BEAT, at + BAR + BEAT + EIGHTH, at + 2 * BAR + BEAT))
	for i in targets.diamonds.size(): plays.append(Tweens.animate(targets.diamonds[i], diamond(i)))
	for i in targets.keyframes.size():
		plays.append(Tweens.animate(targets.keyframes[i], timeline(standing(waypoint(i) + 0.1, Out.BACK, 0.4))))
	return plays

func wordmark() -> Array[TweensGdGroup]:
	var plays: Array[TweensGdGroup] = []
	for i in targets.logo.size(): plays.append(Tweens.animate(targets.logo[i], drop(i)))
	plays.append(Tweens.animate(targets.confetti, burst()))
	plays.append(Tweens.animate(targets.subtitle, timeline(standing(dotted() + EIGHTH, Out.BACK))))
	return plays

func closing() -> Array[TweensGdGroup]:
	var plays: Array[TweensGdGroup] = []
	for i in targets.taglines.size():
		plays.append(Tweens.animate(targets.taglines[i], timeline(standing(cue(Scene.CLOSING) + EIGHTH * i, Out.BACK))))
	return plays

## When a definition is printed, shrinks to its headword, gains new words, and blows away.
func moments(shown: float, shrunk: float, extended: float, blown: float) -> Dictionary:
	return {"print": shown, "shrink": shrunk, "extend": extended, "blow": blown}

## Print a definition, shorten it to a punch line, then blow it away.
func gloss(scene: Scene, at: Dictionary) -> Array[TweensGdGroup]:
	if not (at["print"] < at["shrink"] and at["shrink"] <= at["extend"] and at["extend"] < at["blow"]):
		push_error("The %s definition must print, shrink, extend and blow away in that order." % Scene.find_key(scene))
	var words: Array = targets.glosses[station(scene)].words
	var added: Array = targets.glosses[station(scene)].extra
	var plays: Array[TweensGdGroup] = []
	for i in words.size(): plays.append(Tweens.animate(words[i], printed(words[i], i, at)))
	for j in added.size(): plays.append(Tweens.animate(added[j], extra(added[j], j, at)))
	return plays

## Converts seconds on the loop to a percentage stop. A key outside the loop is reported, then kept at its edge.
func t(at: float) -> float:
	if at < 0 or at > LENGTH: push_error("A key at %.3f s falls outside the %d s loop." % [at, LENGTH])
	return clampf(at, 0, LENGTH) / LENGTH * 100.0

func timeline(frames: Dictionary) -> TweensGdKeyframes:
	var definition := Tweens.keyframes(frames, LENGTH * tempo)
	definition.options.repeats = Tweens.INFINITE
	return definition

## Adds channels at a stop, next to any channels already keyed there.
func key(frames: Dictionary, at: float, channels: Dictionary) -> void:
	var frame: Dictionary = frames.get_or_add(t(at), {})
	frame.merge(channels)

## The downbeat [param count] bars after the first one.
static func bars(count: float) -> float: return 1.105 + BAR * count

# When the dot reaches each ruler tick, BOUNCE letter, ELASTIC letter and keyframe diamond.
func tick(i: int) -> float: return cue(Scene.EASE) + BEAT * i
func landing(i: int) -> float: return cue(Scene.BOUNCE) + HOP + BEAT * i - (EIGHTH if i > 2 else 0.0)
func takeoff() -> float: return landing(0) - BEAT
func stretch(i: int) -> float: return cue(Scene.ELASTIC) + 3 * BEAT + EIGHTH * i
func waypoint(i: int) -> float: return cue(Scene.KEYFRAMES) + BEAT * i
# When the dot walks from FROM to TO, runs up to BOUNCE, and becomes the wordmark's period.
func walk() -> float: return cue(Scene.FROM_TO) + 2 * BEAT
func run_up() -> float: return cue(Scene.BOUNCE) - 3 * BEAT
func dotted() -> float: return cue(Scene.WORDMARK) + BAR

## A page point on a station, in the world.
func on(index: int, point: Vector3) -> Vector3:
	var node: Node3D = targets.stations[index]
	return node.transform * point

## A node's position on its station's page, through the groups it sits in.
func page(node: Node3D) -> Vector3:
	var point := node.position
	var parent := node.get_parent() as Node3D
	while not targets.stations.has(parent):
		point = parent.transform * point
		parent = parent.get_parent() as Node3D
	return point

func middle(letters: Array) -> float:
	return (page(letters[0]).x + page(letters[-1]).x) / 2

## Frames that stand a printed node up from the page, hinged at its foot.
func standing(at: float, easing: int, duration := 0.5) -> Dictionary:
	return {
		t(0): {"rotation_degrees": Vector3(-90, 0, 0)}, t(at): {"rotation_degrees": Vector3(-90, 0, 0)},
		t(at + duration): {"rotation_degrees": Vector3.ZERO, "interpolation": easing},
	}

## Where the camera looks: the rig sits on the target and turns, and the camera backs away from it by the distance.
## Target and turn are relative to the scene's station, so each scene is framed as if unturned. [param at] counts
## seconds from the scene's cue.
func shot(scene: Scene, at: float, target: Vector3, turn: Vector3, distance: float, easing: Variant = null) -> Dictionary:
	return {"scene": scene, "at": at, "target": target, "turn": turn, "distance": distance, "easing": easing}

## A shot in the same scene as the one before it, [param seconds] later.
func after(seconds: float, target: Vector3, turn: Vector3, distance: float, easing: Variant = null) -> Dictionary:
	return {"scene": null, "at": seconds, "target": target, "turn": turn, "distance": distance, "easing": easing}

func shots() -> Array[Dictionary]:
	# Each station word gets a close-up as it stands, each definition a wider frame, and each punch line a close one.
	# Moves take a second or more and start early, so the camera arrives as things happen. A shot with a scene is
	# timed from that scene's cue; after() follows the shot before it, with the seconds the camera takes to move into it.
	return [
		# Up at the dot in the sky, then down to the page with it.
		shot(Scene.OPENING, 0.0, Vector3(-2.06, 2.1, 0), Vector3(25, -4, 0), 4.4),
		after(2.6, Vector3(-2.06, 2.15, 0), Vector3(23, 2, 0), 4.4),
		after(1.4, Vector3(-2.06, 2.1, 0), Vector3(22, 0, 0), 4.4),
		after(1.2, Vector3(-1.9, 0.4, 0.1), Vector3(-22, -10, 0), 4.2, InOut.CUBIC),
		# FROM, the definition, and the punch line, held until its words have nearly blown away.
		shot(Scene.FROM_TO, -0.805, Vector3(-2, 0.32, -0.45), Vector3(-14, -6, 0), 3.0),
		after(0.8, Vector3(-2.06, 0.34, -0.6), Vector3(-15, -4, 0), 2.4),
		after(0.5, Vector3(-2, 0.34, -0.6), Vector3(-15, -3, 0), 2.4),
		after(1.8, Vector3(-0.3, 0.1, 0.5), Vector3(-30, -4, 0), 5.0),
		after(1.1, Vector3(-0.1, 0.1, 0.5), Vector3(-30, -2, 0), 5.0),
		after(1.25, Vector3(-0.6, 0, 0.3), Vector3(-34, 0, 0), 3.9),
		after(4.15, Vector3(-0.45, 0.1, 0.3), Vector3(-34, 1, 0), 3.8, InOut.SINE),
		# A slow pan over to EASE and its definition as its last letter stands, and the punch line.
		shot(Scene.EASE, 3.17, Vector3(5.6, 0, 0.4), Vector3(-30, 2, 0), 5.0, InOut.SINE),
		after(0.9, Vector3(5.7, 0, 0.4), Vector3(-30, 2, 0), 5.0),
		after(0.75, Vector3(5.1, 0, 0.1), Vector3(-34, 0, 0), 4.2),
		after(2.35, Vector3(5.2, 0, 0.1), Vector3(-34, 1, 0), 4.05),
		# The BOUNCE definition, BOUNCE from near the ground, and the punch line as the dot leaves.
		shot(Scene.BOUNCE, -2.755, Vector3(10.6, 0, 0.6), Vector3(-30, 0, 0), 5.0),
		after(1.85, Vector3(10.7, 0, 0.6), Vector3(-30, 0, 0), 4.8),
		after(1.2, Vector3(10.5, 0.48, 0), Vector3(4, -3, 0), 3.5),
		shot(Scene.BOUNCE, HOP, Vector3(10.5, 0.48, 0), Vector3(4, -2.5, 0), 3.5),
		after(0.4, Vector3(10.5, 0.48, 0), Vector3(4, -2, 0), 3.15, Out.BACK),
		after(2.395, Vector3(10.9, 0.48, 0), Vector3(4, 1, 0), 3.3),
		after(1.1, Vector3(10.5, 0, 0.4), Vector3(-34, 0, 0), 3.9),
		after(2.15, Vector3(10.65, 0, 0.4), Vector3(-34, 1, 0), 3.8),
		# ELASTIC, the definition, and the punch line.
		shot(Scene.ELASTIC, 1.645, Vector3(15.3, 0.3, -0.4), Vector3(-12, 0, 0), 3.2),
		after(0.4, Vector3(15.4, 0.3, -0.4), Vector3(-12, 0, 0), 3.2),
		after(0.8, Vector3(16.1, 0, 0.5), Vector3(-28, 0, 0), 5.0),
		after(0.6, Vector3(16.2, 0, 0.5), Vector3(-28, 0, 0), 5.0),
		after(0.9, Vector3(15.6, 0, 0.2), Vector3(-34, 0, 0), 3.9),
		after(2.3, Vector3(15.75, 0, 0.2), Vector3(-34, 1, 0), 3.8),
		# Along KEYFRAMES as its letters stand, the definition, the punch line, and on to the wordmark.
		shot(Scene.KEYFRAMES, 0.67, Vector3(20, 0.25, -0.2), Vector3(-24, -8, 0), 3.0),
		after(1.55, Vector3(21, 0.25, -0.2), Vector3(-24, -2, 0), 3.0),
		after(0.7, Vector3(21.6, 0, 0.6), Vector3(-30, 0, 0), 4.8),
		after(0.75, Vector3(21.7, 0, 0.6), Vector3(-30, 1, 0), 4.8),
		after(0.85, Vector3(21.25, 0, 0.3), Vector3(-34, 0, 0), 3.9),
		after(2.1, Vector3(21.4, 0, 0.3), Vector3(-34, 1, 0), 3.8),
		shot(Scene.WORDMARK, 0.245, Vector3(27.6, 0.5, -0.3), Vector3(-18, 0, 0), 6.2),
		after(3.505, Vector3(27.6, 0.45, -0.3), Vector3(-18, 0, 0), 5.8),
		# Settle on the wordmark with the subtitle and taglines, and drift in until the loop ends.
		shot(Scene.CLOSING, 0.995, Vector3(27.6, 0.25, 0.25), Vector3(-16, 0, 0), 5.6, InOut.SINE),
		after(3.15, Vector3(27.6, 0.25, 0.2), Vector3(-16, 0, 0), 5.4),
	]

## Each shot's scene and time on the loop, as [scene, time]; every shot must come after the one before it.
func timed(list: Array[Dictionary]) -> Array[Array]:
	var times: Array[Array] = []
	for entry in list:
		if entry.scene == null:
			if times.is_empty() or entry.at <= 0:
				push_error("A shot must follow the one before it by a positive time, not %.3f s." % entry.at)
			var previous: Array = times.back() if not times.is_empty() else [Scene.OPENING, 0.0]
			times.append([previous[0], previous[1] + entry.at])
			continue
		var at: float = cue(entry.scene) + entry.at
		if not times.is_empty() and at <= times.back()[1]:
			push_error("The shots before the %s shot at %.3f s from its cue run past it; shorten them by more than %.3f s."
				% [Scene.find_key(entry.scene), entry.at, times.back()[1] - at])
		times.append([entry.scene, at])
	return times

## Adds a shot's easing, if it has one, to its frame.
func eased(frame: Dictionary, entry: Dictionary) -> Dictionary:
	if entry.easing != null: frame["interpolation"] = entry.easing
	return frame

func rig() -> TweensGdKeyframes:
	var list := shots()
	var times := timed(list)
	var frames := {}
	for i in list.size():
		var node: Node3D = targets.stations[station(times[i][0])]
		var target: Vector3 = node.transform * list[i].target
		var turn: Vector3 = list[i].turn + Vector3(0, node.rotation_degrees.y, 0)
		frames[t(times[i][1])] = eased({"position": target, "rotation_degrees": turn}, list[i])
	return timeline(frames)

func lens() -> TweensGdKeyframes:
	var list := shots()
	var times := timed(list)
	var frames := {}
	for i in list.size(): frames[t(times[i][1])] = eased({"z": list[i].distance}, list[i])
	return timeline(frames)

## Paper fades in, and fades back over the jump to the loop's first keys.
func fade() -> TweensGdKeyframes:
	return timeline({t(0): {"alpha": 1.0}, t(0.6): {"alpha": 0.0}, t(54.4): {"alpha": 0.0}, t(54.75): {"alpha": 1.0}})

## A piece of the credit floats up into view from below and keeps drifting up, then floats away like a released
## balloon, turning a little.
func float_up(piece: Node3D, i: int, n: int, at: float) -> TweensGdKeyframes:
	var x := piece.position.x
	var y := piece.position.y
	var rise := at + 0.03 * i
	var release := 5.5 + 0.04 * n
	var turn := 1 if n % 2 == 0 else -1
	return timeline({
		t(0): {"x": x, "y": y - 2.5, "rotation_degrees": Vector3.ZERO, "transparency": 1.0},
		t(rise): {"y": y - 2.5, "transparency": 1.0}, t(rise + 0.4): {"transparency": 0.0},
		t(rise + 2): {"y": y, "interpolation": Out.CUBIC},
		t(release): {"x": x, "y": y + 0.25, "rotation_degrees": Vector3.ZERO, "transparency": 0.0},
		t(release + 1.4): {
			"x": x + (n % 5 - 2) * 0.3, "y": y + 1.25 + n % 3 * 0.35,
			"rotation_degrees": Vector3(0, 0, (25 + n * 13 % 40) * turn), "transparency": 1.0, "interpolation": Out.QUAD,
		},
	})

## A definition word is printed in turn. The headword and part of speech stay and blow away later; the rest fade
## when the definition shrinks.
func printed(word: Node3D, i: int, at: Dictionary) -> TweensGdKeyframes:
	var x := word.position.x
	var z := word.position.z
	var shown: float = at["print"] + 0.07 * i
	var frames := {
		t(0): {"x": x, "y": LIFT, "z": z + 0.12, "rotation_degrees": Vector3(-90, 0, 0), "scale": 1.0, "transparency": 1.0},
		t(shown): {"z": z + 0.12, "transparency": 1.0},
		t(shown + 0.35): {"z": z, "transparency": 0.0, "interpolation": Out.CUBIC},
	}
	if i < 2:
		blow(frames, x, z, i, at["blow"])
	else:
		var gone: float = at["shrink"] + 0.02 * (i - 2)
		key(frames, gone, {"scale": 1.0, "transparency": 0.0})
		key(frames, gone + 0.35, {"scale": 0.8, "transparency": 1.0, "interpolation": In.QUAD})
	return timeline(frames)

## A new word pops in after the headword, one per 8th, then blows away with it.
func extra(word: Node3D, j: int, at: Dictionary) -> TweensGdKeyframes:
	var x := word.position.x
	var z := word.position.z
	var shown: float = at["extend"] + EIGHTH * j
	var frames := {
		t(0): {"x": x, "y": LIFT, "z": z, "rotation_degrees": Vector3(-90, 0, 0), "scale": 0.01, "transparency": 1.0},
		t(shown): {"scale": 0.01, "transparency": 1.0},
		t(shown + 0.15): {"transparency": 0.0}, t(shown + 0.3): {"scale": 1.0, "interpolation": Out.BACK},
	}
	blow(frames, x, z, 2 + j, at["blow"])
	return timeline(frames)

## Lifts a printed word off the page, and the wind carries it away like an autumn leaf.
func blow(frames: Dictionary, x: float, z: float, n: int, at: float) -> void:
	var gust := at + 0.05 * n
	var turn := 1 if n % 2 == 0 else -1
	key(frames, gust, {"x": x, "y": LIFT, "z": z, "rotation_degrees": Vector3(-90, 0, 0), "transparency": 0.0})
	key(frames, gust + 0.5, {
		"x": x + 0.35, "y": 0.3, "z": z - 0.1, "rotation_degrees": Vector3(-60, 25 * turn, 40 * turn), "interpolation": Out.QUAD,
	})
	key(frames, gust + 1.5, {
		"x": x + 2.2 + 0.3 * (n % 3), "y": 0.9 + 0.2 * (n % 2), "z": z - 0.4,
		"rotation_degrees": Vector3(-20, 70 * turn, 160 * turn), "transparency": 1.0, "interpolation": In.QUAD,
	})

## Halfway along a smooth bend from leaving one station to entering the next.
func bend(index: int, from: Vector3, to: Vector3) -> Vector3:
	var leaving: Node3D = targets.stations[index]
	var entering: Node3D = targets.stations[index + 1]
	return (from + to) / 2 + (leaving.basis.x - entering.basis.x) * from.distance_to(to) / 8

## Keys the carrier at a point on the page, in the world, after every point it visited before.
func visit(frames: Dictionary, at: float, point: Vector3, easing: Variant = null) -> void:
	if not frames.is_empty() and t(at) <= frames.keys().back():
		push_error("The dot's path goes back in time at %.3f s." % at)
	var channels := {"x": point.x, "z": point.z}
	if easing != null: channels["interpolation"] = easing
	key(frames, at, channels)

## The dot's path over the page: straight along each station, curving from one to the next.
func route() -> TweensGdKeyframes:
	var origin := on(0, Vector3(middle(targets.from), 0, 0))
	var end := on(0, Vector3(middle(targets.to), 0, 0))
	var ruler := []
	for tick_mark: Node3D in targets.ticks: ruler.append(on(1, Vector3(page(tick_mark).x, 0, 0)))
	var frames := {}
	visit(frames, 0, origin)
	visit(frames, walk(), origin)
	visit(frames, walk() + BAR, end, InOut.SINE)
	visit(frames, walk() + BAR + BEAT, end)
	# Curves over to the ruler and waits at its start.
	visit(frames, (walk() + BAR + BEAT + tick(0) - EIGHTH) / 2, bend(0, end, ruler[0]))
	visit(frames, tick(0) - EIGHTH, ruler[0])
	visit(frames, tick(0), ruler[0])
	# One InOut.Cubic segment: the dot reaches each tick of the ruler on a beat.
	visit(frames, tick(8), ruler[8], InOut.CUBIC)
	visit(frames, run_up(), ruler[8])
	# Speeds up into the first hop.
	var hops: Array = targets.bounce
	var lift_off := on(2, Vector3(page(hops[0]).x - 0.6, 0, 0))
	visit(frames, (run_up() + takeoff()) / 2, bend(1, ruler[8], lift_off))
	visit(frames, takeoff(), lift_off)
	for i in hops.size():
		visit(frames, landing(i), on(2, Vector3(page(hops[i]).x, 0, 0)), Tweens.Interpolation.LINEAR)
	var off := page(hops[-1]).x
	visit(frames, landing(6), on(2, Vector3(off + 1, 0, 0)), Tweens.Interpolation.LINEAR)
	var rebound := on(2, Vector3(off + 1.5, 0, 0))
	visit(frames, landing(6) + EIGHTH, rebound)
	var springs: Array = targets.elastic
	var into := on(3, Vector3(page(springs[0]).x, 0, 0.35))
	var past := on(3, Vector3(page(springs[-1]).x, 0, 0.35))
	visit(frames, (landing(6) + EIGHTH + stretch(0)) / 2, bend(2, rebound, into))
	visit(frames, stretch(0), into)
	visit(frames, stretch(6), past, Tweens.Interpolation.LINEAR)
	# Smooth interpolation curves the path through every diamond.
	var waypoints := []
	for diamond_mark: Node3D in targets.diamonds: waypoints.append(on(4, page(diamond_mark)))
	visit(frames, (stretch(6) + waypoint(0)) / 2, bend(3, past, waypoints[0]))
	for i in waypoints.size(): visit(frames, waypoint(i), waypoints[i])
	var period: Vector3 = targets.period
	var front := on(5, Vector3(period.x, 0, 0.25))
	visit(frames, (waypoint(8) + cue(Scene.WORDMARK)) / 2, bend(4, waypoints[-1], front))
	visit(frames, cue(Scene.WORDMARK), front)
	visit(frames, dotted() - EIGHTH, front)
	visit(frames, dotted(), on(5, period), Out.BACK)
	return timeline(frames)

## The disc above the carrier: it pops into the sky, drops, hops over BOUNCE, squashes and stretches, and turns into
## the period.
func disc() -> TweensGdKeyframes:
	var top: float = targets.cap + RADIUS
	var period: Vector3 = targets.period
	var hidden := Vector3.ONE * 0.01
	var frames := {
		t(0): {"y": 1.6, "scale": hidden, "modulate": TWEENS_BLUE}, t(0.25): {"scale": hidden},
		t(0.85): {"scale": Vector3.ONE, "interpolation": Out.BACK},
		t(2.6): {"y": 1.66}, t(bars(1)): {"y": 1.6},
		t(bars(1) + EIGHTH): {"y": RADIUS, "interpolation": In.QUAD}, t(4.67): {"y": RADIUS + 0.3, "interpolation": Out.QUAD},
		t(bars(1) + BEAT): {"y": RADIUS, "interpolation": In.QUAD}, t(takeoff()): {"y": RADIUS},
		t(landing(5) + EIGHTH): {"y": top + 0.5, "interpolation": Out.QUAD}, t(landing(6)): {"y": RADIUS, "interpolation": In.QUAD},
		t(landing(6) + 0.185): {"y": RADIUS + 0.3, "interpolation": Out.QUAD},
		t(landing(6) + EIGHTH): {"y": RADIUS, "interpolation": In.QUAD},
		t(dotted() - EIGHTH): {"y": RADIUS, "scale": Vector3.ONE, "modulate": TWEENS_BLUE}, t(dotted() - 0.105): {"modulate": GD_GREEN},
		# Becomes the wordmark's period.
		t(dotted()): {"y": period.y, "scale": targets.periodScale, "interpolation": Out.BACK},
	}
	for i in targets.bounce.size():
		# A shorter hop rises less, as under gravity.
		var flight := landing(i) - (takeoff() if i == 0 else landing(i - 1))
		key(frames, landing(i) - flight / 2, {"y": top + 0.7 * flight * flight / (BEAT * BEAT), "interpolation": Out.QUAD})
		key(frames, landing(i), {"y": top, "interpolation": In.QUAD})
	# Rolling over the page, it stretches as it speeds up and squeezes as it slows down.
	roll(frames, walk(), walk() + BAR)
	roll(frames, walk() + BAR + BEAT, tick(0) - EIGHTH)
	roll(frames, tick(0), tick(8))
	stop(frames, cue(Scene.WORDMARK), 1.5)
	# It stays stretched into the first hop and rounds out in the air.
	key(frames, run_up(), {"scale": Vector3.ONE})
	key(frames, takeoff() - 0.225, {"scale": STRETCHED})
	key(frames, takeoff(), {"scale": STRETCHED})
	key(frames, takeoff() + 0.245, {"scale": Vector3.ONE})
	var landings_at := [bars(1) + EIGHTH, bars(1) + BEAT, landing(6)]
	for i in targets.bounce.size(): landings_at.append(landing(i))
	for at: float in landings_at:
		key(frames, at, {"scale": Vector3.ONE})
		key(frames, at + 0.05, {"scale": Vector3(1.3, 0.72, 1)})
		key(frames, at + 0.13, {"scale": Vector3(0.9, 1.12, 1)})
		key(frames, at + 0.28, {"scale": Vector3.ONE})
	return timeline(frames)

## A roll from rest to rest: the disc stretches along the page while it speeds up and squeezes while it slows down.
func roll(frames: Dictionary, begin: float, end: float) -> void:
	var span := end - begin
	key(frames, begin, {"scale": Vector3.ONE})
	key(frames, begin + 0.25 * span, {"scale": STRETCHED})
	stop(frames, end, 0.5 * span)

## The disc squeezes while it brakes and wobbles round again once it stops.
func stop(frames: Dictionary, at: float, braking: float) -> void:
	key(frames, at - braking, {"scale": Vector3.ONE})
	key(frames, at - 0.4 * braking, {"scale": SQUEEZED})
	key(frames, at + 0.15, {"scale": Vector3(1.03, 0.98, 1)})
	key(frames, at + 0.35, {"scale": Vector3.ONE})

## The rim stays a speck behind the disc until it swells to the wordmark's outline in the period.
func rim() -> TweensGdKeyframes:
	var speck := Vector3.ONE * 0.01
	return timeline({
		t(0): {"scale": speck}, t(dotted() - EIGHTH): {"scale": speck},
		t(dotted()): {"scale": targets.rimScale, "interpolation": Out.BACK},
	})

## BOUNCE stands up with a bounce, and each letter squashes under the landing dot.
func bounce(i: int) -> TweensGdKeyframes:
	var frames := standing(takeoff() + 0.08 * i, Out.BOUNCE, 0.6)
	var at := landing(i)
	key(frames, 0, {"scale": Vector3.ONE})
	key(frames, at, {"scale": Vector3.ONE})
	key(frames, at + 0.06, {"scale": Vector3(1.15, 0.78, 1)})
	key(frames, at + 0.45, {"scale": Vector3.ONE, "interpolation": Out.BACK})
	return timeline(frames)

## ELASTIC stands up elastically, and each letter stretches as the dot passes and springs back.
func elastic(i: int) -> TweensGdKeyframes:
	var frames := standing(cue(Scene.ELASTIC) + 3 * EIGHTH + 0.06 * i, Out.ELASTIC, 0.8)
	var at := stretch(i)
	key(frames, 0, {"scale": Vector3.ONE})
	key(frames, at, {"scale": Vector3.ONE})
	key(frames, at + 0.1, {"scale": Vector3(1.6, 0.8, 1)})
	key(frames, at + 1, {"scale": Vector3.ONE, "interpolation": Out.ELASTIC})
	return timeline(frames)

## A keyframe diamond lights up and stands when the dot passes over it.
func diamond(i: int) -> TweensGdKeyframes:
	var at := waypoint(i)
	var frames := standing(at, Out.BACK, 0.4)
	key(frames, 0, {"scale": 1.0, "modulate": SAND})
	key(frames, at, {"scale": 1.0, "modulate": SAND})
	key(frames, at + 0.08, {"scale": 1.6, "modulate": SAFFRON})
	key(frames, at + 0.4, {"scale": 1.0, "interpolation": Out.BACK})
	return timeline(frames)

## A wordmark letter falls onto the page on its 8th, the first on the wordmark's cue, and squashes on landing.
func drop(i: int) -> TweensGdKeyframes:
	var at := cue(Scene.WORDMARK) + EIGHTH * (i - 1)
	var hidden := Vector3.ONE * 0.01
	return timeline({
		t(0): {"y": 3.5, "scale": hidden}, t(at): {"y": 3.5, "scale": hidden}, t(at + 0.1): {"scale": Vector3.ONE},
		t(at + EIGHTH): {"y": 0.0, "scale": Vector3.ONE, "interpolation": In.QUAD},
		t(at + EIGHTH + 0.08): {"scale": Vector3(1.18, 0.8, 1)},
		t(at + EIGHTH + 0.4): {"scale": Vector3.ONE, "interpolation": Out.BACK},
	})

## Where the dot lands on the page as [time, point]: its drop, the end of BOUNCE, and the period.
func landings() -> Array:
	var off := page(targets.bounce[-1]).x
	return [
		[bars(1) + EIGHTH, on(0, Vector3(middle(targets.from), 0, 0))], [landing(6), on(2, Vector3(off + 1, 0, 0))],
		[dotted(), on(5, targets.period)],
	]

## Rings spread over the page from every landing.
func ripple(i: int) -> TweensGdKeyframes:
	var first: Vector3 = landings()[0][1]
	var frames := {t(0): {"x": first.x, "z": first.z, "scale": 0.2, "transparency": 1.0}}
	for spot: Array in landings():
		var at: float = spot[0] + 0.12 * i
		var point: Vector3 = spot[1]
		key(frames, at - 0.02, {"x": point.x, "z": point.z, "scale": 0.2, "transparency": 1.0})
		key(frames, at, {"transparency": 0.2})
		key(frames, at + 1.2, {"scale": 2.6 + 0.6 * i, "transparency": 1.0, "interpolation": Out.CUBIC})
	return timeline(frames)

## The trail lays dots from the dot's first landing and fades out before the wordmark appears. Under the closing
## fade, a burst of speed ages every dot out.
func lay() -> TweensGdKeyframes:
	var gone := cue(Scene.WORDMARK) - 0.43
	var frames := {
		t(0): {"amount_ratio": 0.0, "speed_scale": 1.0, "transparency": 0.0}, t(4.46): {"amount_ratio": 0.0}, t(4.5): {"amount_ratio": 1.0},
	}
	key(frames, gone - 0.8, {"transparency": 0.0})
	key(frames, gone - 0.02, {"amount_ratio": 1.0})
	key(frames, gone, {"amount_ratio": 0.0, "transparency": 1.0})
	key(frames, 54.76, {"speed_scale": 1.0})
	key(frames, 54.78, {"speed_scale": 400.0})
	key(frames, 54.99, {"speed_scale": 400.0})
	key(frames, LENGTH, {"speed_scale": 1.0})
	return timeline(frames)

## Every touchdown of the disc as [time, height]: its drop and rebound, each hop onto a BOUNCE letter, and the
## landing after BOUNCE and its rebound.
func contacts() -> Array:
	var touchdowns := [[bars(1) + EIGHTH, 0.0], [bars(1) + BEAT, 0.0]]
	for i in targets.bounce.size(): touchdowns.append([landing(i), targets.cap])
	touchdowns.append_array([[landing(6), 0.0], [landing(6) + EIGHTH, 0.0]])
	return touchdowns

## A spray of small leaves at every touchdown of the disc.
func impact() -> TweensGdKeyframes:
	var frames := {t(0): {"y": 0.02, "amount_ratio": 0.0}}
	for touchdown: Array in contacts():
		var at: float = touchdown[0]
		var height: float = touchdown[1]
		key(frames, at - 0.04, {"y": height + 0.02})
		key(frames, at - 0.01, {"amount_ratio": 0.0})
		key(frames, at, {"amount_ratio": 1.0})
		key(frames, at + 0.04, {"amount_ratio": 1.0})
		key(frames, at + 0.05, {"amount_ratio": 0.0})
	return timeline(frames)

## Leaves thrown into the air at every landing on the page, and along the wordmark as its letters land.
func toss() -> TweensGdKeyframes:
	var first: Vector3 = landings()[0][1]
	var frames := {t(0): {"x": first.x, "z": first.z, "amount_ratio": 0.0}}
	for spot: Array in landings(): throw(frames, spot[0], spot[1], 0.1)
	# A smaller throw as each wordmark letter lands, just in front of it, so the leaves fly up before the letters.
	for i in targets.logo.size():
		throw(frames, cue(Scene.WORDMARK) + EIGHTH * i, on(5, page(targets.logo[i]) + Vector3.BACK * 0.3), 0.05)
	return timeline(frames)

## Keys the emitter to a spot and opens it for [param open] seconds.
func throw(frames: Dictionary, at: float, spot: Vector3, open: float) -> void:
	key(frames, at - 0.03, {"x": spot.x, "z": spot.z})
	key(frames, at - 0.02, {"amount_ratio": 0.0})
	key(frames, at, {"amount_ratio": 1.0})
	key(frames, at + open, {"amount_ratio": 1.0})
	key(frames, at + open + 0.02, {"amount_ratio": 0.0})

## Confetti bursts from the wordmark when the dot becomes its period.
func burst() -> TweensGdKeyframes:
	var at := dotted()
	return timeline({
		t(0): {"amount_ratio": 0.0}, t(at + 0.025): {"amount_ratio": 0.0}, t(at + 0.045): {"amount_ratio": 1.0},
		t(at + 0.395): {"amount_ratio": 1.0}, t(at + 0.425): {"amount_ratio": 0.0},
	})
