namespace Axial.Result

[<AutoOpen>]
module Builders =
    /// <summary>The fail-fast <c>result { }</c> computation expression.</summary>
    let result = ResultBuilder()
