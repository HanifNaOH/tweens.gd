# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
extends "res://Gallery/GDScript/GalleryAnimation.gd"
# Scene setup is in Showreel.cs. "Along the Way": a dot travels a page from FROM to the period of TWEENS.GD, and each
# word it passes stands up and acts out its name. Every node plays keyframe definitions on one 55-second loop, timed
# to an 80 bpm track.

const LENGTH = 55.0
const RADIUS = 0.14
const LIFT = 0.005
const EMBER = Color("e0573c")
const SAFFRON = Color("f0a830")
const SAND = Color("e9c4a4")
const GD_GREEN = Color("9bd69c")
# The track's grid: 80 bpm in 4/4, its first downbeat at 1.105 s.
const BEAT = 0.75
const EIGHTH = BEAT / 2
const STRETCHED = Vector3(1.1, 0.92, 1)
const SQUEEZED = Vector3(0.92, 1.08, 1)

func animate() -> void:
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
		Tweens.animate(targets.dust, dust()),
		Tweens.animate(targets.confetti, burst()),
		Tweens.animate(targets.godot, float_up(targets.godot, 0, targets.credit.size(), bars(0) + EIGHTH)),
		Tweens.animate(targets.subtitle, timeline(standing(49.48, Out.BACK))),
	]
	var credit: Array = targets.credit
	for i in credit.size(): plays.append(Tweens.animate(credit[i], float_up(credit[i], i, i, bars(0))))
	for g in targets.glosses.size():
		var words: Array = targets.glosses[g].words
		var added: Array = targets.glosses[g].extra
		var at := glossary(g)
		for i in words.size(): plays.append(Tweens.animate(words[i], printed(words[i], i, at)))
		for j in added.size(): plays.append(Tweens.animate(added[j], extra(added[j], j, at)))
	# Each word stands up with the easing it names.
	for i in targets.from.size():
		plays.append(Tweens.animate(targets.from[i], timeline(standing(bars(2) + 0.08 * i, Out.BACK))))
	for i in targets.to.size():
		plays.append(Tweens.animate(targets.to[i], timeline(standing(bars(3) + EIGHTH + 0.08 * i, Out.BACK))))
	for i in targets.ticks.size(): plays.append(Tweens.animate(targets.ticks[i], timeline(standing(tick(i), Out.BACK, 0.35))))
	for i in targets.ease.size():
		plays.append(Tweens.animate(targets.ease[i], timeline(standing(14.23 + BEAT * i, InOut.CUBIC, 0.9))))
	for i in targets.bounce.size(): plays.append(Tweens.animate(targets.bounce[i], bounce(i)))
	for i in targets.elastic.size(): plays.append(Tweens.animate(targets.elastic[i], elastic(i)))
	for i in targets.diamonds.size(): plays.append(Tweens.animate(targets.diamonds[i], diamond(i)))
	for i in targets.keyframes.size():
		plays.append(Tweens.animate(targets.keyframes[i], timeline(standing(waypoint(i) + 0.1, Out.BACK, 0.4))))
	for i in targets.logo.size(): plays.append(Tweens.animate(targets.logo[i], drop(i)))
	for i in targets.taglines.size():
		plays.append(Tweens.animate(targets.taglines[i], timeline(standing(52.355 + EIGHTH * i, Out.BACK))))
	for i in targets.ripples.size(): plays.append(Tweens.animate(targets.ripples[i], ripple(i)))
	for play in plays: await play.wait()

## Converts seconds on the loop to a percentage stop.
func t(at: float) -> float:
	return at / LENGTH * 100.0

func timeline(frames: Dictionary) -> TweensGdKeyframes:
	var definition := Tweens.keyframes(frames, LENGTH * tempo)
	definition.options.repeats = Tweens.INFINITE
	return definition

## Adds channels at a stop, next to any channels already keyed there.
func key(frames: Dictionary, at: float, channels: Dictionary) -> void:
	var frame: Dictionary = frames.get_or_add(t(at), {})
	frame.merge(channels)

## The downbeat [param count] bars after the first one.
func bars(count: int) -> float: return 1.105 + 4 * BEAT * count

# When the dot reaches each ruler tick, BOUNCE letter, ELASTIC letter and keyframe diamond.
func tick(i: int) -> float: return bars(4) + EIGHTH + BEAT * i
# The hops land 0.6 s after the bar; the hop from U to N is half as long, for the track's double bass drum hit.
func landing(i: int) -> float: return bars(8) + 0.6 + BEAT * i - (EIGHTH if i > 2 else 0.0)
func takeoff() -> float: return landing(0) - BEAT
func stretch(i: int) -> float: return bars(10) + 3 * BEAT + EIGHTH * i
func waypoint(i: int) -> float: return bars(12) + BEAT * i

## When a definition is printed, shrinks to its headword, gains new words, and blows away.
func glossary(g: int) -> Dictionary:
	return [
		{"print": 7.48, "shrink": 10.855, "extend": 11.605, "blow": bars(4)},
		{"print": 14.6, "shrink": 17.855, "extend": 18.23, "blow": 19.855},
		{"print": 20.605, "shrink": landing(5) - EIGHTH, "extend": landing(5), "blow": bars(10)},
		{"print": 31.48, "shrink": bars(11), "extend": 34.48, "blow": 36.73},
		{"print": 38.23, "shrink": 41.605, "extend": 41.98, "blow": 43.855},
	][g]

## A page point on a station, in the world.
func on(station: int, page: Vector3) -> Vector3:
	var node: Node3D = targets.stations[station]
	return node.transform * page

func middle(letters: Array) -> float:
	var first: Node3D = letters[0]
	var last: Node3D = letters[-1]
	return (first.position.x + last.position.x) / 2

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
		[7.1, 0, Vector3(-2.06, 0.3, -0.6), Vector3(-10, -4, 0), 2.4],
		[7.7, 0, Vector3(-2, 0.3, -0.6), Vector3(-10, -3, 0), 2.4],
		[8.8, 0, Vector3(-0.6, 0.2, 0.2), Vector3(-28, -4, 0), 4.6],
		[9.4, 0, Vector3(-0.4, 0.2, 0.2), Vector3(-28, -2, -1), 4.6],
		[10.4, 0, Vector3(1.2, 0.3, -0.5), Vector3(-12, 4, 0), 2.4],
		[10.9, 0, Vector3(1.2, 0.3, -0.5), Vector3(-12, 3, 0), 2.4],
		[12.0, 0, Vector3(-0.6, 0, 0.3), Vector3(-36, 0, 0), 3.9],
		[13.1, 0, Vector3(-0.5, 0, 0.3), Vector3(-36, 1, 0), 3.9],
		# EASE as it stands letter by letter, then one slow pull back to the definition, and the punch line.
		[14.2, 1, Vector3(4.7, 0.3, -1), Vector3(-12, 1, 0), 2.8],
		[15.1, 1, Vector3(5, 0.3, -1), Vector3(-12, 0, 0), 2.8],
		[16.5, 1, Vector3(5.6, 0, 0.4), Vector3(-30, 2, 0), 5.0],
		[17.6, 1, Vector3(5.7, 0, 0.4), Vector3(-30, 2, 0), 5.0],
		[18.5, 1, Vector3(4.8, 0, 0.1), Vector3(-34, 0, 0), 3.6],
		[19.8, 1, Vector3(4.9, 0, 0.1), Vector3(-34, 0, 0), 3.6],
		# The BOUNCE definition, BOUNCE from near the ground, and the punch line as the dot leaves.
		[20.9, 2, Vector3(10.6, 0, 0.6), Vector3(-30, 0, 0), 5.0],
		[23.6, 2, Vector3(10.7, 0, 0.6), Vector3(-30, 0, 0), 5.0],
		[24.9, 2, Vector3(10.5, 0.48, 0), Vector3(4, -3, 0), 3.5],
		[landing(0), 2, Vector3(10.5, 0.48, 0), Vector3(4, -2.5, 0), 3.5],
		[landing(0) + 0.4, 2, Vector3(10.5, 0.48, 0), Vector3(4, -2, 0), 3.15, Out.BACK],
		[28.5, 2, Vector3(10.9, 0.48, 0), Vector3(4, 1, 0), 3.3],
		[29.6, 2, Vector3(10.4, 0, 0.4), Vector3(-34, 0, 0), 3.9],
		[31.0, 2, Vector3(10.5, 0, 0.4), Vector3(-34, 1, 0), 3.9],
		# ELASTIC, the definition, and the punch line.
		[32.0, 3, Vector3(15.3, 0.3, -0.4), Vector3(-12, 0, 0), 3.2],
		[32.5, 3, Vector3(15.4, 0.3, -0.4), Vector3(-12, 0, 0), 3.2],
		[33.5, 3, Vector3(16.1, 0, 0.5), Vector3(-28, 0, 0), 5.0],
		[34.1, 3, Vector3(16.2, 0, 0.5), Vector3(-28, 0, 0), 5.0],
		[35.0, 3, Vector3(15.6, 0, 0.2), Vector3(-34, 0, 0), 3.9],
		[36.6, 3, Vector3(15.7, 0, 0.2), Vector3(-34, 0, 0), 3.9],
		# Along KEYFRAMES as its letters stand, the definition, the punch line, and on to the wordmark.
		[37.8, 4, Vector3(20, 0.25, -0.2), Vector3(-24, -8, 0), 3.0],
		[39.5, 4, Vector3(21, 0.25, -0.2), Vector3(-24, -2, 0), 3.0],
		[40.5, 4, Vector3(21.6, 0, 0.6), Vector3(-30, 0, 0), 4.8],
		[41.5, 4, Vector3(21.7, 0, 0.6), Vector3(-30, 1, 0), 4.8],
		[42.3, 4, Vector3(21.25, 0, 0.3), Vector3(-34, 0, 0), 3.9],
		[43.7, 4, Vector3(21.35, 0, 0.3), Vector3(-34, 0, 0), 3.9],
		[46.1, 5, Vector3(27.6, 0.5, -0.3), Vector3(-18, 0, 0), 6.0],
		[49.855, 5, Vector3(27.6, 0.5, -0.3), Vector3(-18, 0, 0), 6.0],
		# Crane back over the journey, then look back at the wordmark.
		[51.355, 5, Vector3(23, 0.3, -0.4), Vector3(-34, 36, 0), 11.0, InOut.SINE],
		[51.6, 5, Vector3(23, 0.3, -0.4), Vector3(-34, 36, 0), 11.0],
		[53.105, 5, Vector3(27.6, 0.25, 0.25), Vector3(-16, 0, 0), 5.6, InOut.CUBIC],
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
	for tick_mark: Node3D in targets.ticks: ruler.append(on(1, Vector3(tick_mark.position.x, 0, 0)))
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
	var lift_off := on(2, Vector3(hops[0].position.x - 0.6, 0, 0))
	visit(frames, (bars(7) + BEAT + takeoff()) / 2, bend(1, ruler[8], lift_off))
	visit(frames, takeoff(), lift_off)
	for i in hops.size():
		visit(frames, landing(i), on(2, Vector3(hops[i].position.x, 0, 0)), Tweens.Interpolation.LINEAR)
	var off: float = hops[-1].position.x
	visit(frames, landing(6), on(2, Vector3(off + 1, 0, 0)), Tweens.Interpolation.LINEAR)
	var rebound := on(2, Vector3(off + 1.5, 0, 0))
	visit(frames, landing(6) + EIGHTH, rebound)
	var springs: Array = targets.elastic
	var into := on(3, Vector3(springs[0].position.x, 0, 0.35))
	var past := on(3, Vector3(springs[-1].position.x, 0, 0.35))
	visit(frames, (landing(6) + EIGHTH + stretch(0)) / 2, bend(2, rebound, into))
	visit(frames, stretch(0), into)
	visit(frames, stretch(6), past, Tweens.Interpolation.LINEAR)
	# Smooth interpolation curves the path through every diamond.
	var waypoints := []
	for diamond_mark: Node3D in targets.diamonds: waypoints.append(on(4, diamond_mark.position))
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
		t(0): {"y": 1.6, "scale": hidden, "modulate": EMBER}, t(0.25): {"scale": hidden},
		t(0.85): {"scale": Vector3.ONE, "interpolation": Out.BACK},
		t(2.6): {"y": 1.66}, t(bars(1)): {"y": 1.6},
		t(bars(1) + EIGHTH): {"y": RADIUS, "interpolation": In.QUAD}, t(4.67): {"y": RADIUS + 0.3, "interpolation": Out.QUAD},
		t(bars(1) + BEAT): {"y": RADIUS, "interpolation": In.QUAD}, t(takeoff()): {"y": RADIUS},
		t(landing(5) + EIGHTH): {"y": top + 0.5, "interpolation": Out.QUAD}, t(landing(6)): {"y": RADIUS, "interpolation": In.QUAD},
		t(landing(6) + 0.185): {"y": RADIUS + 0.3, "interpolation": Out.QUAD},
		t(landing(6) + EIGHTH): {"y": RADIUS, "interpolation": In.QUAD},
		t(48.73): {"y": RADIUS, "scale": Vector3.ONE, "modulate": EMBER}, t(49): {"modulate": GD_GREEN},
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
	var frames := standing(bars(10) + EIGHTH + 0.06 * i, Out.ELASTIC, 0.8)
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

## A wordmark letter falls onto the page on its 8th and squashes on landing.
func drop(i: int) -> TweensGdKeyframes:
	var at := bars(14) + 3 * BEAT + EIGHTH * i
	var hidden := Vector3.ONE * 0.01
	return timeline({
		t(0): {"y": 3.5, "scale": hidden}, t(at): {"y": 3.5, "scale": hidden}, t(at + 0.1): {"scale": Vector3.ONE},
		t(at + EIGHTH): {"y": 0.0, "scale": Vector3.ONE, "interpolation": In.QUAD},
		t(at + EIGHTH + 0.08): {"scale": Vector3(1.18, 0.8, 1)},
		t(at + EIGHTH + 0.4): {"scale": Vector3.ONE, "interpolation": Out.BACK},
	})

## Where the dot lands on the page as [time, point]: its drop, the end of BOUNCE, and the period.
func landings() -> Array:
	var off: float = targets.bounce[-1].position.x
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
		t(44.5): {"transparency": 0.0}, t(45.28): {"amount_ratio": 1.0}, t(45.3): {"amount_ratio": 0.0, "transparency": 1.0},
		t(54.76): {"speed_scale": 1.0}, t(54.78): {"speed_scale": 400.0}, t(54.99): {"speed_scale": 400.0},
		t(LENGTH): {"speed_scale": 1.0},
	})

## Puffs of paper dust at every landing, and along the wordmark as its letters land.
func dust() -> TweensGdKeyframes:
	var first: Vector3 = landings()[0][1]
	var frames := {t(0): {"x": first.x, "z": first.z, "amount_ratio": 0.0}}
	for spot: Array in landings():
		var at: float = spot[0]
		var point: Vector3 = spot[1]
		key(frames, at - 0.03, {"x": point.x, "z": point.z})
		key(frames, at - 0.02, {"amount_ratio": 0.0})
		key(frames, at, {"amount_ratio": 1.0})
		key(frames, at + 0.1, {"amount_ratio": 1.0})
		key(frames, at + 0.12, {"amount_ratio": 0.0})
	var left := on(5, targets.logo[0].position)
	var right := on(5, targets.logo[-1].position)
	key(frames, 45.68, {"x": left.x, "z": left.z})
	key(frames, 45.71, {"amount_ratio": 0.0})
	key(frames, 45.73, {"amount_ratio": 1.0})
	key(frames, 48.355, {"x": right.x, "z": right.z, "interpolation": Tweens.Interpolation.LINEAR})
	key(frames, 48.38, {"amount_ratio": 1.0})
	key(frames, 48.4, {"amount_ratio": 0.0})
	return timeline(frames)

## Confetti bursts from the wordmark when the dot becomes its period.
func burst() -> TweensGdKeyframes:
	return timeline({
		t(0): {"amount_ratio": 0.0}, t(49.13): {"amount_ratio": 0.0}, t(49.15): {"amount_ratio": 1.0},
		t(49.5): {"amount_ratio": 1.0}, t(49.53): {"amount_ratio": 0.0},
	})
