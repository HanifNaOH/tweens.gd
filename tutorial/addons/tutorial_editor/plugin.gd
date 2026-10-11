@tool
extends EditorPlugin


class SceneDebugger extends EditorDebuggerPlugin:

	func _has_capture(prefix: String) -> bool:
		return prefix == "tutorial"


	func _capture(message: String, data: Array, session_id: int) -> bool:
		if message == "tutorial:focus_editor":
			if data.is_empty():
				_focus_editor.call_deferred()
			return true
		if message != "tutorial:open_scene":
			return false
		if data.size() == 1 and data[0] is String:
			var path: String = data[0]
			if path.begins_with("res://Lessons/") and path.ends_with(".tscn") and ResourceLoader.exists(path, "PackedScene"):
				EditorInterface.open_scene_from_path.call_deferred(path)
				EditorInterface.set_main_screen_editor.call_deferred("2D")
				get_session(session_id).send_message("tutorial:focus_editor", [OS.get_process_id()])
		return true


	func _focus_editor() -> void:
		var window := EditorInterface.get_base_control().get_window()
		if window.mode == Window.MODE_MINIMIZED:
			window.mode = Window.MODE_WINDOWED
		window.grab_focus()


var scene_debugger := SceneDebugger.new()


func _enter_tree() -> void:
	add_debugger_plugin(scene_debugger)


func _exit_tree() -> void:
	remove_debugger_plugin(scene_debugger)
