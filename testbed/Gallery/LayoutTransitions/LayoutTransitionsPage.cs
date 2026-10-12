namespace testbed;

public partial class LayoutTransitionsPage : GalleryPage
{
    public override string Heading => "Layout transitions";
    public override string Description => "Four grid patterns, animated as Container-managed slots reflow.";

    protected override GalleryEffect[] CreateEffects() => [new LayoutMorph()];
}
