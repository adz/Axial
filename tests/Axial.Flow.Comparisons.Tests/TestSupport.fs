namespace Axial.Flow.Comparisons.Tests

open System.Threading
open Axial.Flow

[<AutoOpen>]
module TestExtensions =
    module Flow =
        let runSync (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            flow.RunSynchronously(environment)

        let runSyncWithToken (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            flow.RunSynchronously(environment, cancellationToken = cancellationToken)
