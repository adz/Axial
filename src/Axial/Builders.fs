namespace Axial

[<AutoOpen>]
module Builders =
    /// <summary>Re-exports the Axial.Flow workflow builders from the umbrella package.</summary>
    let flow = Axial.Flow.Builders.flow

    /// <summary>Re-exports the Axial.Flow layer builder from the umbrella package.</summary>
    let layer = Axial.Flow.Builders.layer

    /// <summary>Re-exports the Axial.Result result builder from the umbrella package.</summary>
    let result = Axial.Result.Builders.result

    /// <summary>Re-exports the Axial.Validation validate builder from the umbrella package.</summary>
    let validate = Axial.Validation.Builders.validate
