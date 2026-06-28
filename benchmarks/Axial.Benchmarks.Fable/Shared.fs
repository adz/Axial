namespace Axial.Benchmarks.Fable

open System
open System.Threading
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation

[<RequireQualifiedAccess>]
module Shared =
    [<Literal>]
    let SyncDepth = 20

    [<Literal>]
    let AsyncDepth = 20

    [<Literal>]
    let ReaderDepth = 10

    type ReaderEnv =
        {
            Prefix: string
        }

    let consumeResult (result: Result<int, string>) =
        match result with
        | Ok value -> value
        | Error error -> error.Length

    let consumeExit (exit: Exit<int, string>) =
        match exit with
        | Exit.Success value -> value
        | Exit.Failure (Cause.Fail error) -> error.Length
        | Exit.Failure _ -> -1

    let measure iterations (name: string) (work: unit -> 'value) =
        let start = DateTime.UtcNow.Ticks
        let mutable last = Unchecked.defaultof<'value>

        for _ in 1 .. iterations do
            last <- work ()

        let elapsedTicks = DateTime.UtcNow.Ticks - start
        let averageNs = float elapsedTicks * 100.0 / float iterations

        printfn "%s: %.2f ns" name averageNs
        last |> ignore

    let buildSyncManual () =
        let mutable result = Ok 0

        for index in 1 .. SyncDepth do
            result <- result |> Result.bind (fun value -> Ok(value + index))

        result

    let buildSyncFlow () =
        let mutable flow = Flow.succeed 0

        for index in 1 .. SyncDepth do
            flow <- flow |> Flow.bind (fun value -> Flow.succeed(value + index))

        flow

    let buildAsyncManual () =
        let rec loop index value =
            async {
                if index > AsyncDepth then
                    return Ok value
                else
                    let! next = async.Return(value + index)
                    return! loop (index + 1) next
            }

        fun () -> loop 1 0

    let buildReaderManual () =
        fun (environment: ReaderEnv) ->
            async {
                let mutable value = environment.Prefix.Length

                for index in 1 .. ReaderDepth do
                    value <- value + index

                return Ok value
            }

    let buildAsyncFlow () =
        let mutable workflow = Flow.succeed 0

        for index in 1 .. AsyncDepth do
            workflow <-
                workflow
                |> Flow.bind (fun value ->
                    flow {
                        let! next = async { return value + index }
                        return next
                    })

        workflow

    let buildReaderFlow () =
        let mutable workflow =
            Flow.env
            |> Flow.map (fun environment -> environment.Prefix.Length)

        for index in 1 .. ReaderDepth do
            workflow <- workflow |> Flow.map (fun value -> value + index)

        workflow

    let runAsyncResult (workflow: unit -> Async<Result<int, string>>) =
        let mutable completed = false
        let mutable result = 0

        Async.StartWithContinuations(
            workflow (),
            (fun value ->
                result <- consumeResult value
                completed <- true),
            (fun ex -> raise ex),
            (fun _ -> raise (OperationCanceledException())))

        if completed then
            result
        else
            failwith "The Fable async benchmark workflow did not complete synchronously."

#if FABLE_COMPILER
    let runFlow (environment: 'env) (flow: Flow<'env, string, int>) =
        let mutable completed = false
        let mutable result = 0

        Async.StartWithContinuations(
            flow.ToAsync(environment),
            (fun value ->
                result <- consumeExit value
                completed <- true),
            (fun ex -> raise ex),
            (fun _ -> raise (OperationCanceledException())))

        if completed then
            result
        else
            failwith "The Fable flow benchmark did not complete synchronously."
#else
    let runFlow _ _ =
        failwith "Build Axial.Benchmarks.Fable with Fable to run this benchmark runner."
#endif
