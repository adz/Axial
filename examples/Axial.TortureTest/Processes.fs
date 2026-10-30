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
        let callbackEnvironment = { Process = Process.test() :> IProcess }
        let callbackLog = Axial.PlatformService.Log.memory ()
        let observedTime = ref None
        let! callbackResult =
            Process.command $"scripted"
            |> Process.onStarted (fun _ -> flow {
                let! now = Axial.PlatformService.Clock.now
                observedTime.Value <- Some now
                do! Axial.PlatformService.Log.info "process-callback"
            })
            |> Process.toFlow
            |> Flow.localEnv (fun () -> callbackEnvironment)
            |> Flow.withClock (Axial.PlatformService.Clock.``fixed`` callbackTime)
            |> Flow.withLogSink callbackLog
            |> exitOf
#endif
        return [
#if !FABLE_COMPILER
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
