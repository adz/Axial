namespace Axial.Elmish.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Elmish
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

    [<Fact>]
    let ``latest replaces a slow command and cancel suppresses delivery`` () =
        let messages = ConcurrentQueue<int>()
        let started = new TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let interrupted = new TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        use owner = runtime (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let slow =
            Flow.fromTask (fun token ->
                started.TrySetResult() |> ignore
                token.Register(fun () -> interrupted.TrySetResult() |> ignore) |> ignore
                Task.Delay(30000, token).ContinueWith(Func<Task, int>(fun completed -> completed.GetAwaiter().GetResult(); 1)))
        for start in Cmd.OfFlow.Latest.perform "key" owner slow id do start messages.Enqueue
        Assert.True(started.Task.Wait(5000))
        for start in Cmd.OfFlow.Latest.perform "key" owner (Flow.succeed 2) id do start messages.Enqueue
        Assert.True(interrupted.Task.Wait(5000))
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1), 5000))
        Assert.Equal(2, messages.ToArray().[0])
        for start in Cmd.OfFlow.Latest.cancel "key" owner do start messages.Enqueue

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
