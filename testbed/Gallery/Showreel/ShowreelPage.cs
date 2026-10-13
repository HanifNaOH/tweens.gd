// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss
namespace testbed;

public partial class ShowreelPage : GalleryPage
{
    public override string Heading => "Showreel";
    public override string Description => "tweens.gd - Autumn '26 Mini Showreel";
    protected override GalleryEffect[] CreateEffects() => [new Showreel()];

}
