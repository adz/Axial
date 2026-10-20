namespace Axial.Process

open System
open System.Collections.Generic
open System.ComponentModel
open System.Diagnostics
open System.IO
open System.Globalization
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.PlatformService
open Axial.Console
open Axial.FileSystem

[<RequireQualifiedAccess>]
module ProcessStream =
    let private transform operation (ProcessStream factory) =
        ProcessStream(fun () ->
            let stream, completion, guard, failure = factory ()
            operation stream, completion, guard, failure)

    /// Transforms values while preserving process completion.
    let map mapping source =
        transform (FlowStream.map mapping) source

    /// Selects values without losing process completion or skipping drainage of other channels.
    let filter predicate source =
        transform (FlowStream.filter predicate) source

    /// Stops the owned process after a prefix. Zero does not start a process.
    let take count source =
        if count < 0 then
            invalidArg "count" "Count cannot be negative."

        transform (FlowStream.take count) source

    /// Stops before the first nonmatching value, awaits owned cleanup, and reports StoppedEarly.
    let takeWhile predicate source =
        transform (FlowStream.takeWhile predicate) source

    /// Converts to ordinary FlowStream, discarding successful process completion.
    let toFlowStream (ProcessStream factory) =
        FlowStream(fun env token ->
            let (FlowStream stream), _, _, _ = factory ()
            stream env token)

    let private terminal consume (ProcessStream factory) =
        Flow.delay (fun () ->
            let stream, completion, guard, failure = factory ()
            let consumer = consume stream |> Flow.map (fun value -> value, completion ())
            let watcher = guard |> Flow.bind (fun () -> Flow.never)

            Flow.race consumer watcher
            |> Flow.scoped
            |> Flow.foldCause (fun result ->
                failure () |> Option.iter Flow.reportUnobservedCause
                Flow.succeed result) (fun cause ->
                let combined =
                    failure ()
                    |> Option.map (fun producer -> Cause.Both(cause, producer))
                    |> Option.defaultValue cause

                Flow.ofExit (Exit.Failure combined)))

    /// Folds selected values and returns the state together with completion; capture policy is independent.
    let runFold folder initial source =
        terminal (FlowStream.runFold folder initial) source

    /// Collects every selected value plus completion. The collected list can grow without bound.
    let runCollect source = terminal FlowStream.runCollect source

    /// Consumes selected output without accumulating values and returns completion.
    let runDrain source =
        terminal FlowStream.runDrain source |> Flow.map snd

    /// Synchronous callback; use runForEachFlow for explicit service operations.
    let runForEach action source =
        terminal (FlowStream.runForEach action) source |> Flow.map snd

    let private effectfulTerminal consume (ProcessStream factory) =
        Flow.delay (fun () ->
            let stream, completion, guard, failure = factory ()

            let consumer =
                consume (FlowStream.mapError ProcessStreamError.ProcessFailed stream)
                |> Flow.map (fun value -> value, completion ())

            let watcher =
                guard
                |> Flow.mapError ProcessStreamError.ProcessFailed
                |> Flow.bind (fun () -> Flow.never)

            Flow.race consumer watcher
            |> Flow.scoped
            |> Flow.foldCause (fun result ->
                failure () |> Option.iter Flow.reportUnobservedCause
                Flow.succeed result) (fun cause ->
                let combined =
                    failure ()
                    |> Option.map (fun producer ->
                        Cause.Both(cause, Cause.map ProcessStreamError.ProcessFailed producer))
                    |> Option.defaultValue cause

                Flow.ofExit (Exit.Failure combined)))

    /// Runs each callback in the application environment; preserves consumer errors separately.
    let runForEachFlow action source =
        effectfulTerminal
            (FlowStream.runForEachFlow (fun value -> action value |> Flow.mapError ProcessStreamError.ConsumerFailed))
            source
        |> Flow.map snd

    /// Folds with application workflows; typed consumer failures use ConsumerFailed.
    let runFoldFlow folder initial source =
        Flow.delay (fun () ->
            let state = ref initial

            runForEachFlow (fun value -> folder state.Value value |> Flow.map (fun next -> state.Value <- next)) source
            |> Flow.map (fun completion -> state.Value, completion))
