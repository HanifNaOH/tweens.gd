// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss
namespace testbed;

public partial class ShowreelPage : GalleryPage
{
    public override string Heading => "Showreel";
    public override string Description => "A dot crosses a page of words; each stands up with the easing it names.";
    protected override GalleryEffect[] CreateEffects() => [new Showreel()];
}
