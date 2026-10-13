extends "res://Gallery/GDScript/GalleryAnimation.gd"
# Scene setup is in LayoutMorph.cs.

func animate() -> void:
	var grid: Control = targets.grid
	var tree: SceneTree = grid.get_tree()
	for _frame in 2:
		await tree.process_frame
		if not is_instance_valid(grid) or not grid.is_inside_tree():
			return
	var options := TweensGdLayoutOptions.new()
	options.duration = seconds * 0.5
	options.delay = 0.15
	options.ease = Out.CUBIC
	options.size_ease = Out.CUBIC
	options.stagger_each = 0.15
	var layout := Tweens.create_layout(grid, options)
	while true:
		var next: int = int(grid.get_meta("pattern", 1)) % 4 + 1
		var transition := layout.update(func(_root: Control) -> void:
			grid.set_meta("pattern", next)
			grid.queue_sort()
		)
		if await transition.end != Tweens.Reason.COMPLETED:
			return
