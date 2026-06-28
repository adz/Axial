namespace Axial

[<AutoOpen>]
module Builders =
    /// <summary>Re-exports the Axial.Flow workflow builders from the umbrella package.</summary>
    let flow = Axial.Flow.Builders.flow

    /// <summary>Re-exports the Axial.Flow layer builder from the umbrella package.</summary>
    let layer = Axial.Flow.Builders.layer

    /// <summary>Re-exports the Axial.ErrorHandling result builder from the umbrella package.</summary>
    let result = Axial.ErrorHandling.Builders.result

    /// <summary>Re-exports the Axial.Refined refine builder from the umbrella package.</summary>
    let refine = Axial.Refined.Builders.refine

    /// <summary>Re-exports the Axial.Validation validate builder from the umbrella package.</summary>
    let validate = Axial.Validation.Builders.validate
