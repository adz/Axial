namespace Axial.Refined

[<AutoOpen>]
module Builders =
    /// <summary>The fail-fast <c>refine { }</c> computation expression.</summary>
    let refine = RefineBuilder()
