# SPDX-License-Identifier: MIT
# SPDX-FileCopyrightText: 2026 Moritz Voss
extends "res://Gallery/GDScript/GalleryAnimation.gd"
# Scene setup is in Showreel.cs. Every node plays one definition that spans the whole loop.

const CYAN = Color("22e3ff")
const YELLOW = Color("ffd23f")
const PINK = Color("ff3d8b")
const VIOLET = Color("8a6cff")
const LENGTH = 20.0

func animate() -> void:
	var bar := bars()
	var flights := captions()
	var plays: Array[TweensGdGroup] = [
		Tweens.animate(targets.rig, rig()),
		Tweens.animate(targets.camera, lens()),
		Tweens.animate(targets.hero, hero()),
		Tweens.animate(targets.flash, flashes()),
		Tweens.animate(targets.fade, fade()),
	]
	for node: ColorRect in targets.bars: plays.append(Tweens.animate(node, bar))
	for i in targets.letters.size():
		var letter: Label3D = targets.letters[i]
		plays.append(Tweens.animate(letter, letter_flight(i, letter.position, letter.modulate)))
	for i in targets.words.size(): plays.append(Tweens.animate(targets.words[i], flights[i]))
	plays.append(Tweens.animate(targets.ticker, ticker()))
	for i in targets.shockwaves.size(): plays.append(Tweens.animate(targets.shockwaves[i], shockwave(i)))
	plays.append_array([Tweens.animate(targets.stream, flow()), Tweens.animate(targets.wind, wind()), Tweens.animate(targets.sparks, sparks())])
	for i in targets.streaks.size():
		plays.append(Tweens.animate(targets.streaks[i], pass_along(0.8 + fmod(i * 0.618034, 1.0), fmod(i * 0.381966, 1.0))))
	for i in targets.chunks.size():
		var tumble := Vector3(90 * (1 + i % 3), 120, 60 * (i % 2))
		plays.append(Tweens.animate(targets.chunks[i], pass_along(2.6 + fmod(i * 0.618034, 1.0) * 1.4, fmod(i * 0.381966, 1.0) * 2, tumble)))
	for i in targets.wipes.size(): plays.append(Tweens.animate(targets.wipes[i], wipe(i)))
	for play in plays: await play.wait()

## Converts seconds on the loop to a percentage stop.
func t(at: float) -> float:
	return at / LENGTH * 100.0

func timeline(frames: Dictionary, interpolation := Tweens.Interpolation.SMOOTH) -> TweensGdKeyframes:
	var definition := Tweens.keyframes(frames, LENGTH * tempo, Tweens.Ease.LINEAR, interpolation)
	definition.options.repeats = Tweens.INFINITE
	return definition

## Adds channels at a stop, next to any channels already keyed there.
func key(frames: Dictionary, at: float, channels: Dictionary) -> void:
	var frame: Dictionary = frames.get_or_add(t(at), {})
	frame.merge(channels)

## The rig orbits: pitch, yaw and roll. Whip pans ease in and out between held framings.
func rig() -> TweensGdKeyframes:
	return timeline({
		t(0): {"position": Vector3(-2, 0.5, 0), "rotation_degrees": Vector3(-35, -120, 0)},
		t(2.6): {"position": Vector3(0, 0, 0), "rotation_degrees": Vector3(-8, -12, -5)},
		t(4.6): {"rotation_degrees": Vector3(-5, 6, 3)},
		t(5): {"rotation_degrees": Vector3(-6, 10, 0)},
		t(5.5): {"rotation_degrees": Vector3(-12, 90, 0), "interpolation": InOut.EXPO},
		t(6.6): {"rotation_degrees": Vector3(-10, 98, 2)},
		t(6.9): {"rotation_degrees": Vector3(-12, 180, 0), "interpolation": InOut.EXPO},
		t(7.9): {"rotation_degrees": Vector3(-10, 188, -2)},
		t(8.2): {"rotation_degrees": Vector3(-12, 270, 0), "interpolation": InOut.EXPO},
		t(9.2): {"rotation_degrees": Vector3(-10, 278, 2)},
		t(9.5): {"rotation_degrees": Vector3(-12, 360, 0), "interpolation": InOut.EXPO},
		t(10.4): {"rotation_degrees": Vector3(-10, 370, 0)},
		t(11.4): {"position": Vector3(0, 0.3, 0), "rotation_degrees": Vector3(-30, 420, 12)},
		t(13.3): {"rotation_degrees": Vector3(-14, 470, -6)},
		t(14.6): {"rotation_degrees": Vector3(-10, 500, 0)},
		t(15.4): {"position": Vector3(0, 0.1, 0), "rotation_degrees": Vector3(-4, 720, 0), "interpolation": InOut.EXPO},
		t(18.4): {"rotation_degrees": Vector3(-2, 726, 0)},
		t(20): {"rotation_degrees": Vector3(-6, 740, 4)},
	})

## The camera dollies along the rig's Z axis, punches its field of view and shakes on impacts.
func lens() -> TweensGdKeyframes:
	var frames := {
		t(0): {"z": 10.0, "fov": 60.0}, t(2.6): {"z": 6.5, "fov": 44.0}, t(5): {"z": 6.0}, t(5.5): {"z": 4.6},
		t(10.4): {"z": 4.6}, t(11.4): {"z": 6.5, "fov": 50.0}, t(13.3): {"z": 3.4, "fov": 72.0}, t(14.6): {"z": 5.0},
		t(15.4): {"z": 7.5, "fov": 40.0}, t(19.2): {"z": 7.2}, t(20): {"z": 6.4, "fov": 30.0},
	}
	for punch: Array in [[3.2, 44.0, 0.1], [13.4, 72.0, 0.25], [17.0, 40.0, 0.08], [17.5, 38.0, 0.08], [18.0, 36.0, 0.12]]:
		var at: float = punch[0]
		var fov: float = punch[1]
		var shake: float = punch[2]
		key(frames, at, {"fov": fov})
		key(frames, at + 0.1, {"fov": fov - 8})
		key(frames, at + 0.4, {"fov": fov - 2})
		var offsets := [0.0, 1.0, -0.8, 0.6, -0.4, 0.2, 0.0]
		for i in offsets.size():
			key(frames, at + i * 0.04, {"h_offset": offsets[i] * shake, "v_offset": offsets[-1 - i] * shake})
	return timeline(frames)

## A white flash on every impact and whip landing.
func flashes() -> TweensGdKeyframes:
	var frames := {t(0): {"alpha": 0.0}}
	for impact: Array in [[3.2, 0.9], [5.0, 0.5], [5.5, 0.25], [6.9, 0.25], [8.2, 0.25], [9.5, 0.25],
			[13.4, 1.0], [15.4, 0.7], [17.0, 0.35], [17.5, 0.35], [18.0, 0.45]]:
		var at: float = impact[0]
		key(frames, at - 0.04, {"alpha": 0.0})
		key(frames, at, {"alpha": impact[1]})
		key(frames, at + 0.4, {"alpha": 0.0, "interpolation": Out.CUBIC})
	return timeline(frames, Tweens.Interpolation.LINEAR)

## Fades in from black and back out, hiding the jump to the loop's first keys.
func fade() -> TweensGdKeyframes:
	return timeline({t(0): {"alpha": 1.0}, t(0.8): {"alpha": 0.0}, t(19.2): {"alpha": 0.0}, t(20): {"alpha": 1.0}})

## Both letterbox bars play this one definition during the hero shot.
func bars() -> TweensGdKeyframes:
	return timeline({
		t(0): {"scale": Vector2(1, 0)}, t(10.2): {"scale": Vector2(1, 0)},
		t(10.8): {"scale": Vector2(1, 1), "interpolation": Out.CUBIC}, t(14.8): {"scale": Vector2(1, 1)},
		t(15.3): {"scale": Vector2(1, 0), "interpolation": In.CUBIC},
	})

## Places a letter on the title's baseline, shifted by (x, y), tilted by degrees and turned: [x, y, tilt, turn].
func pose(home: Vector3, shape: Array) -> Dictionary:
	var x: float = shape[0]
	var y: float = shape[1]
	var tilt: float = shape[2]
	var turn: Vector3 = shape[3]
	return {"position": home + Vector3(x, y + home.x * tan(deg_to_rad(tilt)), 0), "rotation_degrees": turn + Vector3(0, 0, tilt)}

## Each letter flies in, slides across the frame with a turn, explodes past the camera, slams back from
## behind it and snakes between framings on the title beats.
func letter_flight(i: int, home: Vector3, color: Color) -> TweensGdKeyframes:
	var scatter := Vector3(cos(i * 2.4) * 7, sin(i * 1.9) * 3 + 1, -6.0 - i % 3 * 3)
	var spin := Vector3(i * 67 % 360 - 180, i * 131 % 360 - 180, i * 53 % 90 - 45)
	var blast := Vector3(home.x * 7, 3.0 if i % 2 == 0 else -2.5, 4.5)
	var behind := Vector3(home.x * 2.5, home.y, 11)
	var low_left := [-1.4, -0.55, -8.0, Vector3.ZERO]
	var high_right := [1.3, 0.5, 6.0, Vector3(0, 360, 0)]
	# Each letter trails its left neighbour, so the title snakes from pose to pose.
	var land := 1 + 0.15 * i
	var slam := 15.3 + 0.06 * i
	var lag := 0.03 * i
	# Transparency fades the outline with the glyphs; modulate only tints.
	var frames := {
		t(0): {"position": scatter, "rotation_degrees": spin, "scale": 0.4, "modulate": color, "transparency": 1.0},
		t(land - 0.75): {"position": scatter, "rotation_degrees": spin, "scale": 0.4, "transparency": 1.0},
		t(land - 0.55): {"transparency": 0.0},
		t(land): pose(home, low_left).merged({"scale": 1.0, "interpolation": Out.BACK}),
		t(3.2): {"scale": 1.0, "modulate": color},
		t(3.3 + lag): {"scale": 1.45, "modulate": YELLOW},
		t(3.45 + lag): pose(home, low_left),
		t(3.75 + lag): {"scale": 1.0, "interpolation": Out.BACK},
		t(4.2): {"modulate": color},
		t(4.45 + lag): pose(home, high_right).merged({"interpolation": Out.BACK}),
		t(4.95): pose(home, high_right),
		t(5.35): {"transparency": 0.0},
		t(5.6): {"position": blast, "rotation_degrees": spin * 2, "transparency": 1.0, "interpolation": Out.QUAD},
		t(slam - 0.4): {"transparency": 1.0},
		t(slam - 0.35): {"position": behind, "rotation_degrees": Vector3(0, 0, (i - 4) * 25), "transparency": 0.0},
		t(16.9): {"modulate": color},
		t(18.8): {"modulate": color},
	}
	# The slam lands tilted on the lower left; each beat punches, then slides and twists to the next framing.
	var poses := [
		[-1.5, -0.6, -10.0, Vector3.ZERO], [1.4, 0.5, 8.0, Vector3(0, 360, 0)],
		[-0.9, 0.55, -6.0, Vector3(360, 360, 0)], [0.0, 0.0, 0.0, Vector3(360, 360, 0)],
	]
	var tints := [CYAN, YELLOW, PINK]
	key(frames, slam, pose(home, poses[0]).merged({"interpolation": Out.BACK}))
	for beat in tints.size():
		var at := 17 + 0.5 * beat + lag
		key(frames, at, pose(home, poses[beat]).merged({"scale": 1.0}))
		key(frames, at + 0.08, {"scale": 1.45, "modulate": tints[beat]})
		key(frames, at + 0.35, pose(home, poses[beat + 1]).merged({"scale": 1.0, "interpolation": Out.BACK}))
	return timeline(frames)

## One word per whip pan. Each crosses the frame in the way its channel names.
func captions() -> Array[TweensGdKeyframes]:
	return [
		# POSITION skids in along the bottom, leans as it brakes, then dashes out to the upper right.
		timeline({
			t(0): {"position": Vector3(-7, -0.9, 0), "rotation_degrees": Vector3(0, 0, 0), "transparency": 1.0},
			t(5.25): {"position": Vector3(-7, -0.9, 0), "transparency": 1.0}, t(5.3): {"transparency": 0.0},
			t(5.6): {"position": Vector3(-1.5, -0.9, 0), "rotation_degrees": Vector3(0, 0, 10), "interpolation": Out.BACK},
			t(5.9): {"rotation_degrees": Vector3(0, 0, 0)}, t(6.45): {"position": Vector3(-1.1, -0.85, 0)}, t(6.75): {"transparency": 0.0},
			t(6.8): {"position": Vector3(8, 1.2, 0), "rotation_degrees": Vector3(0, 0, -12), "interpolation": In.BACK},
			t(6.85): {"transparency": 1.0},
		}),
		# ROTATION corkscrews down from the upper right, then barrel-rolls out to the lower left.
		timeline({
			t(0): {"position": Vector3(4.5, 1.6, 0), "rotation_degrees": Vector3(0, -270, 40), "transparency": 1.0},
			t(6.75): {"position": Vector3(4.5, 1.6, 0), "rotation_degrees": Vector3(0, -270, 40), "transparency": 1.0},
			t(6.8): {"transparency": 0.0},
			t(7.2): {"position": Vector3(1.1, 0.8, 0), "rotation_degrees": Vector3(0, 0, -6), "interpolation": Out.BACK},
			t(7.85): {"position": Vector3(0.6, 0.65, 0), "rotation_degrees": Vector3(0, 0, -2)}, t(8.1): {"transparency": 0.0},
			t(8.15): {"position": Vector3(-6, -1.4, 0), "rotation_degrees": Vector3(360, 0, 30), "interpolation": In.CUBIC},
			t(8.2): {"transparency": 1.0},
		}),
		# SCALE pops in on the upper left and hops across the frame, swelling on every landing.
		timeline({
			t(0): {"position": Vector3(-2.3, 0.75, 0), "rotation_degrees": Vector3(0, 0, 0), "scale": 0.01},
			t(8.1): {"scale": 0.01}, t(8.3): {"scale": 1.5, "interpolation": Out.QUAD}, t(8.45): {"scale": 1.0},
			t(8.55): {"position": Vector3(-2.3, 0.75, 0), "rotation_degrees": Vector3(0, 0, 0)},
			t(8.8): {"position": Vector3(0, -0.55, 0), "rotation_degrees": Vector3(0, 0, -10), "scale": 1.35, "interpolation": Out.BACK},
			t(8.95): {"scale": 1.0}, t(9): {"position": Vector3(0, -0.55, 0), "rotation_degrees": Vector3(0, 0, 0)},
			t(9.25): {"position": Vector3(2.2, 0.6, 0), "rotation_degrees": Vector3(0, 0, 8), "scale": 1.35, "interpolation": Out.BACK},
			t(9.35): {"scale": 1.0}, t(9.5): {"scale": 0.01, "interpolation": In.BACK},
		}),
		# COLOR flips up from the lower right and cycles its tint as it slides out to the left.
		timeline({
			t(0): {"position": Vector3(6, -1, 0), "rotation_degrees": Vector3(-90, 0, 0), "modulate": PINK, "transparency": 1.0},
			t(9.35): {"position": Vector3(6, -1, 0), "rotation_degrees": Vector3(-90, 0, 0), "transparency": 1.0},
			t(9.4): {"transparency": 0.0},
			t(9.7): {"position": Vector3(1.4, -0.9, 0), "rotation_degrees": Vector3(0, 0, 0), "interpolation": Out.BACK},
			t(9.75): {"modulate": PINK}, t(10): {"modulate": YELLOW}, t(10.25): {"modulate": CYAN},
			t(10.5): {"position": Vector3(0.3, -0.75, 0), "modulate": VIOLET}, t(10.75): {"modulate": Color.WHITE},
			t(10.8): {"transparency": 0.0},
			t(10.85): {"position": Vector3(-7, -0.4, 0), "rotation_degrees": Vector3(0, 0, 15), "interpolation": In.BACK},
			t(10.9): {"transparency": 1.0},
		}),
	]

## Slides in at the top of the frame, flips down to the bottom for the punch, then shoots off to the left.
func ticker() -> TweensGdKeyframes:
	return timeline({
		t(0): {"position": Vector3(5, 0.9, -3), "rotation_degrees": Vector3(0, 0, 0), "scale": 1.0, "transparency": 1.0},
		t(10.9): {"position": Vector3(5, 0.9, -3), "transparency": 1.0}, t(10.95): {"transparency": 0.0},
		t(11.35): {"position": Vector3(0.9, 0.9, -3), "interpolation": Out.BACK},
		t(12.5): {"position": Vector3(0.4, 0.9, -3), "rotation_degrees": Vector3(0, 0, 0)},
		t(12.95): {"position": Vector3(-0.9, -0.95, -3), "rotation_degrees": Vector3(360, 0, 0), "interpolation": InOut.BACK},
		t(13.4): {"scale": 1.0}, t(13.48): {"scale": 1.4}, t(13.8): {"scale": 1.0, "interpolation": Out.BACK},
		t(14.4): {"position": Vector3(-1.2, -0.95, -3), "rotation_degrees": Vector3(360, 0, 0)}, t(14.7): {"transparency": 0.0},
		t(14.75): {"position": Vector3(-6, -0.95, -3), "rotation_degrees": Vector3(360, 0, -20), "interpolation": In.BACK},
		t(14.8): {"transparency": 1.0},
	})

## The hero rises through the floor, swallows the stream, punches and vanishes.
func hero() -> TweensGdKeyframes:
	return timeline({
		t(0): {"position": Vector3(0, -6, 0), "rotation_degrees": Vector3(0, 0, 0), "scale": 0.01},
		t(10.2): {"position": Vector3(0, -6, 0), "scale": 0.01},
		t(11.3): {"position": Vector3(0, 0.3, 0), "rotation_degrees": Vector3(20, 60, 0), "scale": 1.4, "interpolation": Out.BACK},
		t(13.25): {"rotation_degrees": Vector3(25, 110, 10), "scale": 1.1},
		t(13.4): {"scale": 2.2, "interpolation": Out.QUAD},
		t(13.8): {"scale": 1.4, "interpolation": Out.BACK},
		t(14.5): {"rotation_degrees": Vector3(35, 160, 0), "scale": 1.6},
		t(14.85): {"scale": 0.01, "interpolation": In.BACK},
	})

## Rings expand and fade on the hero's punch, then turn to face the camera for the title beats.
func shockwave(i: int) -> TweensGdKeyframes:
	var frames := {
		t(0): {"rotation_degrees": Vector3(i * 30 - 30, 0, i * 20), "scale": 0.2, "transparency": 1.0},
		t(14.6): {"rotation_degrees": Vector3(i * 30 - 30, 0, i * 20)},
		t(16.4): {"rotation_degrees": Vector3(90, 0, 0)},
	}
	for wave: Array in [[13.4, 8.0], [17.0, 6.0], [18.0, 7.0]]:
		var begin: float = wave[0] + 0.1 * i
		key(frames, begin - 0.02, {"scale": 0.2, "transparency": 1.0})
		key(frames, begin, {"transparency": 0.1})
		key(frames, begin + 0.9, {"scale": wave[1], "transparency": 1.0, "interpolation": Out.CUBIC})
	return timeline(frames)

## The stream's direction: right to left behind the title, rising during the whip pans, into the screen
## toward the hero, out at the camera on its punch, and upward behind the finale. It turns under the flashes.
func flow() -> TweensGdKeyframes:
	return timeline({
		t(0): {"rotation_degrees": Vector3(0, 0, 180)}, t(4.95): {"rotation_degrees": Vector3(0, 0, 180)},
		t(5.1): {"rotation_degrees": Vector3(0, 0, 15)}, t(10): {"rotation_degrees": Vector3(0, 0, 15)},
		t(10.3): {"rotation_degrees": Vector3(0, 90, 0)}, t(13.35): {"rotation_degrees": Vector3(0, 90, 0)},
		t(13.45): {"rotation_degrees": Vector3(0, -90, 0)}, t(15.25): {"rotation_degrees": Vector3(0, -90, 0)},
		t(15.45): {"rotation_degrees": Vector3(0, 0, 90)},
	})

## One trip along the stream: fade in, cross, fade out and return unseen. It repeats after its own delay.
func pass_along(trip: float, delay: float, tumble := Vector3.ZERO) -> TweensGdKeyframes:
	var frames := {
		0: {"x": -16.0, "transparency": 1.0}, 8: {"transparency": 0.0}, 84: {"transparency": 0.0},
		92: {"x": 16.0, "transparency": 1.0}, 100: {"x": -16.0},
	}
	if tumble != Vector3.ZERO:
		for stop: int in [0, 92, 100]: frames[stop]["rotation_degrees"] = tumble if stop == 92 else Vector3.ZERO
	var definition := Tweens.keyframes(frames, trip * tempo, Tweens.Ease.LINEAR, Tweens.Interpolation.LINEAR)
	definition.options.delay = delay * tempo
	definition.options.repeats = Tweens.INFINITE
	return definition

## The wind thickens for the swarm and the warp, then eases for the finale.
func wind() -> TweensGdKeyframes:
	return timeline({
		t(0): {"amount_ratio": 0.3}, t(4.9): {"amount_ratio": 0.3}, t(5.1): {"amount_ratio": 0.6}, t(10): {"amount_ratio": 0.6},
		t(10.3): {"amount_ratio": 1.0}, t(15.25): {"amount_ratio": 1.0}, t(15.45): {"amount_ratio": 0.5},
	})

## A short burst of sparks at every impact: the title punch, the blast, the hero and the title beats.
func sparks() -> TweensGdKeyframes:
	var frames := {t(0): {"position": Vector3(-1.4, -0.4, 0), "amount_ratio": 0.0}}
	for impact: Array in [[3.2, -1.4, -0.4], [5.0, 1.3, 0.65], [13.4, 0.0, 0.3], [15.4, -1.5, -0.45],
			[17.0, -1.5, -0.45], [17.5, 1.4, 0.65], [18.0, -0.9, 0.7]]:
		var at: float = impact[0]
		key(frames, at, {"position": Vector3(impact[1], impact[2], 0), "amount_ratio": 1.0})
		key(frames, at - 0.02, {"amount_ratio": 0.0})
		key(frames, at + 0.12, {"amount_ratio": 1.0})
		key(frames, at + 0.14, {"amount_ratio": 0.0})
	return timeline(frames)

## Flat bars sweep across the lens at each act change, alternating direction.
func wipe(i: int) -> TweensGdKeyframes:
	var lag := 0.05 * i
	return timeline({
		t(0): {"x": -4.0}, t(4.95 + lag): {"x": -4.0}, t(5.35 + lag): {"x": 4.0, "interpolation": InOut.CUBIC},
		t(10.05 + lag): {"x": 4.0}, t(10.45 + lag): {"x": -4.0, "interpolation": InOut.CUBIC},
		t(14.95 + lag): {"x": -4.0}, t(15.35 + lag): {"x": 4.0, "interpolation": InOut.CUBIC},
		t(19.9): {"x": 4.0}, t(20): {"x": -4.0},
	})
