namespace Axial

open System
open System.Threading

/// Classifies an exception thrown by foreign async work. An <c>OperationCanceledException</c> is an interruption only
/// when the runtime's own token asked for it; a library that cancels for its own reasons (an internal timeout, a
/// disposed client) has failed, and reporting that as an interruption nobody requested would hide it.
module internal ForeignCancellation =
#if FABLE_COMPILER
    let runtimeCancellationException () : exn = Platform.runtimeCancellationException ()
#endif

    let isOurs (cancellationToken: CancellationToken) (error: exn) = Platform.isOwnCancellation cancellationToken error

    let causeOf (cancellationToken: CancellationToken) (error: exn) : Cause<'error> =
        Platform.causeOfThrown cancellationToken error

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

    /// Awaits an already-started task but returns (and interrupts) when the runtime token cancels.
    let awaitStarted (started: Task<'source>) (cancellationToken: CancellationToken) : Task<'source> =
#if NET8_0_OR_GREATER
        started.WaitAsync cancellationToken
#else
        task {
            if cancellationToken.IsCancellationRequested then
                cancellationToken.ThrowIfCancellationRequested()

            let cancellation = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            let registration =
                cancellationToken.Register(fun () -> cancellation.TrySetResult() |> ignore)

            try
                let! completed = Task.WhenAny(started, cancellation.Task)

                if obj.ReferenceEquals(completed, started :> Task) then
                    // Awaiting rethrows the task's own exception; .Result would wrap it in an AggregateException.
                    return! started
                else
                    cancellationToken.ThrowIfCancellationRequested()
                    return Unchecked.defaultof<'source>
            finally
                registration.Dispose()
        }
#endif

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
