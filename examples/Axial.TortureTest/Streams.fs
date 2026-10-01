/// Every stream operator on random input: deterministic operators against the List model, concurrent and timed
/// operators against the properties they promise, and early termination and failure against their cleanup.
module Axial.TortureTest.Streams

open System
open System.Threading
open Axial
open Axial.TortureTest.Scenario

let private ms (value: float) = TimeSpan.FromMilliseconds value

/// Whether <paramref name="part" /> appears in <paramref name="whole" /> in the same order.
let private isSubsequence (part: 'a list) (whole: 'a list) =
    let rec go part whole =
        match part, whole with
        | [], _ -> true
        | _, [] -> false
        | p :: ps, w :: ws -> if p = w then go ps ws else go part ws

    go part whole

// <snippet:torture-streams>
let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let input = [ for _ in 1 .. round.Size 300 -> round.Next 20 ]
    let source () = FlowStream.fromSeq input

    flow {
        // Deterministic operators must agree with the List functions they mirror.
        let! mapped = source () |> FlowStream.map ((*) 3) |> FlowStream.filter (fun value -> value % 2 = 0) |> FlowStream.runCollect
        let! chosen = source () |> FlowStream.choose (fun value -> if value > 10 then Some(value - 10) else None) |> FlowStream.runCollect
        let! scanned = source () |> FlowStream.scan (+) 0 |> FlowStream.runCollect
        let! indexed = source () |> FlowStream.indexed |> FlowStream.runCollect
        let! distinct = source () |> FlowStream.distinctUntilChangedBy (fun value -> value / 5) |> FlowStream.runCollect
        let! skipped = source () |> FlowStream.skip 7 |> FlowStream.skipWhile (fun value -> value < 15) |> FlowStream.runCollect
        let! taken = source () |> FlowStream.takeWhile (fun value -> value <> 19) |> FlowStream.take 50 |> FlowStream.runCollect
        let! chunks = source () |> FlowStream.chunkBySize 7 |> FlowStream.runCollect
        let! expanded = source () |> FlowStream.collect (fun value -> FlowStream.fromSeq [ value; -value ]) |> FlowStream.runCollect
        let! appended = FlowStream.singleton -1 |> FlowStream.append (source ()) |> FlowStream.append FlowStream.empty |> FlowStream.runCollect
        let! zipped = source () |> FlowStream.zip (FlowStream.fromSeq [ "a"; "b"; "c" ]) |> FlowStream.runCollect
        let! flowMapped = source () |> FlowStream.mapFlow (fun value -> Flow.ok (value + 1)) |> FlowStream.runCollect
        let! unfolded = FlowStream.unfoldFlow (fun state -> Flow.ok (if state < 10 then Some(state, state + 1) else None)) 0 |> FlowStream.runCollect
        let! single = FlowStream.fromFlow (Flow.ok 42) |> FlowStream.runCollect
        let! localized =
            (FlowStream.fromSeq input : FlowStream<unit, Never, int>)
            |> FlowStream.localEnv (fun (_: ClockEnvironment) -> ())
            |> FlowStream.runCollect
        let! folded = source () |> FlowStream.runFold (+) 0
        let! counted = source () |> FlowStream.runCount
        let! head = source () |> FlowStream.runTryHead
        let! last = source () |> FlowStream.runTryLast
        let! nothing = FlowStream.empty<Axial.ClockEnvironment, Never, int> |> FlowStream.runTryHead
        let seen = ResizeArray<int>()
        do! source () |> FlowStream.tapFlow (fun value -> Flow.delay (fun () -> seen.Add value; Flow.ok ())) |> FlowStream.runDrain
        do! source () |> FlowStream.runForEach seen.Add
        do! source () |> FlowStream.runForEachFlow (fun value -> Flow.delay (fun () -> seen.Add value; Flow.ok ()))

        let modelHolds =
            mapped = (input |> List.map ((*) 3) |> List.filter (fun value -> value % 2 = 0))
            && chosen = (input |> List.choose (fun value -> if value > 10 then Some(value - 10) else None))
            && scanned = (List.scan (+) 0 input |> List.tail)
            && indexed = List.indexed input
            && distinct = (input |> List.fold (fun kept value -> match kept with previous :: _ when previous / 5 = value / 5 -> kept | _ -> value :: kept) [] |> List.rev)
            && skipped = (input |> List.skip (min 7 input.Length) |> List.skipWhile (fun value -> value < 15))
            && taken = (input |> List.takeWhile (fun value -> value <> 19) |> List.truncate 50)
            && chunks = List.chunkBySize 7 input
            && expanded = (input |> List.collect (fun value -> [ value; -value ]))
            && appended = -1 :: input
            && zipped = List.zip (List.truncate 3 input) (List.truncate input.Length [ "a"; "b"; "c" ])
            && flowMapped = List.map ((+) 1) input
            && unfolded = [ 0..9 ]
            && single = [ 42 ]
            && localized = input
            && folded = List.sum input
            && counted = input.Length
            && head = List.tryHead input
            && last = List.tryLast input
            && nothing = None
            && List.ofSeq seen = input @ input @ input

        // Concurrent and timed operators keep the properties they promise, whatever the timing.
        let bound = round.Next 5 + 1
        let active = ref 0
        let peak = ref 0

        let slowly value =
            flow {
                let now = increment active
                lock peak (fun () -> peak.Value <- max peak.Value now)
                do! Flow.sleep (ms (float (round.Next 2)))
                return value
            }
            |> Flow.ensuring (Flow.delay (fun () -> decrement active |> ignore; Flow.ok ()))

        let! parallelResults = source () |> FlowStream.mapFlowPar (Parallelism.bounded bound) slowly |> FlowStream.runCollect
        let busy = Collections.Generic.HashSet<int>()
        let shared = ref false
        let acquired = ref 0
        let released = ref 0

        let pool =
            Resource.ofAsync
                (Flow.delay (fun () -> Flow.ok (increment acquired)))
                (fun _ _ -> async { increment released |> ignore })

        let! pooledResults =
            source ()
            |> FlowStream.mapFlowParUsing (Parallelism.bounded bound) pool (fun resource value ->
                flow {
                    lock busy (fun () -> if not (busy.Add resource) then shared.Value <- true)
                    do! Flow.sleep (ms (float (round.Next 2)))
                    lock busy (fun () -> busy.Remove resource |> ignore)
                    return value
                })
            |> FlowStream.runCollect

        let timed () = source () |> FlowStream.tapFlow (fun _ -> if round.Chance 5 then Flow.sleep (ms 3.0) else Flow.ok ())
        let! lossless = timed () |> FlowStream.buffer (QueueStrategy.BackPressure 4) |> FlowStream.runCollect
        let! sliding = timed () |> FlowStream.buffer (QueueStrategy.Sliding 2) |> FlowStream.runCollect
        let! dropping = timed () |> FlowStream.buffer (QueueStrategy.Dropping 2) |> FlowStream.runCollect
        let! merged = [ source () |> FlowStream.map (fun value -> 1, value); timed () |> FlowStream.map (fun value -> 2, value) ] |> FlowStream.mergePar |> FlowStream.runCollect
        let! groups = timed () |> FlowStream.groupedWithin 5 (ms 2.0) |> FlowStream.runCollect
        let! throttled = timed () |> FlowStream.indexed |> FlowStream.throttle (ms 1.0) |> FlowStream.runCollect
        let! debounced = timed () |> FlowStream.indexed |> FlowStream.debounce (ms 1.0) |> FlowStream.runCollect
        let! switched = timed () |> FlowStream.indexed |> FlowStream.switchMapFlow (fun (index, value) -> Flow.sleep (ms (float (round.Next 2))) |> Flow.map (fun () -> index, value)) |> FlowStream.runCollect

        let indexedInput = List.indexed input
        let byIndex (pairs: (int * int) list) = pairs |> List.map fst |> List.pairwise |> List.forall (fun (a, b) -> a < b)

        let concurrent =
            [ check "mapFlowPar kept every value and its bound" (List.sort parallelResults = List.sort input && peak.Value <= bound)
              check "mapFlowParUsing never shared a resource and released every one" (List.sort pooledResults = List.sort input && not shared.Value && acquired.Value = released.Value)
              check "a lossless buffer kept every value in order" (lossless = input)
              check "a sliding buffer kept values in order, ending with the last" (isSubsequence sliding input && List.tryLast sliding = List.tryLast input)
              check "a dropping buffer kept values in order" (isSubsequence dropping input)
              check "mergePar kept every value and each source's order" (List.sort merged = List.sort ((input |> List.map (fun value -> 1, value)) @ (input |> List.map (fun value -> 2, value))) && inOrderPerProducer (merged |> List.indexed |> List.map (fun (position, (source, _)) -> source, position)))
              check "groupedWithin grouped every value in order, within its size" (List.concat groups = input && groups |> List.forall (fun group -> not group.IsEmpty && group.Length <= 5))
              check "throttle kept values in order, including the first and the last" (byIndex throttled && isSubsequence throttled indexedInput && List.tryHead throttled = List.tryHead indexedInput && List.tryLast throttled = List.tryLast indexedInput)
              check "debounce kept values in order, including the last" (byIndex debounced && isSubsequence debounced indexedInput && List.tryLast debounced = List.tryLast indexedInput)
              check "switchMapFlow kept results in order, including the last" (byIndex switched && isSubsequence switched indexedInput && List.tryLast switched = List.tryLast indexedInput) ]

        // Stopping early, or failing, releases what the stream owns and stops the fibers it started.
        let opened = ref 0
        let closed = ref 0

        let owned () =
            FlowStream.using
                (Resource.ofAsync (Flow.delay (fun () -> increment opened |> ignore; Flow.ok ())) (fun _ _ -> async { increment closed |> ignore }))
                (fun () -> FlowStream.repeatFlow (Flow.ok 1))

        let! early = owned () |> FlowStream.mapFlowPar (Parallelism.bounded 3) slowly |> FlowStream.take 5 |> FlowStream.runCollect

        let! failed =
            owned ()
            |> FlowStream.mapFlow (fun _ -> if round.Chance 20 then Flow.fail "broken" else Flow.ok 1)
            |> FlowStream.mapError (fun error -> error.Length)
            |> FlowStream.runDrain
            |> exitOf

        let! runningAfter = Flow.sleep (ms 5.0) |> Flow.map (fun () -> active.Value)

        return
            [ check "deterministic operators agree with the List model" modelHolds ]
            @ concurrent
            @ [ check "stopping early or failing released every owned resource" (early = [ 1; 1; 1; 1; 1 ] && opened.Value = 2 && closed.Value = 2 && failed = Exit.Failure(Cause.Fail 6))
                check "no stream fiber was left running" (runningAfter = 0) ]
    }
// </snippet:torture-streams>

let scenario : Scenario =
    { Name = "streams"
      Title = "Streams: every operator against the List model or its promised properties, stopped early and failing"
      Run = run }
