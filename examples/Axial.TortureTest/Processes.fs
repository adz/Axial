/// Completion-aware process streams exercised against a portable scripted interpreter.
module Axial.TortureTest.Processes

open System
open Axial
open Axial.Process
open Axial.TortureTest.Scenario

type private Environment =
    { Process: IProcess }
    interface IHasProcess with member this.Process = this.Process

let run (round: Round) : Flow<unit, Never, Check list> =
    let input = [for _ in 1..round.Size 100 -> round.Next 20]
    let captured = {Text="";Bytes=[||];Truncated=false;Available=true}
    let result =
        {ExitCode=0;ExitCodes=[0];StdOut="";StdErr="";StdOutCapture=captured;StdErrCapture=captured
         Stages=[];StartedAt=DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero);Duration=TimeSpan.Zero}
    let mutable runs = 0
    let service =
        {new IProcess with
            member _.Execute(_,observe) = flow {
                runs <- runs+1
                for item in input do
                    do! observe {Stage=0;ObservedChannel=OutputChannel.StdOut;OriginalChannel=OutputChannel.StdOut
                                 Text=string item;Timestamp=DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero)}
                return result
            }}
    let env = {Process=service}
    let source () = Process.command $"scripted" |> Process.streamStdout
    let observe workflow = workflow |> Flow.localEnv(fun (_: unit) -> env) |> Flow.foldCause (Exit.Success >> Flow.succeed) (Exit.Failure >> Flow.succeed)
    flow {
        let! collected = source() |> ProcessStream.map(fun item -> int item.Text) |> ProcessStream.filter(fun item -> item%2=0) |> ProcessStream.runCollect |> observe
        let! folded = source() |> ProcessStream.runFold(fun total item -> total+int item.Text) 0 |> observe
        let! drained = source() |> ProcessStream.runDrain |> observe
        let counted = ref 0
        let! forEach = source() |> ProcessStream.runForEach(fun _ -> counted.Value <- counted.Value+1) |> observe
        let! foldedFlow = source() |> ProcessStream.runFoldFlow(fun total item -> Flow.succeed(total+int item.Text)) 0 |> observe
        let! forEachFlow = source() |> ProcessStream.runForEachFlow(fun _ -> Flow.succeed()) |> observe
        let! early = source() |> ProcessStream.take 1 |> ProcessStream.runCollect |> observe
        let! whileEarly = source() |> ProcessStream.takeWhile(fun _ -> false) |> ProcessStream.runDrain |> observe
        let! converted = source() |> ProcessStream.toFlowStream |> FlowStream.runCollect |> observe
        let beforeZero = runs
        let! zero = source() |> ProcessStream.take 0 |> ProcessStream.runDrain |> observe
#if !FABLE_COMPILER
        let callbackTime = DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)
        let callbackProcess = Process.test()
        callbackProcess.RunHandler <- fun _ -> TestProcess.Result(0, "")
        let callbackEnvironment = { Process = callbackProcess :> IProcess }
        let callbackLog = Axial.Log.memory ()
        let observedTime = ref None
        let! callbackResult =
            Process.command $"scripted"
            |> Process.onStarted (fun _ -> flow {
                let! now = Axial.Clock.now
                observedTime.Value <- Some now
                do! Axial.Log.info "process-callback"
            })
            |> Process.toFlow
            |> Flow.localEnv (fun () -> callbackEnvironment)
            |> Flow.withClock (Axial.Clock.fromValue callbackTime)
            |> Flow.withLogSink callbackLog
            |> exitOf
#endif
#if !FABLE_COMPILER
        let mutable closeStopped = true
        let mutable signalHalfClosed = true
        let mutable childClosedInput = true
        if not (Runtime.InteropServices.RuntimeInformation.IsOSPlatform(Runtime.InteropServices.OSPlatform.Windows)) then
            let! closedInput =
                Axial.Process.DSL.shText "exec 0<&-; printf done"
                |> Process.stdinFromText (String('x', 1024 * 1024))
                |> Process.toFlow
                |> Flow.localEnv (fun () ->
                    { Process = Process.live Axial.FileSystem.FileSystem.live Axial.Console.Console.live })
                |> exitOf
            childClosedInput <-
                match closedInput with
                | Exit.Success result -> result.ExitCode = 0 && result.StdOut = "done"
                | _ -> false
            let signalClock = ManualClock(DateTimeOffset.UnixEpoch)
            let! halfClosed =
                Axial.Process.DSL.shText "read line; exit 0"
                |> Process.stopGracefully [ StopRequest.Terminate, TimeSpan.FromSeconds 17. ]
                |> Process.onStarted (fun handle -> handle.Signal StopRequest.CloseStdIn |> Flow.recover (fun _ -> Flow.succeed ()))
                |> Process.toFlow
                |> Flow.withClock signalClock
                |> Flow.localEnv (fun () ->
                    { Process = Process.live Axial.FileSystem.FileSystem.live Axial.Console.Console.live })
                |> exitOf
            signalHalfClosed <-
                (match halfClosed with Exit.Success result -> result.ExitCode = 0 | _ -> false)
                && signalClock.Sleepers = 0
            let mutable retained = None
            let clock = ManualClock(DateTimeOffset.UnixEpoch)
            let! armed = Deferred.make<unit, Never, unit> ()
            let grace = TimeSpan.FromSeconds 17.
            let notifying =
                { new IClock with
                    member _.UtcNow() = (clock :> IClock).UtcNow()
                    member _.Elapsed() = (clock :> IClock).Elapsed()
                    member _.Sleep(delay, token) =
                        let pending = (clock :> IClock).Sleep(delay, token)
                        if delay = grace then Deferred.succeedNow () armed |> ignore
                        pending }
            let! stopped =
                Axial.Process.DSL.shText "trap '' TERM HUP; while :; do :; done"
                |> Process.stopGracefully [ StopRequest.CloseStdIn, grace ]
                |> Process.overallTimeout (TimeSpan.FromSeconds 100.)
                |> Process.onStarted (fun handle -> flow {
                    retained <- Some handle
                    // EOF cannot terminate this child. Close must start the coordinator's ladder,
                    // even when submitted before output pumps and exit monitors have started.
                    let! _ = handle.Close StopRequest.CloseStdIn |> exitOf
                    do! Deferred.await armed
                    // The second acceptance must precede clock advancement, not shutdown.
                    let! _ = handle.Close StopRequest.CloseStdIn |> exitOf
                    let! _ = handle.Signal StopRequest.CloseStdIn |> exitOf
                    clock.AdvanceBy grace
                    return ()
                })
                |> Process.toFlow
                |> Flow.withClock notifying
                |> Flow.localEnv (fun () ->
                    { Process = Process.live Axial.FileSystem.FileSystem.live Axial.Console.Console.live })
                |> exitOf
            let! afterExit =
                match retained with
                | Some handle -> handle.Signal StopRequest.Terminate |> exitOf
                | None -> Flow.succeed (Exit.Failure (Cause.Fail (ProcessError.StopUnsupported StopRequest.Terminate)))
            closeStopped <-
                (match stopped with Exit.Failure(Cause.Fail(ProcessError.StageFailed _)) -> true | _ -> false)
                && afterExit = Exit.Success ()
                && clock.Sleepers = 0
#endif
        return [
#if !FABLE_COMPILER
            check "a child closing stdin settles its feeder without losing successful output" childClosedInput
            check "Signal half-closes stdin without escalation or leaked timers" signalHalfClosed
            check "repeated Close is accepted with grace parked, then the ladder reaps without leaking timers" closeStopped
            check "process callbacks inherit runtime clock and log sink without a new root" (match callbackResult with Exit.Success _ -> observedTime.Value = Some callbackTime && callbackLog.Entries.Count = 1 | _ -> false)
#endif
            check "transforms preserve completion" (match collected with Exit.Success(values,ProcessCompletion.Completed _) -> values=List.filter(fun item -> item%2=0) input | _ -> false)
            check "pure fold agrees with input" (match folded with Exit.Success(sum,ProcessCompletion.Completed _) -> sum=List.sum input | _ -> false)
            check "drain completes" (match drained with Exit.Success(ProcessCompletion.Completed _) -> true | _ -> false)
            check "sync callback consumes all" (match forEach with Exit.Success(ProcessCompletion.Completed _) -> counted.Value=input.Length | _ -> false)
            check "effectful fold agrees with input" (match foldedFlow with Exit.Success(sum,ProcessCompletion.Completed _) -> sum=List.sum input | _ -> false)
            check "effectful callback completes" (match forEachFlow with Exit.Success(ProcessCompletion.Completed _) -> true | _ -> false)
            check "take stops successfully" (match early with Exit.Success([_],ProcessCompletion.StoppedEarly) -> true | _ -> false)
            check "takeWhile stops successfully" (whileEarly=Exit.Success ProcessCompletion.StoppedEarly)
            check "conversion consumes output" (match converted with Exit.Success values -> values.Length=input.Length | _ -> false)
            check "take zero skips interpreter" (zero=Exit.Success ProcessCompletion.StoppedEarly && beforeZero=runs)
        ]
    }

let scenario : Scenario =
    {Name="processes";Title="Completion-aware process streams";Run=run}
