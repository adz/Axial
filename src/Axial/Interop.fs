namespace Axial

open System
open System.Threading

/// Classifies an exception thrown by foreign async work. An <c>OperationCanceledException</c> is an interruption only
/// when the runtime's own token asked for it; a library that cancels for its own reasons (an internal timeout, a
/// disposed client) has failed, and reporting that as an interruption nobody requested would hide it.
module internal ForeignCancellation =
    let isOurs (cancellationToken: CancellationToken) (error: exn) =
        error :? OperationCanceledException && cancellationToken.IsCancellationRequested

    let causeOf (cancellationToken: CancellationToken) (error: exn) : Cause<'error> =
        if isOurs cancellationToken error then Cause.Interrupt else Cause.Die error

module internal AsyncInterop =
    let from (mapExit: 'source -> Exit<'value, 'error>) (operation: Async<'source>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken ->
            Platform.tryExecution
                (fun () -> operation |> Platform.executionOfAsyncUnguarded cancellationToken mapExit)
                (fun error -> Platform.ofExit (Exit.Failure(ForeignCancellation.causeOf cancellationToken error))))

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
                        return Exit.Failure(ForeignCancellation.causeOf cancellationToken error)
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
                        return Exit.Failure(ForeignCancellation.causeOf cancellationToken error)
                }))
#endif
