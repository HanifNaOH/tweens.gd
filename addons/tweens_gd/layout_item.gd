class_name TweensGdLayoutItem
extends RefCounted

var layout_control: Control
var visual_control: Control

func _init(p_layout_control: Control = null, p_visual_control: Control = null) -> void:
	layout_control = p_layout_control
	visual_control = p_visual_control

func validate(root: Control = null, require_fill: bool = false) -> String:
	if not is_instance_valid(layout_control) or not is_instance_valid(visual_control):
		return "A layout item needs valid slot and visual Controls."
	if not layout_control.is_inside_tree() or not visual_control.is_inside_tree():
		return "A layout item needs in-tree slot and visual Controls."
	if visual_control.get_parent() != layout_control:
		return "The visual Control must be a direct child of its layout slot."
	if root != null and root != layout_control and not root.is_ancestor_of(layout_control):
		return "The layout slot must be a descendant of the controller root."
	if layout_control.get_viewport() != visual_control.get_viewport() or layout_control.get_canvas() != visual_control.get_canvas():
		return "The slot and visual Control must share a viewport and canvas."
	if not is_zero_approx(visual_control.rotation) or not visual_control.scale.is_equal_approx(Vector2.ONE):
		return "The visual Control must have zero rotation and unit scale."
	if not visual_control.pivot_offset.is_equal_approx(Vector2.ZERO):
		return "The visual Control must have a zero pivot offset."
	if require_fill:
		var slot_rect := layout_control.get_global_rect()
		var visual_rect := visual_control.get_global_rect()
		if not visual_rect.position.is_equal_approx(slot_rect.position) or not visual_rect.size.is_equal_approx(slot_rect.size):
			return "The visual Control must fill its layout slot before an update."
	return ""
