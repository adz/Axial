namespace Axial.ElmishFlows.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.ElmishFlows
open Xunit

module CommandTests =
    let private runtime post onDefect =
        let clock =
            { new IClock with
                member _.UtcNow() = DateTimeOffset.UtcNow
                member _.Elapsed() = TimeSpan.FromTicks(Environment.TickCount64)
                member _.Sleep(delay, token) = Task.Delay(delay, token) :> Task }
        ElmishRuntime.start
            { Post = post; OnDefect = onDefect; HistoryCapacity = 1024 }
            (ClockEnvironment(clock))

    [<Fact>]
    let ``success and typed failure dispatch through Post`` () =
        let messages = ConcurrentQueue<int>()
        let posts = ConcurrentQueue<int>()
        let options =
            { Post = fun callback -> posts.Enqueue(Thread.CurrentThread.ManagedThreadId); callback ()
              OnDefect = fun defect -> failwith defect.Rendered
              HistoryCapacity = 32 }
        use runtime = runtime options.Post options.OnDefect
        let success = Cmd.OfFlow.either "ok" runtime (Flow.succeed 42) id (fun (_: string) -> -1)
        let failure = Cmd.OfFlow.either "bad" runtime (Flow.fail "bad") (fun (_: int) -> 0) (fun _ -> -1)
        for command in [ success; failure ] do
            for start in command do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 2), 5000))
        Assert.Equal(2, posts.Count)
        Assert.Contains(42, messages)
        Assert.Contains(-1, messages)

    [<Fact>]
    let ``defects reach OnDefect without dispatch`` () =
        let defects = ConcurrentQueue<CommandDefect>()
        let messages = ConcurrentQueue<int>()
        use owner = runtime (fun callback -> callback ()) defects.Enqueue
        let command = Cmd.OfFlow.either "broken" owner (Flow.die (InvalidOperationException("boom"))) id (fun (_: string) -> -1)
        for start in command do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> defects.Count = 1), 5000))
        Assert.Empty(messages)
        Assert.Equal("broken", defects.ToArray().[0].Name)
        Assert.Contains("boom", defects.ToArray().[0].Rendered)

    [<Fact>]
    let ``start runs fire-and-forget without dispatching`` () =
        let ran = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let messages = ConcurrentQueue<int>()
        use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let work : Flow<ClockEnvironment, Never, unit> =
            Flow.fromTask (fun _ -> ran.TrySetResult() |> ignore; Task.FromResult(()))
        for start in Cmd.OfFlow.start "background" owner work do start messages.Enqueue
        Assert.True(ran.Task.Wait(5000))
        Assert.Empty(messages)

    [<Fact>]
    let ``start reports defects to OnDefect without dispatching`` () =
        let defects = ConcurrentQueue<CommandDefect>()
        let messages = ConcurrentQueue<int>()
        use owner = runtime (fun callback -> callback ()) defects.Enqueue
        let work : Flow<ClockEnvironment, Never, unit> = Flow.die (InvalidOperationException("boom"))
        for start in Cmd.OfFlow.start "background" owner work do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> defects.Count = 1), 5000))
        Assert.Empty(messages)
        Assert.Equal("background", defects.ToArray().[0].Name)
        Assert.Contains("boom", defects.ToArray().[0].Rendered)

    [<Fact>]
    let ``start runs a named fiber in the runtime registry`` () =
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let messages = ConcurrentQueue<int>()
        use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let gate =
            match App.run owner.Environment (Deferred.make<ClockEnvironment, Never, unit> ()) |> Async.RunSynchronously with
            | Exit.Success value -> value
            | _ -> failwith "Could not create gate"
        let work : Flow<ClockEnvironment, Never, unit> =
            flow {
                do! Flow.fromTask (fun _ -> started.TrySetResult() |> ignore; Task.FromResult(()))
                do! Deferred.await gate
            }
        for start in Cmd.OfFlow.start "background" owner work do start messages.Enqueue
        Assert.True(started.Task.Wait(5000))
        Assert.True(SpinWait.SpinUntil((fun () ->
            owner.Registry.Snapshot() |> List.exists (fun dump -> dump.Name = Some "background")), 5000))
        Assert.Empty(messages)
        Assert.True(Deferred.succeedNow () gate)

    [<Fact>]
    let ``latest replaces a slow command and cancel suppresses delivery`` () =
        let messages = ConcurrentQueue<int>()
        let started = new TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let interrupted = new TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let gate =
            match App.run owner.Environment (Deferred.make<ClockEnvironment, Never, int> ()) |> Async.RunSynchronously with
            | Exit.Success value -> value
            | _ -> failwith "Could not create gate"
        let slow =
            flow {
                do! Flow.scopeFinalizer (Flow.fromTask (fun _ -> interrupted.TrySetResult() |> ignore; Task.FromResult(())))
                do! Flow.fromTask (fun _ -> started.TrySetResult() |> ignore; Task.FromResult(()))
                return! Deferred.await gate
            }
        for start in Cmd.OfFlow.Latest.perform "key" owner slow id do start messages.Enqueue
        Assert.True(started.Task.Wait(5000))
        for start in Cmd.OfFlow.Latest.perform "key" owner (Flow.succeed 2) id do start messages.Enqueue
        Assert.True(interrupted.Task.Wait(5000))
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1), 5000))
        Assert.Equal(2, messages.ToArray().[0])

    [<Fact>]
    let ``latest cancel interrupts a running command without dispatch`` () =
        let messages = ConcurrentQueue<int>()
        let started = new TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let released = new TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let gate =
            match App.run owner.Environment (Deferred.make<ClockEnvironment, Never, int> ()) |> Async.RunSynchronously with
            | Exit.Success value -> value
            | _ -> failwith "Could not create gate"
        let work = flow {
            do! Flow.scopeFinalizer (Flow.fromTask (fun _ -> released.TrySetResult() |> ignore; Task.FromResult(())))
            do! Flow.fromTask (fun _ -> started.TrySetResult() |> ignore; Task.FromResult(()))
            return! Deferred.await gate
        }
        for start in Cmd.OfFlow.Latest.perform "k" owner work id do start messages.Enqueue
        Assert.True(started.Task.Wait(5000))
        for start in Cmd.OfFlow.Latest.cancel "k" owner do start messages.Enqueue
        Assert.True(released.Task.Wait(5000))
        Assert.Empty(messages)

    [<Fact>]
    let ``stop interrupts a command and runs its release`` () =
        let messages = ConcurrentQueue<int>()
        let started = new TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let released = new TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let work =
            flow {
                do! Flow.scopeFinalizer (Flow.fromTask (fun _ -> released.TrySetResult() |> ignore; Task.FromResult(())))
                do! Flow.fromTask (fun token -> started.TrySetResult() |> ignore; Task.Delay(30000, token).ContinueWith(Func<Task, unit>(fun completed -> completed.GetAwaiter().GetResult())))
                return 1
            }
        for start in Cmd.OfFlow.perform "waiting" owner work id do start messages.Enqueue
        Assert.True(started.Task.Wait(5000))
        Assert.True(owner.StopAndWait(TimeSpan.FromSeconds 5.0))
        Assert.True(released.Task.Wait(5000))
        Assert.Empty(messages)

    [<Fact>]
    let ``stop on an unpumped single-thread context releases command`` () =
        let previous = SynchronizationContext.Current
        let callbacks = ConcurrentQueue<SendOrPostCallback * obj>()
        let context =
            { new SynchronizationContext() with
                override _.Post(callback, state) = callbacks.Enqueue(callback, state) }
        SynchronizationContext.SetSynchronizationContext(context)
        try
            let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let released = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
            let work = flow {
                do! Flow.scopeFinalizer (Flow.fromTask (fun _ -> released.TrySetResult() |> ignore; Task.FromResult(())))
                do! Flow.fromTask (fun token ->
                    started.TrySetResult() |> ignore
                    Task.Delay(30000, token).ContinueWith(Func<Task, unit>(fun completed -> completed.GetAwaiter().GetResult())))
                return 1
            }
            for start in Cmd.OfFlow.perform "context" owner work id do start ignore
            Assert.True(started.Task.Wait(5000))
            Assert.True(owner.StopAndWait(TimeSpan.FromSeconds 5.0))
            Assert.True(released.Task.Wait(5000))
        finally
            SynchronizationContext.SetSynchronizationContext(previous)

    [<Fact>]
    let ``command first step runs off the caller thread`` () =
        let caller = Thread.CurrentThread.ManagedThreadId
        let observed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
        let messages = ConcurrentQueue<int>()
        use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let work = Flow.delay (fun () -> observed.TrySetResult(Thread.CurrentThread.ManagedThreadId) |> ignore; Flow.succeed 1)
        for start in Cmd.OfFlow.perform "thread" owner work id do start messages.Enqueue
        Assert.True(observed.Task.Wait(5000))
        Assert.NotEqual(caller, observed.Task.Result)
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1), 5000))

    [<Fact>]
    let ``commands started after stop do not dispatch`` () =
        let messages = ConcurrentQueue<int>()
        use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        Assert.True(owner.StopAndWait(TimeSpan.FromSeconds 5.0))
        for start in Cmd.OfFlow.perform "late" owner (Flow.succeed 1) id do start messages.Enqueue
        Assert.Empty(messages)

    [<Fact>]
    let ``release defect during stop is reported despite interruption`` () =
        let defects = ConcurrentQueue<CommandDefect>()
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        use owner = runtime (fun callback -> callback ()) defects.Enqueue
        let work = flow {
            do! Flow.scopeFinalizer (Flow.die (Exception("release failed")))
            do! Flow.fromTask (fun token ->
                started.TrySetResult() |> ignore
                Task.Delay(30000, token).ContinueWith(Func<Task, unit>(fun completed -> completed.GetAwaiter().GetResult())))
            return 1
        }
        for start in Cmd.OfFlow.perform "release" owner work id do start ignore
        Assert.True(started.Task.Wait(5000))
        Assert.True(owner.StopAndWait(TimeSpan.FromSeconds 5.0))
        Assert.True(SpinWait.SpinUntil((fun () -> defects.Count = 1), 5000))
        Assert.Contains("release failed", defects.ToArray().[0].Rendered)

    [<Fact>]
    let ``host callback failures are reported and do not stop later commands`` () =
        let defects = ConcurrentQueue<CommandDefect>()
        let messages = ConcurrentQueue<int>()
        use owner = runtime (fun callback -> callback ()) defects.Enqueue
        for start in Cmd.OfFlow.perform "mapper" owner (Flow.succeed 1) (fun _ -> failwith "mapper exploded") do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> defects.Count = 1), 5000))
        Assert.Contains("mapper exploded", defects.ToArray().[0].Rendered)
        for start in Cmd.OfFlow.perform "next" owner (Flow.succeed 2) id do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1), 5000))

    [<Fact>]
    let ``throwing diagnostic sink does not break subsequent commands`` () =
        let messages = ConcurrentQueue<int>()
        use owner = runtime (fun callback -> callback ()) (fun _ -> failwith "sink exploded")
        for start in Cmd.OfFlow.perform "bad" owner (Flow.die (Exception("bad"))) id do start messages.Enqueue
        for start in Cmd.OfFlow.perform "good" owner (Flow.succeed 2) id do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1), 5000))
        Assert.Equal(2, messages.ToArray().[0])

    [<Fact>]
    let ``completed latest keys are removed`` () =
        let messages = ConcurrentQueue<int>()
        use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        for i in 1 .. 1000 do
            for start in Cmd.OfFlow.Latest.perform (string i) owner (Flow.succeed i) id do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1000), 30000))
        Assert.Equal(0, owner.LatestCount)

    [<Fact>]
    let ``concurrent commands each pass through Post once`` () =
        let messages = ConcurrentQueue<int>()
        let mutable postCount = 0
        use owner = runtime (fun callback -> Interlocked.Increment(&postCount) |> ignore; callback ()) (fun defect -> failwith defect.Rendered)
        for i in 1 .. 1000 do
            let command = Cmd.OfFlow.perform "item" owner (Flow.succeed i) id
            for start in command do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1000), 30000))
        Assert.Equal(1000, postCount)
        Assert.Equal(1000, messages.ToArray() |> Array.distinct |> Array.length)
