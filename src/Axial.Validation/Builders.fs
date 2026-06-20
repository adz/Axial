namespace Axial.Validation

[<AutoOpen>]
module Builders =
    /// <summary>The accumulating <c>validate { }</c> computation expression.</summary>
    let validate = ValidateBuilder()
