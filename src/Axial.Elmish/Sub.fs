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
                    try
                        runtime.Options.Post(fun () ->
                            try
                                if Volatile.Read(&stopped) = 0 then dispatch message
                            with error -> Diagnostics.exceptionFrom name runtime.Options error)
                    with error -> Diagnostics.exceptionFrom name runtime.Options error
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
                            | Exit.Failure(Cause.Fail error) ->
                                try deliver (onError error)
                                with error -> Diagnostics.exceptionFrom name runtime.Options error
                            | Exit.Failure cause ->
                                if not (List.isEmpty (Cause.defects cause)) || not (Cause.isInterrupted cause) then
                                    Diagnostics.cause name runtime.Options cause
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
                                TaskScheduler.Default
                            )
                            |> ignore } ]
