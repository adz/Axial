namespace Axial.ElmishFlows

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Threading
open Axial
open global.Elmish

/// A program runtime with queued UI delivery and captured defects.
[<Sealed>]
type TestRuntime<'env> internal (clock: ManualClock, environment: 'env) =
    let deliveries = ConcurrentQueue<unit -> unit>()
    let messages = ConcurrentQueue<obj>()
    let errors = ConcurrentQueue<CommandDefect>()
    let runtime =
        let runtime =
            new ElmishRuntime<'env>(
                { Post = deliveries.Enqueue
                  OnDefect = errors.Enqueue
                  HistoryCapacity = 128
                  Clock = Some clock
                  LogSink = None })
        let handle = App.start environment (Flow.withFiberRegistry runtime.Registry (runtime.Pump()))
        runtime.SetEnvironment(environment)
        runtime.Attach(handle)
        runtime

    member _.Runtime = runtime
    member _.Dispatched = messages.ToArray() |> Array.toList
    member _.Defects = errors.ToArray() |> Array.toList
    member _.Drain() =
        let mutable callback = Unchecked.defaultof<unit -> unit>
        while deliveries.TryDequeue(&callback) do callback ()

    member _.Run(command: Cmd<'msg>) =
        for start in command do start (fun message -> messages.Enqueue(box message))

    member _.Subscribe(subscription: Sub<'msg>) =
        subscription
        |> List.map (fun (_, start) -> start (fun message -> messages.Enqueue(box message)))

    /// Waits for all queued jobs and registered fibers, then drains UI deliveries.
    member this.Settle(timeout: TimeSpan) =
        let watch = Stopwatch.StartNew() // axial-allow-AXG001: wall-clock test timeout, independent of manual workflow clock
        let mutable quiet = false
        while not quiet && watch.Elapsed < timeout do // axial-allow-AXG001: wall-clock test timeout
            this.Drain()
            quiet <- runtime.Pending = 0 && List.isEmpty (runtime.Registry.Snapshot())
            if not quiet then Thread.Sleep(1) // axial-allow-AXG001: test-thread polling, not a workflow delay
        this.Drain()
        quiet && runtime.Pending = 0 && List.isEmpty (runtime.Registry.Snapshot())

    interface IDisposable with
        member _.Dispose() = (runtime :> IDisposable).Dispose()

[<RequireQualifiedAccess>]
module ElmishRuntime =
    /// <summary>Starts a runtime around a caller-supplied environment.</summary>
    /// <param name="options">Host delivery and diagnostic policy.</param>
    /// <param name="environment">The application dependencies every command runs with.</param>
    /// <returns>A running <see cref="T:Axial.ElmishFlows.ElmishRuntime`1" />.</returns>
    let start (options: ElmishRuntimeOptions) (environment: 'env) =
        let runtime = new ElmishRuntime<'env>(options)
        let handle = App.start environment (Flow.withFiberRegistry runtime.Registry (runtime.Pump()))
        runtime.SetEnvironment(environment)
        runtime.Attach(handle)
        runtime

    /// <summary>Starts a runtime by provisioning a layer inside the runtime's root scope.</summary>
    /// <remarks>
    /// The layer is built with <c>Layer.provide</c> around the runtime pump, so every
    /// resource the layer acquires (a <c>Cache.make*</c> cache, queues, hubs, or a <c>Layer.fromFlow</c> resource)
    /// stays open until <c>Stop</c>/<c>StopAndWait</c> closes the runtime. A typed provisioning failure returns
    /// <c>Error</c> and leaves no runtime running; a defect or interruption during provisioning raises from the
    /// returned <c>Async</c>.
    /// </remarks>
    /// <param name="options">Host delivery and diagnostic policy.</param>
    /// <param name="layer">The layer that builds the runtime's environment.</param>
    /// <param name="input">The input environment required to build the layer.</param>
    /// <returns>A result whose <c>Ok</c> case is the running runtime.</returns>
    let startLayer
        (options: ElmishRuntimeOptions)
        (layer: Layer<'input, 'error, 'env>)
        (input: 'input)
        : Async<Result<ElmishRuntime<'env>, 'error>> =
        async {
            let runtime = new ElmishRuntime<'env>(options)
            let ready : Deferred<Never, Result<ElmishRuntime<'env>, 'error>> =
                Deferred.make () |> Flow.run () |> Exit.toResult |> Result.defaultWith Never.absurd

            let root : Flow<'input, 'error, unit> =
                Layer.provide layer (flow {
                    let! environment = Flow.env
                    runtime.SetEnvironment(environment)
                    Deferred.succeedNow (Ok runtime) ready |> ignore
                    return! runtime.Pump() |> Flow.widenError
                })
                |> Flow.onExit (fun exit ->
                    match exit with
                    | Exit.Success () -> Flow.ok ()
                    | Exit.Failure cause ->
                        match Cause.failures cause with
                        | error :: _ -> Deferred.succeedNow (Error error) ready |> ignore
                        | [] ->
                            match Cause.defects cause with
                            | defect :: _ -> Deferred.dieNow defect ready |> ignore
                            | [] -> Deferred.dieNow (OperationCanceledException("Elmish runtime provisioning was interrupted.")) ready |> ignore
                        Flow.ok ())
                |> Flow.withFiberRegistry runtime.Registry
                |> RuntimeOptions.apply options

            let handle = App.start input root
            runtime.Attach(handle)
            return! Deferred.toTask Never.absurd ready |> Async.AwaitTask
        }

    /// <summary>Starts a long-lived service flow owned by the runtime, outside the Elmish program.</summary>
    /// <remarks>
    /// The service runs as a fiber named <paramref name="name" /> in the runtime's root scope, with the runtime's
    /// environment, so <c>Stop</c>/<c>StopAndWait</c> (or disposal) interrupts it and runs its finalizers. Call it once
    /// after <c>ElmishRuntime.start</c> or <c>startLayer</c> returns, before the Elmish program starts, so the service
    /// does not depend on <c>init</c> or <c>update</c>. A defect is reported to <c>OnDefect</c> under the service name;
    /// interruption is silent. A service that finishes is not restarted, and its name can then be started again.
    /// The service cannot fail with a typed error: handle that inside the flow, for example by publishing it to a hub
    /// the program subscribes to.
    /// </remarks>
    /// <param name="name">The service fiber's diagnostic name; one running service per name.</param>
    /// <param name="service">The <c>Flow&lt;'env, Never, unit&gt;</c> to run for the program's lifetime.</param>
    /// <param name="runtime">The runtime that owns the service.</param>
    /// <returns>
    /// <c>true</c> when the service was started; <c>false</c> when a service with this name is still running or the
    /// runtime is stopping.
    /// </returns>
    let startService (name: string) (service: Flow<'env, Never, unit>) (runtime: ElmishRuntime<'env>) : bool =
        if not (runtime.ClaimService name) then false
        else
            let job : Flow<'env, Never, unit> =
                flow {
                    let! fiber = Flow.forkNamed name (Flow.annotate "elmish.service" name service)
                    let! outcome = Fiber.awaitExit fiber
                    match outcome with
                    | Exit.Success () -> ()
                    | Exit.Failure cause ->
                        if not (List.isEmpty (Cause.defects cause)) || not (Cause.isInterrupted cause) then
                            Diagnostics.cause name runtime.Options cause
                }
                |> Flow.ensuring (Flow.delay (fun () -> runtime.ReleaseService name; Flow.ok ()))
            if runtime.Enqueue job then true
            else
                runtime.ReleaseService name
                false

    /// Creates a runtime with queued dispatch and captured defects for tests.
    let test (clock: ManualClock) (environment: 'env) = new TestRuntime<'env>(clock, environment)

[<RequireQualifiedAccess>]
module TestRuntime =
    let drain (runtime: TestRuntime<'env>) = runtime.Drain()
    let dispatched (runtime: TestRuntime<'env>) = runtime.Dispatched
    let defects (runtime: TestRuntime<'env>) = runtime.Defects
    let settle timeout (runtime: TestRuntime<'env>) = runtime.Settle(timeout)
    let run command (runtime: TestRuntime<'env>) = runtime.Run(command)
    let subscribe subscription (runtime: TestRuntime<'env>) = runtime.Subscribe(subscription)
