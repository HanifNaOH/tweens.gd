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
		Tweens.animate(targets.dust, dust()),
		Tweens.animate(targets.confetti, burst()),
		Tweens.animate(targets.subtitle, timeline(standing(49.48, Out.BACK))),
	]
	var credit: Array = targets.credit
	var engine: Array = targets.engine
	for i in credit.size(): plays.append(Tweens.animate(credit[i], float_up(credit[i], i, i, bars(0))))
	for i in engine.size():
		plays.append(Tweens.animate(engine[i], float_up(engine[i], i, credit.size() + i, bars(0) + EIGHTH)))
	for g in targets.glosses.size():
		var words: Array = targets.glosses[g].words
		var added: Array = targets.glosses[g].extra
		var at := glossary(g)
		for i in words.size(): plays.append(Tweens.animate(words[i], printed(words[i], i, at)))
		for j in added.size(): plays.append(Tweens.animate(added[j], extra(added[j], j, at)))
	# Each word stands up with the easing it names.
	for i in targets.from.size():
		plays.append(Tweens.animate(targets.from[i], timeline(standing(bars(2) + 0.08 * i, Out.BACK))))
	for i in targets.to.size(): plays.append(Tweens.animate(targets.to[i], timeline(standing(11 + 0.08 * i, Out.BACK))))
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

# When the dot reaches each ruler tick, BOUNCE letter, ELASTIC letter and keyframe diamond: all on the beat.
func tick(i: int) -> float: return bars(4) + EIGHTH + BEAT * i
func landing(i: int) -> float: return bars(8) + BEAT * i
func stretch(i: int) -> float: return bars(10) + 3 * BEAT + EIGHTH * i
func waypoint(i: int) -> float: return bars(12) + BEAT * i

## When a definition is printed, shrinks to its headword, gains new words, and blows away.
func glossary(g: int) -> Dictionary:
	return [
		{"print": 7.48, "shrink": 10.855, "extend": 11.605, "blow": bars(4)},
		{"print": 14.6, "shrink": 19.855, "extend": 20.605, "blow": bars(7)},
		{"print": 31.48, "shrink": 34.855, "extend": 35.605, "blow": 36.73},
	][g]

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

## The rig looks up at the dot in the sky, follows it down to the page and along the path, leaning into its moves,
## then cranes back to reveal the journey and looks back to frame the wordmark.
func rig() -> TweensGdKeyframes:
	return timeline({
		t(0): {"position": Vector3(-2.06, 1.6, 0), "rotation_degrees": Vector3(24, -4, 0)},
		t(2.6): {"position": Vector3(-2.06, 1.62, 0), "rotation_degrees": Vector3(22, 2, 0)},
		t(4): {"position": Vector3(-2.06, 1.55, 0), "rotation_degrees": Vector3(21, 0, 0)},
		t(5.2): {"position": Vector3(-1.9, 0.4, 0.1), "rotation_degrees": Vector3(-22, -10, 0), "interpolation": InOut.CUBIC},
		t(7.1): {"position": Vector3(-1.5, 0.35, 0.2), "rotation_degrees": Vector3(-24, -9, 0)},
		t(8.6): {"position": Vector3(-1.2, 0.35, 0.25), "rotation_degrees": Vector3(-25, -7, 0)},
		t(9.9): {"position": Vector3(-0.8, 0.35, 0.25), "rotation_degrees": Vector3(-25, -3, -3)},
		t(11.3): {"position": Vector3(0, 0.35, 0.3), "rotation_degrees": Vector3(-25, 0, 2.5)},
		t(12.4): {"position": Vector3(0.3, 0.35, 0.3), "rotation_degrees": Vector3(-26, 0, 0)},
		t(13.1): {"position": Vector3(0.7, 0.35, 0.28), "rotation_degrees": Vector3(-26, 1, 0)},
		t(14.6): {"position": Vector3(3, 0.35, 0.25), "rotation_degrees": Vector3(-27, 3, 0)},
		t(16.5): {"position": Vector3(4.6, 0.35, 0.3), "rotation_degrees": Vector3(-28, 2, -2)},
		t(19.5): {"position": Vector3(5.4, 0.35, 0.35), "rotation_degrees": Vector3(-28, 3, 1.5)},
		t(20.8): {"position": Vector3(4.3, 0.3, 0.6), "rotation_degrees": Vector3(-34, 0, 0)},
		t(22.1): {"position": Vector3(4.4, 0.3, 0.6), "rotation_degrees": Vector3(-34, 0, 0)},
		t(24.7): {"position": Vector3(8.8, 0.5, 0.05), "rotation_degrees": Vector3(-20, -6, -2)},
		t(25.4): {"position": Vector3(9.4, 0.55, 0), "rotation_degrees": Vector3(-20, -6, 0)},
		t(28.9): {"position": Vector3(11.2, 0.6, -0.05), "rotation_degrees": Vector3(-20, -4, 1.5)},
		t(30): {"position": Vector3(12.7, 0.4, 0.2), "rotation_degrees": Vector3(-24, 2, 0)},
		t(33.3): {"position": Vector3(15.6, 0.4, 0.45), "rotation_degrees": Vector3(-26, 2, 0)},
		t(35): {"position": Vector3(15.7, 0.4, 0.45), "rotation_degrees": Vector3(-26, 1, 0)},
		t(36.2): {"position": Vector3(16, 0.4, 0.4), "rotation_degrees": Vector3(-26, 0, 0)},
		t(37.1): {"position": Vector3(18.2, 0.35, 0.1), "rotation_degrees": Vector3(-28, -6, 0)},
		t(43.1): {"position": Vector3(23, 0.35, 0), "rotation_degrees": Vector3(-30, 10, 0)},
		t(46.1): {"position": Vector3(27.6, 0.5, -0.3), "rotation_degrees": Vector3(-18, 0, 0)},
		t(49.855): {"position": Vector3(27.6, 0.5, -0.3), "rotation_degrees": Vector3(-18, 0, 0)},
		t(51.355): {"position": Vector3(23, 0.3, -0.4), "rotation_degrees": Vector3(-34, 36, 0), "interpolation": InOut.SINE},
		t(51.6): {"position": Vector3(23, 0.3, -0.4), "rotation_degrees": Vector3(-34, 36, 0)},
		t(53.105): {"position": Vector3(27.6, 0.25, 0.25), "rotation_degrees": Vector3(-16, 0, 0), "interpolation": InOut.CUBIC},
	})

## The camera dollies per beat, punches in on the bounce section, twangs on a rubber band at ELASTIC, pulls far back
## for the reveal, and comes in again on the wordmark.
func lens() -> TweensGdKeyframes:
	return timeline({
		t(0): {"z": 3.6, "rotation_degrees": Vector3.ZERO}, t(4): {"z": 3.6}, t(5.2): {"z": 4.2, "interpolation": InOut.CUBIC},
		t(7.1): {"z": 4.6}, t(13.5): {"z": 4.8}, t(19.5): {"z": 5.2}, t(20.8): {"z": 4.2}, t(22.1): {"z": 4.2},
		t(24.7): {"z": 4.8}, t(bars(8)): {"z": 4.8}, t(25.5): {"z": 4.3, "interpolation": Out.BACK},
		t(28.9): {"z": 4.5}, t(30): {"z": 4.8},
		t(stretch(0)): {"z": 5.3, "rotation_degrees": Vector3.ZERO}, t(33.45): {"z": 5.8, "rotation_degrees": Vector3(0, 0, 3.5)},
		t(35): {"z": 5.3, "rotation_degrees": Vector3.ZERO, "interpolation": Out.ELASTIC},
		t(37.1): {"z": 5.0}, t(43.1): {"z": 5.2}, t(46.1): {"z": 6.0}, t(49.855): {"z": 6.0},
		t(51.355): {"z": 11.0, "interpolation": InOut.SINE}, t(51.6): {"z": 11.0}, t(53.105): {"z": 5.6, "interpolation": InOut.CUBIC},
	})

## Paper fades in, and fades back over the jump to the loop's first keys.
func fade() -> TweensGdKeyframes:
	return timeline({t(0): {"alpha": 1.0}, t(0.6): {"alpha": 0.0}, t(54.4): {"alpha": 0.0}, t(54.75): {"alpha": 1.0}})

## A credit letter floats up into view from below and keeps drifting up, then floats away like a released balloon,
## turning a little.
func float_up(letter: Node3D, i: int, n: int, at: float) -> TweensGdKeyframes:
	var x := letter.position.x
	var y := letter.position.y
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

## The dot's path over the page.
func route() -> TweensGdKeyframes:
	var origin := middle(targets.from)
	var end := middle(targets.to)
	var off: float = targets.bounce[-1].position.x
	var period: Vector3 = targets.period
	var frames := {
		t(0): {"x": origin, "z": 0.0}, t(8.605): {"x": origin},
		t(11.605): {"x": end, "interpolation": InOut.SINE}, t(12.355): {"x": end},
		t(tick(0)): {"x": targets.ticks[0].position.x, "interpolation": InOut.SINE},
		# One InOut.Cubic segment: the dot reaches each tick of the ruler on a beat.
		t(tick(8)): {"x": targets.ticks[8].position.x, "interpolation": InOut.CUBIC},
		t(bars(7)): {"x": targets.ticks[8].position.x},
		# Speeds up into the first hop, which lands on the B on bar 9.
		t(bars(8) - BEAT): {"x": targets.bounce[0].position.x - 0.6, "interpolation": In.SINE},
	}
	for i in targets.bounce.size():
		key(frames, landing(i), {"x": targets.bounce[i].position.x, "interpolation": Tweens.Interpolation.LINEAR})
	key(frames, landing(6), {"x": off + 1, "interpolation": Tweens.Interpolation.LINEAR})
	key(frames, 29.98, {"x": off + 1.5, "z": 0.0})
	key(frames, stretch(0), {"x": targets.elastic[0].position.x, "z": 0.35})
	key(frames, stretch(6), {"x": targets.elastic[6].position.x, "z": 0.35, "interpolation": Tweens.Interpolation.LINEAR})
	# Smooth interpolation curves the path through every diamond.
	for i in targets.diamonds.size():
		var diamond_at: Vector3 = targets.diamonds[i].position
		key(frames, waypoint(i), {"x": diamond_at.x, "z": diamond_at.z})
	key(frames, bars(15), {"x": period.x, "z": 0.4})
	key(frames, 48.73, {"x": period.x, "z": 0.4})
	key(frames, bars(16), {"x": period.x, "z": period.z, "interpolation": Out.BACK})
	return timeline(frames)

## The disc above the carrier: it pops into the sky, drops, hops over BOUNCE, squashes and stretches, and turns into
## the period.
func disc() -> TweensGdKeyframes:
	var top: float = targets.cap + RADIUS
	var hidden := Vector3.ONE * 0.01
	var frames := {
		t(0): {"y": 1.6, "scale": hidden, "modulate": EMBER}, t(0.25): {"scale": hidden},
		t(0.85): {"scale": Vector3.ONE, "interpolation": Out.BACK},
		t(2.6): {"y": 1.66}, t(bars(1)): {"y": 1.6},
		t(bars(1) + EIGHTH): {"y": RADIUS, "interpolation": In.QUAD}, t(4.67): {"y": RADIUS + 0.3, "interpolation": Out.QUAD},
		t(bars(1) + BEAT): {"y": RADIUS, "interpolation": In.QUAD}, t(bars(8) - BEAT): {"y": RADIUS},
		t(landing(5) + EIGHTH): {"y": top + 0.5, "interpolation": Out.QUAD}, t(landing(6)): {"y": RADIUS, "interpolation": In.QUAD},
		t(29.79): {"y": RADIUS + 0.3, "interpolation": Out.QUAD}, t(29.98): {"y": RADIUS, "interpolation": In.QUAD},
		t(48.73): {"y": RADIUS, "scale": Vector3.ONE, "modulate": EMBER}, t(49): {"modulate": GD_GREEN},
		# Shrinks to the size of the wordmark's period, resting on the page.
		t(bars(16)): {"y": RADIUS * 0.45, "scale": Vector3(0.45, 0.45, 1), "interpolation": Out.BACK},
	}
	for i in targets.bounce.size():
		key(frames, landing(i) - EIGHTH, {"y": top + 0.7, "interpolation": Out.QUAD})
		key(frames, landing(i), {"y": top, "interpolation": In.QUAD})
	var landings_at := [bars(1) + EIGHTH, bars(1) + BEAT, landing(6)]
	for i in targets.bounce.size(): landings_at.append(landing(i))
	for at: float in landings_at:
		key(frames, at, {"scale": Vector3.ONE})
		key(frames, at + 0.05, {"scale": Vector3(1.3, 0.72, 1)})
		key(frames, at + 0.13, {"scale": Vector3(0.9, 1.12, 1)})
		key(frames, at + 0.28, {"scale": Vector3.ONE})
	return timeline(frames)

## BOUNCE stands up with a bounce, and each letter squashes under the landing dot.
func bounce(i: int) -> TweensGdKeyframes:
	var frames := standing(bars(8) - BEAT + 0.08 * i, Out.BOUNCE, 0.6)
	var at := landing(i)
	key(frames, 0, {"scale": Vector3.ONE})
	key(frames, at, {"scale": Vector3.ONE})
	key(frames, at + 0.06, {"scale": Vector3(1.15, 0.78, 1)})
	key(frames, at + 0.45, {"scale": Vector3.ONE, "interpolation": Out.BACK})
	return timeline(frames)

## ELASTIC stands up elastically, and each letter stretches as the dot passes and springs back.
func elastic(i: int) -> TweensGdKeyframes:
	var frames := standing(bars(10) + 0.06 * i, Out.ELASTIC, 0.8)
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

## Where the dot lands on the page as [time, x, z]: its drop, the end of BOUNCE, and the period.
func landings() -> Array:
	var period: Vector3 = targets.period
	var off: float = targets.bounce[-1].position.x
	return [[bars(1) + EIGHTH, middle(targets.from), 0.0], [landing(6), off + 1, 0.0], [bars(16), period.x, period.z]]

## Rings spread over the page from every landing.
func ripple(i: int) -> TweensGdKeyframes:
	var frames := {t(0): {"x": middle(targets.from), "z": 0.0, "scale": 0.2, "transparency": 1.0}}
	for spot: Array in landings():
		var at: float = spot[0] + 0.12 * i
		key(frames, at - 0.02, {"x": spot[1], "z": spot[2], "scale": 0.2, "transparency": 1.0})
		key(frames, at, {"transparency": 0.2})
		key(frames, at + 1.2, {"scale": 2.6 + 0.6 * i, "transparency": 1.0, "interpolation": Out.CUBIC})
	return timeline(frames)

## The trail lays dots from the dot's first landing until the camera looks back, then fades out. Under the closing
## fade, a burst of speed ages every dot out.
func lay() -> TweensGdKeyframes:
	return timeline({
		t(0): {"amount_ratio": 0.0, "speed_scale": 1.0, "transparency": 0.0}, t(4.46): {"amount_ratio": 0.0}, t(4.5): {"amount_ratio": 1.0},
		t(51.58): {"amount_ratio": 1.0}, t(51.6): {"amount_ratio": 0.0, "transparency": 0.0}, t(52.6): {"transparency": 1.0},
		t(54.76): {"speed_scale": 1.0}, t(54.78): {"speed_scale": 400.0}, t(54.99): {"speed_scale": 400.0},
		t(LENGTH): {"speed_scale": 1.0},
	})

## Puffs of paper dust at every landing, and along the wordmark as its letters land.
func dust() -> TweensGdKeyframes:
	var frames := {t(0): {"x": middle(targets.from), "z": 0.0, "amount_ratio": 0.0}}
	for spot: Array in landings():
		var at: float = spot[0]
		key(frames, at - 0.03, {"x": spot[1], "z": spot[2]})
		key(frames, at - 0.02, {"amount_ratio": 0.0})
		key(frames, at, {"amount_ratio": 1.0})
		key(frames, at + 0.1, {"amount_ratio": 1.0})
		key(frames, at + 0.12, {"amount_ratio": 0.0})
	var first: Node3D = targets.logo[0]
	var last: Node3D = targets.logo[-1]
	key(frames, 45.68, {"x": first.position.x, "z": first.position.z})
	key(frames, 45.71, {"amount_ratio": 0.0})
	key(frames, 45.73, {"amount_ratio": 1.0})
	key(frames, 48.355, {"x": last.position.x, "interpolation": Tweens.Interpolation.LINEAR})
	key(frames, 48.38, {"amount_ratio": 1.0})
	key(frames, 48.4, {"amount_ratio": 0.0})
	return timeline(frames)

## Confetti bursts from the wordmark when the dot becomes its period.
func burst() -> TweensGdKeyframes:
	return timeline({
		t(0): {"amount_ratio": 0.0}, t(49.13): {"amount_ratio": 0.0}, t(49.15): {"amount_ratio": 1.0},
		t(49.5): {"amount_ratio": 1.0}, t(49.53): {"amount_ratio": 0.0},
	})
