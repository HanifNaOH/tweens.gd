using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using tweens.gd;
namespace testbed;

public sealed partial class NestedParentLayout
{
    private async Task AnimateAsync()
    {
        var tree = root.GetTree();
        for (var frame = 0; frame < 2; frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
        }

        var items = new List<LayoutItem>(slotsA.Length + slotsB.Length);
        for (var index = 0; index < slotsA.Length; index++) items.Add(new LayoutItem(slotsA[index], visualsA[index]));
        for (var index = 0; index < slotsB.Length; index++) items.Add(new LayoutItem(slotsB[index], visualsB[index]));
        var layout = Tweens.CreateLayoutFromItems(root, items, new LayoutOptions
        {
            Duration = Seconds * 0.45,
            Ease = Out.Cubic,
            SizeEase = Out.Cubic,
        });
        var aIsInParentOne = true;
        while (true)
        {
            var transition = layout.Update(_ =>
            {
                if (aIsInParentOne)
                {
                    foreach (var slot in slotsA) leftGroup.RemoveChild(slot);
                    foreach (var slot in slotsB) rightGroup.RemoveChild(slot);
                    for (var index = slotsB.Length - 1; index >= 0; index--) leftGroup.AddChild(slotsB[index]);
                    for (var index = slotsA.Length - 1; index >= 0; index--) rightGroup.AddChild(slotsA[index]);
                }
                else
                {
                    foreach (var slot in slotsA) rightGroup.RemoveChild(slot);
                    foreach (var slot in slotsB) leftGroup.RemoveChild(slot);
                    foreach (var slot in slotsA) leftGroup.AddChild(slot);
                    foreach (var slot in slotsB) rightGroup.AddChild(slot);
                }
                leftGroup.QueueSort();
                rightGroup.QueueSort();
                parentRow.QueueSort();
                root.QueueSort();
            });
            if (await transition.End != Reason.Completed) return;
            aIsInParentOne = !aIsInParentOne;
        }
    }
}
