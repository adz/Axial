namespace Axial.ElmishFlows.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.ElmishFlows
open Xunit

module StreamCommandTests =
    let private liveClock () =
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow
            member _.Elapsed() = TimeSpan.FromTicks(Environment.TickCount64)
            member _.Sleep(delay, token) = Task.Delay(delay, token) }

    let private owner post onDefect =
        ElmishRuntime.start
            { Post = post; OnDefect = onDefect; HistoryCapacity = 64; Clock = Some(liveClock ()); LogSink = None }
            ()

    let private gate () : Deferred<Never, unit> =
        Deferred.make () |> Flow.run () |> Exit.toResult |> Result.defaultWith Never.absurd

    let private signal () = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

    let private mark (source: TaskCompletionSource<unit>) : Flow<'env, 'error, unit> =
        Flow.delay (fun () -> source.TrySetResult() |> ignore; Flow.ok ())

    [<Fact>]
    let ``latest stream dispatches every element in order through Post`` () =
        let messages = ConcurrentQueue<int>()
        let mutable posts = 0
        use runtime = owner (fun callback -> Interlocked.Increment(&posts) |> ignore; callback ()) (fun defect -> failwith defect.Rendered)
        let numbers : FlowStream<unit, string, int> = FlowStream.fromSeq [ 1 .. 50 ]
        let command = Cmd.OfFlowStream.latest "numbers" runtime numbers id (fun _ -> -1)
        for start in command do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 50), 5000))
        Assert.Equal<int>([| 1 .. 50 |], messages.ToArray())
        Assert.True(SpinWait.SpinUntil((fun () -> runtime.LatestCount = 0), 5000))
        // 50 elements plus the completion that releases the key, all on the host's dispatch path.
        Assert.Equal(51, Volatile.Read(&posts))

    [<Fact>]
    let ``a newer stream for the same key interrupts the older one`` () =
        let messages = ConcurrentQueue<int>()
        let firstStarted = signal ()
        let firstReleased = signal ()
        use runtime = owner (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let hold = gate ()
        let older : FlowStream<unit, string, int> =
            FlowStream.singleton 1
            |> FlowStream.append (FlowStream.fromFlow (flow {
                do! Flow.scopeFinalizer (mark firstReleased)
                do! mark firstStarted
                do! Deferred.await hold |> Flow.widenError
                return 2
            }))
        for start in Cmd.OfFlowStream.latest "search" runtime older id (fun _ -> -1) do start messages.Enqueue
        Assert.True(firstStarted.Task.Wait(5000))
        let newer : FlowStream<unit, string, int> = FlowStream.fromSeq [ 10; 11 ]
        for start in Cmd.OfFlowStream.latest "search" runtime newer id (fun _ -> -1) do start messages.Enqueue
        Assert.True(firstReleased.Task.Wait(5000))
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 3), 5000))
        Deferred.succeedNow () hold |> ignore
        Assert.True(SpinWait.SpinUntil((fun () -> runtime.LatestCount = 0), 5000))
        Assert.Equal<int>([| 1; 10; 11 |], messages.ToArray())

    [<Fact>]
    let ``elements of a replaced stream still waiting in Post are dropped`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        use test = ElmishRuntime.test clock ()
        let older : FlowStream<unit, string, string> = FlowStream.fromSeq [ "a1"; "a2" ]
        TestRuntime.run (Cmd.OfFlowStream.latest "key" test.Runtime older id (fun error -> "error:" + error)) test
        // Wait until the older stream has posted its first element, without delivering it.
        Assert.True(SpinWait.SpinUntil((fun () -> test.QueuedDeliveries = 1), 5000))
        let newer : FlowStream<unit, string, string> = FlowStream.fromSeq [ "b1" ]
        TestRuntime.run (Cmd.OfFlowStream.latest "key" test.Runtime newer id (fun error -> "error:" + error)) test
        Assert.True(TestRuntime.settle (TimeSpan.FromSeconds 5.0) test)
        Assert.Equal<obj list>([ box "b1" ], TestRuntime.dispatched test)
        Assert.Equal(0, test.Runtime.LatestCount)

    [<Fact>]
    let ``different keys stream independently`` () =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        use test = ElmishRuntime.test clock ()
        let left : FlowStream<unit, string, string> = FlowStream.fromSeq [ "l1"; "l2" ]
        let right : FlowStream<unit, string, string> = FlowStream.fromSeq [ "r1" ]
        TestRuntime.run (Cmd.OfFlowStream.latest "left" test.Runtime left id id) test
        TestRuntime.run (Cmd.OfFlowStream.latest "right" test.Runtime right id id) test
        Assert.True(TestRuntime.settle (TimeSpan.FromSeconds 5.0) test)
        let dispatched = TestRuntime.dispatched test |> List.map unbox<string>
        Assert.Equal<string list>([ "l1"; "l2"; "r1" ], List.sort dispatched)
        Assert.Equal<string list>([ "l1"; "l2" ], dispatched |> List.filter (fun value -> value.StartsWith "l"))

    [<Fact>]
    let ``a typed failure dispatches after the elements before it`` () =
        let messages = ConcurrentQueue<string>()
        use runtime = owner (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let failing : FlowStream<unit, string, string> =
            FlowStream.fromSeq [ "one"; "two" ]
            |> FlowStream.append (FlowStream.fromFlow (Flow.fail "broken"))
        for start in Cmd.OfFlowStream.latest "failing" runtime failing id (fun error -> "error:" + error) do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 3), 5000))
        Assert.Equal<string>([| "one"; "two"; "error:broken" |], messages.ToArray())
        Assert.True(SpinWait.SpinUntil((fun () -> runtime.LatestCount = 0), 5000))

    [<Fact>]
    let ``a defect reaches OnDefect and dispatches nothing more`` () =
        let messages = ConcurrentQueue<string>()
        let defects = ConcurrentQueue<CommandDefect>()
        use runtime = owner (fun callback -> callback ()) defects.Enqueue
        let broken : FlowStream<unit, string, string> =
            FlowStream.singleton "first"
            |> FlowStream.append (FlowStream.fromFlow (Flow.die (InvalidOperationException "stream exploded")))
        for start in Cmd.OfFlowStream.latest "broken" runtime broken id (fun error -> "error:" + error) do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> defects.Count = 1), 5000))
        Assert.Equal("broken", defects.ToArray().[0].Name)
        Assert.Contains("stream exploded", defects.ToArray().[0].Rendered)
        Assert.True(SpinWait.SpinUntil((fun () -> runtime.LatestCount = 0), 5000))
        Assert.Equal<string>([| "first" |], messages.ToArray())

    [<Fact>]
    let ``a throwing element mapper is reported and later elements still dispatch`` () =
        let messages = ConcurrentQueue<int>()
        let defects = ConcurrentQueue<CommandDefect>()
        use runtime = owner (fun callback -> callback ()) defects.Enqueue
        let numbers : FlowStream<unit, string, int> = FlowStream.fromSeq [ 1; 2; 3 ]
        let toMessage value = if value = 2 then failwith "mapper exploded" else value
        for start in Cmd.OfFlowStream.latest "mapper" runtime numbers toMessage (fun _ -> -1) do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 2 && defects.Count = 1), 5000))
        Assert.Equal<int>([| 1; 3 |], messages.ToArray())
        Assert.Contains("mapper exploded", defects.ToArray().[0].Rendered)

    [<Fact>]
    let ``Latest cancel interrupts a running stream`` () =
        let messages = ConcurrentQueue<int>()
        let released = signal ()
        let started = signal ()
        use runtime = owner (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        let endless : FlowStream<unit, string, int> =
            FlowStream.fromFlow (flow {
                do! Flow.scopeFinalizer (mark released)
                do! mark started
                return! Flow.never
            })
        for start in Cmd.OfFlowStream.latest "endless" runtime endless id (fun _ -> -1) do start messages.Enqueue
        Assert.True(started.Task.Wait(5000))
        for start in Cmd.OfFlow.Latest.cancel "endless" runtime do start messages.Enqueue
        Assert.True(released.Task.Wait(5000))
        Assert.Equal(0, runtime.LatestCount)
        Assert.Empty(messages)

    [<Fact>]
    let ``stopping the runtime interrupts the stream and drops undelivered elements`` () =
        let deliveries = ConcurrentQueue<unit -> unit>()
        let messages = ConcurrentQueue<int>()
        use runtime = owner deliveries.Enqueue (fun defect -> failwith defect.Rendered)
        let endless : FlowStream<unit, string, int> =
            FlowStream.singleton 1 |> FlowStream.append (FlowStream.fromFlow Flow.never)
        for start in Cmd.OfFlowStream.latest "endless" runtime endless id (fun _ -> -1) do start messages.Enqueue
        // The stream waits for its first element to reach update, so stop finds it suspended on that delivery.
        Assert.True(SpinWait.SpinUntil((fun () -> deliveries.Count = 1), 5000))
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        let mutable callback = Unchecked.defaultof<unit -> unit>
        while deliveries.TryDequeue(&callback) do callback ()
        Assert.Empty(messages)
        Assert.Equal(0, runtime.Registry.LiveFiberCount)

    [<Fact>]
    let ``a stream started after stop dispatches nothing`` () =
        let messages = ConcurrentQueue<int>()
        use runtime = owner (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        let numbers : FlowStream<unit, string, int> = FlowStream.fromSeq [ 1; 2 ]
        for start in Cmd.OfFlowStream.latest "late" runtime numbers id (fun _ -> -1) do start messages.Enqueue
        Assert.Equal(0, runtime.Pending)
        Assert.Empty(messages)
        Assert.Equal(0, runtime.LatestCount)

    [<Fact>]
    let ``a stream keeps at most one element waiting in Post and delivers every element`` () =
        let deliveries = ConcurrentQueue<unit -> unit>()
        let messages = ConcurrentQueue<int>()
        let mutable mostWaiting = 0
        let post (callback: unit -> unit) =
            deliveries.Enqueue callback
            let waiting = deliveries.Count
            let mutable seen = Volatile.Read(&mostWaiting)
            while waiting > seen && Interlocked.CompareExchange(&mostWaiting, waiting, seen) <> seen do
                seen <- Volatile.Read(&mostWaiting)
        use runtime = owner post (fun defect -> failwith defect.Rendered)
        let numbers : FlowStream<unit, string, int> = FlowStream.fromSeq [ 1 .. 200 ]
        for start in Cmd.OfFlowStream.latest "fast" runtime numbers id (fun _ -> -1) do start messages.Enqueue
        let watch = Diagnostics.Stopwatch.StartNew()
        while runtime.LatestCount > 0 && watch.ElapsedMilliseconds < 10000L do
            // A slow UI thread: the stream must wait for update instead of flooding the dispatcher.
            Thread.Sleep 1
            let mutable callback = Unchecked.defaultof<unit -> unit>
            if deliveries.TryDequeue(&callback) then callback ()
        Assert.Equal(0, runtime.LatestCount)
        Assert.Equal<int>([| 1 .. 200 |], messages.ToArray())
        Assert.Equal(1, Volatile.Read(&mostWaiting))

    [<Fact>]
    let ``a typed failure still waiting in Post when the runtime stops is dropped`` () =
        let deliveries = ConcurrentQueue<unit -> unit>()
        let messages = ConcurrentQueue<string>()
        use runtime = owner deliveries.Enqueue (fun defect -> failwith defect.Rendered)
        let failing : FlowStream<unit, string, string> = FlowStream.fromFlow (Flow.fail "broken")
        for start in Cmd.OfFlowStream.latest "failing" runtime failing id (fun error -> "error:" + error) do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> deliveries.Count = 1), 5000))
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        let mutable callback = Unchecked.defaultof<unit -> unit>
        while deliveries.TryDequeue(&callback) do callback ()
        Assert.Empty(messages)

    [<Fact>]
    let ``a Latest command result still waiting in Post when the runtime stops is dropped`` () =
        let deliveries = ConcurrentQueue<unit -> unit>()
        let messages = ConcurrentQueue<int>()
        use runtime = owner deliveries.Enqueue (fun defect -> failwith defect.Rendered)
        for start in Cmd.OfFlow.Latest.perform "load" runtime (Flow.succeed 1) id do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> deliveries.Count = 1), 5000))
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        let mutable callback = Unchecked.defaultof<unit -> unit>
        while deliveries.TryDequeue(&callback) do callback ()
        Assert.Empty(messages)
