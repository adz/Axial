/// The monitoring pipeline from the "Torture test" docs page, and the invariants it must keep.
module Axial.TortureTest.Pipeline

open System
open Axial
open Axial.State

// <snippet:torture-pipeline>
/// One invariant and whether it held on a run.
type Check = { Invariant: string; Held: bool }

let readers = 4
let samplesPerReader = 500

/// Runs the pipeline once and returns every invariant with whether it held. Interleavings differ on every run, so a
/// correct runtime must hold every invariant on every run.
let run () : Flow<unit, Never, Check list> =
    flow {
        let everySample = [ for reader in 1..readers do for sample in 1..samplesPerReader -> reader, sample ]
        let recorded = ResizeArray<int * int>()
        let stolen = ResizeArray<int * int>()
        let published = ResizeArray<int * int>()
        let lateView = ResizeArray<int * int>()
        let! latest = SubscriptionRef.make (0, 0)

        let! inputs, display, alarms =
            flow {
                let! (inputs: Queue<int * int>) = Queue.bounded 8
                let! (feed: Hub<int * int>) = Hub.make ()
                let! historian = feed |> Hub.subscribe (QueueStrategy.BackPressure 32)
                let! display = feed |> Hub.subscribe (QueueStrategy.Sliding 1)
                let! alarms = feed |> Hub.subscribe (QueueStrategy.Dropping 4)

                // The historian must record everything, including what is still queued when the scope closes.
                let! _ =
                    historian
                    |> FlowStream.fromDequeue
                    |> FlowStream.runForEach recorded.Add
                    |> Flow.forkGraceful (Hub.shutdown feed) (TimeSpan.FromSeconds 30.0)

                // A view that joins the latest-sample reference and follows 200 changes of it.
                let! _ =
                    latest
                    |> SubscriptionRef.changes QueueStrategy.Unbounded
                    |> FlowStream.take 200
                    |> FlowStream.runForEach lateView.Add
                    |> Flow.forkGraceful (Flow.ok ()) (TimeSpan.FromSeconds 30.0)

                // The control loop publishes every input and keeps the latest sample current.
                let! _ =
                    inputs
                    |> FlowStream.fromDequeue
                    |> FlowStream.runForEachFlow (fun sample ->
                        flow {
                            do! feed |> Hub.publish sample |> Flow.ignore
                            published.Add sample
                            do! latest |> SubscriptionRef.set sample
                        })
                    |> Flow.forkGraceful (Dequeue.shutdown inputs) (TimeSpan.FromSeconds 30.0)

                // Device readers feed the control loop concurrently, while a saboteur keeps starting takes on the
                // same queue and interrupting them at once. A take that wins a sample before its interruption keeps
                // it; every other sample it was handed must go back to the queue.
                let readersFeed =
                    [ for reader in 1..readers -> FlowStream.fromSeq [ for sample in 1..samplesPerReader -> reader, sample ] ]
                    |> FlowStream.mergePar
                    |> FlowStream.runIntoQueue inputs

                let saboteur =
                    flow {
                        for _ in 1..300 do
                            let! taker = inputs |> Dequeue.take |> Flow.fork
                            let! exit = Fiber.interrupt taker

                            match exit with
                            | Exit.Success sample -> stolen.Add sample
                            | Exit.Failure _ -> ()
                    }

                do! Flow.zipPar readersFeed saboteur |> Flow.ignore
                return inputs, display, alarms
            }
            |> Flow.scoped

        // The scope has closed: the control loop drained its queue, then the historian drained the hub.
        let! inputStats = Dequeue.stats inputs
        let! displayStats = Dequeue.stats display
        let! shown = Dequeue.takeAll display
        let! alarmStats = Dequeue.stats alarms
        let! raised = Dequeue.takeAll alarms

        let history = List.ofSeq recorded
        let order = List.ofSeq published
        let view = List.ofSeq lateView
        let inOrder reader = history |> List.filter (fst >> (=) reader) |> List.pairwise |> List.forall (fun (a, b) -> snd a < snd b)

        // The view starts with whatever was current when it joined, then follows every later update.
        let expectedView =
            match List.tryFindIndex ((=) (List.head view)) order with
            | Some index -> order |> List.skip index |> List.truncate view.Length
            | None -> (0, 0) :: (order |> List.truncate (view.Length - 1))

        let check invariant held = { Invariant = invariant; Held = held }

        return
            [ check "every sample is recorded or was won by a take, exactly once" (List.sort (history @ List.ofSeq stolen) = everySample)
              check "the historian recorded every published sample, in publish order" (history = order)
              check "each reader's samples stay in order" ([ 1..readers ] |> List.forall inOrder)
              check "the input queue is shut down and empty" (inputStats.IsShutdown && inputStats.Size = 0)
              check "the sliding display ends on the newest sample" (shown = [ List.last history ])
              check
                  "the sliding display accepted every sample and evicted all but one"
                  (displayStats.Accepted = int64 history.Length && displayStats.Evicted = int64 history.Length - 1L)
              check "the dropping alarms kept the first four samples" (raised = List.truncate 4 history)
              check "the dropping alarms counted every other sample as dropped" (alarmStats.Dropped = int64 history.Length - 4L)
              check "the late view saw the current sample, then every update without a gap or duplicate" (view = expectedView) ]
    }
// </snippet:torture-pipeline>
