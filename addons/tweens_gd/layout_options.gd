class_name TweensGdLayoutOptions
extends RefCounted

var duration := 0.35
var delay := 0.0
@warning_ignore("shadowed_global_identifier")
var ease: int = Out.QUAD
var size_ease: Variant = null
var stagger_each := 0.0
var playback: TweensGdPlaybackOptions
var visual_path: NodePath = ^"Visual"

func validate() -> String:
	if not is_finite(duration) or duration < 0.0:
		return "Layout duration must be finite and non-negative."
	if not is_finite(delay) or delay < 0.0:
		return "Layout delay must be finite and non-negative."
	if not is_finite(stagger_each) or stagger_each < 0.0:
		return "Layout stagger_each must be finite and non-negative."
	var selected_ease: int = ease
	if not is_finite(Tweens.Easing.evaluate(selected_ease, 0.5)):
		return "Layout easing is invalid."
	if size_ease != null:
		if not size_ease is int:
			return "Layout size_ease is invalid."
		@warning_ignore("unsafe_call_argument")
		var size_ease_progress: float = Tweens.Easing.evaluate(size_ease, 0.5)
		if not is_finite(size_ease_progress):
			return "Layout size_ease is invalid."
	if visual_path.is_empty() or visual_path.get_name_count() != 1 or visual_path.get_subname_count() != 0:
		return "Layout visual_path must name one direct-child Control."
	if playback != null:
		if playback.process_mode not in Tweens.Process.values():
			return "Layout playback process_mode is invalid."
		if playback.pause_mode not in Tweens.Pause.values():
			return "Layout playback pause_mode is invalid."
	return ""

func copy() -> TweensGdLayoutOptions:
	var copied := TweensGdLayoutOptions.new()
	copied.duration = duration
	copied.delay = delay
	copied.ease = ease
	copied.size_ease = size_ease
	copied.stagger_each = stagger_each
	copied.visual_path = visual_path
	if playback != null:
		copied.playback = Tweens.playback_options(playback.process_mode, playback.pause_mode, playback.use_unscaled_time)
	return copied
