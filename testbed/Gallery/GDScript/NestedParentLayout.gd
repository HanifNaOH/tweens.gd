extends "res://Gallery/GDScript/GalleryAnimation.gd"
# Scene setup is in LayoutTransitions/NestedParentLayout.cs.

func animate() -> void:
	var root: Control = targets.root
	var parent_row: Control = targets.parent_row
	var left_group: Control = targets.left_group
	var right_group: Control = targets.right_group
	var slots_a: Array[Control] = targets.slots_a
	var visuals_a: Array[Control] = targets.visuals_a
	var slots_b: Array[Control] = targets.slots_b
	var visuals_b: Array[Control] = targets.visuals_b
	var tree: SceneTree = root.get_tree()
	for _frame in 2:
		await tree.process_frame
		if not is_instance_valid(root) or not root.is_inside_tree():
			return

	var items: Array[TweensGdLayoutItem] = []
	for index in slots_a.size():
		items.append(TweensGdLayoutItem.new(slots_a[index], visuals_a[index]))
	for index in slots_b.size():
		items.append(TweensGdLayoutItem.new(slots_b[index], visuals_b[index]))
	var options := TweensGdLayoutOptions.new()
	options.duration = seconds * 0.45
	options.ease = Out.CUBIC
	options.size_ease = Out.CUBIC
	var layout := Tweens.create_layout_from_items(root, items, options)
	var a_is_in_parent_one := true
	while true:
		var transition := layout.update(func(_root: Control) -> void:
			if a_is_in_parent_one:
				for slot in slots_a: left_group.remove_child(slot)
				for slot in slots_b: right_group.remove_child(slot)
				for index in range(slots_b.size() - 1, -1, -1): left_group.add_child(slots_b[index])
				for index in range(slots_a.size() - 1, -1, -1): right_group.add_child(slots_a[index])
			else:
				for slot in slots_a: right_group.remove_child(slot)
				for slot in slots_b: left_group.remove_child(slot)
				for slot in slots_a: left_group.add_child(slot)
				for slot in slots_b: right_group.add_child(slot)
			left_group.queue_sort()
			right_group.queue_sort()
			parent_row.queue_sort()
			root.queue_sort()
		)
		if await transition.end != Tweens.Reason.COMPLETED:
			return
		a_is_in_parent_one = not a_is_in_parent_one
