namespace Axial.Tests

open System
open Axial
open Swensen.Unquote
open Xunit

module WorkflowHubTests =
    let private suspendedPublishers (subscription: Dequeue<'a>) () =
        Platform.lock subscription.Gate (fun () -> subscription.Offerers.Count)

    let private suspendedTakers (subscription: Dequeue<'a>) () =
        Platform.lock subscription.Gate (fun () -> subscription.Takers.Count)

    /// Polls a condition on internals so a test can act once a fiber is known to be suspended.
    let private waitUntil (condition: unit -> bool) : Flow<unit, 'error, unit> =
        flow {
            let mutable remaining = 5000

            while not (condition ()) && remaining > 0 do
                do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)
                remaining <- remaining - 1
        }

    let private isInterrupted (exit: Exit<'value, 'error>) =
        match exit with
        | Exit.Failure cause -> Cause.isInterrupted cause
        | Exit.Success _ -> false

    /// Subscribes in a scope owned by a forked fiber. Returns the subscription and a flow that closes that scope.
    let private subscribeInOwnScope strategy (hub: Hub<'a>) : Flow<unit, 'error, Dequeue<'a> * Flow<unit, 'error, unit>> =
        flow {
            let! handedOut = Deferred.make<unit, 'error, Dequeue<'a>> ()
            let! close = Deferred.make<unit, 'error, unit> ()

            let! owner =
                flow {
                    let! subscription = hub |> Hub.subscribe strategy
                    do! Deferred.succeed subscription handedOut |> Flow.ignore
                    do! Deferred.await close
                }
                |> Flow.scoped
                |> Flow.fork

            let! subscription = Deferred.await handedOut

            let closeScope =
                flow {
                    do! Deferred.succeed () close |> Flow.ignore
                    do! Fiber.join owner
                }

            return subscription, closeScope
        }

    [<Fact>]
    let ``Hub: every subscriber sees every value in the same order`` () =
        let workflow : Flow<unit, Never, (int * int) list list> =
            flow {
                let! (hub: Hub<int * int>) = Hub.make ()
                let! subscriptions = [ 1..8 ] |> Flow.traverse (fun _ -> hub |> Hub.subscribe QueueStrategy.Unbounded)

                // Each publisher yields to the thread pool before every publish, so publishes genuinely overlap.
                let publisher id =
                    flow {
                        for index in 1..500 do
                            do! Flow.fromTask (fun _ -> task { do! System.Threading.Tasks.Task.Yield() })
                            do! hub |> Hub.publish (id, index) |> Flow.ignore
                    }

                do! [ publisher 1; publisher 2; publisher 3; publisher 4 ] |> Flow.sequencePar |> Flow.ignore
                return! subscriptions |> Flow.traverse Dequeue.takeAll
            }

        match Flow.runSync () workflow with
        | Exit.Success(first :: others) ->
            test <@ first.Length = 2000 && others |> List.forall ((=) first) @>

            for id in 1..4 do
                let fromPublisher = first |> List.filter (fst >> (=) id) |> List.map snd
                test <@ fromPublisher = [ 1..500 ] @>
        | other -> failwith $"Expected subscriptions, got {other}"

    [<Fact>]
    let ``Hub: a slow sliding subscriber keeps the newest values without delaying the publisher`` () =
        let workflow : Flow<unit, Never, PublishResult * int list> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! display = hub |> Hub.subscribe (QueueStrategy.Sliding 2)
                let! result = hub |> Hub.publishAll [ 1..10 ]
                let! kept = Dequeue.takeAll display
                return result, kept
            }

        test <@ Flow.runSync () workflow = Exit.Success({ Delivered = 10; Dropped = 0; Evicted = 8 }, [ 9; 10 ]) @>

    [<Fact>]
    let ``Hub: a slow dropping subscriber counts its drops without delaying the publisher`` () =
        let workflow : Flow<unit, Never, PublishResult * int list> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! display = hub |> Hub.subscribe (QueueStrategy.Dropping 2)
                let! result = hub |> Hub.publishAll [ 1..5 ]
                let! kept = Dequeue.takeAll display
                return result, kept
            }

        test <@ Flow.runSync () workflow = Exit.Success({ Delivered = 2; Dropped = 3; Evicted = 0 }, [ 1; 2 ]) @>

    [<Fact>]
    let ``Hub: a full back-pressure subscriber suspends publish until drained or closed`` () =
        let workflow : Flow<unit, Never, PublishResult * PublishResult * int> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! (historian: Dequeue<int>), closeHistorian = hub |> subscribeInOwnScope (QueueStrategy.BackPressure 1)

                do! hub |> Hub.publish 1 |> Flow.ignore
                let! blocked = hub |> Hub.publish 2 |> Flow.fork
                do! waitUntil (fun () -> suspendedPublishers historian () = 1)
                let! _ = Dequeue.take historian
                let! afterDrain = Fiber.join blocked

                let! blockedAgain = hub |> Hub.publish 3 |> Flow.fork
                do! waitUntil (fun () -> suspendedPublishers historian () = 1)
                do! closeHistorian
                let! afterClose = Fiber.join blockedAgain
                let! remaining = Hub.subscriberCount hub
                return afterDrain, afterClose, remaining
            }

        test <@ Flow.runSync () workflow = Exit.Success({ Delivered = 1; Dropped = 0; Evicted = 0 }, { Delivered = 0; Dropped = 0; Evicted = 0 }, 0) @>

    [<Fact>]
    let ``Hub: a lossless historian gets everything while lossy displays never block the publisher`` () =
        let workflow : Flow<unit, Never, int list * int list * int list * PublishResult * bool> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! historian = hub |> Hub.subscribe (QueueStrategy.BackPressure 128)
                let! display = hub |> Hub.subscribe (QueueStrategy.Sliding 1)
                let! alarms = hub |> Hub.subscribe (QueueStrategy.Dropping 1)
                let! recorded = historian |> FlowStream.fromDequeue |> FlowStream.runCollect |> Flow.fork

                // tryPublish succeeds only when the publish needs no waiting, so every Some proves that neither
                // lossy subscriber (nor the historian, whose buffer has room) ever held the publisher up.
                let! results = [ 1..100 ] |> Flow.traverse (fun reading -> hub |> Hub.tryPublish reading)
                do! Hub.shutdown hub

                let! history = Fiber.join recorded
                let! latest = Dequeue.takeAll display
                let! firstAlarm = Dequeue.takeAll alarms
                let total = results |> List.choose id |> List.fold PublishResult.add PublishResult.empty
                return history, latest, firstAlarm, total, results |> List.forall Option.isSome
            }

        test <@
            Flow.runSync () workflow =
                Exit.Success([ 1..100 ], [ 100 ], [ 1 ], { Delivered = 201; Dropped = 99; Evicted = 99 }, true)
        @>

    [<Fact>]
    let ``Hub: tryPublish refuses instead of waiting on a full back-pressure subscriber`` () =
        let workflow : Flow<unit, Never, PublishResult option * PublishResult option * int list * int list> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! historian = hub |> Hub.subscribe (QueueStrategy.BackPressure 1)
                let! display = hub |> Hub.subscribe QueueStrategy.Unbounded
                let! accepted = hub |> Hub.tryPublish 1
                let! refused = hub |> Hub.tryPublish 2
                let! history = Dequeue.takeAll historian
                let! shown = Dequeue.takeAll display
                return accepted, refused, history, shown
            }

        // A refused value reaches no subscriber, so nobody sees a value the others missed.
        test <@
            Flow.runSync () workflow =
                Exit.Success(Some { Delivered = 2; Dropped = 0; Evicted = 0 }, None, [ 1 ], [ 1 ])
        @>

    [<Fact>]
    let ``Hub: direct tryPublish distinguishes published, full, and shutdown`` () =
        let workflow : Flow<unit, Never, HubTryPublishResult * HubTryPublishResult * HubTryPublishResult * int list * int list> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! historian = hub |> Hub.subscribe (QueueStrategy.BackPressure 1)
                let! display = hub |> Hub.subscribe QueueStrategy.Unbounded
                let first = hub |> Hub.tryPublishNow 1
                let full = hub |> Hub.tryPublishNow 2
                let! history = Dequeue.takeAll historian
                let! shown = Dequeue.takeAll display
                do! Hub.shutdown hub
                let shut = hub |> Hub.tryPublishNow 3
                return first, full, shut, history, shown
            }

        test <@
            Flow.runSync () workflow =
                Exit.Success(
                    HubTryPublishResult.Published { Delivered = 2; Dropped = 0; Evicted = 0 },
                    HubTryPublishResult.Full,
                    HubTryPublishResult.Shutdown,
                    [ 1 ],
                    [ 1 ]
                )
        @>

    [<Fact>]
    let ``Hub: direct tryPublish reports busy while another publisher waits`` () =
        let workflow : Flow<unit, Never, HubTryPublishResult * int list> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! historian = hub |> Hub.subscribe (QueueStrategy.BackPressure 1)
                do! hub |> Hub.publish 1 |> Flow.ignore
                let! blocked = hub |> Hub.publish 2 |> Flow.fork
                do! waitUntil (fun () -> suspendedPublishers historian () = 1)
                let busy = hub |> Hub.tryPublishNow 3
                let! first = Dequeue.take historian
                do! Fiber.join blocked |> Flow.ignore
                let! rest = Dequeue.takeAll historian
                return busy, first :: rest
            }

        test <@ Flow.runSync () workflow = Exit.Success(HubTryPublishResult.Busy, [ 1; 2 ]) @>

    [<Fact>]
    let ``Hub: shutting a subscription down unsubscribes it and releases a waiting publisher`` () =
        let workflow : Flow<unit, Never, int * PublishResult * int * bool> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! historian = hub |> Hub.subscribe (QueueStrategy.BackPressure 1)
                do! hub |> Hub.publish 1 |> Flow.ignore
                let! blocked = hub |> Hub.publish 2 |> Flow.fork
                do! waitUntil (fun () -> suspendedPublishers historian () = 1)
                let! before = Hub.subscriberCount hub
                do! Dequeue.shutdown historian
                let! released = Fiber.join blocked
                let! after = Hub.subscriberCount hub
                let! finished = Dequeue.isShutdown historian
                return before - after, released, after, finished
            }

        test <@ Flow.runSync () workflow = Exit.Success(1, PublishResult.empty, 0, true) @>

    [<Fact>]
    let ``Hub: a scoped hub shuts down with its scope and wakes awaitShutdown`` () =
        let workflow : Flow<unit, Never, int list * bool> =
            flow {
                let! hub, subscription =
                    flow {
                        let! (hub: Hub<int>) = Hub.makeScoped ()
                        let! subscription = hub |> Hub.subscribe QueueStrategy.Unbounded
                        do! hub |> Hub.publishAll [ 1; 2 ] |> Flow.ignore
                        return hub, subscription
                    }
                    |> Flow.scoped

                do! Hub.awaitShutdown hub
                let! drained = subscription |> FlowStream.fromDequeue |> FlowStream.runCollect
                let! isShut = Hub.isShutdown hub
                return drained, isShut
            }

        test <@ Flow.runSync () workflow = Exit.Success([ 1; 2 ], true) @>

    [<Fact>]
    let ``Hub: closing a subscriber's scope unsubscribes it`` () =
        let workflow : Flow<unit, Never, int * int> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! _, closeFirst = hub |> subscribeInOwnScope QueueStrategy.Unbounded
                let! _, closeSecond = hub |> subscribeInOwnScope QueueStrategy.Unbounded
                let! both = Hub.subscriberCount hub
                do! closeFirst
                let! one = Hub.subscriberCount hub
                do! closeSecond
                return both, one
            }

        test <@ Flow.runSync () workflow = Exit.Success(2, 1) @>

    [<Fact>]
    let ``Hub: a late subscriber does not receive earlier values`` () =
        let workflow : Flow<unit, Never, int list> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! _ = hub |> Hub.subscribe QueueStrategy.Unbounded
                do! hub |> Hub.publishAll [ 1; 2 ] |> Flow.ignore
                let! late = hub |> Hub.subscribe QueueStrategy.Unbounded
                do! hub |> Hub.publish 3 |> Flow.ignore
                return! Dequeue.takeAll late
            }

        test <@ Flow.runSync () workflow = Exit.Success [ 3 ] @>

    [<Fact>]
    let ``Hub: shutdown drains backlogs, ends streams, and interrupts later publishes`` () =
        let workflow : Flow<unit, Never, int list * int list * bool * bool * bool> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! backlog = hub |> Hub.subscribe QueueStrategy.Unbounded
                let! streamed = hub |> Hub.subscribe QueueStrategy.Unbounded
                let! stream = streamed |> FlowStream.fromDequeue |> FlowStream.runCollect |> Flow.fork
                do! hub |> Hub.publishAll [ 1; 2; 3 ] |> Flow.ignore
                do! Hub.shutdown hub
                do! Hub.shutdown hub

                let! drained = Dequeue.takeAll backlog
                let! streamedValues = Fiber.join stream
                let! isShut = Hub.isShutdown hub
                let! latePublish = hub |> Hub.publish 4 |> Flow.fork |> Flow.bind Fiber.interrupt
                let! lateTake = Dequeue.take backlog |> Flow.fork |> Flow.bind Fiber.interrupt
                return drained, streamedValues, isShut, isInterrupted latePublish, isInterrupted lateTake
            }

        test <@ Flow.runSync () workflow = Exit.Success([ 1; 2; 3 ], [ 1; 2; 3 ], true, true, true) @>

    [<Fact>]
    let ``Hub: an interrupted subscription take never loses an element`` () =
        let attempt () : Flow<unit, Never, bool> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! subscription = hub |> Hub.subscribe QueueStrategy.Unbounded
                let! taker = Dequeue.take subscription |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers subscription () = 1)
                let! interrupted, _ = Flow.zipPar (Fiber.interrupt taker) (hub |> Hub.publish 7)
                let! remaining = Dequeue.poll subscription

                return
                    match interrupted, remaining with
                    | Exit.Success 7, None -> true
                    | exit, Some 7 when isInterrupted exit -> true
                    | _ -> false
            }

        let workflow = List.init 1000 (fun _ -> attempt ()) |> Flow.sequence
        test <@ Flow.runSync () workflow |> Exit.map (List.forall id) = Exit.Success true @>

    [<Fact>]
    let ``Hub: a non-positive subscription capacity is a defect`` () =
        let workflow : Flow<unit, Never, Dequeue<int>> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                return! hub |> Hub.subscribe (QueueStrategy.BackPressure 0)
            }

        match Flow.runSync () workflow with
        | Exit.Failure(Cause.Die error) -> test <@ error :? ArgumentOutOfRangeException @>
        | other -> failwith $"Expected a defect, got {other}"
