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
	await _custom_visual_draw_order_is_preserved()
	await _opt_in_layout_z_order_restores_visual_settings()
	await _resizes_existing_items()
	await _resizes_during_animation_follow_live_slot_bounds()
	await _deferred_reflow_holds_visual_rects()
	await _rejects_overlap_and_cancels_cleanly()
	_empty_auto_discovery_does_not_mutate()
	await _zero_duration_repeated_updates_and_freed_slots()
	await _invalid_configuration_does_not_mutate()
	await _unstable_reflow_fails_within_bound()
	await _root_exit_finishes_owner()
	await _nested_slots_are_found_only_when_requested()
	await _new_slots_fade_and_scale_in()
	await _new_slots_appear_at_once_by_default()
	await _freed_slots_fade_out_and_are_freed()
	await _detached_slots_get_their_visual_back()
	await _global_z_order_holds_across_branches()
	_invalid_enter_and_exit_scales_are_rejected()
	await _empty_layout_animates_its_first_item_in()
	_explicit_items_reject_enter_and_exit()
	await _exiting_visual_follows_a_resized_root()
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

func _custom_visual_draw_order_is_preserved() -> void:
	var scene := _make_container(false, 2)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	visuals[0].z_index = 100
	visuals[0].z_as_relative = false
	visuals[1].z_index = -100
	visuals[1].z_as_relative = true
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.3
	var layout := T.create_layout(root, options)
	var transition := layout.update(func(_container: Control) -> void:
		root.move_child(slots[1], 0)
		root.queue_sort()
	)
	var waited := 0
	while not transition.is_animating and waited < 80:
		await _wait_frames(1)
		waited += 1
	check(transition.is_animating, "draw-order test reaches the animation phase")
	check(visuals[0].z_index == 100 and not visuals[0].z_as_relative,
		"transition preserves first visual Z settings during animation")
	check(visuals[1].z_index == -100 and visuals[1].z_as_relative,
		"transition preserves second visual Z settings during animation")
	transition.cancel()
	var reason: int = await transition.end
	check(reason == T.Reason.CANCELLED, "draw-order test cancels cleanly")
	check(visuals[0].z_index == 100 and not visuals[0].z_as_relative,
		"transition preserves first visual Z settings after cancellation")
	check(visuals[1].z_index == -100 and visuals[1].z_as_relative,
		"transition preserves second visual Z settings after cancellation")
	root.queue_free()

func _opt_in_layout_z_order_restores_visual_settings() -> void:
	var scene := _make_container(false, 2)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	visuals[0].z_index = 100
	visuals[0].z_as_relative = false
	visuals[1].z_index = -100
	visuals[1].z_as_relative = true
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.3
	options.use_layout_z_order = true
	var layout := T.create_layout(root, options)
	var transition := layout.update(func(_container: Control) -> void:
		root.move_child(slots[1], 0)
		root.queue_sort()
	)
	var waited := 0
	while not transition.is_animating and waited < 80:
		await _wait_frames(1)
		waited += 1
	check(transition.is_animating, "opt-in draw-order test reaches the animation phase")
	check(visuals[0].z_index == 1 and visuals[0].z_as_relative,
		"opt-in layout order is applied to the first visual")
	check(visuals[1].z_index == 0 and visuals[1].z_as_relative,
		"opt-in layout order is applied to the second visual")
	transition.cancel()
	var reason: int = await transition.end
	check(reason == T.Reason.CANCELLED, "opt-in draw-order test cancels cleanly")
	check(visuals[0].z_index == 100 and not visuals[0].z_as_relative,
		"cancellation restores first visual Z settings")
	check(visuals[1].z_index == -100 and visuals[1].z_as_relative,
		"cancellation restores second visual Z settings")
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

func _make_slot(slot_name: String) -> Control:
	var slot := Control.new()
	slot.name = slot_name
	slot.custom_minimum_size = Vector2(60, 40)
	var visual := ColorRect.new()
	visual.name = "Visual"
	visual.color = Color.RED
	visual.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	slot.add_child(visual)
	return slot

func _nested_slots_are_found_only_when_requested() -> void:
	var column := VBoxContainer.new()
	column.size = Vector2(320, 240)
	column.custom_minimum_size = column.size
	_host.add_child(column)
	var group := HBoxContainer.new()
	group.name = "Group"
	var slots: Array[Control] = [_make_slot("A"), _make_slot("B")]
	for slot in slots:
		group.add_child(slot)
	column.add_child(group)
	await _wait_frames(2)

	var flat := T.create_layout(column).update(func(_container: Control) -> void: pass)
	check(flat.completion_reason == T.Reason.FAILED, "direct-child discovery finds nothing in a wrapped layout")

	var options := TweensGdLayoutOptions.new()
	options.discover_nested = true
	options.duration = 0.05
	var layout := T.create_layout(column, options)
	var visual_b := slots[1].get_node("Visual") as Control
	var before := visual_b.global_position
	var transition := layout.update(func(_container: Control) -> void:
		group.move_child(slots[1], 0)
		group.queue_sort()
	)
	check(visual_b.global_position.is_equal_approx(before), "nested update holds the visual at its first position")
	var reason: int = await transition.end
	check(reason == T.Reason.COMPLETED, "nested layout completes")
	check(visual_b.global_position.is_equal_approx(slots[1].global_position), "nested visual reaches its slot")
	check(not slots[1].global_position.is_equal_approx(before), "nested slot moved")
	column.queue_free()

func _new_slots_fade_and_scale_in() -> void:
	var scene := _make_container(false, 2)
	var root := scene.root
	await _wait_frames(2)
	var added := _make_slot("New")
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.1
	options.animate_enter = true
	options.enter_scale = 0.5
	var layout1 := T.create_layout(root, options)
	var transition := layout1.update(func(container: Control) -> void:
		container.add_child(added)
	)
	var visual := added.get_node("Visual") as Control
	check(visual.modulate.a == 0.0, "entering visual starts invisible")
	var waited := 0
	while visual.modulate.a <= 0.2 and waited < 200:
		await _wait_frames(1)
		waited += 1
	check(visual.modulate.a > 0.2 and visual.modulate.a < 1.0, "entering visual fades in")
	check(visual.scale.x < 1.0 and visual.scale.x >= 0.5, "entering visual scales up")
	var reason: int = await transition.end
	check(reason == T.Reason.COMPLETED, "enter transition completes")
	check(visual.modulate.a == 1.0 and visual.scale == Vector2.ONE and visual.pivot_offset == Vector2.ZERO,
		"entering visual is restored afterwards")
	root.queue_free()

func _new_slots_appear_at_once_by_default() -> void:
	var scene := _make_container(false, 2)
	var root := scene.root
	await _wait_frames(2)
	var added := _make_slot("New")
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.05
	var layout2 := T.create_layout(root, options)
	var transition := layout2.update(func(container: Control) -> void:
		container.add_child(added)
	)
	check((added.get_node("Visual") as Control).modulate.a == 1.0, "new visuals are untouched without animate_enter")
	var reason: int = await transition.end
	check(reason == T.Reason.COMPLETED, "plain update with a new slot completes")
	root.queue_free()

func _freed_slots_fade_out_and_are_freed() -> void:
	var scene := _make_container(false, 3)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	var gone := visuals[1]
	var start := gone.global_position
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.1
	options.animate_exit = true
	options.exit_scale = 0.5
	var layout3 := T.create_layout(root, options)
	var transition := layout3.update(func(_container: Control) -> void:
		slots[1].queue_free()
	)
	check(is_instance_valid(gone) and gone.top_level and gone.get_parent() == root, "exiting visual floats above the layout")
	check(gone.global_position.is_equal_approx(start), "exiting visual stays where it was")
	var reason: int = await transition.end
	check(reason == T.Reason.COMPLETED, "exit transition completes")
	await _wait_frames(2)
	check(not is_instance_valid(gone), "exiting visual is freed afterwards")
	check(visuals[2].global_position.is_equal_approx(slots[2].global_position), "surviving visual reaches its slot")
	root.queue_free()

func _detached_slots_get_their_visual_back() -> void:
	var scene := _make_container(false, 2)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.05
	options.animate_exit = true
	var layout4 := T.create_layout(root, options)
	var transition := layout4.update(func(container: Control) -> void:
		container.remove_child(slots[0])
	)
	check(visuals[0].top_level, "detached slot's visual floats during the exit")
	var reason: int = await transition.end
	check(reason == T.Reason.COMPLETED, "detached exit completes")
	check(visuals[0].get_parent() == slots[0] and not visuals[0].top_level, "visual is handed back to the detached slot")
	check(visuals[0].modulate.a == 1.0 and visuals[0].scale == Vector2.ONE, "handed-back visual is restored")
	slots[0].free()
	root.queue_free()

func _global_z_order_holds_across_branches() -> void:
	var root := Control.new()
	root.size = Vector2(320, 240)
	_host.add_child(root)
	var left := HBoxContainer.new()
	left.z_index = 50
	var right := HBoxContainer.new()
	right.z_index = -30
	right.position = Vector2(0, 100)
	var a := _make_slot("A")
	var b := _make_slot("B")
	left.add_child(a)
	right.add_child(b)
	root.add_child(left)
	root.add_child(right)
	await _wait_frames(2)
	var visual_a := a.get_node("Visual") as Control
	var visual_b := b.get_node("Visual") as Control
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.1
	options.use_global_z_order = true
	var items: Array[TweensGdLayoutItem] = [TweensGdLayoutItem.new(b, visual_b), TweensGdLayoutItem.new(a, visual_a)]
	var layout := T.create_layout_from_items(root, items, options)
	var transition := layout.update(func(_container: Control) -> void: pass)
	var waited := 0
	while not transition.is_animating and waited < 80:
		await _wait_frames(1)
		waited += 1
	check(not visual_a.z_as_relative and not visual_b.z_as_relative, "global order is absolute")
	check(visual_b.z_index < visual_a.z_index and visual_b.z_index >= 50, "global order holds across branches")
	transition.cancel()
	var reason: int = await transition.end
	check(reason == T.Reason.CANCELLED, "global z order test cancels cleanly")
	check(visual_a.z_as_relative and visual_b.z_as_relative and visual_a.z_index == 0, "global z order is restored")
	root.queue_free()

func _invalid_enter_and_exit_scales_are_rejected() -> void:
	var scene := _make_container(false, 1)
	var options := TweensGdLayoutOptions.new()
	options.enter_scale = -1.0
	var layout5 := T.create_layout(scene.root, options)
	var transition := layout5.update(func(_container: Control) -> void: pass)
	check(transition.completion_reason == T.Reason.FAILED and not transition.error.is_empty(), "negative enter_scale is rejected")
	scene.root.queue_free()

func _empty_layout_animates_its_first_item_in() -> void:
	var root := HBoxContainer.new()
	root.size = Vector2(320, 240)
	root.custom_minimum_size = root.size
	_host.add_child(root)
	await _wait_frames(2)
	var added := _make_slot("First")
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.05
	options.animate_enter = true
	var layout := T.create_layout(root, options)
	var transition := layout.update(func(container: Control) -> void:
		container.add_child(added)
	)
	var visual := added.get_node("Visual") as Control
	check(visual.modulate.a == 0.0, "first item starts invisible in an empty layout")
	var reason: int = await transition.end
	check(reason == T.Reason.COMPLETED and visual.modulate.a == 1.0, "first item animates into an empty layout")
	var empty := HBoxContainer.new()
	_host.add_child(empty)
	var rejected := T.create_layout(empty).update(func(_container: Control) -> void: pass)
	check(rejected.completion_reason == T.Reason.FAILED, "an empty layout without animate_enter is still rejected")
	empty.queue_free()
	root.queue_free()

func _explicit_items_reject_enter_and_exit() -> void:
	var scene := _make_container(false, 1)
	var items: Array[TweensGdLayoutItem] = [TweensGdLayoutItem.new(scene.slots[0], scene.visuals[0])]
	var flags: Array[StringName] = [&"animate_enter", &"animate_exit"]
	for flag in flags:
		var options := TweensGdLayoutOptions.new()
		options.set(flag, true)
		var layout := T.create_layout_from_items(scene.root, items, options)
		var state := MutationState.new()
		var transition := layout.update(func(_container: Control) -> void: state.called = true)
		check(not state.called, "%s with explicit items does not run the mutation" % flag)
		check(transition.completion_reason == T.Reason.FAILED and not transition.error.is_empty(),
			"%s with explicit items is rejected" % flag)
	scene.root.queue_free()

func _exiting_visual_follows_a_resized_root() -> void:
	var scene := _make_container(false, 3)
	var root := scene.root
	var slots: Array[Control] = scene.slots
	var visuals: Array[Control] = scene.visuals
	await _wait_frames(2)
	var gone := visuals[1]
	var before := gone.size
	var options := TweensGdLayoutOptions.new()
	options.duration = 0.05
	options.delay = 0.3
	options.animate_exit = true
	var layout := T.create_layout(root, options)
	var transition := layout.update(func(_container: Control) -> void:
		slots[1].queue_free()
	)
	root.size = Vector2(root.size.x * 2.0, root.size.y)
	await _wait_frames(3)
	check(gone.top_level and is_equal_approx(gone.size.x, before.x * 2.0), "exiting visual follows a resized root")
	transition.cancel()
	await transition.end
	root.queue_free()
