class_name TweensGdLayoutHandle
extends RefCounted

signal ended(reason: int)

var completion_reason := -1
var error := ""
var is_terminal: bool:
	get:
		return completion_reason != -1
var is_paused: bool:
	get:
		return _group.is_paused if _group != null else _paused
var end: Variant:
	get:
		return ended if completion_reason == -1 else completion_reason

var _group: TweensGdGroup
var _paused := false
var _cancel_requested := false

static func rejected(message: String) -> TweensGdLayoutHandle:
	var handle := TweensGdLayoutHandle.new()
	handle._finish(Tweens.Reason.FAILED, message)
	return handle

func pause() -> void:
	if is_terminal:
		return
	_paused = true
	if _group != null:
		_group.pause()

func resume() -> void:
	if is_terminal:
		return
	_paused = false
	if _group != null:
		_group.resume()

func cancel() -> void:
	if is_terminal:
		return
	_cancel_requested = true
	if _group != null:
		_group.cancel()

func _attach_group(group: TweensGdGroup) -> void:
	_group = group
	if _paused:
		_group.pause()
	if _cancel_requested:
		_group.cancel()

func _finish(reason: int, message: String = "") -> void:
	if is_terminal:
		return
	completion_reason = reason
	error = message
	_group = null
	ended.emit(reason)
