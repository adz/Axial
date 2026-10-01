/// Tasks, ValueTasks, Async, and blocking calls brought into flows, interrupted at random.
module Axial.TortureTest.Interop

open System
open System.Threading
open Axial
open Axial.TortureTest.Scenario

type private Kind =
    | Value
    | Failure
    | Thrown

// <snippet:torture-interop>
let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let calls = round.Size 120

    flow {
        let interruptionsSeen = ref 0

        // The operation waits a little, observing its cancellation token, then produces its kind of outcome.
        let wait (token: CancellationToken) =
#if FABLE_COMPILER
            // JavaScript cannot block, so a blocking call there only checks its token.
            if token.IsCancellationRequested then
#else
            if token.WaitHandle.WaitOne(round.Next 3) then
#endif
                increment interruptionsSeen |> ignore
                token.ThrowIfCancellationRequested()

        let asyncOperation kind : Async<int> =
            async {
                let! token = Async.CancellationToken
                do! Async.Sleep(round.Next 3)

                if token.IsCancellationRequested then
                    increment interruptionsSeen |> ignore

                match kind with
                // The scenario checks that an exception thrown by foreign code becomes a defect.
                | Thrown -> return raise (InvalidOperationException "thrown") // axial-allow-raise
                | _ -> return 7
            }

        // Each call adapts a random kind of foreign operation, and is interrupted a third of the time.
        let adapted index : Flow<Axial.ClockEnvironment, string, int> =
            let kind = [| Value; Failure; Thrown |].[round.Next 3]

            let result value : Result<int, string> =
                match kind with
                | Failure -> Error "failed"
                | _ -> Ok value

            let exnToText (error: exn) = error.Message

            let produce () =
                match kind with
                | Thrown -> raise (InvalidOperationException "thrown") // axial-allow-raise
                | _ -> 7

            match index % 7 with
            | 0 -> Flow.fromAsync (asyncOperation kind)
            | 1 -> Flow.fromAsyncResult (async { let! value = asyncOperation Value in return result value })
            | 2 -> Flow.attemptAsync (asyncOperation kind) |> Flow.mapError exnToText
            | 3 -> Flow.fromBlocking (fun token -> wait token; produce ())
            | 4 -> Flow.fromBlockingResult (fun token -> wait token; result 7)
            | 5 -> Flow.attemptBlocking (fun token -> wait token; produce ()) |> Flow.mapError exnToText
#if FABLE_COMPILER
            | _ -> Flow.fromAsync (asyncOperation kind)
#else
            | _ ->
                match round.Next 9 with
                | 0 -> Flow.fromTask (fun token -> task { wait token; return produce () })
                | 1 -> Flow.fromTaskResult (fun token -> task { wait token; return result 7 })
                | 2 -> Flow.attemptTask (fun token -> task { wait token; return produce () }) |> Flow.mapError exnToText
                | 3 -> Flow.fromValueTask (fun token -> Tasks.ValueTask<int>(task { wait token; return 7 }))
                | 4 -> Flow.fromValueTaskResult (fun token -> Tasks.ValueTask<Result<int, string>>(task { wait token; return result 7 }))
                | 5 -> Flow.attemptValueTask (fun token -> Tasks.ValueTask<int>(task { wait token; return 7 })) |> Flow.mapError exnToText
                | 6 -> Flow.awaitStartedTask (task { return 7 }) |> Flow.orElse (Flow.awaitStartedTaskResult (task { return result 7 })) |> Flow.orElse (Flow.attemptStartedTask (task { return 7 }) |> Flow.mapError exnToText)
                | 7 -> Flow.awaitStartedValueTask (Tasks.ValueTask<int>(7)) |> Flow.orElse (Flow.awaitStartedValueTaskResult (Tasks.ValueTask<Result<int, string>>(result 7))) |> Flow.orElse (Flow.attemptStartedValueTask (Tasks.ValueTask<int>(7)) |> Flow.mapError exnToText)
                | _ -> Flow.fromBlocking (fun token -> wait token; 7) |> Flow.catchCancellation (fun _ -> "cancelled")
#endif

        let! fibers = [ 1..calls ] |> Flow.traverse (adapted >> Flow.fork)
        let! exits = fibers |> Flow.traverse (fun fiber -> if round.Chance 33 then Fiber.interrupt fiber else Fiber.await fiber)

        // Every outcome is a value, a typed failure, a defect from a thrown exception, or an interruption.
        let wellFormed =
            exits
            |> List.forall (fun exit ->
                match exit with
                | Exit.Success 7 -> true
                | Exit.Success _ -> false
                | Exit.Failure cause when Cause.isInterrupted cause -> true
                | Exit.Failure cause -> not (Cause.failures cause).IsEmpty || not (Cause.defects cause).IsEmpty)

        // A flow run as a task or an async gives the same exit as running it inside a flow.
        let sample : Flow<Axial.ClockEnvironment, string, int> = Flow.ok 5 |> Flow.bind (fun value -> if value > 3 then Flow.fail "big" else Flow.ok value)
        let! direct = sample |> exitOf
        let! viaAsync = Flow.fromAsync (Flow.toAsync clockEnvironment sample)
#if FABLE_COMPILER
        let viaTask = viaAsync
        let viaRun = direct
        let coldAsExpected = true
#else
        let viaRun = sample |> Flow.run clockEnvironment

        // Cold tasks start only when a flow binds them, and bind directly in flow { }.
        let startedCold = ref 0
        let count () = increment startedCold |> ignore

        let cold =
            [ ColdTask.create (fun token -> task { count (); wait token; return 1 })
              ColdTask.fromTaskFactory (fun () -> task { count (); return 2 })
              ColdTask.awaitStartedTask (Tasks.Task.FromResult 3)
              ColdTask.fromValueTaskFactory (fun _ -> count (); Tasks.ValueTask<int>(4))
              ColdTask.fromValueTaskFactoryWithoutCancellation (fun () -> count (); Tasks.ValueTask<int>(5))
              ColdTask.awaitStartedValueTask (Tasks.ValueTask<int>(6)) ]

        let startedBeforeBinding = startedCold.Value
        let! coldValues = cold |> Flow.traverse (fun coldTask -> flow { let! value = coldTask in return value })
        let! runValue = Flow.fromTask (fun token -> ColdTask.run token (List.head cold))
        let coldAsExpected = startedBeforeBinding = 0 && coldValues = [ 1..6 ] && runValue = 1 && startedCold.Value = 5

        // startTask runs the flow to completion by itself, so there is no token to pass on.
        let! viaTask = Flow.fromTask (fun _ -> Flow.startTask clockEnvironment sample) // axial-allow-discarded-cancellation
#endif

        // A flow that checks the runtime's token stops when interrupted, even while it never suspends.
        let spins = ref 0
        let spinnerToken = ref CancellationToken.None

        let spinner : Flow<Axial.ClockEnvironment, Never, unit> =
            flow {
                let! token = Flow.cancellationToken
                spinnerToken.Value <- token

                // The loop has no exit of its own: only ensureNotCanceled can end it, as an interruption.
                while true do
                    increment spins |> ignore
                    do! Flow.ensureNotCanceled
                    do! Flow.sleep TimeSpan.Zero
            }

        let! spinning = Flow.fork spinner
        do! Flow.sleep (TimeSpan.FromMilliseconds 2.0)
        let! spinExit = Fiber.interrupt spinning

        return
            [ check "every adapted call produced a value, a typed failure, a defect, or an interruption" wellFormed
              check "a flow run as a task or an async has the same exit" (viaAsync = direct && viaTask = direct && viaRun = direct)
              check "cold tasks started only when bound, once per binding" coldAsExpected
              check "a flow checking its token stopped when interrupted, and the token it read was cancelled" (isInterrupted spinExit && spinnerToken.Value.IsCancellationRequested) ]
    }
// </snippet:torture-interop>

let scenario : Scenario =
    { Name = "interop"
      Title = "Interop: Task, ValueTask, Async, and blocking calls, interrupted at random"
      Run = run }
