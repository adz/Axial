namespace Axial.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Swensen.Unquote
open Xunit

module RunOnTests =
    [<Fact>]
    let ``default adaptive places consistently CPU-heavy work on the thread pool`` () =
        let placement = RunOn.adaptive ()
        let caller = Thread.CurrentThread.ManagedThreadId
        let threads = ConcurrentQueue<int>()
        let operation : Flow<unit, string, unit> =
            Flow.delay (fun () ->
                Thread.SpinWait 100_000
                threads.Enqueue Thread.CurrentThread.ManagedThreadId
                Flow.ok ())
            |> Flow.runOn placement

        for _ in 1..4 do
            test <@ operation.RunSynchronously(()) = Exit.Success() @>
        let observed = threads.ToArray()
        test <@ observed[0..2] |> Array.forall ((=) caller) @>
        test <@ observed[3] <> caller @>

    [<Fact>]
    let ``default adaptive keeps a cold waiting operation inline`` () =
        let placement = RunOn.adaptive ()
        let caller = Thread.CurrentThread.ManagedThreadId
        let threads = ConcurrentQueue<int>()
        let operation : Flow<unit, string, unit> =
            Flow.fromTask (fun _ ->
                threads.Enqueue Thread.CurrentThread.ManagedThreadId
                let completion = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
                ThreadPool.QueueUserWorkItem(fun _ -> completion.SetResult()) |> ignore
                completion.Task)
            |> Flow.runOn placement

        for _ in 1..4 do
            test <@ operation.RunSynchronously(()) = Exit.Success() @>
        test <@ threads.ToArray() |> Array.forall ((=) caller) @>

    [<Fact>]
    let ``default adaptive ignores one slow first sample when later samples are cheap`` () =
        let placement = RunOn.adaptive ()
        let caller = Thread.CurrentThread.ManagedThreadId
        let mutable invocation = 0
        let threads = ConcurrentQueue<int>()
        let operation : Flow<unit, string, unit> =
            Flow.delay (fun () ->
                invocation <- invocation + 1
                if invocation = 1 then TestSupport.occupyThread (TimeSpan.FromMilliseconds 10.)
                threads.Enqueue Thread.CurrentThread.ManagedThreadId
                Flow.ok ())
            |> Flow.runOn placement

        for _ in 1..4 do
            test <@ operation.RunSynchronously(()) = Exit.Success() @>
        test <@ threads.ToArray() |> Array.forall ((=) caller) @>

    [<Fact>]
    let ``adaptive counts synchronously defecting invocation attempts`` () =
        let placement = RunOn.adaptive ()
        let state =
            match placement with
            | RunOn.Adaptive state -> state
            | _ -> failwith "Expected adaptive placement"
        let operation : Flow<unit, string, unit> =
            Flow.delay (fun () ->
                Thread.SpinWait 100_000
                raise (InvalidOperationException "sample"))
            |> Flow.runOn placement

        for _ in 1..3 do
            match operation.RunSynchronously(()) with
            | Exit.Failure(Cause.Die defect) -> test <@ defect.Message = "sample" @>
            | exit -> failwithf "Expected defect, got %A" exit
        test <@ state.Decision = 2 @>

    [<Fact>]
    let ``adaptive sampling is safe when first calls are concurrent`` () =
        let placement = RunOn.adaptiveWith { Threshold = TimeSpan.Zero; Samples = 8 }
        let operation : Flow<unit, string, int> =
            Flow.delay (fun () -> Thread.SpinWait 50_000; Flow.ok Thread.CurrentThread.ManagedThreadId)
            |> Flow.runOn placement
        let callers = [| for _ in 1..8 -> Task.Run(fun () -> operation.RunSynchronously(())) |]
        Task.WhenAll(callers).GetAwaiter().GetResult() |> ignore
        let caller = Thread.CurrentThread.ManagedThreadId
        let next = operation.RunSynchronously(())
        match next with
        | Exit.Success thread -> test <@ thread <> caller @>
        | failure -> failwithf "Expected success, got %A" failure

    [<Fact>]
    let ``runOn ThreadPool lets eight synchronous sequencePar branches start concurrently`` () =
        use allStarted = new CountdownEvent(8)
        use release = new ManualResetEventSlim(false)
        let threadIds = ConcurrentDictionary<int, byte>()

        let branch () : Flow<unit, string, int> =
            Flow.delay (fun () ->
                threadIds.TryAdd(Thread.CurrentThread.ManagedThreadId, 0uy) |> ignore
                allStarted.Signal() |> ignore
                release.Wait(TimeSpan.FromSeconds 20.0) |> ignore
                Flow.ok 1)
            |> Flow.runOn RunOn.ThreadPool

        let workflow = [ for _ in 1..8 -> branch () ] |> Flow.sequencePar
        let running =
            Task.Factory.StartNew(
                (fun () -> workflow.RunSynchronously(())),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)

        let startedConcurrently =
            try
                allStarted.Wait(TimeSpan.FromSeconds 20.0)
            finally
                release.Set()

        let exit = running.WaitAsync(TimeSpan.FromSeconds 20.0).GetAwaiter().GetResult()

        test <@ startedConcurrently @>
        test <@ exit = Exit.Success(List.replicate 8 1) @>
        test <@ threadIds.Count > 1 @>

    [<Fact>]
    let ``unwrapped completed flows stay inline and Inline returns the source unchanged`` () =
        let caller = Thread.CurrentThread.ManagedThreadId
        let source : Flow<unit, string, int> =
            flow {
                let before = Thread.CurrentThread.ManagedThreadId
                let! value = Flow.fromTask (fun _ -> Task.FromResult 1)
                let after = Thread.CurrentThread.ManagedThreadId
                return before + after + value
            }

        let inlineSource = Flow.runOn RunOn.Inline source
        test <@ obj.ReferenceEquals(source, inlineSource) @>
        test <@ source.RunSynchronously(()) = Exit.Success(caller + caller + 1) @>

    [<Fact>]
    let ``a sibling failure interrupts runOn ThreadPool at a checkpoint and zipPar waits for cleanup`` () =
        use started = new ManualResetEventSlim(false)
        let cleaned = ref false
        let slices = ref 0

        let rec cpu () : Flow<unit, string, unit> =
            flow {
                started.Set()
                Thread.SpinWait 100_000
                Interlocked.Increment(&slices.contents) |> ignore
                do! Flow.ensureNotCanceled
                return! cpu ()
            }

        let worker =
            cpu ()
            |> Flow.ensuring (Flow.delay (fun () -> cleaned.Value <- true; Flow.ok ()))
            |> Flow.runOn RunOn.ThreadPool

        let failure = Flow.delay (fun () -> started.Wait(); Flow.fail "boom")
        let exit = Flow.zipPar worker failure |> Flow.runSync ()

        test <@ exit = Exit.Failure(Cause.Fail "boom") @>
        test <@ cleaned.Value @>
        test <@ slices.Value > 0 @>

    [<Fact>]
    let ``an already cancelled runOn does not start its operation`` () =
        use cts = new CancellationTokenSource()
        cts.Cancel()
        let started = ref false

        let exit : Exit<int, string> =
            Flow.delay (fun () -> started.Value <- true; Flow.ok 1)
            |> Flow.runOn RunOn.ThreadPool
            |> Flow.runSyncWithToken () cts.Token

        test <@ exit = Exit.Failure Cause.Interrupt @>
        test <@ not started.Value @>

    [<Fact>]
    let ``cancellation after scheduling prevents the operation from starting`` () =
        let pair = new ConcurrentExclusiveSchedulerPair()
        let scheduler = pair.ExclusiveScheduler
        use occupied = new ManualResetEventSlim(false)
        use releaseScheduler = new ManualResetEventSlim(false)
        use cts = new CancellationTokenSource()
        let started = ref false

        let blocker =
            Task.Factory.StartNew(
                (fun () -> occupied.Set(); releaseScheduler.Wait()),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                scheduler)

        test <@ occupied.Wait(TimeSpan.FromSeconds 10.0) @>

        let running : Task<Exit<int, string>> =
            Flow.delay (fun () -> started.Value <- true; Flow.ok 1)
            |> Flow.runOn (RunOn.Scheduler scheduler)
            |> fun source -> source.StartAsTask((), cancellationToken = cts.Token)

        cts.Cancel()
        releaseScheduler.Set()

        test <@ running.GetAwaiter().GetResult() = Exit.Failure Cause.Interrupt @>
        test <@ not started.Value @>
        blocker.GetAwaiter().GetResult()
        pair.Complete()

    [<Fact>]
    let ``runOn keeps the environment and scoped resource ownership`` () =
        let released = ref false

        let resource : Resource<int, string, int> =
            Resource.create
                (Flow.envWith id)
                (fun _ -> Flow.delay (fun () -> released.Value <- true; Flow.ok ()))

        let workflow =
            flow {
                let! value = Flow.scopeResource resource |> Flow.runOn RunOn.ThreadPool
                return value, released.Value
            }
            |> Flow.scoped

        test <@ workflow.RunSynchronously(42) = Exit.Success(42, false) @>
        test <@ released.Value @>

    [<Fact>]
    let ``an exclusive scheduler stays current after awaits and serialises execution`` () =
        let pair = new ConcurrentExclusiveSchedulerPair()
        let scheduler = pair.ExclusiveScheduler
        let active = ref 0
        let peak = ref 0
        let schedulers = ConcurrentBag<TaskScheduler>()

        let phase () =
            Flow.delay (fun () ->
                let now = Interlocked.Increment(&active.contents)
                let mutable observed = peak.Value
                while now > observed && Interlocked.CompareExchange(&peak.contents, now, observed) <> observed do
                    observed <- peak.Value
                schedulers.Add TaskScheduler.Current
                Thread.SpinWait 250_000
                Interlocked.Decrement(&active.contents) |> ignore
                Flow.ok ())

        let branch () : Flow<unit, string, unit> =
            flow {
                do! phase ()
                do! Flow.fromTask (fun _ -> task { do! Task.Delay 10 })
                do! phase ()
            }
            |> Flow.runOn (RunOn.Scheduler scheduler)

        let exit = Flow.sequencePar [ branch (); branch () ] |> Flow.runSync ()
        pair.Complete()

        test <@ exit = Exit.Success [ (); () ] @>
        test <@ peak.Value = 1 @>
        test <@ schedulers.Count = 4 @>
        test <@ schedulers |> Seq.forall (fun current -> obj.ReferenceEquals(current, scheduler)) @>

    [<Fact>]
    let ``runOn preserves resource ownership under parallel combinators and fork`` () =
        let environment = TestSupport.clockEnv ()

        let resource (released: bool ref) (acquired: ManualResetEventSlim option) =
            Resource.create
                (Flow.delay (fun () ->
                    acquired |> Option.iter (fun gate -> gate.Set())
                    Flow.ok "resource"))
                (fun _ -> Flow.delay (fun () -> released.Value <- true; Flow.ok ()))

        let zipReleased = ref false
        let zipExit =
            flow {
                let! _, _ =
                    Flow.zipPar
                        (Flow.scopeResource (resource zipReleased None) |> Flow.runOn RunOn.ThreadPool)
                        (Flow.ok 2)
                return zipReleased.Value
            }
            |> Flow.scoped
            |> Flow.runSync environment

        let sequenceReleased = ref false
        let sequenceExit =
            flow {
                let! _ =
                    [ Flow.scopeResource (resource sequenceReleased None) |> Flow.runOn RunOn.ThreadPool ]
                    |> Flow.sequencePar
                return sequenceReleased.Value
            }
            |> Flow.scoped
            |> Flow.runSync environment

        let raceWinnerReleased = ref false
        let raceWinnerExit =
            flow {
                let! value =
                    Flow.race
                        (Flow.scopeResource (resource raceWinnerReleased None) |> Flow.runOn RunOn.ThreadPool)
                        Flow.never
                return value, raceWinnerReleased.Value
            }
            |> Flow.scoped
            |> Flow.runSync environment

        let raceLoserReleased = ref false
        use loserAcquired = new ManualResetEventSlim(false)
        use loserReleased = new ManualResetEventSlim(false)
        let raceLoserResource =
            Resource.create
                (Flow.delay (fun () -> loserAcquired.Set(); Flow.ok "resource"))
                (fun _ ->
                    Flow.delay (fun () ->
                        raceLoserReleased.Value <- true
                        loserReleased.Set()
                        Flow.ok ()))
        let raceLoser =
            flow {
                let! _ = Flow.scopeResource raceLoserResource
                do! Flow.never
                return "loser"
            }
            |> Flow.runOn RunOn.ThreadPool
        let raceLoserExit =
            Flow.race
                raceLoser
                (Flow.delay (fun () ->
                    let acquired = loserAcquired.Wait(TimeSpan.FromSeconds 10.0)
                    Flow.ok (if acquired then "winner" else "loser acquisition timed out")))
            |> Flow.runSync environment
        let raceLoserReleasedEventually = loserReleased.Wait(TimeSpan.FromSeconds 10.0)

        let forkReleased = ref false
        let forkExit =
            flow {
                let! fiber =
                    Flow.scopeResource (resource forkReleased None)
                    |> Flow.runOn RunOn.ThreadPool
                    |> Flow.fork
                let! value = Fiber.join fiber
                return value, forkReleased.Value
            }
            |> Flow.runSync environment

        test <@ zipExit = Exit.Success false @>
        test <@ zipReleased.Value @>
        test <@ sequenceExit = Exit.Success false @>
        test <@ sequenceReleased.Value @>
        test <@ raceWinnerExit = Exit.Success("resource", false) @>
        test <@ raceWinnerReleased.Value @>
        test <@ raceLoserExit = Exit.Success "winner" @>
        test <@ raceLoserReleasedEventually @>
        test <@ raceLoserReleased.Value @>
        test <@ forkExit = Exit.Success("resource", true) @>

    [<Fact>]
    let ``runOn restores the caller scheduler and nested exclusive placement completes`` () =
        let pair = new ConcurrentExclusiveSchedulerPair()
        let scheduler = pair.ExclusiveScheduler
        let inside = ref Unchecked.defaultof<TaskScheduler>
        let after = ref Unchecked.defaultof<TaskScheduler>

        let placed value =
            Flow.delay (fun () -> inside.Value <- TaskScheduler.Current; Flow.ok value)
            |> Flow.runOn (RunOn.Scheduler scheduler)

        let workflow : Flow<unit, string, int> =
            flow {
                let! value = placed 1
                after.Value <- TaskScheduler.Current

                let! nested =
                    flow {
                        let! inner = placed 2
                        return inner + 1
                    }
                    |> Flow.runOn (RunOn.Scheduler scheduler)

                return value + nested
            }

        let running = workflow.StartAsTask(())
        let exit = running.WaitAsync(TimeSpan.FromSeconds 10.0).GetAwaiter().GetResult()
        pair.Complete()

        test <@ exit = Exit.Success 4 @>
        test <@ obj.ReferenceEquals(inside.Value, scheduler) @>
        test <@ not (obj.ReferenceEquals(after.Value, scheduler)) @>

    [<Fact>]
    let ``DedicatedThread starts the synchronous part outside the pool`` () =
        let exit : Exit<bool, string> =
            Flow.delay (fun () -> Flow.ok Thread.CurrentThread.IsThreadPoolThread)
            |> Flow.runOn RunOn.DedicatedThread
            |> Flow.runSync ()

        test <@ exit = Exit.Success false @>

    [<Fact>]
    let ``runOn preserves typed failures and defects`` () =
        let typed : Exit<int, string> = Flow.fail "no" |> Flow.runOn RunOn.ThreadPool |> Flow.runSync ()
        let defect : Exit<int, string> =
            Flow.delay (fun () -> raise (InvalidOperationException "broken"))
            |> Flow.runOn RunOn.ThreadPool
            |> Flow.runSync ()

        test <@ typed = Exit.Failure(Cause.Fail "no") @>
        test <@ match defect with Exit.Failure(Cause.Die error) -> error.Message = "broken" | _ -> false @>
