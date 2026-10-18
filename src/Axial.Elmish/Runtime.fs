namespace Axial.Elmish

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
      HistoryCapacity: int }

/// Owns the root scope for all commands in one Elmish program.
[<Sealed>]
type ElmishRuntime<'env when 'env :> IHasClock> internal (options: ElmishRuntimeOptions, environment: 'env) =
    let registry = FiberRegistry(options.HistoryCapacity)
    let queue = ConcurrentQueue<Flow<'env, Never, unit>>()
    let signal = new SemaphoreSlim(0)
    let gate = obj ()
    let latest = Dictionary<string, int64 * FiberId option>()
    let mutable generation = 0L

    let rec pump () : Flow<'env, Never, unit> =
        flow {
            do! Flow.fromTask (fun token -> signal.WaitAsync(token).ContinueWith(Func<Task, unit>(fun completed -> completed.GetAwaiter().GetResult())))
            let mutable job = Unchecked.defaultof<Flow<'env, Never, unit>>
            if queue.TryDequeue(&job) then
                let! _ = Flow.fork job
                ()
            return! pump ()
        }

    let handle = App.start environment (Flow.withFiberRegistry registry (pump ()))

    member _.Environment = environment
    member _.Registry = registry
    member internal _.Options = options

    member internal _.Enqueue(job: Flow<'env, Never, unit>) =
        lock gate (fun () ->
            if handle.Status = AppStatus.Running then
                queue.Enqueue job
                signal.Release() |> ignore)

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

    member _.Stop() : Async<unit> = async { let! _ = handle.Stop() in return () }
    member _.StopAndWait(timeout: TimeSpan) = handle.StopAndWait timeout
    interface IDisposable with
        member _.Dispose() = (handle :> IDisposable).Dispose()

[<RequireQualifiedAccess>]
module ElmishRuntime =
    let start (options: ElmishRuntimeOptions) (environment: 'env when 'env :> IHasClock) =
        new ElmishRuntime<'env>(options, environment)
