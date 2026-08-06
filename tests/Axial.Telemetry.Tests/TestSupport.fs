namespace Axial.Tests

open Axial

[<AutoOpen>]
module TestExtensions =
    module Flow =
        let runSync (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            flow.RunSynchronously(environment)
