using Godot;

namespace tutorial;

/// <summary>Opens lesson scenes through the tutorial's editor debugger plugin.</summary>
internal static class EditorBridge
{
    public static void OpenScene(string path)
    {
        if (!EngineDebugger.HasCapture("tutorial"))
            EngineDebugger.RegisterMessageCapture("tutorial", Callable.From<string, Godot.Collections.Array, bool>(Capture));
        EngineDebugger.SendMessage("tutorial:open_scene", new Godot.Collections.Array { path });
    }

    private static bool Capture(string message, Godot.Collections.Array data)
    {
        if (message != "focus_editor")
            return false;
        if (data.Count == 1 && data[0].VariantType == Variant.Type.Int && data[0].AsInt32() > 0)
        {
            // Windows requires the foreground process to let the editor take focus. Grant it on each click.
            DisplayServer.EnableForStealingFocus(data[0].AsInt32());
            EngineDebugger.SendMessage("tutorial:focus_editor", new Godot.Collections.Array());
        }
        return true;
    }
}
