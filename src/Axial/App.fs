namespace Axial

open System
open System.Threading
#if !FABLE_COMPILER
open System.Threading.Tasks
#endif

/// <summary>Describes the lifecycle state of a running application.</summary>
[<RequireQualifiedAccess>]
type AppStatus =
    /// <summary>The root workflow is running.</summary>
    | Running
    /// <summary>Stop has been requested and the root workflow is finishing cleanup.</summary>
    | Stopping
    /// <summary>The root workflow and all of its scope finalizers have completed.</summary>
    | Completed

/// <summary>
/// Owns one running root workflow and provides coordinated application shutdown.
/// </summary>
/// <remarks>
/// Calling <see cref="M:Axial.AppHandle`2.Stop" /> more than once is safe. Every caller observes the same
/// final <see cref="T:Axial.Exit`2" /> after the root scope has closed. Disposing the handle requests stop but
/// cannot wait for asynchronous finalizers; await <c>Stop()</c> or <c>Completion</c> when cleanup must finish before
/// the surrounding process or host exits.
/// </remarks>
type AppHandle<'error, 'value> internal
    (
        cancellationSource: CancellationTokenSource,
        completion: Async<Exit<'value, 'error>>,
        status: unit -> AppStatus,
        requestStop: unit -> unit
#if !FABLE_COMPILER
        , settledTask: Task
#endif
    ) =

    /// <summary>Gets the current application lifecycle state.</summary>
    member _.Status = status ()

    /// <summary>Waits for the root workflow and its scope finalizers to complete.</summary>
    member _.Completion = completion

    /// <summary>Requests cooperative interruption and waits for the final application exit.</summary>
    member _.Stop() : Async<Exit<'value, 'error>> =
        async {
            requestStop ()
            return! completion
        }

#if !FABLE_COMPILER
    /// <summary>Requests stop and blocks until the root and its finalizers settle, or the timeout expires. Safe on UI threads.</summary>
    /// <returns>True if the application settled before the timeout.</returns>
    member _.StopAndWait(timeout: TimeSpan) : bool =
        requestStop ()
        settledTask.Wait(timeout)  // axial-allow-AXG001: StopAndWait is the documented synchronous boundary
#endif

    interface IDisposable with
        member _.Dispose() =
            requestStop ()
            // CancellationTokenSource.Dispose does not wait for registered callbacks or application cleanup.
            // The source is disposed by the runner after the root workflow settles.
            ignore cancellationSource

/// <summary>Starts and controls root Flow applications without requiring an external hosting framework.</summary>
[<RequireQualifiedAccess>]
module App =
    let private startCore
        (externalCancellationToken: CancellationToken option)
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : AppHandle<'error, 'value> =
        let gate = obj ()
        let cancellationSource = new CancellationTokenSource()
        let mutable currentStatus = AppStatus.Running
        let mutable settled: Exit<'value, 'error> option = None
        let mutable waiters: (Exit<'value, 'error> -> unit) list = []
#if !FABLE_COMPILER
        let settledSignal = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
#endif

        let requestStop () =
            let shouldCancel =
                lock gate (fun () ->
                    match currentStatus with
                    | AppStatus.Running ->
                        currentStatus <- AppStatus.Stopping
                        true
                    | AppStatus.Stopping
                    | AppStatus.Completed -> false)

            if shouldCancel then
                cancellationSource.Cancel()

        let status () = lock gate (fun () -> currentStatus)

        let completion =
            async {
                return!
                    Async.FromContinuations(fun (onSuccess, _, _) ->
#if FABLE_COMPILER
                        // Resumed from the app's settle callback, so reinstall the waiter's ambient runtime.
                        let onSuccess = Platform.resumeWith (Platform.captureAmbient ()) onSuccess
#endif
                        let immediate =
                            lock gate (fun () ->
                                match settled with
                                | Some exit -> Some exit
                                | None ->
                                    waiters <- onSuccess :: waiters
                                    None)

                        immediate |> Option.iter onSuccess)
            }

        let settle exit =
            let callbacks =
                lock gate (fun () ->
                    match settled with
                    | Some _ -> []
                    | None ->
                        settled <- Some exit
                        currentStatus <- AppStatus.Completed
                        let callbacks = List.rev waiters
                        waiters <- []
                        callbacks)

#if !FABLE_COMPILER
            settledSignal.TrySetResult() |> ignore
#endif
            for callback in callbacks do
                callback exit

        let externalRegistration =
            externalCancellationToken
            |> Option.map (fun token -> token.Register(Action requestStop))

        let finish exit =
            settle exit
#if !FABLE_COMPILER
            externalRegistration |> Option.iter _.Dispose()
#else
            // CancellationTokenRegistration.Dispose is not implemented by Fable. The registration becomes
            // unreachable with the application state after either token cancellation or application completion.
            ignore externalRegistration
#endif
            cancellationSource.Dispose()

        let startRoot () =
            Async.StartWithContinuations(
                application.ToAsync(environment, cancellationToken = cancellationSource.Token),
                finish,
                (fun error -> finish (Exit.Failure(Cause.Die error))),
                (fun _ -> finish (Exit.Failure Cause.Interrupt)))

#if FABLE_COMPILER
        startRoot ()
        new AppHandle<'error, 'value>(cancellationSource, completion, status, requestStop)
#else
        ThreadPool.UnsafeQueueUserWorkItem((fun (_: obj) -> startRoot ()), null) |> ignore
        new AppHandle<'error, 'value>(cancellationSource, completion, status, requestStop, settledSignal.Task)
#endif

    /// <summary>Starts a root workflow and returns a handle that owns its lifetime.</summary>
    /// <param name="environment">The explicit environment supplied to the root workflow.</param>
    /// <param name="application">The root workflow to start.</param>
    /// <returns>A handle for observing completion or requesting coordinated stop.</returns>
    /// <platforms>Fable compatible</platforms>
    let start
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : AppHandle<'error, 'value> =
        startCore None environment application

    /// <summary>Builds services in a new root scope and returns them with a handle that keeps the scope alive.</summary>
    /// <remarks>For a desktop app's per-repository lifetime, start on repository open and call
    /// <c>StopAndWait</c> on repository switch. Unlike <c>App.start</c>, this publishes the result of
    /// the build while keeping its resources alive. Unlike <c>ElmishRuntime.startLayer</c>, it does not
    /// own an Elmish program. A typed build failure returns Error after scope cleanup. A defect rethrows its
    /// exception; interruption throws OperationCanceledException. Cancelling the caller stops the app and waits
    /// for its finalizers before cancelling the caller's async.</remarks>
    /// <platforms>Fable compatible</platforms>
    let startWith
        (environment: 'env)
        (build: Flow<'env, 'error, 'services>)
        : Async<Result<AppHandle<'error, unit> * 'services, 'error>> =
        async {
            let published = System.Threading.Tasks.TaskCompletionSource<Exit<'services, 'error>>(
                System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously)
            let application : Flow<'env, 'error, unit> =
                Flow.bind (fun services ->
                    published.TrySetResult(Exit.Success services) |> ignore
                    Flow.never) build
            let running = start environment application
            Async.StartWithContinuations(
                running.Completion,
                (fun exit ->
                    match exit with
                    | Exit.Failure cause -> published.TrySetResult(Exit.Failure cause) |> ignore
                    | Exit.Success () ->
                        published.TrySetException(InvalidOperationException("Application completed before publishing services")) |> ignore),
                (fun error -> published.TrySetException(error) |> ignore),
                (fun _ -> published.TrySetCanceled() |> ignore))
            let! callerToken = Async.CancellationToken
            let! outcome =
                Async.FromContinuations(fun (success, failure, cancelled) ->
                    let mutable registration = Unchecked.defaultof<CancellationTokenRegistration>
                    let mutable completed = 0
                    let tryComplete () =
#if FABLE_COMPILER
                        let notDone = completed = 0
                        if notDone then completed <- 1
                        notDone
#else
                        Interlocked.Exchange(&completed, 1) = 0
#endif
                    let finish action =
#if !FABLE_COMPILER
                        registration.Dispose()
#endif
                        action ()
                    let finishOnce action =
                        if tryComplete () then finish action
                    registration <- callerToken.Register(Action(fun () ->
                        // Claim before stopping so a publication racing this cancellation cannot deliver
                        // success after the caller asked to cancel. Stop always runs: if the caller
                        // cancelled after publication, nothing may be left running.
                        let won = tryComplete ()
                        Async.StartWithContinuations(
                            running.Stop(),
                            (fun _ -> if won then finish (fun () -> cancelled (OperationCanceledException("startWith was cancelled", callerToken)))),
                            (fun error -> if won then finish (fun () -> failure error)),
                            (fun error -> if won then finish (fun () -> cancelled error)),
                            cancellationToken = Platform.Cancellation.never)))
                    Async.StartWithContinuations(
                        async { return! published.Task |> Async.AwaitTask },
                        (fun exit -> if not callerToken.IsCancellationRequested then finishOnce (fun () -> success exit)),
                        (fun error -> if not callerToken.IsCancellationRequested then finishOnce (fun () -> failure error)),
                        (fun error -> if not callerToken.IsCancellationRequested then finishOnce (fun () -> cancelled error)),
                        cancellationToken = Platform.Cancellation.never))
            match outcome with
            | Exit.Success services -> return Ok(running, services)
            | exit ->
                let! _ = running.Completion
                return Exit.toResult exit |> Result.map (fun _ -> failwith "unreachable")
        }

    /// <summary>Starts a root workflow linked to an external cancellation token.</summary>
    /// <param name="cancellationToken">A host-owned token that requests application stop when cancelled.</param>
    /// <param name="environment">The explicit environment supplied to the root workflow.</param>
    /// <param name="application">The root workflow to start.</param>
    /// <returns>A handle for observing completion or requesting coordinated stop.</returns>
    /// <platforms>Fable compatible</platforms>
    let startWithCancellation
        (cancellationToken: CancellationToken)
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : AppHandle<'error, 'value> =
        startCore (Some cancellationToken) environment application

    /// <summary>Runs a root workflow to completion using the caller's asynchronous cancellation token.</summary>
    /// <param name="environment">The explicit environment supplied to the root workflow.</param>
    /// <param name="application">The root workflow to run.</param>
    /// <returns>The final exit after the root scope has closed.</returns>
    /// <platforms>Fable compatible</platforms>
    let run
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : Async<Exit<'value, 'error>> =
        async {
            let! cancellationToken = Async.CancellationToken
            return! (startWithCancellation cancellationToken environment application).Completion
        }

    /// <summary>Runs a root workflow to completion using an explicit host-owned cancellation token.</summary>
    /// <param name="cancellationToken">A host-owned token that requests application stop when cancelled.</param>
    /// <param name="environment">The explicit environment supplied to the root workflow.</param>
    /// <param name="application">The root workflow to run.</param>
    /// <returns>The final exit after the root scope has closed.</returns>
    /// <platforms>Fable compatible</platforms>
    let runWithCancellation
        (cancellationToken: CancellationToken)
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : Async<Exit<'value, 'error>> =
        (startWithCancellation cancellationToken environment application).Completion
