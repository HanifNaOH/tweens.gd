# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
extends "res://Gallery/GDScript/GalleryAnimation.gd"
# The scene is Showreel.tscn. "Along the Way": a dot travels a page from FROM to the period of TWEENS.GD, and each
# word it passes stands up and acts out its name. Every node plays keyframe definitions on one 55-second loop, timed
# to an 80 bpm track.

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
const STRETCHED = Vector3(1.1, 0.92, 1)
const SQUEEZED = Vector3(0.92, 1.08, 1)

func animate() -> void:
	# Scenes share the loop's clock, so their lead-ins and exits can overlap.
	# Build every scene before waiting for any of its infinite tweens.
	# Edit a cue's time to move its lettering, definition and punch line together.
	# Camera, dot and landing effects are choreographed separately in journey().
	var scenes: Array[Array] = [
		journey(),
		opening_credit(bars(0)),
		from_to(bars(2)),
		ease_scene(tick(0)),
		bounce_scene(21.98),
		elastic_scene(31.855),
		keyframes_scene(waypoint(0)),
		wordmark(bars(15) - EIGHTH),
		closing(bars(16) + 2 * BEAT),
	]
	for scene in scenes:
		for play: TweensGdGroup in scene: await play.wait()

## The camera, travelling dot and landing effects run across scene boundaries.
func journey() -> Array[TweensGdGroup]:
	# The trail keeps each dot it lays for exactly one loop.
	targets.trail.lifetime = LENGTH * tempo
	var path := route()
	var plays: Array[TweensGdGroup] = [
		Tweens.animate(targets.rig, rig()),
		Tweens.animate(targets.camera, lens()),
		Tweens.animate(targets.fade, fade()),
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

func opening_credit(at: float) -> Array[TweensGdGroup]:
	var shift := at - bars(0)
	var credit: Array = targets.credit
	var plays: Array[TweensGdGroup] = [
		Tweens.animate(targets.godot, float_up(targets.godot, 0, credit.size(), bars(0) + EIGHTH, shift)),
	]
	for i in credit.size(): plays.append(Tweens.animate(credit[i], float_up(credit[i], i, i, bars(0), shift)))
	return plays

func from_to(at: float) -> Array[TweensGdGroup]:
	var shift := at - bars(2)
	var plays := definition_scene(0, shift)
	for i in targets.from.size():
		plays.append(Tweens.animate(targets.from[i], timeline(standing(bars(2) + 0.08 * i, Out.BACK), shift)))
	for i in targets.to.size():
		plays.append(Tweens.animate(targets.to[i], timeline(standing(bars(3) + EIGHTH + 0.08 * i, Out.BACK), shift)))
	return plays

func ease_scene(at: float) -> Array[TweensGdGroup]:
	var shift := at - tick(0)
	var plays := definition_scene(1, shift)
	for i in targets.ticks.size(): plays.append(Tweens.animate(targets.ticks[i], timeline(standing(tick(i), Out.BACK, 0.35), shift)))
	for i in targets.ease.size():
		plays.append(Tweens.animate(targets.ease[i], timeline(standing(tick(i + 1), InOut.CUBIC, 0.9), shift)))
	return plays

func bounce_scene(at: float) -> Array[TweensGdGroup]:
	var shift := at - 21.98
	var plays := definition_scene(2, shift)
	for i in targets.bounce.size(): plays.append(Tweens.animate(targets.bounce[i], bounce(i, shift)))
	return plays

func elastic_scene(at: float) -> Array[TweensGdGroup]:
	var shift := at - 31.855
	var plays := definition_scene(3, shift)
	for i in targets.elastic.size(): plays.append(Tweens.animate(targets.elastic[i], elastic(i, shift)))
	return plays

func keyframes_scene(at: float) -> Array[TweensGdGroup]:
	var shift := at - waypoint(0)
	var plays := definition_scene(4, shift)
	for i in targets.diamonds.size(): plays.append(Tweens.animate(targets.diamonds[i], diamond(i, shift)))
	for i in targets.keyframes.size():
		plays.append(Tweens.animate(targets.keyframes[i], timeline(standing(waypoint(i) + 0.1, Out.BACK, 0.4), shift)))
	return plays

func wordmark(at: float) -> Array[TweensGdGroup]:
	var shift := at - (bars(15) - EIGHTH)
	var plays: Array[TweensGdGroup] = [
		Tweens.animate(targets.confetti, burst(shift)),
		Tweens.animate(targets.subtitle, timeline(standing(49.48, Out.BACK), shift)),
	]
	for i in targets.logo.size(): plays.append(Tweens.animate(targets.logo[i], drop(i, shift)))
	return plays

func closing(at: float) -> Array[TweensGdGroup]:
	var shift := at - (bars(16) + 2 * BEAT)
	var plays: Array[TweensGdGroup] = []
	for i in targets.taglines.size():
		plays.append(Tweens.animate(targets.taglines[i], timeline(standing(bars(16) + 2 * BEAT + EIGHTH * i, Out.BACK), shift)))
	return plays

## Print a definition, shorten it to a punch line, then blow it away.
func definition_scene(station: int, shift: float) -> Array[TweensGdGroup]:
	var words: Array = targets.glosses[station].words
	var added: Array = targets.glosses[station].extra
	var at := glossary(station)
	var plays: Array[TweensGdGroup] = []
	for i in words.size(): plays.append(Tweens.animate(words[i], printed(words[i], i, at, shift)))
	for j in added.size(): plays.append(Tweens.animate(added[j], extra(added[j], j, at, shift)))
	return plays

## Converts seconds on the loop to a percentage stop.
func t(at: float) -> float:
	return at / LENGTH * 100.0

func timeline(frames: Dictionary, shift := 0.0) -> TweensGdKeyframes:
	var definition := Tweens.keyframes(frames, LENGTH * tempo)
	definition.options.repeats = Tweens.INFINITE
	definition.options.delay = maxf(0.0, shift) * tempo
	definition.options.offset = maxf(0.0, -shift) * tempo
	definition.options.fill = Tweens.Fill.BOTH
	return definition

## Adds channels at a stop, next to any channels already keyed there.
func key(frames: Dictionary, at: float, channels: Dictionary) -> void:
	var frame: Dictionary = frames.get_or_add(t(at), {})
	frame.merge(channels)

## The downbeat [param count] bars after the first one.
func bars(count: int) -> float: return 1.105 + 4 * BEAT * count

# When the dot reaches each ruler tick, BOUNCE letter, ELASTIC letter and keyframe diamond.
func tick(i: int) -> float: return bars(4) + 3 * EIGHTH + BEAT * i
# The hops land 0.6 s after the bar; the hop from U to N is half as long, for the track's double bass drum hit.
func landing(i: int) -> float: return bars(8) + 0.6 + BEAT * i - (EIGHTH if i > 2 else 0.0)
func takeoff() -> float: return landing(0) - BEAT
func stretch(i: int) -> float: return bars(10) + 3 * BEAT + EIGHTH * i
func waypoint(i: int) -> float: return bars(12) + 3 * EIGHTH + BEAT * i

## When a definition is printed, shrinks to its headword, gains new words, and blows away.
func glossary(g: int) -> Dictionary:
	return [
		{"print": 7.48, "shrink": 10.855, "extend": 11.605, "blow": bars(4) + BEAT},
		{"print": 15.35, "shrink": 18.23, "extend": 18.605, "blow": 21.355},
		{"print": 21.98, "shrink": landing(5) - EIGHTH, "extend": landing(5), "blow": bars(10) + BEAT},
		{"print": 31.855, "shrink": 34.48, "extend": 34.855, "blow": 37.855},
		{"print": 38.98, "shrink": 41.98, "extend": 42.355, "blow": 44.98},
	][g]

## A page point on a station, in the world.
func on(station: int, point: Vector3) -> Vector3:
	var node: Node3D = targets.stations[station]
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

## Where the camera looks at a moment, as [time, station, target, turn, distance] and an optional easing: the rig
## sits on the target and turns, and the camera backs away from it by the distance. Target and turn are relative to
## the station, so each scene is framed as if unturned.
func shots() -> Array:
	# Each station word gets a close-up as it stands, each definition a wider frame, and each punch line a close one.
	# Moves take a second or more and start early, so the camera arrives as things happen.
	return [
		# Up at the dot in the sky, then down to the page with it.
		[0.0, 0, Vector3(-2.06, 2.1, 0), Vector3(25, -4, 0), 4.4],
		[2.6, 0, Vector3(-2.06, 2.15, 0), Vector3(23, 2, 0), 4.4],
		[4.0, 0, Vector3(-2.06, 2.1, 0), Vector3(22, 0, 0), 4.4],
		[5.2, 0, Vector3(-1.9, 0.4, 0.1), Vector3(-22, -10, 0), 4.2, InOut.CUBIC],
		# FROM, the definition, TO, and the punch line.
		[6.3, 0, Vector3(-2, 0.32, -0.45), Vector3(-14, -6, 0), 3.0],
		[7.1, 0, Vector3(-2.06, 0.34, -0.6), Vector3(-15, -4, 0), 2.4],
		[7.6, 0, Vector3(-2, 0.34, -0.6), Vector3(-15, -3, 0), 2.4],
		[8.9, 0, Vector3(-0.3, 0.1, 0.5), Vector3(-30, -4, 0), 5.0],
		[9.5, 0, Vector3(-0.1, 0.1, 0.5), Vector3(-30, -2, 0), 5.0],
		[10.6, 0, Vector3(1.1, 0.25, -0.35), Vector3(-22, 2, 0), 3.4],
		[10.9, 0, Vector3(1.15, 0.25, -0.35), Vector3(-22, 2, 0), 3.3],
		[12.15, 0, Vector3(-0.6, 0, 0.3), Vector3(-34, 0, 0), 3.9],
		[13.75, 0, Vector3(-0.45, 0, 0.3), Vector3(-34, 1, 0), 3.8],
		# EASE as it stands letter by letter, then one slow pull back to the definition, and the punch line.
		[14.95, 1, Vector3(4.7, 0.3, -1), Vector3(-12, 1, 0), 2.8],
		[15.85, 1, Vector3(5, 0.3, -1), Vector3(-12, 0, 0), 2.8],
		[17.25, 1, Vector3(5.6, 0, 0.4), Vector3(-30, 2, 0), 5.0],
		[18.15, 1, Vector3(5.7, 0, 0.4), Vector3(-30, 2, 0), 5.0],
		[18.9, 1, Vector3(5.1, 0, 0.1), Vector3(-34, 0, 0), 4.2],
		[21.25, 1, Vector3(5.2, 0, 0.1), Vector3(-34, 1, 0), 4.05],
		# The BOUNCE definition, BOUNCE from near the ground, and the punch line as the dot leaves.
		[22.35, 2, Vector3(10.6, 0, 0.6), Vector3(-30, 0, 0), 5.0],
		[24.2, 2, Vector3(10.7, 0, 0.6), Vector3(-30, 0, 0), 4.8],
		[25.4, 2, Vector3(10.5, 0.48, 0), Vector3(4, -3, 0), 3.5],
		[landing(0), 2, Vector3(10.5, 0.48, 0), Vector3(4, -2.5, 0), 3.5],
		[landing(0) + 0.4, 2, Vector3(10.5, 0.48, 0), Vector3(4, -2, 0), 3.15, Out.BACK],
		[28.5, 2, Vector3(10.9, 0.48, 0), Vector3(4, 1, 0), 3.3],
		[29.6, 2, Vector3(10.5, 0, 0.4), Vector3(-34, 0, 0), 3.9],
		[31.75, 2, Vector3(10.65, 0, 0.4), Vector3(-34, 1, 0), 3.8],
		# ELASTIC, the definition, and the punch line.
		[32.75, 3, Vector3(15.3, 0.3, -0.4), Vector3(-12, 0, 0), 3.2],
		[33.15, 3, Vector3(15.4, 0.3, -0.4), Vector3(-12, 0, 0), 3.2],
		[33.95, 3, Vector3(16.1, 0, 0.5), Vector3(-28, 0, 0), 5.0],
		[34.55, 3, Vector3(16.2, 0, 0.5), Vector3(-28, 0, 0), 5.0],
		[35.45, 3, Vector3(15.6, 0, 0.2), Vector3(-34, 0, 0), 3.9],
		[37.75, 3, Vector3(15.75, 0, 0.2), Vector3(-34, 1, 0), 3.8],
		# Along KEYFRAMES as its letters stand, the definition, the punch line, and on to the wordmark.
		[38.9, 4, Vector3(20, 0.25, -0.2), Vector3(-24, -8, 0), 3.0],
		[40.45, 4, Vector3(21, 0.25, -0.2), Vector3(-24, -2, 0), 3.0],
		[41.15, 4, Vector3(21.6, 0, 0.6), Vector3(-30, 0, 0), 4.8],
		[41.9, 4, Vector3(21.7, 0, 0.6), Vector3(-30, 1, 0), 4.8],
		[42.75, 4, Vector3(21.25, 0, 0.3), Vector3(-34, 0, 0), 3.9],
		[44.85, 4, Vector3(21.4, 0, 0.3), Vector3(-34, 1, 0), 3.8],
		[46.35, 5, Vector3(27.6, 0.5, -0.3), Vector3(-18, 0, 0), 6.2],
		[49.855, 5, Vector3(27.6, 0.45, -0.3), Vector3(-18, 0, 0), 5.8],
		# Settle on the wordmark with the subtitle and taglines, and drift in until the loop ends.
		[51.6, 5, Vector3(27.6, 0.25, 0.25), Vector3(-16, 0, 0), 5.6, InOut.SINE],
		[54.75, 5, Vector3(27.6, 0.25, 0.2), Vector3(-16, 0, 0), 5.4],
	]

## Adds a shot's easing, if it has one, to its frame.
func eased(frame: Dictionary, shot: Array) -> Dictionary:
	if shot.size() > 5: frame["interpolation"] = shot[5]
	return frame

func rig() -> TweensGdKeyframes:
	var frames := {}
	for shot: Array in shots():
		var station: Node3D = targets.stations[shot[1]]
		var target: Vector3 = station.transform * shot[2]
		var turn: Vector3 = shot[3] + Vector3(0, station.rotation_degrees.y, 0)
		frames[t(shot[0])] = eased({"position": target, "rotation_degrees": turn}, shot)
	return timeline(frames)

func lens() -> TweensGdKeyframes:
	var frames := {}
	for shot: Array in shots(): frames[t(shot[0])] = eased({"z": shot[4]}, shot)
	return timeline(frames)

## Paper fades in, and fades back over the jump to the loop's first keys.
func fade() -> TweensGdKeyframes:
	return timeline({t(0): {"alpha": 1.0}, t(0.6): {"alpha": 0.0}, t(54.4): {"alpha": 0.0}, t(54.75): {"alpha": 1.0}})

## A piece of the credit floats up into view from below and keeps drifting up, then floats away like a released
## balloon, turning a little.
func float_up(piece: Node3D, i: int, n: int, at: float, shift: float) -> TweensGdKeyframes:
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
	}, shift)

## A definition word is printed in turn. The headword and part of speech stay and blow away later; the rest fade
## when the definition shrinks.
func printed(word: Node3D, i: int, at: Dictionary, shift: float) -> TweensGdKeyframes:
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
	return timeline(frames, shift)

## A new word pops in after the headword, one per 8th, then blows away with it.
func extra(word: Node3D, j: int, at: Dictionary, shift: float) -> TweensGdKeyframes:
	var x := word.position.x
	var z := word.position.z
	var shown: float = at["extend"] + EIGHTH * j
	var frames := {
		t(0): {"x": x, "y": LIFT, "z": z, "rotation_degrees": Vector3(-90, 0, 0), "scale": 0.01, "transparency": 1.0},
		t(shown): {"scale": 0.01, "transparency": 1.0},
		t(shown + 0.15): {"transparency": 0.0}, t(shown + 0.3): {"scale": 1.0, "interpolation": Out.BACK},
	}
	blow(frames, x, z, 2 + j, at["blow"])
	return timeline(frames, shift)

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
func bend(station: int, from: Vector3, to: Vector3) -> Vector3:
	var leaving: Node3D = targets.stations[station]
	var entering: Node3D = targets.stations[station + 1]
	return (from + to) / 2 + (leaving.basis.x - entering.basis.x) * from.distance_to(to) / 8

## Keys the carrier at a point on the page, in the world.
func visit(frames: Dictionary, at: float, point: Vector3, easing: Variant = null) -> void:
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
	visit(frames, 8.605, origin)
	visit(frames, 11.605, end, InOut.SINE)
	visit(frames, 12.355, end)
	# Curves over to the ruler and waits at its start.
	visit(frames, (12.355 + tick(0) - EIGHTH) / 2, bend(0, end, ruler[0]))
	visit(frames, tick(0) - EIGHTH, ruler[0])
	visit(frames, tick(0), ruler[0])
	# One InOut.Cubic segment: the dot reaches each tick of the ruler on a beat.
	visit(frames, tick(8), ruler[8], InOut.CUBIC)
	visit(frames, bars(7) + BEAT, ruler[8])
	# Speeds up into the first hop.
	var hops: Array = targets.bounce
	var lift_off := on(2, Vector3(page(hops[0]).x - 0.6, 0, 0))
	visit(frames, (bars(7) + BEAT + takeoff()) / 2, bend(1, ruler[8], lift_off))
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
	visit(frames, (waypoint(8) + bars(15)) / 2, bend(4, waypoints[-1], front))
	visit(frames, bars(15), front)
	visit(frames, 48.73, front)
	visit(frames, bars(16), on(5, period), Out.BACK)
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
		t(48.73): {"y": RADIUS, "scale": Vector3.ONE, "modulate": TWEENS_BLUE}, t(49): {"modulate": GD_GREEN},
		# Becomes the wordmark's period.
		t(bars(16)): {"y": period.y, "scale": targets.periodScale, "interpolation": Out.BACK},
	}
	for i in targets.bounce.size():
		# A shorter hop rises less, as under gravity.
		var flight := landing(i) - (takeoff() if i == 0 else landing(i - 1))
		key(frames, landing(i) - flight / 2, {"y": top + 0.7 * flight * flight / (BEAT * BEAT), "interpolation": Out.QUAD})
		key(frames, landing(i), {"y": top, "interpolation": In.QUAD})
	# Rolling over the page, it stretches as it speeds up and squeezes as it slows down.
	roll(frames, 8.605, 11.605)
	roll(frames, 12.355, tick(0) - EIGHTH)
	roll(frames, tick(0), tick(8))
	stop(frames, bars(15), 1.5)
	# It stays stretched into the first hop and rounds out in the air.
	key(frames, bars(7) + BEAT, {"scale": Vector3.ONE})
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
		t(0): {"scale": speck}, t(48.73): {"scale": speck},
		t(bars(16)): {"scale": targets.rimScale, "interpolation": Out.BACK},
	})

## BOUNCE stands up with a bounce, and each letter squashes under the landing dot.
func bounce(i: int, shift: float) -> TweensGdKeyframes:
	var frames := standing(takeoff() + 0.08 * i, Out.BOUNCE, 0.6)
	var at := landing(i)
	key(frames, 0, {"scale": Vector3.ONE})
	key(frames, at, {"scale": Vector3.ONE})
	key(frames, at + 0.06, {"scale": Vector3(1.15, 0.78, 1)})
	key(frames, at + 0.45, {"scale": Vector3.ONE, "interpolation": Out.BACK})
	return timeline(frames, shift)

## ELASTIC stands up elastically, and each letter stretches as the dot passes and springs back.
func elastic(i: int, shift: float) -> TweensGdKeyframes:
	var frames := standing(bars(10) + 3 * EIGHTH + 0.06 * i, Out.ELASTIC, 0.8)
	var at := stretch(i)
	key(frames, 0, {"scale": Vector3.ONE})
	key(frames, at, {"scale": Vector3.ONE})
	key(frames, at + 0.1, {"scale": Vector3(1.6, 0.8, 1)})
	key(frames, at + 1, {"scale": Vector3.ONE, "interpolation": Out.ELASTIC})
	return timeline(frames, shift)

## A keyframe diamond lights up and stands when the dot passes over it.
func diamond(i: int, shift: float) -> TweensGdKeyframes:
	var at := waypoint(i)
	var frames := standing(at, Out.BACK, 0.4)
	key(frames, 0, {"scale": 1.0, "modulate": SAND})
	key(frames, at, {"scale": 1.0, "modulate": SAND})
	key(frames, at + 0.08, {"scale": 1.6, "modulate": SAFFRON})
	key(frames, at + 0.4, {"scale": 1.0, "interpolation": Out.BACK})
	return timeline(frames, shift)

## A wordmark letter falls onto the page on its 8th, the first on bar 15, and squashes on landing.
func drop(i: int, shift: float) -> TweensGdKeyframes:
	var at := bars(15) + EIGHTH * (i - 1)
	var hidden := Vector3.ONE * 0.01
	return timeline({
		t(0): {"y": 3.5, "scale": hidden}, t(at): {"y": 3.5, "scale": hidden}, t(at + 0.1): {"scale": Vector3.ONE},
		t(at + EIGHTH): {"y": 0.0, "scale": Vector3.ONE, "interpolation": In.QUAD},
		t(at + EIGHTH + 0.08): {"scale": Vector3(1.18, 0.8, 1)},
		t(at + EIGHTH + 0.4): {"scale": Vector3.ONE, "interpolation": Out.BACK},
	}, shift)

## Where the dot lands on the page as [time, point]: its drop, the end of BOUNCE, and the period.
func landings() -> Array:
	var off := page(targets.bounce[-1]).x
	return [
		[bars(1) + EIGHTH, on(0, Vector3(middle(targets.from), 0, 0))], [landing(6), on(2, Vector3(off + 1, 0, 0))],
		[bars(16), on(5, targets.period)],
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
	return timeline({
		t(0): {"amount_ratio": 0.0, "speed_scale": 1.0, "transparency": 0.0}, t(4.46): {"amount_ratio": 0.0}, t(4.5): {"amount_ratio": 1.0},
		t(44.875): {"transparency": 0.0}, t(45.655): {"amount_ratio": 1.0}, t(45.675): {"amount_ratio": 0.0, "transparency": 1.0},
		t(54.76): {"speed_scale": 1.0}, t(54.78): {"speed_scale": 400.0}, t(54.99): {"speed_scale": 400.0},
		t(LENGTH): {"speed_scale": 1.0},
	})

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
	for i in targets.logo.size(): throw(frames, bars(15) + EIGHTH * i, on(5, page(targets.logo[i]) + Vector3.BACK * 0.3), 0.05)
	return timeline(frames)

## Keys the emitter to a spot and opens it for [param open] seconds.
func throw(frames: Dictionary, at: float, spot: Vector3, open: float) -> void:
	key(frames, at - 0.03, {"x": spot.x, "z": spot.z})
	key(frames, at - 0.02, {"amount_ratio": 0.0})
	key(frames, at, {"amount_ratio": 1.0})
	key(frames, at + open, {"amount_ratio": 1.0})
	key(frames, at + open + 0.02, {"amount_ratio": 0.0})

## Confetti bursts from the wordmark when the dot becomes its period.
func burst(shift: float) -> TweensGdKeyframes:
	return timeline({
		t(0): {"amount_ratio": 0.0}, t(49.13): {"amount_ratio": 0.0}, t(49.15): {"amount_ratio": 1.0},
		t(49.5): {"amount_ratio": 1.0}, t(49.53): {"amount_ratio": 0.0},
	}, shift)
