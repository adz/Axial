namespace Axial.ElmishFlows

open System
open System.Threading
open Axial
open global.Elmish

[<RequireQualifiedAccess>]
module Sub =
    module OfFlowStream =
        /// Disposal requests interruption without blocking; scoped finalizers finish in the background.
        let subscribe (id: SubId) (runtime: ElmishRuntime<'env>) (stream: FlowStream<'env, 'error, 'msg>) (onError: 'error -> 'msg) : Sub<'msg> =
            let name = String.concat "/" id
            [ id, fun dispatch ->
                let mutable stopped = 0
                let stop : Deferred<Never, unit> =
                    Deferred.make () |> Flow.run () |> Exit.toResult |> Result.defaultWith Never.absurd
                let deliver message =
                    try
                        runtime.Options.Post(fun () ->
                            try
                                if Volatile.Read(&stopped) = 0 then dispatch message
                            with error -> Diagnostics.exceptionFrom name runtime.Options error)
                    with error -> Diagnostics.exceptionFrom name runtime.Options error
                let job : Flow<'env, Never, unit> =
                    flow {
                        let work =
                            stream
                            |> FlowStream.runForEach (fun message -> deliver message)
                            |> Flow.annotate "elmish.subscription" name
                        let! fiber = Flow.forkNamed name work
                        // The deferred remembers disposal even when the pump has not started this job yet.
                        let! _ = Flow.fork (flow {
                            do! Deferred.await stop
                            let! _ = Fiber.interrupt fiber
                            return ()
                        })
                        let! outcome = Fiber.awaitExit fiber
                        if Volatile.Read(&stopped) = 0 then
                            match outcome with
                            | Exit.Success () -> ()
                            | Exit.Failure(Cause.Fail error) ->
                                try deliver (onError error)
                                with error -> Diagnostics.exceptionFrom name runtime.Options error
                            | Exit.Failure cause ->
                                if not (List.isEmpty (Cause.defects cause)) || not (Cause.isInterrupted cause) then
                                    Diagnostics.cause name runtime.Options cause
                    }
                runtime.Enqueue job |> ignore
                // Elmish disposes subscriptions on its dispatch (UI) thread, so Dispose must not block: it requests the
                // interruption, applied once the fiber has started, and the stream's finalizers finish in the background.
                { new IDisposable with
                    member _.Dispose() =
                        if Interlocked.Exchange(&stopped, 1) = 0 then
                            Deferred.succeedNow () stop |> ignore } ]
