namespace Axial.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Axial
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
            let env = { Process = Process.live FileSystem.live Console.live }
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
            let env = { Process = Process.live FileSystem.live Console.live }
            let running =
                shText "trap '' TERM; echo ready; exec sleep 30"
                |> stdoutTo (Output.callback (fun _ -> Flow.delay (fun () -> ready.TrySetResult() |> ignore; Flow.ok ())))
                |> Process.timeout (TimeSpan.FromSeconds 5.)
                |> stopGracefully [StopRequest.Terminate, TimeSpan.FromSeconds 3.]
                |> toFlow |> Flow.withClock clock |> Flow.withLogSink LogSink.none
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
            let env = { Process = Process.live FileSystem.live Console.live }
            let finish () : Flow<unit, string, unit> = flow {
                finishing.TrySetResult() |> ignore
                return! Flow.never
            }
            let running =
                shText "printf payload"
                |> stdoutTo (OutputTarget.Sink((fun _ -> Flow.ok ()), finish))
                |> drainTimeout (TimeSpan.FromSeconds 3.)
                |> toFlow |> Flow.withClock clock |> Flow.withLogSink LogSink.none
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

module ProcessExecutionClockTests =
    type private ExecutionEnv = { Process: IProcess } interface IHasProcess with member this.Process = this.Process

    [<Fact>]
    let ``same live process service uses each root execution clock`` () = task {
        if not (OperatingSystem.IsWindows()) then
            let first = Clock.fromValue(DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero))
            let second = Clock.fromValue(DateTimeOffset(2003, 2, 2, 0, 0, 0, TimeSpan.Zero))
            let service = Process.live FileSystem.live Console.live
            let run clock =
                (shText "printf clock" |> toFlow)
                |> Flow.withClock clock
                |> Flow.withLogSink LogSink.none
                |> Flow.run { Process = service }
            match run first, run second with
            | Exit.Success a, Exit.Success b ->
                Assert.Equal(DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero), a.StartedAt)
                Assert.Equal(DateTimeOffset(2003, 2, 2, 0, 0, 0, TimeSpan.Zero), b.StartedAt)
            | other -> failwithf "Expected both roots to succeed, got %A" other
    }

    let private signallingClock (clock: ManualClock) (registered: SemaphoreSlim) =
        { new IClock with
            member _.UtcNow() = (clock :> IClock).UtcNow()
            member _.Elapsed() = (clock :> IClock).Elapsed()
            member _.Sleep(delay, token) =
                let pending = (clock :> IClock).Sleep(delay, token)
                registered.Release() |> ignore
                pending }

    [<Fact>]
    let ``same live process service isolates simultaneous execution clocks`` () = task {
        if not (OperatingSystem.IsWindows()) then
            let service = Process.live FileSystem.live Console.live
            let first = ManualClock(DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero))
            let second = ManualClock(DateTimeOffset(2003, 2, 2, 0, 0, 0, TimeSpan.Zero))
            let run clock label =
                let entered = TaskCompletionSource<ProcessOutput>(TaskCreationOptions.RunContinuationsAsynchronously)
                let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
                let observing =
                    { new IProcess with
                        member _.Execute(specification, _) =
                            service.Execute(specification, fun output -> async {
                                entered.TrySetResult output |> ignore
                                do! Async.AwaitTask release.Task
                            }) }
                let running =
                    shText ("printf " + label) |> toFlow
                    |> Flow.withClock clock
                    |> Flow.withLogSink LogSink.none
                    |> fun source -> source.StartAsTask({ Process = observing })
                running, entered.Task, release
            let a, enteredA, releaseA = run first "a"
            let b, enteredB, releaseB = run second "b"
            try
                let! outputA = enteredA.WaitAsync(TimeSpan.FromSeconds 10.)
                let! outputB = enteredB.WaitAsync(TimeSpan.FromSeconds 10.)
                Assert.False(a.IsCompleted)
                Assert.False(b.IsCompleted)
                Assert.Equal((first :> IClock).UtcNow(), outputA.Timestamp)
                Assert.Equal((second :> IClock).UtcNow(), outputB.Timestamp)
                Assert.Equal("a", outputA.Text)
                Assert.Equal("b", outputB.Text)
                first.AdvanceBy(TimeSpan.FromSeconds 7.)
                Assert.Equal(TimeSpan.Zero, (second :> IClock).Elapsed())
                second.AdvanceBy(TimeSpan.FromSeconds 13.)
            finally
                releaseA.TrySetResult() |> ignore
                releaseB.TrySetResult() |> ignore
            let! firstExit = a.WaitAsync(TimeSpan.FromSeconds 20.0)
            let! secondExit = b.WaitAsync(TimeSpan.FromSeconds 20.0)
            match firstExit, secondExit with
            | Exit.Success a, Exit.Success b ->
                Assert.Equal(DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero), a.StartedAt)
                Assert.Equal(DateTimeOffset(2003, 2, 2, 0, 0, 0, TimeSpan.Zero), b.StartedAt)
                Assert.Equal(TimeSpan.FromSeconds 7., a.Duration)
                Assert.Equal(TimeSpan.FromSeconds 13., b.Duration)
            | other -> failwithf "Expected both concurrent roots to succeed, got %A" other
    }

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``external close ladder cannot replace the run clock`` (fromOtherFlow: bool) = task {
        if not (OperatingSystem.IsWindows()) then
            let runClock = ManualClock(DateTimeOffset.UnixEpoch)
            let unrelated = ManualClock(DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero))
            use registered = new SemaphoreSlim(0)
            use rescue = new CancellationTokenSource()
            let clock = signallingClock runClock registered
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            let service = Process.live FileSystem.live Console.live
            let running =
                shText "trap '' TERM; echo ready; exec sleep 30"
                |> stdoutTo (Output.callback (fun _ -> Flow.delay (fun () -> ready.TrySetResult() |> ignore; Flow.ok ())))
                |> overallTimeout (TimeSpan.FromSeconds 10.)
                |> stopGracefully [ StopRequest.Terminate, TimeSpan.FromSeconds 1. ]
                |> onStarted (fun handle -> Flow.delay (fun () -> started.TrySetResult handle |> ignore; Flow.ok ()))
                |> toFlow
                |> Flow.withClock clock
                |> Flow.withLogSink LogSink.none
                |> fun source -> source.StartAsTask({ Process = service }, rescue.Token)
            try
                do! ready.Task.WaitAsync(TimeSpan.FromSeconds 10.0)
                let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 10.0)
                // The external caller only submits Close; the coordinator retains the run clock.
                let close =
                    Task.Run(fun () ->
                        let request = handle.Close StopRequest.Terminate
                        if fromOtherFlow then request |> Flow.withClock unrelated |> Flow.run ()
                        else request.RunSynchronously(()))
                let! closeExit = close.WaitAsync(TimeSpan.FromSeconds 10.0)
                match closeExit with
                | Exit.Success () -> ()
                | other -> failwithf "Expected the external close to be accepted, got %A" other
                unrelated.AdvanceBy(TimeSpan.FromDays 1.)
                Assert.False(running.IsCompleted, "the unrelated clock must not drive the run's timers")
                Assert.True(registered.Wait(TimeSpan.FromSeconds 10.0), "overall timeout must register on the run clock")
                Assert.False(running.IsCompleted, "the TERM grace has not elapsed on the run clock")
                Assert.True(registered.Wait(TimeSpan.FromSeconds 10.0), "the stop grace must register on the run clock")
                runClock.AdvanceBy(TimeSpan.FromSeconds 1.)
                let! exit = running.WaitAsync(TimeSpan.FromSeconds 10.0)
                match exit with
                | Exit.Failure(Cause.Fail(ProcessError.StageFailed _)) -> ()
                | other -> failwithf "Expected external close to kill after the grace ladder, got %A" other
            finally
                rescue.Cancel()
                runClock.AdvanceBy(TimeSpan.FromDays 1.)
                unrelated.AdvanceBy(TimeSpan.FromDays 1.)
    }

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``one shot signal leaves shutdown ownership with caller`` (halfClose: bool) = task {
        if not (OperatingSystem.IsWindows()) then
            let clock = ManualClock(DateTimeOffset.UnixEpoch)
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let acknowledged = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            use rescue = new CancellationTokenSource()
            let service = Process.live FileSystem.live Console.live
            let mutable deliveries = 0
            let prelude = if halfClose then "read line; echo request; " else ""
            let running =
                shText ("trap 'echo request' TERM; trap 'exit 0' USR1; echo ready; " + prelude + "while :; do :; done")
                |> stdinFrom (Input.flowStream (FlowStream.repeatFlow (Flow.never)))
                |> stdoutTo (Output.callback (fun output -> Flow.delay (fun () ->
                    let text = System.Text.Encoding.UTF8.GetString output
                    if text.Contains "ready" then ready.TrySetResult() |> ignore
                    if text.Contains "request" then
                        Interlocked.Increment(&deliveries) |> ignore
                        acknowledged.TrySetResult() |> ignore
                    Flow.ok ())))
                |> onStarted (fun handle -> Flow.delay (fun () -> started.TrySetResult handle |> ignore; Flow.ok ()))
                |> stopGracefully [ StopRequest.Terminate, TimeSpan.FromSeconds 17. ]
                |> toFlow |> Flow.withClock clock |> Flow.withLogSink LogSink.none
                |> fun work -> work.StartAsTask({ Process = service }, rescue.Token)
            let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 10.)
            try
                do! ready.Task.WaitAsync(TimeSpan.FromSeconds 10.)
                let signal request = handle.Signal request |> fun work -> work.StartAsTask(())
                let! unsupported = signal StopRequest.CloseMainWindow
                Assert.Equal(Exit.Failure(Cause.Fail(ProcessError.StopUnsupported StopRequest.CloseMainWindow)), unsupported)
                let request = if halfClose then StopRequest.CloseStdIn else StopRequest.Terminate
                let! accepted = signal request
                Assert.Equal(Exit.Success (), accepted)
                do! acknowledged.Task.WaitAsync(TimeSpan.FromSeconds 10.)
                clock.AdvanceBy(TimeSpan.FromDays 1.)
                Assert.False(running.IsCompleted, "one request must not acquire shutdown ownership")
                Assert.Equal(0, clock.Sleepers)
                let usr1 = if OperatingSystem.IsMacOS() then 30 else 10
                let! finished = signal (StopRequest.Signal usr1)
                Assert.Equal(Exit.Success (), finished)
                let! exit = running.WaitAsync(TimeSpan.FromSeconds 10.)
                match exit with
                | Exit.Success result -> Assert.Equal(0, result.ExitCode)
                | other -> failwithf "Expected caller-owned exit, got %A" other
                Assert.Equal(1, Volatile.Read(&deliveries))
                let! afterExit = signal StopRequest.Terminate
                Assert.Equal(Exit.Success (), afterExit)
                Assert.Equal(0, clock.Sleepers)
            finally
                rescue.Cancel()
                clock.AdvanceBy(TimeSpan.FromDays 1.)
                try
                    use child = Diagnostics.Process.GetProcessById handle.ProcessId
                    if not child.HasExited then child.Kill(entireProcessTree = true)
                with :? ArgumentException -> ()
    }

    [<Fact>]
    let ``Windows one shot mapping rejects console signals and half closes stdin`` () = task {
        if OperatingSystem.IsWindows() then
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            use rescue = new CancellationTokenSource()
            let running =
                Process.commandArgs "cmd.exe" [ "/c"; "more >nul" ]
                |> stdinFrom (Input.flowStream (FlowStream.repeatFlow Flow.never))
                |> onStarted (fun handle -> Flow.delay (fun () -> started.TrySetResult handle |> ignore; Flow.ok ()))
                |> toFlow
                |> fun work -> work.StartAsTask({ Process = Process.live FileSystem.live Console.live }, rescue.Token)
            try
                let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 10.)
                for request in [ StopRequest.Interrupt; StopRequest.Terminate; StopRequest.Hangup; StopRequest.Signal 1 ] do
                    let! result = handle.Signal request |> fun work -> work.StartAsTask(())
                    Assert.Equal(Exit.Failure(Cause.Fail(ProcessError.StopUnsupported request)), result)
                let! accepted = handle.Signal StopRequest.CloseStdIn |> fun work -> work.StartAsTask(())
                Assert.Equal(Exit.Success (), accepted)
                let! result = running.WaitAsync(TimeSpan.FromSeconds 10.)
                match result with
                | Exit.Success value -> Assert.Equal(0, value.ExitCode)
                | other -> failwithf "Expected EOF completion, got %A" other
            finally
                rescue.Cancel()
    }

    [<Theory>]
    [<InlineData("timeout")>]
    [<InlineData("close")>]
    [<InlineData("close-twice")>]
    [<InlineData("close-before-timeout")>]
    [<InlineData("timeout-before-close")>]
    [<InlineData("signal-during-close")>]
    let ``close ladder runs once with native signals grace and engine reap`` (mode: string) = task {
        if not (OperatingSystem.IsWindows()) then
            let clock = ManualClock(DateTimeOffset.UnixEpoch)
            use registrations = new SemaphoreSlim(0)
            let delays = ConcurrentQueue<TimeSpan>()
            let deadlines = ConcurrentQueue<string * TimeSpan * TimeSpan>()
            let registry = FiberRegistry()
            let mutable activeTimers = 0
            let timeoutElapsed = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let directory = IO.Path.Combine(IO.Path.GetTempPath(), "axial-grace-" + global.System.Guid.NewGuid().ToString("N"))
            IO.Directory.CreateDirectory directory |> ignore
            use watcher = new IO.FileSystemWatcher(directory)
            let termSeen = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let hupSeen = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let secondHupSeen = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            watcher.Created.Add(fun event ->
                if event.Name = "term" then termSeen.TrySetResult() |> ignore
                if event.Name = "hup-1" then hupSeen.TrySetResult() |> ignore
                if event.Name = "hup-2" then secondHupSeen.TrySetResult() |> ignore)
            watcher.EnableRaisingEvents <- true
            let observed = ConcurrentDictionary<string, TaskCompletionSource<ProcessOutput>>()
            for text in [ "ready"; "term"; "hup" ] do
                observed[text] <- TaskCompletionSource<ProcessOutput>(TaskCreationOptions.RunContinuationsAsynchronously)
            let notifying =
                { new IClock with
                    member _.UtcNow() = (clock :> IClock).UtcNow()
                    member _.Elapsed() = (clock :> IClock).Elapsed()
                    member _.Sleep(delay, token) =
                        let started = (clock :> IClock).Elapsed()
                        let pending = (clock :> IClock).Sleep(delay, token)
                        deadlines.Enqueue("armed", delay, started)
                        // Record before releasing the engine continuation: later assertions inspect the
                        // decision-time history, never whether an OS callback happened to run yet.
                        Interlocked.Increment(&activeTimers) |> ignore
                        let audited = task {
                            try
                                do! pending
                                deadlines.Enqueue("elapsed", delay, (clock :> IClock).Elapsed())
                                if delay = TimeSpan.FromSeconds 11. then timeoutElapsed.TrySetResult() |> ignore
                            finally
                                Interlocked.Decrement(&activeTimers) |> ignore
                        }
                        delays.Enqueue delay
                        registrations.Release() |> ignore
                        audited }
            let waitTimer expected = task {
                let mutable found = false
                while not found do
                    let! signalled = registrations.WaitAsync(TimeSpan.FromSeconds 10.)
                    Assert.True(signalled, $"timer {expected} was not registered")
                    let mutable delay = TimeSpan.Zero
                    Assert.True(delays.TryDequeue(&delay))
                    found <- delay = expected
            }
            use rescue = new CancellationTokenSource()
            let mutable nativeId = 0
            let started = TaskCompletionSource<ProcessStarted>(TaskCreationOptions.RunContinuationsAsynchronously)
            let mutable rescueKilled = false
            let service = Process.live FileSystem.live Console.live
            let observing =
                { new IProcess with
                    member _.Execute(specification, _) =
                        service.Execute(specification, fun output -> async {
                            match observed.TryGetValue(output.Text.Trim()) with
                            | true, signal -> signal.TrySetResult output |> ignore
                            | _ -> () }) }
            let waitObserved label (pending: Task<'a>) = task {
                try return! pending.WaitAsync(TimeSpan.FromSeconds 10.)
                with error -> return raise (Exception(label, error))
            }
            let running =
                // The engine intentionally stops output callbacks when timeout begins; native signal acknowledgements
                // use a separate watcher, installed before launch. Each HUP creates a fresh acknowledgement
                // after appending, so observing a one-shot HUP cannot acknowledge the ladder's later HUP.
                shText $"hupCount=0; trap 'printf x >> \"{directory}/term\"' TERM; trap 'printf x >> \"{directory}/hup\"; hupCount=$((hupCount + 1)); : > \"{directory}/hup-$hupCount\"' HUP; echo ready; while :; do :; done"
                |> onStarted (fun handle -> Flow.delay (fun () -> nativeId <- handle.ProcessId; started.TrySetResult handle |> ignore; Flow.ok ()))
                |> (if mode.Contains("timeout") then overallTimeout (TimeSpan.FromSeconds 11.) else id)
                |> stopGracefully [ StopRequest.Terminate, TimeSpan.FromSeconds 17.; StopRequest.Hangup, TimeSpan.FromSeconds 23. ]
                |> toFlow |> Flow.withClock notifying |> Flow.withLogSink LogSink.none
                |> Flow.withFiberRegistry registry
                |> fun source -> source.StartAsTask({ Process = observing }, rescue.Token)
            try
                let! ready = observed["ready"].Task.WaitAsync(TimeSpan.FromSeconds 10.)
                Assert.Equal(DateTimeOffset.UnixEpoch, ready.Timestamp)
                let! handle = started.Task.WaitAsync(TimeSpan.FromSeconds 10.)
                let close () =
                    Task.Run(fun () -> handle.Close StopRequest.Terminate |> Flow.withClock (Clock.fromValue DateTimeOffset.MaxValue) |> Flow.run ())
                let timeoutFirst = mode = "timeout" || mode = "timeout-before-close"
                if mode.Contains("timeout") then do! waitTimer (TimeSpan.FromSeconds 11.)
                if timeoutFirst then
                    clock.AdvanceBy(TimeSpan.FromSeconds 11.)
                else
                    let! accepted = (close ()).WaitAsync(TimeSpan.FromSeconds 10.)
                    match accepted with
                    | Exit.Success () -> ()
                    | other -> failwithf "Close was not accepted: %A" other
                do! waitObserved "TERM acknowledgement" termSeen.Task
                do! waitTimer (TimeSpan.FromSeconds 17.)
                if mode = "close-twice" || mode = "timeout-before-close" then
                    // Acceptance must not depend on advancing the active ladder's grace clock.
                    let! accepted = waitObserved "Close acceptance while grace is parked" (close ())
                    Assert.Equal(Exit.Success (), accepted)
                    Assert.False(running.IsCompleted, "Close waited for shutdown instead of acceptance")
                if mode = "signal-during-close" then
                    let! accepted = handle.Signal StopRequest.Hangup |> fun work -> work.StartAsTask(())
                    Assert.Equal(Exit.Success (), accepted)
                    do! waitObserved "one-shot HUP during grace" hupSeen.Task
                    Assert.False(running.IsCompleted)
                if mode = "close-before-timeout" then
                    clock.AdvanceBy(TimeSpan.FromSeconds 11.)
                    do! waitObserved "competing timeout expiry" timeoutElapsed.Task
                    clock.AdvanceBy(TimeSpan.FromSeconds 6.)
                else
                    clock.AdvanceBy(TimeSpan.FromSeconds 17.)
                let ladderHup = if mode = "signal-during-close" then secondHupSeen.Task else hupSeen.Task
                do! waitObserved "ladder HUP acknowledgement" ladderHup
                do! waitTimer (TimeSpan.FromSeconds 23.)
                clock.AdvanceBy(TimeSpan.FromSeconds 22.)
                clock.AdvanceBy(TimeSpan.FromSeconds 1.)
                let! exit = waitObserved "engine completion" running
                match exit with
                | Exit.Failure(Cause.Fail(ProcessError.OverallTimedOut _)) when timeoutFirst -> ()
                | Exit.Failure(Cause.Fail(ProcessError.StageFailed _)) when not timeoutFirst -> ()
                | other -> failwithf "Unexpected ladder result for %s: %A" mode other
                Assert.Equal(0, Volatile.Read(&activeTimers))
                Assert.Equal(0, clock.Sleepers)
                Assert.Equal(0, registry.LiveFiberCount)
                let! afterExit = (close ()).WaitAsync(TimeSpan.FromSeconds 10.)
                Assert.Equal(Exit.Success (), afterExit)
                // Native traps count requests, not watcher deliveries. The engine must not send
                // the initial request again when it enters the shared ladder, or on a second Close.
                Assert.Equal("x", IO.File.ReadAllText(IO.Path.Combine(directory, "term")))
                Assert.Equal((if mode = "signal-during-close" then "xx" else "x"), IO.File.ReadAllText(IO.Path.Combine(directory, "hup")))
                let offset = if timeoutFirst then 11. else 0.
                let expected =
                    [ if mode.Contains("timeout") then
                          "armed", TimeSpan.FromSeconds 11., TimeSpan.Zero
                          "elapsed", TimeSpan.FromSeconds 11., TimeSpan.FromSeconds 11.
                      "armed", TimeSpan.FromSeconds 17., TimeSpan.FromSeconds offset
                      "elapsed", TimeSpan.FromSeconds 17., TimeSpan.FromSeconds(offset + 17.)
                      "armed", TimeSpan.FromSeconds 23., TimeSpan.FromSeconds(offset + 17.)
                      "elapsed", TimeSpan.FromSeconds 23., TimeSpan.FromSeconds(offset + 40.) ]
                Assert.Equal<(string * TimeSpan * TimeSpan) list>(
                    List.sort expected, deadlines.ToArray() |> Array.toList |> List.sort)
                Assert.NotEqual(0, nativeId)
                // Check before cancellation, further clock advancement, or rescue kill. A returned
                // timeout is not enough: the engine must already have killed and reaped the child.
                try
                    use child = Diagnostics.Process.GetProcessById nativeId
                    Assert.True(child.HasExited, "engine returned while the native child was still alive")
                with :? ArgumentException -> () // PID is already gone/reaped
            finally
                rescue.Cancel()
                clock.AdvanceBy(TimeSpan.FromDays 1.)
                // A failing assertion must not strand the busy-loop child behind an unadvanced grace timer.
                if nativeId <> 0 then
                    try
                        use child = Diagnostics.Process.GetProcessById nativeId
                        if not child.HasExited then
                            rescueKilled <- true
                            child.Kill(entireProcessTree = true)
                        Assert.True(child.WaitForExit(5000), "rescue could not reap native child")
                    with :? ArgumentException -> () // already reaped by the engine
                watcher.EnableRaisingEvents <- false
                IO.Directory.Delete(directory, true)
                Assert.False(rescueKilled, "safety-net cleanup had to kill the child; engine did not")
    }

    [<Fact>]
    let ``process timers follow the runTestWith platform clock`` () =
        if not (OperatingSystem.IsWindows()) then
            let platform = TestPlatform.create ()
            let ready = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let env = { Process = Process.live FileSystem.live Console.live }
            let workflow = flow {
                let! running =
                    shText "trap '' TERM; echo ready; exec sleep 30"
                    |> stdoutTo (Output.callback (fun _ -> Flow.delay (fun () -> ready.TrySetResult() |> ignore; Flow.ok ())))
                    |> Process.timeout (TimeSpan.FromSeconds 5.)
                    |> toFlow
                    |> Flow.withLogSink LogSink.none
                    |> Flow.fork
                do! Flow.fromTask (fun _ -> ready.Task)
                platform.Clock.AdvanceBy(TimeSpan.FromSeconds 5.)
                return! Fiber.join running
            }
            match workflow |> Flow.runTestWith platform env with
            | Exit.Failure(Cause.Fail(ProcessError.StageTimedOut _)) -> ()
            | other -> failwithf "Expected the platform clock to drive the process timeout, got %A" other
