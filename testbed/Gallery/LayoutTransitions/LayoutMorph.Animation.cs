using System.Threading.Tasks;
using Godot;
using tweens.gd;

namespace testbed;

// Scene setup is in LayoutMorph.cs.
public sealed partial class LayoutMorph
{
    private async Task AnimateAsync()
    {
        if (!GodotObject.IsInstanceValid(grid) || !grid.IsInsideTree())
            return;
        var tree = grid.GetTree();
        for (var frame = 0; frame < 2; frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(grid) || !grid.IsInsideTree())
                return;
        }

        var layout = Tweens.CreateLayout(grid, new LayoutOptions
        {
            Duration = Seconds * 0.5,
            Delay = 0.15,
            Ease = Out.Cubic,
            SizeEase = Out.Cubic,
            StaggerEach = 0.15,
        });
        var pattern = 1;
        while (true)
        {
            var next = pattern % 4 + 1;
            var transition = layout.Update(_ =>
            {
                pattern = next;
                grid.SetMeta("pattern", pattern);
                grid.QueueSort();
            });
            if (await transition.End != Reason.Completed)
                return;
        }
    }
}
