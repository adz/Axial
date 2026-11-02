namespace Axial.ElmishFlows.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.ElmishFlows
open Xunit

module SubscriptionTests =
    let private owner post defect =
        let clock =
            { new IClock with
                member _.UtcNow() = DateTimeOffset.UtcNow
                member _.Elapsed() = TimeSpan.FromTicks(Environment.TickCount64)
                member _.Sleep(delay, token) = Task.Delay(delay, token) }
        ElmishRuntime.start { Post = post; OnDefect = defect; HistoryCapacity = 32; Clock = Some clock; LogSink = None } ()

    [<Fact>]
    let ``inbox drops full offers and counts them`` () =
        use runtime = owner (fun callback -> callback ()) ignore
        let inbox = Inbox.create 1 runtime
        Assert.True(Inbox.post 1 inbox)
        Assert.False(Inbox.post 2 inbox)
        Assert.Equal(1L, Inbox.dropped inbox)

    [<Fact>]
    let ``subscription delivers ordered messages through Post and dispose releases resource`` () =
        let messages = ConcurrentQueue<int>()
        let mutable posts = 0
        let released = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        use runtime = owner (fun callback -> Interlocked.Increment(&posts) |> ignore; callback ()) (fun defect -> failwith defect.Rendered)
        let inbox = Inbox.create 8 runtime
        let source =
            Inbox.stream inbox
            |> FlowStream.mapFlow (fun value -> flow {
                // axial-allow-AXG005: wrapped operation has no cancellation-token overload
                do! Flow.scopeFinalizer (Flow.fromTask (fun _ -> released.TrySetResult() |> ignore; Task.FromResult(())))
                return value
            })
        let subscription = Sub.OfFlowStream.subscribe ["values"] runtime source (fun (_: string) -> -1)
        use active = snd (List.head subscription) messages.Enqueue
        Assert.True(Inbox.post 1 inbox)
        Assert.True(Inbox.post 2 inbox)
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 2), 5000))
        Assert.Equal<int>([| 1; 2 |], messages.ToArray())
        Assert.Equal(2, posts)
        // Dispose only requests the interruption (it runs on the UI thread and must not block); the release follows.
        active.Dispose()
        Assert.True(released.Task.Wait(TimeSpan.FromSeconds 5.0))

    [<Fact>]
    let ``dispose returns while interrupted subscription finalizer is still running`` () =
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let finalizing = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let finished = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        use runtime = owner (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let source : FlowStream<unit, string, int> =
            FlowStream.repeatFlow (flow {
                do! Flow.scopeFinalizer (Flow.fromTask (fun _ -> task {
                    finalizing.TrySetResult() |> ignore
                    do! release.Task
                    finished.TrySetResult() |> ignore
                }))
                started.TrySetResult() |> ignore
                do! Flow.sleep (TimeSpan.FromDays 1.0)
                return 1
            })
        use active = snd (List.head (Sub.OfFlowStream.subscribe ["gated-cleanup"] runtime source (fun _ -> -1))) ignore
        Assert.True(started.Task.Wait(TimeSpan.FromSeconds 5.0))
        try
            let disposing = Task.Run(fun () -> active.Dispose())
            Assert.True(disposing.Wait(TimeSpan.FromSeconds 5.0), "Dispose blocked on cleanup")
            Assert.True(finalizing.Task.Wait(TimeSpan.FromSeconds 5.0), "Dispose did not interrupt the started fiber")
            Assert.False(finished.Task.IsCompleted)
        finally
            release.TrySetResult() |> ignore
        Assert.True(finished.Task.Wait(TimeSpan.FromSeconds 5.0))

    [<Fact>]
    let ``immediate disposal interrupts a subscription once it starts`` () =
        use runtime = owner (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        for index in 1 .. 50 do
            let inbox = Inbox.create 1 runtime
            let subscription = Sub.OfFlowStream.subscribe [string index] runtime (Inbox.stream inbox) (fun (_: string) -> -1)
            let active = snd (List.head subscription) (fun _ -> failwith "disposed subscription dispatched")
            active.Dispose()
        Assert.True(SpinWait.SpinUntil((fun () -> runtime.Pending = 0), 5000), "Disposed subscription fibers did not settle")

    [<Fact>]
    let ``manual clock search pipeline dispatches only the latest query`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        use test = ElmishRuntime.test clock ()
        let inbox = Inbox.create 8 test.Runtime
        let searching = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let search query : Flow<unit, string, string> = flow {
            searching.TrySetResult() |> ignore
            do! Flow.sleep (TimeSpan.FromMilliseconds 100.0)
            return $"result-{query}"
        }
        // Inbox.stream, with each pull counted: the fourth pull starts only after the third query was handed on.
        let pulls = ref 0
        let fourthPull = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let source =
            FlowStream.repeatFlow (Flow.delay (fun () ->
                if Interlocked.Increment(&pulls.contents) = 4 then fourthPull.TrySetResult() |> ignore
                Dequeue.take inbox.Queue))
            |> FlowStream.debounce (TimeSpan.FromMilliseconds 300.0)
            |> FlowStream.switchMapFlow search
            |> FlowStream.map (fun result -> $"found:{result}")
        let subscription = Sub.OfFlowStream.subscribe ["search"] test.Runtime source (fun error -> $"error:{error}")
        use active = TestRuntime.subscribe subscription test |> List.head
        Assert.True(Inbox.post "a" inbox)
        Assert.True(Inbox.post "ab" inbox)
        Assert.True(Inbox.post "abc" inbox)
        // The debounce re-arms per item. The fourth pull proves the whole burst was handed to the debounce, and the
        // armed timer proves it is now waiting out the quiet period for the last query.
        Assert.True(fourthPull.Task.Wait(5000))
        Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers > 0), 5000))
        clock.AdvanceBy(TimeSpan.FromMilliseconds 300.0)
        Assert.True(searching.Task.Wait(5000))
        Assert.True(SpinWait.SpinUntil((fun () -> clock.Sleepers > 0), 5000))
        clock.AdvanceBy(TimeSpan.FromMilliseconds 100.0)
        Assert.True(SpinWait.SpinUntil((fun () ->
            TestRuntime.drain test
            not (List.isEmpty (TestRuntime.dispatched test))), 5000))
        active.Dispose()
        Assert.True(TestRuntime.settle (TimeSpan.FromSeconds 5.0) test)
        Assert.Equal<obj list>([box "found:result-abc"], TestRuntime.dispatched test)

    [<Fact>]
    let ``typed failure dispatches and defect reports without messages`` () =
        let messages = ConcurrentQueue<int>()
        let defects = ConcurrentQueue<CommandDefect>()
        use runtime = owner (fun callback -> callback ()) defects.Enqueue
        let failing : FlowStream<unit, string, int> = FlowStream.unfoldFlow (fun _ -> Flow.fail "bad") ()
        use typed = snd (List.head (Sub.OfFlowStream.subscribe ["typed"] runtime failing (fun _ -> 9))) messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1), 5000))
        Assert.Equal(9, messages.ToArray().[0])
        let broken : FlowStream<unit, string, int> = FlowStream.unfoldFlow (fun _ -> Flow.die (Exception("boom"))) ()
        use defect = snd (List.head (Sub.OfFlowStream.subscribe ["broken"] runtime broken (fun _ -> 0))) messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> defects.Count = 1), 5000))
        Assert.Equal(1, messages.Count)
