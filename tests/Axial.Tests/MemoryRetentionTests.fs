namespace Axial.Tests

open System
open System.Threading.Tasks
open Axial
open Axial.State
open Swensen.Unquote
open Xunit

/// Tests that measure process-wide state (managed heap size, the global STM waiter list) run alone, so other tests
/// running in parallel cannot disturb the measurement.
[<CollectionDefinition("Process-wide measurements", DisableParallelization = true)>]
type ProcessWideMeasurements() = class end

[<Collection("Process-wide measurements")>]
module MemoryRetentionTests =
    let private liveBytes () =
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
        GC.GetTotalMemory true

    /// One step that genuinely suspends, so each loop iteration completes asynchronously.
    let private suspend () : Flow<unit, Never, unit> =
        Flow.fromTask (fun _ -> task { do! Task.Yield() })

    /// Runs <paramref name="iterations" /> of a loop and returns how many bytes stayed live between an early and a
    /// late iteration. <paramref name="build" /> receives the per-iteration callback to invoke.
    let private retainedDuringLoop (build: (int -> unit) -> Flow<unit, Never, unit>) : int64 =
        let early = 2_000
        let late = 42_000
        let mutable earlyBytes = 0L
        let mutable lateBytes = 0L

        let observe index =
            if index = early then earlyBytes <- liveBytes ()
            elif index = late then lateBytes <- liveBytes ()

        let exit = build observe |> Flow.runSync ()
        test <@ exit = Exit.Success () @>
        lateBytes - earlyBytes

    // Before loops ran in constant memory, each of these retained 400-600 bytes per iteration: about 16-24 MB over
    // the 40,000 measured iterations. The bound leaves room for allocator noise.
    let private bound () = 4L * 1024L * 1024L

    [<Fact>]
    let ``A flow while loop that suspends each iteration runs in constant memory`` () =
        let retained =
            retainedDuringLoop (fun observe ->
                flow {
                    let mutable index = 0

                    while index <= 42_000 do
                        do! suspend ()
                        observe index
                        index <- index + 1
                })

        test <@ retained < bound () @>

    [<Fact>]
    let ``A stream consumer whose pulls suspend runs in constant memory`` () =
        let retained =
            retainedDuringLoop (fun observe ->
                FlowStream.unfoldFlow
                    (fun index -> suspend () |> Flow.map (fun () -> if index <= 42_000 then Some(index, index + 1) else None))
                    0
                |> FlowStream.filter (fun index -> index % 2 = 0 || index = 42_001)
                |> FlowStream.runForEachFlow (fun index -> Flow.ok (observe index)))

        test <@ retained < bound () @>

    [<Fact>]
    let ``Schedule repeat runs in constant memory`` () =
        let retained =
            retainedDuringLoop (fun observe ->
                let counter = ref 0

                suspend ()
                |> Flow.map (fun () ->
                    observe counter.Value
                    counter.Value <- counter.Value + 1)
                |> Schedule.repeat (Schedule.recurs 42_000))

        test <@ retained < bound () @>

    [<Fact>]
    let ``A queue consumer loop runs in constant memory`` () =
        let retained =
            retainedDuringLoop (fun observe ->
                flow {
                    let! (queue: Queue<int>) = Queue.bounded 16

                    let! producer =
                        flow {
                            for index in 0..42_000 do
                                do! queue |> Queue.offer index |> Flow.ignore

                            do! Dequeue.shutdown queue
                        }
                        |> Flow.fork

                    do! queue |> FlowStream.fromDequeue |> FlowStream.runForEach observe
                    do! Flow.join producer
                })

        test <@ retained < bound () @>

    [<Fact>]
    let ``STM: an interrupted retry withdraws its wake-up`` () =
        let workflow : Flow<unit, Never, int * int> =
            flow {
                let! gate = TRef.make false |> STM.atomically
                let before = STM.pendingRetries ()

                let! waiter =
                    stm {
                        let! isOpen = TRef.get gate
                        if not isOpen then return! STM.retry
                    }
                    |> STM.atomically
                    |> Flow.fork

                let rec untilWaiting remaining =
                    flow {
                        if STM.pendingRetries () = before && remaining > 0 then
                            do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 1.0)
                            return! untilWaiting (remaining - 1)
                    }

                do! untilWaiting 5000
                let waiting = STM.pendingRetries () - before
                let! _ = Flow.interrupt waiter
                return waiting, STM.pendingRetries () - before
            }

        test <@ Flow.runSync () workflow = Exit.Success(1, 0) @>
