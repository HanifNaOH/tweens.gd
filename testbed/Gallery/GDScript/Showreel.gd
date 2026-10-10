# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
extends "res://Gallery/GDScript/GalleryAnimation.gd"
# Scene setup is in Showreel.cs. A dot crosses the page from FROM to the period of TWEENS.GD, and each word it passes
# stands up and acts out its name. Every node plays keyframe definitions on one 20-second loop.

const LENGTH = 20.0
const RADIUS = 0.14
const EMBER = Color("e0573c")
const SAFFRON = Color("f0a830")
const SAND = Color("e9c4a4")
const GD_GREEN = Color("9bd69c")

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
		Tweens.animate(targets.subtitle, timeline(standing(17.7, Out.BACK))),
	]
	# Each word stands up with the easing it names.
	for i in targets.from.size(): plays.append(Tweens.animate(targets.from[i], timeline(standing(1 + 0.08 * i, Out.BACK))))
	for i in targets.to.size(): plays.append(Tweens.animate(targets.to[i], timeline(standing(3 + 0.08 * i, Out.BACK))))
	for i in targets.ticks.size(): plays.append(Tweens.animate(targets.ticks[i], timeline(standing(tick(i), Out.BACK, 0.35))))
	for i in targets.ease.size():
		plays.append(Tweens.animate(targets.ease[i], timeline(standing(4.5 + 0.35 * i, InOut.CUBIC, 0.9))))
	for i in targets.bounce.size(): plays.append(Tweens.animate(targets.bounce[i], bounce(i)))
	for i in targets.elastic.size(): plays.append(Tweens.animate(targets.elastic[i], elastic(i)))
	for i in targets.diamonds.size(): plays.append(Tweens.animate(targets.diamonds[i], diamond(i)))
	for i in targets.keyframes.size():
		plays.append(Tweens.animate(targets.keyframes[i], timeline(standing(waypoint(i) + 0.1, Out.BACK, 0.4))))
	for i in targets.logo.size(): plays.append(Tweens.animate(targets.logo[i], drop(i)))
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

# When the dot reaches each ruler tick, BOUNCE letter, ELASTIC letter and keyframe diamond.
func tick(i: int) -> float: return 4.4 + 0.3 * i
func landing(i: int) -> float: return 7.75 + 0.45 * i
func stretch(i: int) -> float: return 11.3 + 0.18 * i
func waypoint(i: int) -> float: return 12.9 + 0.3 * i

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

## The rig glides along the journey, turning gently, then cranes back to reveal it.
func rig() -> TweensGdKeyframes:
	return timeline({
		t(0): {"position": Vector3(-1.4, 0.4, 0), "rotation_degrees": Vector3(-20, -18, 0)},
		t(1.8): {"position": Vector3(-1.1, 0.4, 0), "rotation_degrees": Vector3(-22, -12, 0)},
		t(3.7): {"position": Vector3(-0.2, 0.35, -0.3), "rotation_degrees": Vector3(-25, -4, 0)},
		t(6.8): {"position": Vector3(5, 0.35, -0.5), "rotation_degrees": Vector3(-28, 6, 0)},
		t(9.6): {"position": Vector3(10.4, 0.6, -0.1), "rotation_degrees": Vector3(-20, -6, 0)},
		t(12.3): {"position": Vector3(15.6, 0.4, -0.2), "rotation_degrees": Vector3(-25, 8, 0)},
		t(15): {"position": Vector3(21.2, 0.35, -0.3), "rotation_degrees": Vector3(-30, -5, 0)},
		t(17.4): {"position": Vector3(27.6, 0.5, -0.3), "rotation_degrees": Vector3(-18, 0, 0)},
		t(17.7): {"position": Vector3(27.6, 0.5, -0.3), "rotation_degrees": Vector3(-18, 0, 0)},
		t(19.1): {"position": Vector3(23, 0.3, -0.4), "rotation_degrees": Vector3(-34, 36, 0), "interpolation": InOut.SINE},
	})

## The camera dollies out a little per station, then far back for the reveal.
func lens() -> TweensGdKeyframes:
	return timeline({
		t(0): {"z": 3.8}, t(1.8): {"z": 4.0}, t(3.7): {"z": 4.6}, t(6.8): {"z": 5.0}, t(9.6): {"z": 4.8},
		t(12.3): {"z": 4.8}, t(15): {"z": 5.0}, t(17.4): {"z": 6.0}, t(17.7): {"z": 6.0},
		t(19.1): {"z": 11.0, "interpolation": InOut.SINE},
	})

## Paper fades in, and fades back over the jump to the loop's first keys.
func fade() -> TweensGdKeyframes:
	return timeline({t(0): {"alpha": 1.0}, t(0.7): {"alpha": 0.0}, t(19.45): {"alpha": 0.0}, t(19.85): {"alpha": 1.0}})

## The dot's path over the page.
func route() -> TweensGdKeyframes:
	var origin := middle(targets.from)
	var end := middle(targets.to)
	var off: float = targets.bounce[-1].position.x
	var period: Vector3 = targets.period
	var frames := {
		t(0): {"x": origin, "z": 0.0}, t(1.8): {"x": origin},
		t(3.4): {"x": end, "interpolation": InOut.SINE}, t(3.7): {"x": end},
		t(tick(0)): {"x": targets.ticks[0].position.x, "interpolation": InOut.SINE},
		# One InOut.Cubic segment: the dot reaches each tick of the ruler at equal time steps.
		t(tick(8)): {"x": targets.ticks[8].position.x, "interpolation": InOut.CUBIC},
		t(7.3): {"x": targets.bounce[0].position.x - 0.9},
	}
	for i in targets.bounce.size():
		key(frames, landing(i), {"x": targets.bounce[i].position.x, "interpolation": Tweens.Interpolation.LINEAR})
	key(frames, 10.45, {"x": off + 1, "interpolation": Tweens.Interpolation.LINEAR})
	key(frames, 10.75, {"x": off + 1.5, "z": 0.0})
	key(frames, stretch(0), {"x": targets.elastic[0].position.x, "z": 0.35})
	key(frames, stretch(6), {"x": targets.elastic[6].position.x, "z": 0.35, "interpolation": Tweens.Interpolation.LINEAR})
	# Smooth interpolation curves the path through every diamond.
	for i in targets.diamonds.size():
		var diamond_at: Vector3 = targets.diamonds[i].position
		key(frames, waypoint(i), {"x": diamond_at.x, "z": diamond_at.z})
	key(frames, 16.8, {"x": period.x, "z": 0.4})
	key(frames, 17, {"x": period.x, "z": 0.4})
	key(frames, 17.4, {"x": period.x, "z": period.z, "interpolation": Out.BACK})
	return timeline(frames)

## The disc above the carrier: drop, hops over BOUNCE, squash and stretch, and its turn into the period.
func disc() -> TweensGdKeyframes:
	var top: float = targets.cap + RADIUS
	var frames := {
		t(0): {"y": 3.2, "scale": Vector3.ONE, "modulate": EMBER}, t(0.3): {"y": 3.2},
		t(0.9): {"y": RADIUS, "interpolation": In.QUAD}, t(1.12): {"y": RADIUS + 0.35, "interpolation": Out.QUAD},
		t(1.35): {"y": RADIUS, "interpolation": In.QUAD}, t(7.3): {"y": RADIUS},
		t(10.22): {"y": top + 0.5, "interpolation": Out.QUAD}, t(10.45): {"y": RADIUS, "interpolation": In.QUAD},
		t(10.6): {"y": RADIUS + 0.3, "interpolation": Out.QUAD}, t(10.75): {"y": RADIUS, "interpolation": In.QUAD},
		t(17): {"y": RADIUS, "scale": Vector3.ONE, "modulate": EMBER}, t(17.3): {"modulate": GD_GREEN},
		# Shrinks to the size of the wordmark's period, resting on the page.
		t(17.4): {"y": RADIUS * 0.45, "scale": Vector3(0.45, 0.45, 1), "interpolation": Out.BACK},
	}
	for i in targets.bounce.size():
		key(frames, landing(i) - 0.225, {"y": top + 0.7, "interpolation": Out.QUAD})
		key(frames, landing(i), {"y": top, "interpolation": In.QUAD})
	var landings_at := [0.9, 1.35, 10.45]
	for i in targets.bounce.size(): landings_at.append(landing(i))
	for at: float in landings_at:
		key(frames, at, {"scale": Vector3.ONE})
		key(frames, at + 0.05, {"scale": Vector3(1.3, 0.72, 1)})
		key(frames, at + 0.13, {"scale": Vector3(0.9, 1.12, 1)})
		key(frames, at + 0.28, {"scale": Vector3.ONE})
	return timeline(frames)

## BOUNCE stands up with a bounce, and each letter squashes under the landing dot.
func bounce(i: int) -> TweensGdKeyframes:
	var frames := standing(7 + 0.08 * i, Out.BOUNCE, 0.6)
	var at := landing(i)
	key(frames, 0, {"scale": Vector3.ONE})
	key(frames, at, {"scale": Vector3.ONE})
	key(frames, at + 0.06, {"scale": Vector3(1.15, 0.78, 1)})
	key(frames, at + 0.45, {"scale": Vector3.ONE, "interpolation": Out.BACK})
	return timeline(frames)

## ELASTIC stands up elastically, and each letter stretches as the dot passes and springs back.
func elastic(i: int) -> TweensGdKeyframes:
	var frames := standing(10.4 + 0.06 * i, Out.ELASTIC, 0.8)
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

## A wordmark letter falls onto the page and squashes on landing.
func drop(i: int) -> TweensGdKeyframes:
	var at := 15.7 + 0.1 * i
	var hidden := Vector3.ONE * 0.01
	return timeline({
		t(0): {"y": 3.5, "scale": hidden}, t(at): {"y": 3.5, "scale": hidden}, t(at + 0.1): {"scale": Vector3.ONE},
		t(at + 0.5): {"y": 0.0, "scale": Vector3.ONE, "interpolation": In.QUAD},
		t(at + 0.58): {"scale": Vector3(1.18, 0.8, 1)}, t(at + 0.9): {"scale": Vector3.ONE, "interpolation": Out.BACK},
	})

## Where the dot lands on the page as [time, x, z]: its drop, the end of BOUNCE, and the period.
func landings() -> Array:
	var period: Vector3 = targets.period
	var off: float = targets.bounce[-1].position.x
	return [[0.9, middle(targets.from), 0.0], [10.45, off + 1, 0.0], [17.4, period.x, period.z]]

## Rings spread over the page from every landing.
func ripple(i: int) -> TweensGdKeyframes:
	var frames := {t(0): {"x": middle(targets.from), "z": 0.0, "scale": 0.2, "transparency": 1.0}}
	for spot: Array in landings():
		var at: float = spot[0] + 0.12 * i
		key(frames, at - 0.02, {"x": spot[1], "z": spot[2], "scale": 0.2, "transparency": 1.0})
		key(frames, at, {"transparency": 0.2})
		key(frames, at + 1.2, {"scale": 2.6 + 0.6 * i, "transparency": 1.0, "interpolation": Out.CUBIC})
	return timeline(frames)

## The trail lays dots once the dot lands. Under the closing fade, a burst of speed ages every dot out.
func lay() -> TweensGdKeyframes:
	return timeline({
		t(0): {"amount_ratio": 0.0, "speed_scale": 1.0}, t(0.88): {"amount_ratio": 0.0}, t(0.92): {"amount_ratio": 1.0},
		t(19.4): {"amount_ratio": 1.0}, t(19.42): {"amount_ratio": 0.0},
		t(19.86): {"speed_scale": 1.0}, t(19.9): {"speed_scale": 400.0}, t(19.98): {"speed_scale": 400.0}, t(20): {"speed_scale": 1.0},
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
	key(frames, 16.15, {"x": first.position.x, "z": first.position.z})
	key(frames, 16.18, {"amount_ratio": 0.0})
	key(frames, 16.2, {"amount_ratio": 1.0})
	key(frames, 16.92, {"x": last.position.x, "interpolation": Tweens.Interpolation.LINEAR})
	key(frames, 16.95, {"amount_ratio": 1.0})
	key(frames, 16.97, {"amount_ratio": 0.0})
	return timeline(frames)

## Confetti bursts from the wordmark when the dot becomes its period.
func burst() -> TweensGdKeyframes:
	return timeline({
		t(0): {"amount_ratio": 0.0}, t(17.42): {"amount_ratio": 0.0}, t(17.45): {"amount_ratio": 1.0},
		t(17.8): {"amount_ratio": 1.0}, t(17.83): {"amount_ratio": 0.0},
	})
