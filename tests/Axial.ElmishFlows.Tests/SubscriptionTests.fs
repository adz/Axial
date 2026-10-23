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
        ElmishRuntime.start { Post = post; OnDefect = defect; HistoryCapacity = 32 } (ClockEnvironment(clock))

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
    let ``manual clock search pipeline dispatches only the latest query`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        use test = ElmishRuntime.test (ClockEnvironment(clock))
        let inbox = Inbox.create 8 test.Runtime
        let searching = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let search query : Flow<ClockEnvironment, string, string> = flow {
            searching.TrySetResult() |> ignore
            do! Flow.sleep (TimeSpan.FromMilliseconds 100.0)
            return $"result-{query}"
        }
        let source =
            Inbox.stream inbox
            |> FlowStream.debounce (TimeSpan.FromMilliseconds 300.0)
            |> FlowStream.switchMapFlow search
            |> FlowStream.map (fun result -> $"found:{result}")
        let subscription = Sub.OfFlowStream.subscribe ["search"] test.Runtime source (fun error -> $"error:{error}")
        use active = TestRuntime.subscribe subscription test |> List.head
        Assert.True(Inbox.post "a" inbox)
        Assert.True(Inbox.post "ab" inbox)
        Assert.True(Inbox.post "abc" inbox)
        // The debounce re-arms per item, so wait until the whole burst has left the inbox, then for its timer.
        // A fixed sleep here raced the stream under full-suite load and let "a" debounce on its own.
        let queued () =
            match (Dequeue.size inbox.Queue).RunSynchronously(()) with
            | Exit.Success count -> count
            | _ -> -1
        let drained = SpinWait.SpinUntil((fun () -> queued () = 0 && clock.Sleepers > 0), 5000)
        Assert.True(drained, $"queued={queued ()} sleepers={clock.Sleepers} size={(Dequeue.size inbox.Queue).RunSynchronously(())}")
        // The debounce's timer cannot fire while the manual clock is still, so let any in-flight stream pull hand
        // the last item to the debounce before advancing. Without this, a full-suite run could advance while the
        // last item was still between the inbox and the debounce and then wait on a search that never started.
        Thread.Sleep(100)
        Assert.True(SpinWait.SpinUntil((fun () -> queued () = 0 && clock.Sleepers > 0), 5000))
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
        let failing : FlowStream<ClockEnvironment, string, int> = FlowStream.unfoldFlow (fun _ -> Flow.fail "bad") ()
        use typed = snd (List.head (Sub.OfFlowStream.subscribe ["typed"] runtime failing (fun _ -> 9))) messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1), 5000))
        Assert.Equal(9, messages.ToArray().[0])
        let broken : FlowStream<ClockEnvironment, string, int> = FlowStream.unfoldFlow (fun _ -> Flow.die (Exception("boom"))) ()
        use defect = snd (List.head (Sub.OfFlowStream.subscribe ["broken"] runtime broken (fun _ -> 0))) messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> defects.Count = 1), 5000))
        Assert.Equal(1, messages.Count)
