namespace Axial.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.PlatformService
open Axial.Process
open Axial.Process.DSL
open Axial.FileSystem
open Axial.Console
open Xunit

type private RuntimeProcessEnv =
    { Process: IProcess }
    interface IHasProcess with member this.Process = this.Process

module ProcessRuntimeTests =
    [<Fact>]
    let ``process host records expose authority rather than disconnected clocks`` () =
        Assert.DoesNotContain(typeof<ProcessHostEnvironment>.GetProperties(), fun property -> property.Name = "Clock")
        Assert.DoesNotContain(typeof<TestProcessHostEnvironment>.GetProperties(), fun property -> property.Name = "Clock")

    let private signalling (clock: ManualClock) (registered: SemaphoreSlim) (delays: ConcurrentQueue<TimeSpan>) =
        { new IClock with
            member _.UtcNow() = (clock :> IClock).UtcNow()
            member _.Elapsed() = (clock :> IClock).Elapsed()
            member _.Sleep(delay, token) =
                let pending = (clock :> IClock).Sleep(delay, token)
                delays.Enqueue delay
                registered.Release() |> ignore
                pending }

    [<Fact>]
    let ``process callbacks inherit caller services rather than engine time or fresh defaults`` () =
        if not (OperatingSystem.IsWindows()) then
            let engine = ManualClock(DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero))
            let callerTime = DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero)
            let caller = Clock.fromValue callerTime
            let log = Log.memory ()
            let seen = ConcurrentQueue<string * DateTimeOffset * int * System.Guid>()
            let annotations = ConcurrentQueue<string * string>()
            let telemetry =
                { new ITelemetry with
                    member _.Annotate(name, value) = annotations.Enqueue(name, value)
                    member _.Attribute _ = () }
            let probe label =
                (flow {
                    let! now = Clock.now
                    let! random = Random.next
                    let! guid = Guid.newGuid
                    seen.Enqueue(label, now, random, guid)
                    do! Log.info ("callback-" + label)
                }) |> Flow.annotate "callback" label
            let startedReleased = ref false
            let env = { Process = Process.live engine FileSystem.live Console.live }
            let result =
                shText "printf payload"
                |> stdoutTo (OutputTarget.Sink((fun _ ->
                    Flow.delay (fun () -> Assert.True(startedReleased.Value, "onStarted scope must close before output"); probe "output")), (fun () -> probe "finish")))
                |> onStarted (fun _ -> flow {
                    do! Flow.scopeFinalizer (Flow.delay (fun () -> startedReleased.Value <- true; Flow.ok ()))
                    do! probe "started"
                })
                |> toFlow
                |> Flow.withClock caller
                |> Flow.withRandom (Random.fromValue 17)
                |> Flow.withGuid (Guid.fromValue System.Guid.Empty)
                |> Flow.withLogSink log
                |> Flow.withTelemetry telemetry
                |> Flow.run env
            match result with
            | Exit.Success _ -> ()
            | other -> failwithf "%A" other
            for label in ["started"; "output"; "finish"] do
                Assert.Contains(seen, fun (name, _, _, _) -> name = label)
                Assert.Contains(log.Entries, fun entry -> entry.Message = "callback-" + label)
                Assert.Contains(("callback", label), annotations)
            for _, now, random, guid in seen do
                Assert.Equal(callerTime, now)
                Assert.Equal(17, random)
                Assert.Equal(System.Guid.Empty, guid)
            Assert.Equal(seen.Count, log.Entries.Count)

    [<Fact>]
    let ``explicit process clock drives timeout and kill grace independently of caller clock`` () = task {
        if not (OperatingSystem.IsWindows()) then
            let engine = ManualClock(DateTimeOffset.UnixEpoch)
            let caller = ManualClock(DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero))
            use registered = new SemaphoreSlim(0)
            use rescue = new CancellationTokenSource()
            let delays = ConcurrentQueue<TimeSpan>()
            let clock = signalling engine registered delays
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let env = { Process = Process.live clock FileSystem.live Console.live }
            let running =
                shText "trap '' TERM; echo ready; exec sleep 30"
                |> stdoutTo (Output.callback (fun _ -> Flow.delay (fun () -> ready.TrySetResult() |> ignore; Flow.ok ())))
                |> Process.timeout (TimeSpan.FromSeconds 5.)
                |> stopGracefully [StopRequest.Terminate, TimeSpan.FromSeconds 3.]
                |> toFlow |> Flow.withClock caller |> Flow.withLogSink LogSink.none
                |> fun source -> source.StartAsTask(env, rescue.Token)
            try
                do! ready.Task.WaitAsync(TimeSpan.FromSeconds 10.)
                Assert.True(registered.Wait(TimeSpan.FromSeconds 10.))
                engine.AdvanceBy(TimeSpan.FromSeconds 5.)
                Assert.True(registered.Wait(TimeSpan.FromSeconds 10.), "Grace must register on the engine clock")
                Assert.False(running.IsCompleted)
                Assert.Contains(TimeSpan.FromSeconds 3., delays)
                engine.AdvanceBy(TimeSpan.FromSeconds 3.)
                let! result = running.WaitAsync(TimeSpan.FromSeconds 10.)
                match result with
                | Exit.Failure(Cause.Fail(ProcessError.StageTimedOut _)) -> ()
                | other -> failwithf "Expected timeout after manual kill grace, got %A" other
                Assert.Equal(TimeSpan.Zero, (caller :> IClock).Elapsed())
            finally
                rescue.Cancel()
                caller.AdvanceBy(TimeSpan.FromDays 1.)
                engine.AdvanceBy(TimeSpan.FromDays 1.)
    }

    [<Fact>]
    let ``explicit process clock drives post exit drain rather than caller clock`` () = task {
        if not (OperatingSystem.IsWindows()) then
            let engine = ManualClock(DateTimeOffset.UnixEpoch)
            let caller = ManualClock(DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero))
            use registered = new SemaphoreSlim(0)
            use rescue = new CancellationTokenSource()
            let delays = ConcurrentQueue<TimeSpan>()
            let clock = signalling engine registered delays
            let finishing = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let env = { Process = Process.live clock FileSystem.live Console.live }
            let finish () : Flow<unit, string, unit> = flow {
                finishing.TrySetResult() |> ignore
                return! Flow.never
            }
            let running =
                shText "printf payload"
                |> stdoutTo (OutputTarget.Sink((fun _ -> Flow.ok ()), finish))
                |> drainTimeout (TimeSpan.FromSeconds 3.)
                |> toFlow |> Flow.withClock caller |> Flow.withLogSink LogSink.none
                |> fun source -> source.StartAsTask(env, rescue.Token)
            try
                do! finishing.Task.WaitAsync(TimeSpan.FromSeconds 10.)
                Assert.True(registered.Wait(TimeSpan.FromSeconds 10.), "Drain must register on the engine clock")
                Assert.Contains(TimeSpan.FromSeconds 3., delays)
                Assert.False(running.IsCompleted)
                engine.AdvanceBy(TimeSpan.FromSeconds 3.)
                let! result = running.WaitAsync(TimeSpan.FromSeconds 10.)
                match result with
                | Exit.Failure(Cause.Fail(ProcessError.IoFailed failure)) -> Assert.Contains("after all process stages exited", failure.Message)
                | other -> failwithf "Expected manual drain timeout, got %A" other
                Assert.Equal(TimeSpan.Zero, (caller :> IClock).Elapsed())
            finally
                rescue.Cancel()
                caller.AdvanceBy(TimeSpan.FromDays 1.)
                engine.AdvanceBy(TimeSpan.FromDays 1.)
    }
