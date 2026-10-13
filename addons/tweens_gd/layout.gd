class_name TweensGdLayout
extends RefCounted

const LayoutHandle = preload("layout_handle.gd")
const LayoutItem = preload("layout_item.gd")
const LayoutOptions = preload("layout_options.gd")
const MAX_REFLOW_FRAMES := 12
const MAX_Z_INDEX := 4096

class Snapshot extends RefCounted:
	var item: TweensGdLayoutItem
	var first_global_position := Vector2.ZERO
	var first_local_position := Vector2.ZERO
	var first_size := Vector2.ZERO
	var original_scale := Vector2.ONE
	var original_z_index := 0
	var original_z_as_relative := true
	var order := 0
	var final_global_position := Vector2.ZERO
	var final_size := Vector2.ZERO
	var has_final := false

var options: TweensGdLayoutOptions = TweensGdLayoutOptions.new()
var active := false
var _root: Control
var _explicit_items: Array[TweensGdLayoutItem] = []
var _uses_explicit_items := false

func _init(root: Control = null, layout_options: TweensGdLayoutOptions = null,
		explicit_items: Array[TweensGdLayoutItem] = null) -> void:
	_root = root
	if layout_options != null:
		options = layout_options.copy()
	if explicit_items != null:
		_uses_explicit_items = true
		_explicit_items.assign(explicit_items)

func update(change: Callable, transition_options: TweensGdLayoutOptions = null) -> TweensGdLayoutHandle:
	if active:
		return LayoutHandle.rejected("This layout already has an active update.")
	if not is_instance_valid(_root) or not _root.is_inside_tree():
		return LayoutHandle.rejected("A layout update needs an in-tree Control root.")
	if not change.is_valid():
		return LayoutHandle.rejected("A layout update needs a valid mutation Callable.")
	var selected_options := options if transition_options == null else transition_options
	var run_options := selected_options.copy()
	if not _uses_explicit_items:
		run_options.visual_path = options.visual_path
	var options_error := run_options.validate()
	if not options_error.is_empty():
		return LayoutHandle.rejected(options_error)
	var items := _discover_items()
	if items.is_empty():
		return LayoutHandle.rejected("A layout update needs at least one item.")
	var item_error := _validate_items(items)
	if not item_error.is_empty():
		return LayoutHandle.rejected(item_error)
	var snapshots := _capture(items)
	var tree := _root.get_tree()
	var handle := LayoutHandle.new()
	active = true
	change.call(_root)
	if not is_instance_valid(_root) or not _root.is_inside_tree():
		active = false
		_settle_snapshots(snapshots)
		handle._finish(Tweens.Reason.OWNER_EXITED)
		return handle
	_hold_first_state(snapshots)
	call_deferred("_hold_first_state_if_active", handle, snapshots)
	@warning_ignore("missing_await")
	_run_after_change(handle, snapshots, run_options, tree)
	return handle

func _discover_items() -> Array[TweensGdLayoutItem]:
	if _uses_explicit_items:
		return _explicit_items.duplicate()
	var items: Array[TweensGdLayoutItem] = []
	if not is_instance_valid(_root):
		return items
	for child in _root.get_children():
		if not child is Control:
			continue
		var slot := child as Control
		var visual := slot.get_node_or_null(options.visual_path) as Control
		if visual != null:
			items.append(LayoutItem.new(slot, visual))
	return items

func _validate_items(items: Array[TweensGdLayoutItem]) -> String:
	var slots: Dictionary = {}
	var visuals: Dictionary = {}
	for item in items:
		if item == null or not is_instance_valid(item):
			return "A layout item cannot be null or freed."
		var item_error := item.validate(_root, true)
		if not item_error.is_empty():
			return item_error
		var slot_id := item.layout_control.get_instance_id()
		var visual_id := item.visual_control.get_instance_id()
		if slots.has(slot_id) or visuals.has(visual_id):
			return "Layout items must use unique slots and visual Controls."
		slots[slot_id] = true
		visuals[visual_id] = true
	return ""

func _capture(items: Array[TweensGdLayoutItem]) -> Array[Snapshot]:
	var snapshots: Array[Snapshot] = []
	for index in items.size():
		var item := items[index]
		var snapshot := Snapshot.new()
		snapshot.item = item
		snapshot.first_global_position = item.visual_control.global_position
		snapshot.first_local_position = item.visual_control.position
		snapshot.first_size = item.visual_control.size
		snapshot.original_scale = item.visual_control.scale
		snapshot.original_z_index = item.visual_control.z_index
		snapshot.original_z_as_relative = item.visual_control.z_as_relative
		snapshot.order = index
		snapshots.append(snapshot)
	return snapshots

func _run_after_change(handle: TweensGdLayoutHandle, snapshots: Array[Snapshot], run_options: TweensGdLayoutOptions,
		tree: SceneTree) -> void:
	var previous_rects := _layout_rects(snapshots)
	var stable_frames := 0
	var stable := false
	for frame in MAX_REFLOW_FRAMES:
		if not is_instance_valid(_root) or not _root.is_inside_tree():
			active = false
			_settle_snapshots(snapshots)
			handle._finish(Tweens.Reason.OWNER_EXITED)
			return
		await tree.process_frame
		if not is_instance_valid(_root) or not _root.is_inside_tree():
			active = false
			_settle_snapshots(snapshots)
			handle._finish(Tweens.Reason.OWNER_EXITED)
			return
		_hold_first_state(snapshots)
		call_deferred("_hold_first_state_if_active", handle, snapshots)
		var current_rects := _layout_rects(snapshots)
		if frame > 0 and _same_rects(current_rects, previous_rects):
			stable_frames += 1
		else:
			stable_frames = 0
		previous_rects = current_rects
		if stable_frames >= 2:
			stable = true
			break
	_capture_final_rects(snapshots)
	if not stable:
		_settle_snapshots(snapshots)
		active = false
		if handle._cancel_requested:
			handle._finish(Tweens.Reason.CANCELLED)
		else:
			handle._finish(Tweens.Reason.FAILED, "Container layout did not stabilize within %d frames." % MAX_REFLOW_FRAMES)
		return
	if handle._cancel_requested:
		_settle_snapshots(snapshots)
		active = false
		handle._finish(Tweens.Reason.CANCELLED)
		return
	var matched := _matching_snapshots(snapshots)
	var handles: Array[TweensGdHandle] = []
	for index in matched.size():
		var snapshot := matched[index]
		_raise_visual(index, matched.size(), snapshot.item.visual_control)
		var item_delay := run_options.delay + index * run_options.stagger_each
		var position_definition := Tweens.value(0.0, 1.0, run_options.duration, run_options.ease, item_delay)
		position_definition.on_update = func(_tween: TweensGdHandle, progress: float) -> void:
			_apply_position_progress(snapshot, progress)
		var selected_size_ease: int = run_options.ease if run_options.size_ease == null else run_options.size_ease
		var size_definition := Tweens.value(0.0, 1.0, run_options.duration, selected_size_ease, item_delay)
		size_definition.on_update = func(_tween: TweensGdHandle, progress: float) -> void:
			_apply_size_progress(snapshot, progress)
		handles.append(Tweens.play(_root, position_definition, null, run_options.playback))
		handles.append(Tweens.play(_root, size_definition, null, run_options.playback))
	if handles.is_empty():
		_settle_snapshots(snapshots)
		active = false
		handle._finish(Tweens.Reason.COMPLETED)
		return
	var group := Tweens.group(handles)
	handle._attach_group(group)
	var reason: int = await group.end
	var error_message: String = group.error
	if reason == Tweens.Reason.COMPLETED:
		handle._set_following(true)
		var final_rects_stable: bool = await _follow_live_rects(handle, snapshots, tree, run_options.playback)
		handle._set_following(false)
		if not is_instance_valid(_root) or not _root.is_inside_tree():
			_settle_snapshots(snapshots)
			active = false
			handle._finish(Tweens.Reason.OWNER_EXITED)
			return
		if handle._cancel_requested:
			_settle_snapshots(snapshots)
			active = false
			handle._finish(Tweens.Reason.CANCELLED)
			return
		if not final_rects_stable:
			_settle_snapshots(snapshots)
			active = false
			handle._finish(Tweens.Reason.FAILED, "Live layout targets stopped before settling.")
			return
	_settle_snapshots(snapshots)
	active = false
	handle._finish(reason, error_message)

func _follow_live_rects(handle: TweensGdLayoutHandle, snapshots: Array[Snapshot], tree: SceneTree,
		playback: TweensGdPlaybackOptions) -> bool:
	var previous := _layout_rects(snapshots)
	var consecutive := 0
	while is_instance_valid(_root) and _root.is_inside_tree() and not handle._cancel_requested:
		if playback == null or playback.process_mode == Tweens.Process.PROCESS:
			await tree.process_frame
		else:
			await tree.physics_frame
		if not is_instance_valid(_root) or not _root.is_inside_tree() or handle._cancel_requested:
			return false
		var current := _layout_rects(snapshots)
		if handle.is_paused or not _can_follow(playback):
			consecutive = 0
			previous = current
			continue
		_apply_current_rects(snapshots)
		if _same_rects(current, previous):
			consecutive += 1
		else:
			consecutive = 0
		previous = current
		if consecutive >= 2:
			return true
	return false

func _can_follow(playback: TweensGdPlaybackOptions) -> bool:
	if playback == null:
		return _root.can_process()
	match playback.pause_mode:
		Tweens.Pause.ALWAYS:
			return true
		Tweens.Pause.SCENE_TREE:
			return not _root.get_tree().paused
		_:
			return _root.can_process()

func _matching_snapshots(snapshots: Array[Snapshot]) -> Array[Snapshot]:
	var current_items := _discover_items()
	var by_slot: Dictionary = {}
	for snapshot in snapshots:
		if is_instance_valid(snapshot.item.layout_control):
			by_slot[snapshot.item.layout_control.get_instance_id()] = snapshot
	var matched: Array[Snapshot] = []
	for index in current_items.size():
		var item := current_items[index]
		if not item.validate(_root).is_empty():
			continue
		var slot_id := item.layout_control.get_instance_id()
		if by_slot.has(slot_id):
			var snapshot: Snapshot = by_slot[slot_id]
			if item.visual_control == snapshot.item.visual_control:
				snapshot.order = index
				matched.append(snapshot)
	return matched

func _layout_rects(snapshots: Array[Snapshot]) -> Array[Rect2]:
	var rects: Array[Rect2] = []
	for snapshot in snapshots:
		if snapshot.item.validate(_root).is_empty():
			var slot := snapshot.item.layout_control
			rects.append(slot.get_global_rect())
	return rects

func _same_rects(a: Array[Rect2], b: Array[Rect2]) -> bool:
	if a.size() != b.size():
		return false
	for index in a.size():
		if not a[index].position.is_equal_approx(b[index].position) or not a[index].size.is_equal_approx(b[index].size):
			return false
	return true

func _capture_final_rects(snapshots: Array[Snapshot]) -> void:
	for snapshot in snapshots:
		if snapshot.item.validate().is_empty():
			var slot := snapshot.item.layout_control
			snapshot.final_global_position = slot.global_position
			snapshot.final_size = slot.size
			snapshot.has_final = true

func _apply_position_progress(snapshot: Snapshot, progress: float) -> void:
	if not snapshot.item.validate(_root).is_empty():
		return
	snapshot.item.visual_control.global_position = snapshot.first_global_position.lerp(
		snapshot.item.layout_control.global_position, progress)

func _apply_size_progress(snapshot: Snapshot, progress: float) -> void:
	if not snapshot.item.validate(_root).is_empty():
		return
	snapshot.item.visual_control.size = snapshot.first_size.lerp(snapshot.item.layout_control.size, progress)

func _apply_current_rects(snapshots: Array[Snapshot]) -> void:
	for snapshot in snapshots:
		if not snapshot.item.validate(_root).is_empty():
			continue
		snapshot.item.visual_control.global_position = snapshot.item.layout_control.global_position
		snapshot.item.visual_control.size = snapshot.item.layout_control.size

func _hold_first_state(snapshots: Array[Snapshot]) -> void:
	for snapshot in snapshots:
		if not snapshot.item.validate(_root).is_empty():
			continue
		var visual := snapshot.item.visual_control
		visual.global_position = snapshot.first_global_position
		visual.size = snapshot.first_size
		visual.scale = snapshot.original_scale

func _hold_first_state_if_active(handle: TweensGdLayoutHandle, snapshots: Array[Snapshot]) -> void:
	if not active or handle.is_terminal:
		return
	_hold_first_state(snapshots)

func _raise_visual(order: int, count: int, visual: Control) -> void:
	visual.z_as_relative = false
	visual.z_index = clampi(MAX_Z_INDEX - count + order + 1, -MAX_Z_INDEX, MAX_Z_INDEX)

func _settle_snapshots(snapshots: Array[Snapshot]) -> void:
	for snapshot in snapshots:
		if not is_instance_valid(snapshot.item.visual_control):
			continue
		var visual := snapshot.item.visual_control
		var slot := snapshot.item.layout_control
		if is_instance_valid(slot) and visual.get_parent() == slot:
			if slot.is_inside_tree():
				visual.global_position = slot.global_position
				visual.size = slot.size
				snapshot.final_global_position = slot.global_position
				snapshot.final_size = slot.size
				snapshot.has_final = true
			elif snapshot.has_final:
				if visual.is_inside_tree():
					visual.global_position = snapshot.final_global_position
				else:
					visual.position = Vector2.ZERO
				visual.size = snapshot.final_size
			elif not visual.is_inside_tree():
				visual.position = snapshot.first_local_position
				visual.size = snapshot.first_size
			visual.scale = snapshot.original_scale
		visual.z_as_relative = snapshot.original_z_as_relative
		visual.z_index = snapshot.original_z_index
