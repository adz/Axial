namespace Axial.ElmishFlows

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open global.Elmish

/// A program runtime with queued UI delivery and captured defects.
[<Sealed>]
type TestRuntime<'env when 'env :> IHasClock> internal (environment: 'env) =
    let deliveries = ConcurrentQueue<unit -> unit>()
    let messages = ConcurrentQueue<obj>()
    let errors = ConcurrentQueue<CommandDefect>()
    let runtime =
        let runtime =
            ElmishRuntime<'env>(
                { Post = deliveries.Enqueue
                  OnDefect = errors.Enqueue
                  HistoryCapacity = 128 })
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
        let watch = Stopwatch.StartNew() // axial-allow-effect: clock (wall-clock test timeout, independent of manual workflow clock)
        let mutable quiet = false
        while not quiet && watch.Elapsed < timeout do // axial-allow-effect: clock (wall-clock test timeout)
            this.Drain()
            quiet <- runtime.Pending = 0 && List.isEmpty (runtime.Registry.Snapshot())
            if not quiet then Thread.Sleep(1) // axial-allow-effect: sleep (test-thread polling, not a workflow delay)
        this.Drain()
        quiet && runtime.Pending = 0 && List.isEmpty (runtime.Registry.Snapshot())

    interface IDisposable with
        member _.Dispose() = (runtime :> IDisposable).Dispose()

[<RequireQualifiedAccess>]
module ElmishRuntime =
    /// <summary>Starts a runtime around a caller-supplied environment.</summary>
    /// <param name="options">Host delivery and diagnostic policy.</param>
    /// <param name="environment">The environment every command runs with; it must implement <see cref="T:Axial.IHasClock" />.</param>
    /// <returns>A running <see cref="T:Axial.ElmishFlows.ElmishRuntime`1" />.</returns>
    let start (options: ElmishRuntimeOptions) (environment: 'env when 'env :> IHasClock) =
        let runtime = ElmishRuntime<'env>(options)
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
            let runtime = ElmishRuntime<'env>(options)
            let ready = TaskCompletionSource<Result<ElmishRuntime<'env>, 'error>>(TaskCreationOptions.RunContinuationsAsynchronously)

            let root : Flow<'input, 'error, unit> =
                Layer.provide layer (flow {
                    let! environment = Flow.env
                    runtime.SetEnvironment(environment)
                    ready.TrySetResult(Ok runtime) |> ignore
                    return! runtime.Pump() |> Flow.widenError
                })
                |> Flow.onExit (fun exit ->
                    match exit with
                    | Exit.Success () -> Flow.ok ()
                    | Exit.Failure cause ->
                        match Cause.failures cause with
                        | error :: _ -> ready.TrySetResult(Error error) |> ignore
                        | [] ->
                            match Cause.defects cause with
                            | defect :: _ -> ready.TrySetException(defect) |> ignore
                            | [] -> ready.TrySetException(OperationCanceledException("Elmish runtime provisioning was interrupted.")) |> ignore
                        Flow.ok ())
                |> Flow.withFiberRegistry runtime.Registry

            let handle = App.start input root
            runtime.Attach(handle)
            return! ready.Task |> Async.AwaitTask
        }

    /// Creates a runtime with queued dispatch and captured defects for tests.
    let test (environment: 'env when 'env :> IHasClock) = new TestRuntime<'env>(environment)

[<RequireQualifiedAccess>]
module TestRuntime =
    let drain (runtime: TestRuntime<'env>) = runtime.Drain()
    let dispatched (runtime: TestRuntime<'env>) = runtime.Dispatched
    let defects (runtime: TestRuntime<'env>) = runtime.Defects
    let settle timeout (runtime: TestRuntime<'env>) = runtime.Settle(timeout)
    let run command (runtime: TestRuntime<'env>) = runtime.Run(command)
    let subscribe subscription (runtime: TestRuntime<'env>) = runtime.Subscribe(subscription)
