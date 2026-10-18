namespace Axial.Elmish

open System
open System.Threading
open System.Threading.Tasks
open Axial
open global.Elmish

[<RequireQualifiedAccess>]
module Sub =
    module OfFlowStream =
        /// Disposal interrupts the fiber and waits for its scoped finalizers to finish.
        let subscribe (id: SubId) (runtime: ElmishRuntime<'env>) (stream: FlowStream<'env, 'error, 'msg>) (onError: 'error -> 'msg) : Sub<'msg> =
            let name = String.concat "/" id
            [ id, fun dispatch ->
                let mutable stopped = 0
                let ready = new TaskCompletionSource<FiberId option>(TaskCreationOptions.RunContinuationsAsynchronously)
                let finished = new TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
                let deliver message =
                    runtime.Options.Post(fun () -> if Volatile.Read(&stopped) = 0 then dispatch message)
                let job : Flow<'env, Never, unit> =
                    flow {
                        // axial-allow-discarded-cancellation
                        do! Flow.scopeFinalizer (Flow.fromTask (fun _ ->
                            ready.TrySetResult(None) |> ignore
                            finished.TrySetResult() |> ignore
                            Task.FromResult(())))
                        let work =
                            stream
                            |> FlowStream.runForEach (fun message -> deliver message)
                            |> Flow.annotate "elmish.subscription" name
                        let! fiber = Flow.forkNamed name work
                        ready.TrySetResult(Some fiber.Metadata.Id) |> ignore
                        let! outcome = Fiber.await fiber
                        if Volatile.Read(&stopped) = 0 then
                            match outcome with
                            | Exit.Success () -> ()
                            | Exit.Failure(Cause.Fail error) -> deliver (onError error)
                            | Exit.Failure cause when Cause.isInterrupted cause -> ()
                            | Exit.Failure cause ->
                                runtime.Options.OnDefect
                                    { Name = name
                                      Error = match cause with Cause.Die ex -> Some ex | _ -> None
                                      Rendered = Cause.prettyPrint (fun error -> match box error with null -> "null" | value -> value.ToString()) cause }
                        finished.TrySetResult() |> ignore
                    }
                if not (runtime.Enqueue job) then
                    ready.TrySetResult(None) |> ignore
                    finished.TrySetResult() |> ignore
                // Elmish disposes subscriptions on its dispatch (UI) thread, so Dispose must not block: it requests the
                // interruption, applied once the fiber has started, and the stream's finalizers finish in the background.
                { new IDisposable with
                    member _.Dispose() =
                        if Interlocked.Exchange(&stopped, 1) = 0 then
                            ready.Task.ContinueWith(
                                (fun (started: Task<_>) ->
                                    match started.Result with // axial-allow-effect: blocking (continuation of a completed task)
                                    | Some fiberId -> runtime.Registry.Interrupt fiberId |> ignore
                                    | None -> ()),
                                TaskContinuationOptions.ExecuteSynchronously
                            )
                            |> ignore } ]
