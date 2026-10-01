namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowStreamTests =
    [<Fact>]
    let ``FlowStream: consumes sequence correctly`` () =
        let mutable sum = 0
        let stream = FlowStream.fromSeq [1; 2; 3; 4; 5]
        let workflow = 
            stream 
            |> FlowStream.map (fun v -> v * 2)
            |> FlowStream.runForEach (fun v -> sum <- sum + v)

        let result = Flow.runSync (TestSupport.clockEnv ()) workflow
        test <@ result = Exit.Success () @>
        test <@ sum = 30 @>

    [<Fact>]
    let ``FlowStream: transforms bounds and collects without leaving Flow`` () =
        let result =
            FlowStream.fromSeq [ 1..8 ]
            |> FlowStream.filter (fun value -> value % 2 = 0)
            |> FlowStream.map (fun value -> value * 10)
            |> FlowStream.skip 1
            |> FlowStream.take 2
            |> FlowStream.runCollect
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ result = Exit.Success [ 40; 60 ] @>

    [<Fact>]
    let ``FlowStream: effectful unfold map tap and fold preserve typed effects`` () =
        let seen = ResizeArray<int>()
        let stream =
            FlowStream.unfoldFlow (fun value -> Flow.ok (if value > 3 then None else Some(value, value + 1))) 1
            |> FlowStream.mapFlow (fun value -> Flow.ok (value * 2))
            |> FlowStream.tapFlow (fun value -> flow { seen.Add value })

        let result = stream |> FlowStream.runFold (+) 0 |> Flow.runSync (TestSupport.clockEnv ())
        test <@ result = Exit.Success 12 @>
        test <@ seen |> Seq.toList = [ 2; 4; 6 ] @>

    [<Fact>]
    let ``FlowStream: chunked retains bounded non-empty batches`` () =
        let result =
            FlowStream.fromSeq [ 1..7 ]
            |> FlowStream.chunkBySize 3
            |> FlowStream.runCollect
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ result = Exit.Success [ [ 1; 2; 3 ]; [ 4; 5; 6 ]; [ 7 ] ] @>
        raises<ArgumentException> <@ FlowStream.fromSeq [ 1 ] |> FlowStream.chunkBySize 0 |> ignore @>

    [<Fact>]
    let ``FlowStream: strict parallel batches preserve order and bound`` () =
        let mutable active = 0
        let mutable maximum = 0

        let mapper value =
            Flow.fromTask (fun cancellationToken -> task {
                let current = Interlocked.Increment(&active)
                let mutable observed = Volatile.Read(&maximum)
                while current > observed && Interlocked.CompareExchange(&maximum, current, observed) <> observed do
                    observed <- Volatile.Read(&maximum)
                try
                    do! Task.Delay(10 + (5 - value % 5) * 5, cancellationToken)
                    return value * 10
                finally
                    Interlocked.Decrement(&active) |> ignore
            })

        let result =
            FlowStream.fromSeq [ 1..12 ]
            |> FlowStream.chunkBySize 3
            |> FlowStream.mapFlow (List.map mapper >> Flow.sequencePar)
            |> FlowStream.collect FlowStream.fromSeq
            |> FlowStream.runCollect
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ result = Exit.Success [ for value in 1..12 -> value * 10 ] @>
        test <@ maximum = 3 @>
        raises<ArgumentException> <@ Parallelism.bounded 0 |> ignore @>

    [<Fact>]
    let ``FlowStream: strict parallel batches stop before the next batch on failure`` () =
        let started = ResizeArray<int>()
        let mapper value = flow {
            lock started (fun () -> started.Add value)
            if value = 2 then return! Flow.fail "failed"
            return value
        }

        let result =
            FlowStream.fromSeq [ 1..9 ]
            |> FlowStream.chunkBySize 3
            |> FlowStream.mapFlow (List.map mapper >> Flow.sequencePar)
            |> FlowStream.collect FlowStream.fromSeq
            |> FlowStream.runCollect
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ result = Exit.Failure(Cause.Fail "failed") @>
        test <@ started |> Seq.forall (fun value -> value <= 3) @>

    [<Fact>]
    let ``FlowStream: parallel map continuously replenishes its ordered window`` () =
        let mutable active = 0
        let mutable maximum = 0
        let started = ResizeArray<int>()

        let mapper value =
            Flow.fromTask (fun cancellationToken -> task {
                lock started (fun () -> started.Add value)
                let current = Interlocked.Increment(&active)
                let mutable observed = Volatile.Read(&maximum)
                while current > observed && Interlocked.CompareExchange(&maximum, current, observed) <> observed do
                    observed <- Volatile.Read(&maximum)
                try
                    do! Task.Delay(10 + (5 - value % 5) * 5, cancellationToken)
                    return value * 10
                finally
                    Interlocked.Decrement(&active) |> ignore
            })

        let result =
            FlowStream.fromSeq [ 1..12 ]
            |> FlowStream.mapFlowPar (Parallelism.bounded 3) mapper
            |> FlowStream.runCollect
            |> Flow.runSync (TestSupport.clockEnv ())

        match result with
        | Exit.Success values -> test <@ List.sort values = [ for value in 1..12 -> value * 10 ] @>
        | other -> failwith $"Unexpected result: {other}"
        test <@ maximum = 3 @>
        test <@ started.Count = 12 @>

    [<Fact>]
    let ``FlowStream: parallel map reports later failure without waiting for earlier work`` () =
        let mutable active = 0
        let mapper value =
            if value = 2 then Flow.fail "failed"
            else
                Flow.fromTask (fun cancellationToken -> task {
                    Interlocked.Increment(&active) |> ignore
                    try
                        do! Task.Delay(Timeout.Infinite, cancellationToken)
                        return value
                    finally
                        Interlocked.Decrement(&active) |> ignore
                })

        let result =
            FlowStream.fromSeq [ 1; 2 ]
            |> FlowStream.mapFlowPar (Parallelism.bounded 2) mapper
            |> FlowStream.runCollect
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ result = Exit.Failure(Cause.Fail "failed") @>
        test <@ active = 0 @>

    [<Fact>]
    let ``FlowStream: terminal consumers close their child scope before continuation`` () =
        let mutable released = false
        let resource =
            Flow.scopeAcquireRelease
                (Flow.ok ())
                (fun () _ -> released <- true; Task.CompletedTask)

        let result =
            flow {
                do! resource |> FlowStream.fromFlow |> FlowStream.runDrain
                return released
            }
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ result = Exit.Success true @>

    [<Fact>]
    let ``FlowStream: early parallel termination interrupts remaining mappings`` () =
        let mutable active = 0
        let mapper value =
            Flow.fromTask (fun cancellationToken -> task {
                Interlocked.Increment(&active) |> ignore
                try
                    if value = 1 then return value
                    else
                        do! Task.Delay(Timeout.Infinite, cancellationToken)
                        return value
                finally
                    Interlocked.Decrement(&active) |> ignore
            })

        let result =
            FlowStream.fromSeq [ 1..4 ]
            |> FlowStream.mapFlowPar (Parallelism.bounded 4) mapper
            |> FlowStream.take 1
            |> FlowStream.runCollect
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ result = Exit.Success [ 1 ] @>
        test <@ active = 0 @>

    [<Fact>]
    let ``FlowStream: append collect and zip compose lazily`` () =
        let expanded =
            FlowStream.fromSeq [ 1; 2 ]
            |> FlowStream.append (FlowStream.singleton 3)
            |> FlowStream.collect (fun value -> FlowStream.fromSeq [ value; value * 10 ])

        let result =
            expanded
            |> FlowStream.zip (FlowStream.fromSeq [ "a"; "b"; "c"; "d"; "e"; "f" ])
            |> FlowStream.runCollect
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ result = Exit.Success [ (1, "a"); (10, "b"); (2, "c"); (20, "d"); (3, "e"); (30, "f") ] @>

    // Emits `values`, waiting `gap` before each one after the first.
    let private timed (gaps: (int * int) list) : FlowStream<ClockEnvironment, string, int> =
        FlowStream.unfoldFlow
            (fun remaining ->
                flow {
                    match remaining with
                    | [] -> return None
                    | (value, gap) :: rest ->
                        if gap > 0 then do! Flow.sleep (TimeSpan.FromMilliseconds(float gap))
                        return Some(value, rest)
                })
            gaps

    [<Fact>]
    let ``fromHub subscribes for the life of the stream`` () =
        let workflow : Flow<ClockEnvironment, Never, int list * int> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()

                let! consumer =
                    hub
                    |> FlowStream.fromHub QueueStrategy.Unbounded
                    |> FlowStream.take 2
                    |> FlowStream.runCollect
                    |> Flow.fork

                while Platform.lock hub.Gate (fun () -> hub.Subscriptions.Count) = 0 do
                    do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)

                do! hub |> Hub.publishAll [ 1; 2; 3 ] |> Flow.ignore
                let! values = Fiber.join consumer
                let! remaining = Hub.subscriberCount hub
                return values, remaining
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success([ 1; 2 ], 0) @>

    [<Fact>]
    let ``fromSchedule ticks on the fixed-rate grid`` () =
        let ticks =
            flow {
                let! started = runtimeNow ()

                return!
                    Schedule.fixedRate (TimeSpan.FromMilliseconds 50.0)
                    |> FlowStream.fromSchedule
                    |> FlowStream.take 4
                    |> FlowStream.mapFlow (fun tick -> runtimeNow () |> Flow.map (fun now -> tick, (now - started).TotalMilliseconds))
                    |> FlowStream.runCollect
            }
            |> runOnManualTime

        test <@ ticks = Exit.Success [ 0, 50.0; 1, 100.0; 2, 150.0; 3, 200.0 ] @>

    [<Fact>]
    let ``runIntoQueue and runIntoHub feed every value in order`` () =
        let workflow : Flow<ClockEnvironment, Never, int list * int list> =
            flow {
                let! (queue: Queue<int>) = Queue.bounded 2
                let! consumer = queue |> FlowStream.fromDequeue |> FlowStream.runCollect |> Flow.fork
                do! FlowStream.fromSeq [ 1..10 ] |> FlowStream.runIntoQueue queue
                do! Dequeue.shutdown queue
                let! fromQueue = Fiber.join consumer

                let! (hub: Hub<int>) = Hub.make ()
                let! subscription = hub |> Hub.subscribe QueueStrategy.Unbounded
                do! FlowStream.fromSeq [ 1..5 ] |> FlowStream.runIntoHub hub
                let! fromHub = Dequeue.takeAll subscription
                return fromQueue, fromHub
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success([ 1..10 ], [ 1..5 ]) @>

    [<Fact>]
    let ``buffer keeps order, never drops the end, and delivers a failure after buffered values`` () =
        let lossless = FlowStream.fromSeq [ 1..200 ] |> FlowStream.buffer (QueueStrategy.BackPressure 2) |> FlowStream.runCollect |> Flow.runSync (TestSupport.clockEnv ())
        test <@ lossless = Exit.Success [ 1..200 ] @>

        // A sliding buffer may skip values, but it always keeps the newest one, and the end still arrives.
        match FlowStream.fromSeq [ 1..1000 ] |> FlowStream.buffer (QueueStrategy.Sliding 1) |> FlowStream.runCollect |> Flow.runSync (TestSupport.clockEnv ()) with
        | Exit.Success values ->
            test <@ List.last values = 1000 && values = List.sort values && values = List.distinct values @>
        | other -> failwith $"Expected values, got {other}"

        let seen = ResizeArray<int>()

        let failing =
            FlowStream.fromSeq [ 1; 2 ]
            |> FlowStream.append (FlowStream.fromFlow (Flow.fail "boom"))
            |> FlowStream.buffer (QueueStrategy.BackPressure 4)
            |> FlowStream.runForEach seen.Add
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ failing = Exit.Failure(Cause.Fail "boom") && List.ofSeq seen = [ 1; 2 ] @>

    [<Fact>]
    let ``mergePar interleaves streams by arrival and ends when all end`` () =
        let merged =
            [ timed [ 1, 10; 4, 30 ]; timed [ 2, 20; 5, 30 ]; timed [ 3, 30 ] ]
            |> FlowStream.mergePar
            |> FlowStream.runCollect
            |> runOnManualTime

        test <@ merged = Exit.Success [ 1; 2; 3; 4; 5 ] @>

    [<Fact>]
    let ``mergePar fails with the first failure and stops the other streams`` () =
        let endless = Schedule.spaced (TimeSpan.FromHours 1.0) |> FlowStream.fromSchedule |> FlowStream.map (fun _ -> 0)

        let failed =
            [ endless; FlowStream.fromFlow (Flow.fail "boom") ]
            |> FlowStream.mergePar
            |> FlowStream.runCollect
            |> runOnManualTime

        let stoppedEarly =
            [ endless; endless ]
            |> FlowStream.mergePar
            |> FlowStream.take 3
            |> FlowStream.runCollect
            |> runOnManualTime

        test <@ failed = Exit.Failure(Cause.Fail "boom") @>
        test <@ stoppedEarly = Exit.Success [ 0; 0; 0 ] @>

    [<Fact>]
    let ``groupedWithin emits on size, on window, and flushes the tail`` () =
        let groups =
            timed [ 1, 0; 2, 0; 3, 0; 4, 0; 5, 600; 6, 0 ]
            |> FlowStream.groupedWithin 3 (TimeSpan.FromMilliseconds 200.0)
            |> FlowStream.runCollect
            |> runOnManualTime

        test <@ groups = Exit.Success [ [ 1; 2; 3 ]; [ 4 ]; [ 5; 6 ] ] @>

    [<Fact>]
    let ``debounce keeps the last value of each burst`` () =
        let values =
            timed [ 1, 0; 2, 5; 3, 5; 4, 250; 5, 5 ]
            |> FlowStream.debounce (TimeSpan.FromMilliseconds 100.0)
            |> FlowStream.runCollect
            |> runOnManualTime

        test <@ values = Exit.Success [ 3; 5 ] @>

    [<Fact>]
    let ``throttle emits the first value then the latest per interval`` () =
        let values =
            timed [ 1, 0; 2, 5; 3, 5; 4, 5; 5, 300 ]
            |> FlowStream.throttle (TimeSpan.FromMilliseconds 100.0)
            |> FlowStream.runCollect
            |> runOnManualTime

        test <@ values = Exit.Success [ 1; 4; 5 ] @>

    [<Fact>]
    let ``switchMapFlow interrupts the flow for a superseded value`` () =
        let interrupted = ref 0

        let search (query: int) : Flow<ClockEnvironment, string, string> =
            flow {
                do! Flow.sleep (TimeSpan.FromMilliseconds 400.0)
                return $"result-{query}"
            }
            |> Flow.fold Flow.ok (fun cause ->
                if Cause.isInterrupted cause then Interlocked.Increment(&interrupted.contents) |> ignore
                Flow.ofExit (Exit.Failure cause))

        let results =
            // Values 2 and 3 arrive long before the 400 ms search finishes; 4 arrives long after 3's result.
            timed [ 1, 0; 2, 30; 3, 30; 4, 900 ]
            |> FlowStream.switchMapFlow search
            |> FlowStream.runCollect
            |> runOnManualTime

        test <@ results = Exit.Success [ "result-3"; "result-4" ] @>
        test <@ interrupted.Value = 2 @>

    [<Fact>]
    let ``time operators stop their producer when the consumer stops early`` () =
        let pulled = ref 0

        let endless : FlowStream<ClockEnvironment, string, int> =
            FlowStream.unfoldFlow
                (fun n ->
                    flow {
                        Interlocked.Increment(&pulled.contents) |> ignore
                        do! Flow.sleep (TimeSpan.FromMilliseconds 5.0)
                        return Some(n, n + 1)
                    })
                0

        let first =
            endless
            |> FlowStream.throttle (TimeSpan.FromMilliseconds 1.0)
            |> FlowStream.take 3
            |> FlowStream.runCollect
            |> runOnManualTime

        let pulledAtEnd = pulled.Value
        Thread.Sleep 100

        test <@ first = Exit.Success [ 0; 1; 2 ] @>
        test <@ pulled.Value - pulledAtEnd <= 1 @>

    [<Fact>]
    let ``repeatFlow runs its flow once per pull`` () =
        let runs = ref 0
        let next : Flow<ClockEnvironment, string, int> = Flow.delay (fun () -> runs.Value <- runs.Value + 1; Flow.ok runs.Value)

        let values = FlowStream.repeatFlow next |> FlowStream.take 3 |> FlowStream.runCollect |> Flow.runSync (TestSupport.clockEnv ())

        test <@ values = Exit.Success [ 1; 2; 3 ] @>
        test <@ runs.Value = 3 @>

    [<Fact>]
    let ``using releases its resource when the stream ends, fails, or is cut short`` () =
        let acquired = ref 0
        let released = ref 0

        let resource : Resource<ClockEnvironment, string, int> =
            Resource.ofAsync
                (Flow.delay (fun () -> acquired.Value <- acquired.Value + 1; Flow.ok 10))
                (fun _ _ -> async { released.Value <- released.Value + 1 })

        let numbers = FlowStream.using resource (fun start -> FlowStream.fromSeq [ start .. start + 4 ])

        let all = numbers |> FlowStream.runCollect |> Flow.runSync (TestSupport.clockEnv ())
        let releasedAfterAll = released.Value
        let first = numbers |> FlowStream.take 2 |> FlowStream.runCollect |> Flow.runSync (TestSupport.clockEnv ())
        let releasedAfterTake = released.Value

        let failing =
            FlowStream.using resource (fun _ -> FlowStream.fromFlow (Flow.fail "boom" : Flow<ClockEnvironment, string, int>))
            |> FlowStream.runDrain
            |> Flow.runSync (TestSupport.clockEnv ())

        test <@ all = Exit.Success [ 10..14 ] @>
        test <@ first = Exit.Success [ 10; 11 ] @>
        test <@ failing = Exit.Failure(Cause.Fail "boom") @>
        test <@ (releasedAfterAll, releasedAfterTake, released.Value) = (1, 2, 3) @>
        test <@ acquired.Value = 3 @>

    [<Fact>]
    let ``runTryHead pulls one value, runTryLast and runCount consume everything`` () =
        let pulled = ref 0

        let counted : FlowStream<ClockEnvironment, string, int> =
            FlowStream.repeatFlow (Flow.delay (fun () -> pulled.Value <- pulled.Value + 1; Flow.ok pulled.Value))

        let head = counted |> FlowStream.runTryHead |> Flow.runSync (TestSupport.clockEnv ())
        let emptyHead : Exit<int option, string> = FlowStream.empty |> FlowStream.runTryHead |> Flow.runSync (TestSupport.clockEnv ())
        let last = FlowStream.fromSeq [ 1..5 ] |> FlowStream.runTryLast |> Flow.runSync (TestSupport.clockEnv ())
        let emptyLast : Exit<int option, string> = FlowStream.empty |> FlowStream.runTryLast |> Flow.runSync (TestSupport.clockEnv ())
        let count : Exit<int64, string> = FlowStream.fromSeq [ 1..7 ] |> FlowStream.runCount |> Flow.runSync (TestSupport.clockEnv ())

        test <@ head = Exit.Success(Some 1) && pulled.Value = 1 @>
        test <@ emptyHead = Exit.Success None @>
        test <@ last = Exit.Success(Some 5) @>
        test <@ emptyLast = Exit.Success None @>
        test <@ count = Exit.Success 7L @>

    [<Fact>]
    let ``a slow consumer holds mapFlowPar's producer within the parallelism bound`` () =
        let produced = ref 0
        let consumed = ref 0
        let lead = ref 0

        let source : FlowStream<ClockEnvironment, string, int> =
            FlowStream.repeatFlow (Flow.delay (fun () -> Flow.ok (Interlocked.Increment(&produced.contents))))
            |> FlowStream.take 20

        let result =
            source
            |> FlowStream.mapFlowPar (Parallelism.bounded 2) Flow.ok
            |> FlowStream.runForEachFlow (fun _ ->
                flow {
                    consumed.Value <- consumed.Value + 1
                    lead.Value <- max lead.Value (produced.Value - consumed.Value)
                    do! Flow.sleep (TimeSpan.FromMilliseconds 5.0)
                })
            |> runOnManualTime

        test <@ result = Exit.Success () @>
        test <@ consumed.Value = 20 @>
        // Pulling stops while the consumer is busy: never more than the two in-flight mappings are ahead.
        test <@ lead.Value <= 2 @>
