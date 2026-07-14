namespace Axial.Flow

open System
open System.Threading

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
/// Calling <see cref="M:Axial.Flow.AppHandle`2.Stop" /> more than once is safe. Every caller observes the same
/// final <see cref="T:Axial.Flow.Exit`2" /> after the root scope has closed. Disposing the handle requests stop but
/// cannot wait for asynchronous finalizers; await <c>Stop()</c> or <c>Completion</c> when cleanup must finish before
/// the surrounding process or host exits.
/// </remarks>
type AppHandle<'error, 'value> internal
    (
        cancellationSource: CancellationTokenSource,
        completion: Async<Exit<'value, 'error>>,
        status: unit -> AppStatus,
        requestStop: unit -> unit
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

        Async.StartWithContinuations(
            application.ToAsync(environment, cancellationToken = cancellationSource.Token),
            finish,
            (fun error -> finish (Exit.Failure(Cause.Die error))),
            (fun _ -> finish (Exit.Failure Cause.Interrupt)),
            cancellationToken = cancellationSource.Token)

        new AppHandle<'error, 'value>(cancellationSource, completion, status, requestStop)

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
