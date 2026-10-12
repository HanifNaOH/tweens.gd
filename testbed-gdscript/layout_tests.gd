extends RefCounted

const T = preload("res://addons/tweens_gd/tweens.gd")
const Suite = preload("tests.gd")

var _host: Suite

func run(host: Node) -> bool:
	_host = host
	await _reorders_existing_items()
	await _resizes_existing_items()
	await _deferred_reflow_holds_visual_rects()
	await _rejects_overlap_and_cancels_cleanly()
	await _zero_duration_repeated_updates_and_freed_slots()
	_invalid_configuration_does_not_mutate()
	await _unstable_reflow_fails_within_bound()
	await _root_exit_finishes_owner()
	await _wait_frames(2)
	return true

func check(condition: bool, message: String) -> void:
	_host.check(condition, message)

func _make_container(vertical: bool, count: int) -> Array:
	var root: Control = VBoxContainer.new() if vertical else HBoxContainer.new()
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
	return [root, slots, visuals]

func _wait_frames(count: int) -> void:
	for _frame in count:
		await _host.get_tree().process_frame

func _reorders_existing_items() -> void:
	var scene := _make_container(false, 3)
	var root := scene[0] as HBoxContainer
	var slots: Array[Control] = scene[1]
	var visuals: Array[Control] = scene[2]
	await _wait_frames(2)
	var first_positions: Array[Vector2] = []
	for visual in visuals:
		first_positions.append(visual.global_position)
	var defaults := TweensGdLayoutOptions.new()
	defaults.duration = 0.2
	var layout := T.create_layout(root, defaults)
	var transition_options := defaults.copy()
	transition_options.duration = 0.1
	var run := layout.update(
		func(container: Control) -> void:
			var row := container as HBoxContainer
			row.move_child(slots[2], 0)
			row.queue_sort(),
		transition_options
	)
	for index in visuals.size():
		check((visuals[index] as Control).global_position.is_equal_approx(first_positions[index]),
			"layout update holds visual %d at its first position before reflow" % index)
	var reason: int = await run.end
	check(reason == T.Reason.COMPLETED and run.error.is_empty(), "reorder transition completes")
	check(visuals[2].global_position.is_equal_approx(slots[2].global_position), "moved visual reaches its reordered slot")
	for visual in visuals:
		check(visual.z_index == 0 and visual.z_as_relative, "successful transition restores visual draw order")
	check(layout.options.duration == defaults.duration, "per-update options do not mutate controller defaults")
	root.queue_free()

func _resizes_existing_items() -> void:
	var scene := _make_container(true, 2)
	var root := scene[0] as VBoxContainer
	var slots: Array[Control] = scene[1]
	var visuals: Array[Control] = scene[2]
	await _wait_frames(2)
	var first_size: Vector2 = visuals[0].size
	var second_position: Vector2 = visuals[1].global_position
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.08
	options.size_ease = Out.CUBIC
	options.stagger_each = 0.01
	var layout := T.create_layout(root)
	var run := layout.update(
		func(_container: Control) -> void:
			slots[0].custom_minimum_size.y = 100.0
			root.queue_sort(),
		options
	)
	var reason: int = await run.end
	check(reason == T.Reason.COMPLETED, "resize transition completes")
	check(not visuals[0].size.is_equal_approx(first_size), "resized visual interpolates to its slot size")
	check(visuals[0].size.is_equal_approx(slots[0].size), "resized visual settles at the first slot size")
	check(not visuals[1].global_position.is_equal_approx(second_position), "following visual tracks Container reflow")
	check(visuals[1].global_position.is_equal_approx(slots[1].global_position), "following visual settles at its new slot position")
	root.queue_free()

func _deferred_reflow_holds_visual_rects() -> void:
	var scene := _make_container(true, 3)
	var root := scene[0] as VBoxContainer
	var slots: Array[Control] = scene[1]
	var visuals: Array[Control] = scene[2]
	await _wait_frames(2)
	var first_rects: Array[Rect2] = []
	for visual in visuals:
		first_rects.append((visual as Control).get_global_rect())
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.5
	options.delay = 0.5
	var layout := T.create_layout(root, options)
	var run := layout.update(
		func(container: Control) -> void:
			var column := container as VBoxContainer
			column.move_child(slots[2], 0)
			column.queue_sort()
	)
	for _sample in 3:
		await _wait_frames(1)
		for index in visuals.size():
			var current_rect: Rect2 = (visuals[index] as Control).get_global_rect()
			var first_rect: Rect2 = first_rects[index]
			check(current_rect.position.distance_to(first_rect.position) < 0.05
				and current_rect.size.distance_to(first_rect.size) < 0.05,
				"deferred Container reflow holds visual %d at its first rect" % index)
	run.cancel()
	check(await run.end == T.Reason.CANCELLED, "deferred reflow test cancels cleanly")
	root.queue_free()

func _rejects_overlap_and_cancels_cleanly() -> void:
	var scene := _make_container(false, 3)
	var root := scene[0] as HBoxContainer
	var slots: Array[Control] = scene[1]
	var visuals: Array[Control] = scene[2]
	await _wait_frames(2)
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.5
	var layout := T.create_layout(root, options)
	var run := layout.update(
		func(container: Control) -> void:
			var row := container as HBoxContainer
			row.move_child(slots[2], 0)
			row.queue_sort()
	)
	var mutation_called := false
	var rejected := layout.update(func(_container: Control) -> void: mutation_called = true)
	check(rejected.completion_reason == T.Reason.FAILED, "overlapping layout update is rejected")
	check(not mutation_called, "rejected update does not invoke its mutation")
	await _wait_frames(5)
	run.cancel()
	var reason: int = await run.end
	check(reason == T.Reason.CANCELLED, "cancelled layout reports cancellation")
	for index in visuals.size():
		check((visuals[index] as Control).global_position.is_equal_approx((slots[index] as Control).global_position),
			"cancelled layout snaps visual %d to its final slot" % index)
	root.queue_free()

func _invalid_configuration_does_not_mutate() -> void:
	var scene := _make_container(false, 1)
	var root := scene[0] as HBoxContainer
	await _wait_frames(1)
	var options := TweensGdLayoutOptions.new()
	options.duration = NAN
	var layout := T.create_layout(root, options)
	var mutation_called := false
	var run := layout.update(func(_container: Control) -> void: mutation_called = true)
	check(run.completion_reason == T.Reason.FAILED, "invalid layout options fail immediately")
	check(not mutation_called, "invalid layout options do not invoke the mutation")
	check(not run.error.is_empty(), "failed layout handle retains an error message")
	root.queue_free()

func _zero_duration_repeated_updates_and_freed_slots() -> void:
	var scene := _make_container(false, 3)
	var root := scene[0] as HBoxContainer
	var slots: Array[Control] = scene[1]
	var visuals: Array[Control] = scene[2]
	await _wait_frames(2)
	var layout := T.create_layout(root)
	var instant_options := TweensGdLayoutOptions.new()
	instant_options.duration = 0.0
	var first := layout.update(
		func(container: Control) -> void:
			var row := container as HBoxContainer
			row.move_child(slots[2], 0)
			row.queue_sort(),
		instant_options
	)
	check(await first.end == T.Reason.COMPLETED, "zero-duration layout settles successfully")
	await _wait_frames(1)
	check(visuals[2].global_position.is_equal_approx(slots[2].global_position), "zero-duration visual reaches its slot")
	var next_options := TweensGdLayoutOptions.new()
	next_options.duration = 0.05
	var second := layout.update(
		func(container: Control) -> void:
			var row := container as HBoxContainer
			slots[1].queue_free()
			row.move_child(slots[0], 0)
			row.queue_sort(),
		next_options
	)
	check(await second.end == T.Reason.COMPLETED, "a completed controller accepts another update with a freed slot")
	check(visuals[0].global_position.is_equal_approx(slots[0].global_position), "surviving first visual reaches its slot")
	check(visuals[2].global_position.is_equal_approx(slots[2].global_position), "surviving reordered visual reaches its slot")
	check(not layout.active, "repeated transition releases the controller")
	root.queue_free()

func _root_exit_finishes_owner() -> void:
	var scene := _make_container(false, 2)
	var root := scene[0] as HBoxContainer
	await _wait_frames(2)
	var layout := T.create_layout(root)
	var run := layout.update(func(container: Control) -> void: container.queue_free())
	var reason: int = await run.end
	check(reason == T.Reason.OWNER_EXITED, "root exit settles a pending layout transition")
	check(not layout.active, "root exit releases the layout controller")

func _unstable_reflow_fails_within_bound() -> void:
	var scene := _make_container(true, 1)
	var root := scene[0] as VBoxContainer
	var slots: Array[Control] = scene[1]
	await _wait_frames(2)
	var state := {"expanded": false}
	var jitter := Timer.new()
	jitter.wait_time = 0.001
	jitter.one_shot = false
	jitter.timeout.connect(func() -> void:
		state.expanded = not state.expanded
		slots[0].custom_minimum_size.y = 41.0 if state.expanded else 40.0
		root.queue_sort()
	)
	root.add_child(jitter)
	jitter.start()
	var layout := T.create_layout(root)
	var run := layout.update(func(_container: Control) -> void: pass)
	var reason: int = await run.end
	jitter.stop()
	check(reason == T.Reason.FAILED, "unstable Container reflow fails at the frame bound")
	check(run.error.contains("did not stabilize"), "unstable reflow explains its failure")
	check(not layout.active, "unstable reflow releases the controller")
	root.queue_free()
