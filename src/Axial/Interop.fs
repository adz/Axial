namespace Axial

open System
open System.Threading

module internal AsyncInterop =
    let from (mapExit: 'source -> Exit<'value, 'error>) (operation: Async<'source>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            Platform.tryExecution
                (fun () -> operation |> Platform.executionOfAsyncUnguarded cancellationToken mapExit)
                (fun error ->
                    let cause = if error :? OperationCanceledException then Cause.Interrupt else Cause.Die error
                    Platform.ofExit (Exit.Failure cause)))

#if !FABLE_COMPILER
open System.Threading.Tasks

module internal TaskInterop =
    let from
        (mapExit: 'source -> Exit<'value, 'error>)
        (factory: CancellationToken -> Task<'source>)
        : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    try
                        let! source = factory cancellationToken
                        return mapExit source
                    with error ->
                        let cause = if error :? OperationCanceledException then Cause.Interrupt else Cause.Die error
                        return Exit.Failure cause
                }))

module internal ValueTaskInterop =
    let from
        (mapExit: 'source -> Exit<'value, 'error>)
        (factory: CancellationToken -> ValueTask<'source>)
        : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            ValueTask<Exit<'value, 'error>>(
                task {
                    try
                        let! source = (factory cancellationToken).AsTask()
                        return mapExit source
                    with error ->
                        let cause = if error :? OperationCanceledException then Cause.Interrupt else Cause.Die error
                        return Exit.Failure cause
                }))
#endif
