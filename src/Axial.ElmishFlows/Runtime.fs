namespace Axial.ElmishFlows

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Axial

/// A command defect rendered without F# structural formatting.
type CommandDefect =
    { Name: string
      Error: exn option
      Rendered: string }
    override this.ToString() = this.Name + ": " + this.Rendered

/// Host delivery and diagnostic policy.
type ElmishRuntimeOptions =
    { Post: (unit -> unit) -> unit
      OnDefect: CommandDefect -> unit
      HistoryCapacity: int
      Clock: IClock option }

/// Owns the root scope for all commands in one Elmish program.
[<Sealed>]
type ElmishRuntime<'env> internal (options: ElmishRuntimeOptions) =
    let registry = FiberRegistry(options.HistoryCapacity)
    let queue = ConcurrentQueue<Flow<'env, Never, unit>>()
    let signal = new SemaphoreSlim(0)
    let gate = obj ()
    let latest = Dictionary<string, int64 * FiberId option>()
    let mutable generation = 0L
    let mutable pending = 0
    let mutable stopping = false
    let shutdowns = ResizeArray<unit -> unit>()
    let mutable environment = Unchecked.defaultof<'env>
    let mutable isStopped = (fun () -> true)
    let mutable stop = (fun () -> async { return () })
    let mutable stopAndWait = (fun (_: TimeSpan) -> false)

    let rec pump () : Flow<'env, Never, unit> =
        flow {
            do! Flow.fromTask (fun token -> task { do! signal.WaitAsync(token) })
            let mutable job = Unchecked.defaultof<Flow<'env, Never, unit>>
            if queue.TryDequeue(&job) then
                let! _ = Flow.fork job
                ()
            return! pump ()
        }

    member _.Environment = environment
    member _.Registry = registry
    member internal _.Options = options
    member internal _.Pending = Volatile.Read(&pending)
    member internal _.LatestCount = lock gate (fun () -> latest.Count)
    member internal _.Pump() : Flow<'env, Never, unit> =
        match options.Clock with
        | Some clock -> pump () |> Flow.withClock clock
        | None -> pump ()

    member internal _.SetEnvironment(environmentValue: 'env) =
        environment <- environmentValue

    member internal _.Attach(handle: AppHandle<'error, unit>) =
        isStopped <- (fun () -> handle.Status <> AppStatus.Running)
        stop <- (fun () -> async {
            let! _ = handle.Stop()
            return ()
        })
        stopAndWait <- (fun timeout -> handle.StopAndWait timeout)

    // Post callbacks and generation mutations share gate; dispatch occurs inside this boundary.
    member internal _.DeliverIfCurrent(key: string, version: int64, dispatch: unit -> unit) =
        lock gate (fun () ->
            match latest.TryGetValue key with
            | true, (v, _) when v = version ->
                latest.Remove key |> ignore
                dispatch ()
            | _ -> ())
    member internal _.Complete(key: string, version: int64) =
        lock gate (fun () ->
            match latest.TryGetValue key with
            | true, (v, _) when v = version -> latest.Remove key |> ignore
            | _ -> ())
    member internal _.IsStopped = isStopped ()
    member internal _.RegisterShutdown(action) =
        lock gate (fun () ->
            if not (isStopped ()) then shutdowns.Add action
            else action ())

    member internal _.Enqueue(job: Flow<'env, Never, unit>) =
        lock gate (fun () ->
            if not stopping && not (isStopped ()) then
                Interlocked.Increment(&pending) |> ignore
                queue.Enqueue (Flow.ensuring (Flow.delay (fun () -> Interlocked.Decrement(&pending) |> ignore; Flow.ok ())) job)
                signal.Release() |> ignore
                true
            else false)

    member internal _.Begin(key: string) =
        lock gate (fun () ->
            generation <- generation + 1L
            match latest.TryGetValue key with
            | true, (_, Some id) -> registry.Interrupt id |> ignore
            | _ -> ()
            latest.[key] <- (generation, None)
            generation)

    member internal _.IsCurrent(key: string, version: int64) =
        lock gate (fun () ->
            match latest.TryGetValue key with
            | true, (v, _) -> v = version
            | _ -> false)

    member internal _.SetCurrent(key: string, version: int64, id: FiberId) =
        lock gate (fun () ->
            match latest.TryGetValue key with
            | true, (v, _) when v = version -> latest.[key] <- (v, Some id)
            | _ -> registry.Interrupt id |> ignore)

    member internal _.Cancel(key: string) =
        lock gate (fun () ->
            match latest.TryGetValue key with
            | true, (_, Some id) -> registry.Interrupt id |> ignore
            | _ -> ()
            latest.Remove key |> ignore)

    member private _.Drain() =
        lock gate (fun () ->
            stopping <- true
            let mutable discarded = Unchecked.defaultof<Flow<'env, Never, unit>>
            while queue.TryDequeue(&discarded) do
                Interlocked.Decrement(&pending) |> ignore
            let pending = shutdowns.ToArray()
            shutdowns.Clear()
            for shutdown in pending do shutdown ())

    member this.Stop() : Async<unit> = async {
        this.Drain()
        do! stop ()
    }
    member this.StopAndWait(timeout: TimeSpan) =
        this.Drain()
        stopAndWait timeout
    interface IDisposable with
        member this.Dispose() = this.StopAndWait(TimeSpan.FromSeconds 30.0) |> ignore
