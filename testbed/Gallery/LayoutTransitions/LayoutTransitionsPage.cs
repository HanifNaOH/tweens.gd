namespace testbed;

public partial class LayoutTransitionsPage : GalleryPage
{
    public override string Heading => "Layout transitions";
    public override string Description => "Container slots reflow in a mosaic; A and B exchange nested parent branches.";

    protected override GalleryEffect[] CreateEffects() => [new LayoutMorph(), new NestedParentLayout()];
}
