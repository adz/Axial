namespace Axial.Flow

[<AutoOpen>]
module Builders =
    /// <summary>The universal <c>flow { }</c> computation expression.</summary>
    let flow = FlowBuilder()

    /// <summary>The <c>layer { }</c> computation expression for provisioning explicit service environments.</summary>
    let layer = LayerBuilder()
