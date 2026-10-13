extends RefCounted

const T = preload("res://addons/tweens_gd/tweens.gd")
const Suite = preload("tests.gd")

class LayoutFixture extends RefCounted:
	var root: Container
	var slots: Array[Control] = []
	var visuals: Array[Control] = []

class MutationState extends RefCounted:
	var called := false

var _host: Suite

func run(host: Node) -> bool:
	_host = host
	await _reorders_existing_items()
	await _resizes_existing_items()
	await _resizes_during_animation_follow_live_slot_bounds()
	await _deferred_reflow_holds_visual_rects()
	await _rejects_overlap_and_cancels_cleanly()
	await _empty_auto_discovery_does_not_mutate()
	await _zero_duration_repeated_updates_and_freed_slots()
	await _invalid_configuration_does_not_mutate()
	await _unstable_reflow_fails_within_bound()
	await _root_exit_finishes_owner()
	await _wait_frames(2)
	return true

func check(condition: bool, message: String) -> void:
	_host.check(condition, message)

func _make_container(vertical: bool, count: int) -> LayoutFixture:
	var root: Container
	if vertical:
		root = VBoxContainer.new()
	else:
		root = HBoxContainer.new()
	root.size = Vector2(320, 240)
	root.custom_minimum_size = root.size
	_host.add_child(root)
	var slots: Array[Control] = []
	var visuals: Array[Control] = []
	for index in count:
		var slot := Control.new()
		slot.name = "Slot%d" % index
		slot.custom_minimum_size = Vector2(60, 40)
		var visual := ColorRect.new()
		visual.name = "Visual"
		visual.color = Color(0.2 + index * 0.1, 0.4, 0.7, 1.0)
		visual.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		slot.add_child(visual)
		root.add_child(slot)
		slots.append(slot)
		visuals.append(visual)
	var fixture := LayoutFixture.new()
	fixture.root = root
	fixture.slots = slots
	fixture.visuals = visuals
	return fixture

func _wait_frames(count: int) -> void:
	for _frame in count:
		await _host.get_tree().process_frame

func _reorders_existing_items() -> void:
	var scene := _make_container(false, 3)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	var first_positions: Array[Vector2] = []
	for visual in visuals:
		first_positions.append(visual.global_position)
	var defaults := TweensGdLayoutOptions.new()
	defaults.duration = 0.2
	var layout := T.create_layout(root, defaults)
	var transition_options := defaults.copy()
	transition_options.duration = 0.1
	var transition := layout.update(
		func(_container: Control) -> void:
			root.move_child(slots[2], 0)
			root.queue_sort(),
		transition_options
	)
	for index in visuals.size():
		check((visuals[index] as Control).global_position.is_equal_approx(first_positions[index]),
			"layout update holds visual %d at its first position before reflow" % index)
	var reason: int = await transition.end
	check(reason == T.Reason.COMPLETED and transition.error.is_empty(), "reorder transition completes")
	check(visuals[2].global_position.is_equal_approx(slots[2].global_position), "moved visual reaches its reordered slot")
	for visual in visuals:
		check(visual.z_index == 0 and visual.z_as_relative, "successful transition restores visual draw order")
	check(layout.options.duration == defaults.duration, "per-update options do not mutate controller defaults")
	root.queue_free()

func _resizes_existing_items() -> void:
	var scene := _make_container(true, 2)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	var first_size: Vector2 = visuals[0].size
	var second_position: Vector2 = visuals[1].global_position
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.08
	options.size_ease = Out.CUBIC
	options.stagger_each = 0.01
	var layout := T.create_layout(root)
	var transition := layout.update(
		func(_container: Control) -> void:
			slots[0].custom_minimum_size.y = 100.0
			root.queue_sort(),
		options
	)
	var reason: int = await transition.end
	check(reason == T.Reason.COMPLETED, "resize transition completes")
	check(not visuals[0].size.is_equal_approx(first_size), "resized visual interpolates to its slot size")
	check(visuals[0].size.is_equal_approx(slots[0].size), "resized visual settles at the first slot size")
	check(not visuals[1].global_position.is_equal_approx(second_position), "following visual tracks Container reflow")
	check(visuals[1].global_position.is_equal_approx(slots[1].global_position), "following visual settles at its new slot position")
	root.queue_free()

func _resizes_during_animation_follow_live_slot_bounds() -> void:
	var scene := _make_container(false, 2)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	for slot in slots:
		slot.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	await _wait_frames(2)
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.3
	var layout := T.create_layout(root, options)
	var transition := layout.update(func(_container: Control) -> void:
		root.move_child(slots[1], 0)
		root.queue_sort()
	)
	var waited := 0
	while not transition.is_animating and waited < 20:
		await _wait_frames(1)
		waited += 1
	check(transition.is_animating, "layout transition reaches its animation phase")
	for frame in 24:
		root.size.x = 340.0 + frame * 8.0
		root.queue_sort()
		await _wait_frames(1)
	var reason: int = await transition.end
	check(reason == T.Reason.COMPLETED, "transition follows repeated resizes to completion")
	for index in slots.size():
		var visual_rect: Rect2 = visuals[index].get_global_rect()
		var slot_rect: Rect2 = slots[index].get_global_rect()
		check(visual_rect.position.is_equal_approx(slot_rect.position)
			and visual_rect.size.is_equal_approx(slot_rect.size),
			"visual %d settles at the live slot rect" % index)
	root.queue_free()

func _deferred_reflow_holds_visual_rects() -> void:
	var scene := _make_container(true, 3)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	var first_rects: Array[Rect2] = []
	for visual in visuals:
		first_rects.append((visual as Control).get_global_rect())
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.5
	options.delay = 0.5
	var layout := T.create_layout(root, options)
	var transition := layout.update(
		func(_container: Control) -> void:
			root.move_child(slots[2], 0)
			root.queue_sort()
	)
	for _sample in 3:
		await _wait_frames(1)
		for index in visuals.size():
			var current_rect: Rect2 = (visuals[index] as Control).get_global_rect()
			var first_rect: Rect2 = first_rects[index]
			check(current_rect.position.distance_to(first_rect.position) < 0.05
				and current_rect.size.distance_to(first_rect.size) < 0.05,
				"deferred Container reflow holds visual %d at its first rect" % index)
	transition.cancel()
	var reason: int = await transition.end
	check(reason == T.Reason.CANCELLED, "deferred reflow test cancels cleanly")
	root.queue_free()

func _rejects_overlap_and_cancels_cleanly() -> void:
	var scene := _make_container(false, 3)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.5
	var layout := T.create_layout(root, options)
	var transition := layout.update(
		func(_container: Control) -> void:
			root.move_child(slots[2], 0)
			root.queue_sort()
	)
	var mutation_state := MutationState.new()
	var rejected := layout.update(func(_container: Control) -> void: mutation_state.called = true)
	check(rejected.completion_reason == T.Reason.FAILED, "overlapping layout update is rejected")
	check(not mutation_state.called, "rejected update does not invoke its mutation")
	await _wait_frames(5)
	transition.cancel()
	var reason: int = await transition.end
	check(reason == T.Reason.CANCELLED, "cancelled layout reports cancellation")
	for index in visuals.size():
		check((visuals[index] as Control).global_position.is_equal_approx((slots[index] as Control).global_position),
			"cancelled layout snaps visual %d to its final slot" % index)
	root.queue_free()

func _invalid_configuration_does_not_mutate() -> void:
	var scene := _make_container(false, 1)
	var root := scene.root
	await _wait_frames(1)
	var options := TweensGdLayoutOptions.new()
	options.duration = NAN
	var layout := T.create_layout(root, options)
	var mutation_state := MutationState.new()
	var transition := layout.update(func(_container: Control) -> void: mutation_state.called = true)
	check(transition.completion_reason == T.Reason.FAILED, "invalid layout options fail immediately")
	check(not mutation_state.called, "invalid layout options do not invoke the mutation")
	check(not transition.error.is_empty(), "failed layout handle retains an error message")
	root.queue_free()

func _empty_auto_discovery_does_not_mutate() -> void:
	var scene := _make_container(false, 0)
	var root := scene.root
	var layout := T.create_layout(root)
	var mutation_state := MutationState.new()
	var transition := layout.update(func(_container: Control) -> void: mutation_state.called = true)
	check(transition.completion_reason == T.Reason.FAILED, "layout without discovered items fails immediately")
	check(not mutation_state.called, "layout without discovered items does not invoke its mutation")
	check(not transition.error.is_empty(), "empty discovered layout explains its failure")
	root.queue_free()

func _zero_duration_repeated_updates_and_freed_slots() -> void:
	var scene := _make_container(false, 3)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	var layout := T.create_layout(root)
	var instant_options := TweensGdLayoutOptions.new()
	instant_options.duration = 0.0
	var first := layout.update(
		func(_container: Control) -> void:
			root.move_child(slots[2], 0)
			root.queue_sort(),
		instant_options
	)
	var first_reason: int = await first.end
	check(first_reason == T.Reason.COMPLETED, "zero-duration layout settles successfully")
	await _wait_frames(1)
	check(visuals[2].global_position.is_equal_approx(slots[2].global_position), "zero-duration visual reaches its slot")
	var next_options := TweensGdLayoutOptions.new()
	next_options.duration = 0.05
	var second := layout.update(
		func(_container: Control) -> void:
			slots[1].queue_free()
			root.move_child(slots[0], 0)
			root.queue_sort(),
		next_options
	)
	var second_reason: int = await second.end
	check(second_reason == T.Reason.COMPLETED, "a completed controller accepts another update with a freed slot")
	check(visuals[0].global_position.is_equal_approx(slots[0].global_position), "surviving first visual reaches its slot")
	check(visuals[2].global_position.is_equal_approx(slots[2].global_position), "surviving reordered visual reaches its slot")
	check(not layout.active, "repeated transition releases the controller")
	root.queue_free()

func _root_exit_finishes_owner() -> void:
	var scene := _make_container(false, 2)
	var root := scene.root
	await _wait_frames(2)
	var layout := T.create_layout(root)
	var transition := layout.update(func(container: Control) -> void: container.queue_free())
	var reason: int = await transition.end
	check(reason == T.Reason.OWNER_EXITED, "root exit settles a pending layout transition")
	check(not layout.active, "root exit releases the layout controller")

func _unstable_reflow_fails_within_bound() -> void:
	var scene := _make_container(true, 1)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	await _wait_frames(2)
	var state := {"expanded": false}
	var jitter := Timer.new()
	jitter.wait_time = 0.001
	jitter.one_shot = false
	var connection := jitter.timeout.connect(func() -> void:
		state.expanded = not state.expanded
		slots[0].custom_minimum_size.y = 41.0 if state.expanded else 40.0
		root.queue_sort()
	)
	check(connection == OK, "unstable reflow timer connects")
	root.add_child(jitter)
	jitter.start()
	var layout := T.create_layout(root)
	var transition := layout.update(func(_container: Control) -> void: pass)
	var reason: int = await transition.end
	jitter.stop()
	check(reason == T.Reason.FAILED, "unstable Container reflow fails at the frame bound")
	check(transition.error.contains("did not stabilize"), "unstable reflow explains its failure")
	check(not layout.active, "unstable reflow releases the controller")
	root.queue_free()
