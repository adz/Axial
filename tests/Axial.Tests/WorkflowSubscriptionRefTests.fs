namespace Axial.Tests

open System
open Axial
open Axial.State
open Swensen.Unquote
open Xunit

module WorkflowSubscriptionRefTests =
    let private subscribers (reference: SubscriptionRef<'a>) () =
        Platform.lock reference.Hub.Gate (fun () -> reference.Hub.Subscriptions.Count)

    /// Polls until a stream has subscribed, so a test can update the reference knowing the stream will see it.
    let private waitForSubscriber (reference: SubscriptionRef<'a>) : Flow<unit, 'error, unit> =
        flow {
            let mutable remaining = 5000

            while subscribers reference () = 0 && remaining > 0 do
                do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 1.0)
                remaining <- remaining - 1
        }

    [<Fact>]
    let ``SubscriptionRef: changes starts with the current value and then every update`` () =
        let workflow : Flow<unit, Never, int list * int> =
            flow {
                let! counter = SubscriptionRef.make 10
                do! counter |> SubscriptionRef.set 11

                let! seen =
                    counter
                    |> SubscriptionRef.changes QueueStrategy.Unbounded
                    |> FlowStream.take 4
                    |> FlowStream.runCollect
                    |> Flow.fork

                do! waitForSubscriber counter
                do! counter |> SubscriptionRef.update ((+) 1)
                let! doubled = counter |> SubscriptionRef.modify (fun value -> value * 2, value * 2)
                do! counter |> SubscriptionRef.set 0
                let! values = Flow.join seen
                return values, doubled
            }

        test <@ Flow.runSync () workflow = Exit.Success([ 11; 12; 24; 0 ], 24) @>

    [<Fact>]
    let ``SubscriptionRef: a stream joining during concurrent updates sees no gap and no duplicate`` () =
        let attempt () : Flow<unit, Never, bool> =
            flow {
                let! counter = SubscriptionRef.make 0

                let! updater =
                    flow {
                        for _ in 1..200 do
                            do! counter |> SubscriptionRef.update ((+) 1)
                    }
                    |> Flow.fork

                // Join part way through the updates, then read until the final value.
                let! values =
                    counter
                    |> SubscriptionRef.changes QueueStrategy.Unbounded
                    |> FlowStream.takeWhile (fun value -> value < 200)
                    |> FlowStream.runCollect

                do! Flow.join updater
                let expected = [ (List.tryHead values |> Option.defaultValue 200) .. 199 ]
                return values = expected
            }

        let workflow = List.init 200 (fun _ -> attempt ()) |> Flow.sequence
        test <@ Flow.runSync () workflow |> Exit.map (List.forall id) = Exit.Success true @>

    [<Fact>]
    let ``SubscriptionRef: a sliding stream keeps only the latest value and never delays updates`` () =
        let workflow : Flow<unit, Never, int list> =
            flow {
                let! reading = SubscriptionRef.make 0
                let! tookFirst = Deferred.make<unit, Never, unit> ()
                let! release = Deferred.make<unit, Never, unit> ()

                // The display takes the current value, then stalls until every update below has been made.
                let! display =
                    reading
                    |> SubscriptionRef.changes (QueueStrategy.Sliding 1)
                    |> FlowStream.tapFlow (fun value ->
                        flow {
                            if value = 0 then
                                do! Deferred.succeed () tookFirst |> Flow.ignore
                                do! Deferred.await release
                        })
                    |> FlowStream.take 2
                    |> FlowStream.runCollect
                    |> Flow.fork

                do! Deferred.await tookFirst

                // Every update completes while the display is stalled: a sliding buffer never holds the writer up.
                for value in 1..50 do
                    do! reading |> SubscriptionRef.set value

                do! Deferred.succeed () release |> Flow.ignore
                return! Flow.join display
            }

        test <@ Flow.runSync () workflow = Exit.Success [ 0; 50 ] @>

    [<Fact>]
    let ``SubscriptionRef: a stream unsubscribes when it ends`` () =
        let workflow : Flow<unit, Never, int list * int> =
            flow {
                let! reading = SubscriptionRef.make 7
                let! first = reading |> SubscriptionRef.changes QueueStrategy.Unbounded |> FlowStream.take 1 |> FlowStream.runCollect
                return first, subscribers reading ()
            }

        test <@ Flow.runSync () workflow = Exit.Success([ 7 ], 0) @>
