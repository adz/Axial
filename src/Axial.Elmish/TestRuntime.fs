namespace Axial.ElmishFlows

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Threading
open Axial
open global.Elmish

/// A program runtime with queued UI delivery and captured defects.
[<Sealed>]
type TestRuntime<'env when 'env :> IHasClock> internal (environment: 'env) =
    let deliveries = ConcurrentQueue<unit -> unit>()
    let messages = ConcurrentQueue<obj>()
    let errors = ConcurrentQueue<CommandDefect>()
    let runtime = new ElmishRuntime<'env>(
                      { Post = deliveries.Enqueue
                        OnDefect = errors.Enqueue
                        HistoryCapacity = 128 }, environment)

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
    let start (options: ElmishRuntimeOptions) (environment: 'env when 'env :> IHasClock) =
        new ElmishRuntime<'env>(options, environment)

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
