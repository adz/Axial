namespace Axial.Tests

open System
open Axial
open Swensen.Unquote
open Xunit

module WorkflowHubTests =
    let private suspendedPublishers (subscription: Subscription<'a>) () =
        Platform.lock subscription.Queue.Gate (fun () -> subscription.Queue.Offerers.Count)

    let private suspendedTakers (subscription: Subscription<'a>) () =
        Platform.lock subscription.Queue.Gate (fun () -> subscription.Queue.Takers.Count)

    /// Polls a condition on internals so a test can act once a fiber is known to be suspended.
    let private waitUntil (condition: unit -> bool) : Flow<unit, 'error, unit> =
        flow {
            let mutable remaining = 5000

            while not (condition ()) && remaining > 0 do
                do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 1.0)
                remaining <- remaining - 1
        }

    let private isInterrupted (exit: Exit<'value, 'error>) =
        match exit with
        | Exit.Failure cause -> Cause.isInterrupted cause
        | Exit.Success _ -> false

    /// Subscribes in a scope owned by a forked fiber. Returns the subscription and a flow that closes that scope.
    let private subscribeInOwnScope strategy (hub: Hub<'a>) : Flow<unit, 'error, Subscription<'a> * Flow<unit, 'error, unit>> =
        flow {
            let! handedOut = Deferred.make<unit, 'error, Subscription<'a>> ()
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
                    do! Flow.join owner
                }

            return subscription, closeScope
        }

    [<Fact>]
    let ``Hub: every subscriber sees every value in the same order`` () =
        let workflow : Flow<unit, Never, (int * int) list list> =
            flow {
                let! (hub: Hub<int * int>) = Hub.make ()
                let! subscriptions = [ 1..8 ] |> Flow.traverse (fun _ -> hub |> Hub.subscribe SubscriberStrategy.Unbounded)

                // Each publisher yields to the thread pool before every publish, so publishes genuinely overlap.
                let publisher id =
                    flow {
                        for index in 1..500 do
                            do! Flow.fromTask (fun _ -> task { do! System.Threading.Tasks.Task.Yield() })
                            do! hub |> Hub.publish (id, index) |> Flow.ignore
                    }

                do! [ publisher 1; publisher 2; publisher 3; publisher 4 ] |> Flow.sequencePar |> Flow.ignore
                return! subscriptions |> Flow.traverse Subscription.takeAll
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
                let! display = hub |> Hub.subscribe (SubscriberStrategy.Sliding 2)
                let! result = hub |> Hub.publishAll [ 1..10 ]
                let! kept = Subscription.takeAll display
                return result, kept
            }

        test <@ Flow.runSync () workflow = Exit.Success({ Delivered = 10; Dropped = 0 }, [ 9; 10 ]) @>

    [<Fact>]
    let ``Hub: a slow dropping subscriber counts its drops without delaying the publisher`` () =
        let workflow : Flow<unit, Never, PublishResult * int list> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! display = hub |> Hub.subscribe (SubscriberStrategy.Dropping 2)
                let! result = hub |> Hub.publishAll [ 1..5 ]
                let! kept = Subscription.takeAll display
                return result, kept
            }

        test <@ Flow.runSync () workflow = Exit.Success({ Delivered = 2; Dropped = 3 }, [ 1; 2 ]) @>

    [<Fact>]
    let ``Hub: a full back-pressure subscriber suspends publish until drained or closed`` () =
        let workflow : Flow<unit, Never, PublishResult * PublishResult * int> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! (historian: Subscription<int>), closeHistorian = hub |> subscribeInOwnScope (SubscriberStrategy.BackPressure 1)

                do! hub |> Hub.publish 1 |> Flow.ignore
                let! blocked = hub |> Hub.publish 2 |> Flow.fork
                do! waitUntil (fun () -> suspendedPublishers historian () = 1)
                let! _ = Subscription.take historian
                let! afterDrain = Flow.join blocked

                let! blockedAgain = hub |> Hub.publish 3 |> Flow.fork
                do! waitUntil (fun () -> suspendedPublishers historian () = 1)
                do! closeHistorian
                let! afterClose = Flow.join blockedAgain
                let! remaining = Hub.subscriberCount hub
                return afterDrain, afterClose, remaining
            }

        test <@ Flow.runSync () workflow = Exit.Success({ Delivered = 1; Dropped = 0 }, { Delivered = 0; Dropped = 0 }, 0) @>

    [<Fact>]
    let ``Hub: a lossless historian gets everything while lossy displays never block the publisher`` () =
        let workflow : Flow<unit, Never, int list * int list * int list * int> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! historian = hub |> Hub.subscribe (SubscriberStrategy.BackPressure 4)
                let! display = hub |> Hub.subscribe (SubscriberStrategy.Sliding 1)
                let! alarms = hub |> Hub.subscribe (SubscriberStrategy.Dropping 1)
                let! recorded = historian |> FlowStream.fromSubscription |> FlowStream.runCollect |> Flow.fork

                let! results = [ 1..100 ] |> Flow.traverse (fun reading -> hub |> Hub.publish reading)
                do! Hub.shutdown hub

                let! history = Flow.join recorded
                let! latest = Subscription.takeAll display
                let! firstAlarm = Subscription.takeAll alarms
                return history, latest, firstAlarm, results |> List.sumBy _.Dropped
            }

        test <@ Flow.runSync () workflow = Exit.Success([ 1..100 ], [ 100 ], [ 1 ], 99) @>

    [<Fact>]
    let ``Hub: closing a subscriber's scope unsubscribes it`` () =
        let workflow : Flow<unit, Never, int * int> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! _, closeFirst = hub |> subscribeInOwnScope SubscriberStrategy.Unbounded
                let! _, closeSecond = hub |> subscribeInOwnScope SubscriberStrategy.Unbounded
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
                let! _ = hub |> Hub.subscribe SubscriberStrategy.Unbounded
                do! hub |> Hub.publishAll [ 1; 2 ] |> Flow.ignore
                let! late = hub |> Hub.subscribe SubscriberStrategy.Unbounded
                do! hub |> Hub.publish 3 |> Flow.ignore
                return! Subscription.takeAll late
            }

        test <@ Flow.runSync () workflow = Exit.Success [ 3 ] @>

    [<Fact>]
    let ``Hub: shutdown drains backlogs, ends streams, and interrupts later publishes`` () =
        let workflow : Flow<unit, Never, int list * int list * bool * bool * bool> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! backlog = hub |> Hub.subscribe SubscriberStrategy.Unbounded
                let! streamed = hub |> Hub.subscribe SubscriberStrategy.Unbounded
                let! stream = streamed |> FlowStream.fromSubscription |> FlowStream.runCollect |> Flow.fork
                do! hub |> Hub.publishAll [ 1; 2; 3 ] |> Flow.ignore
                do! Hub.shutdown hub
                do! Hub.shutdown hub

                let! drained = Subscription.takeAll backlog
                let! streamedValues = Flow.join stream
                let! isShut = Hub.isShutdown hub
                let! latePublish = hub |> Hub.publish 4 |> Flow.fork |> Flow.bind Flow.interrupt
                let! lateTake = Subscription.take backlog |> Flow.fork |> Flow.bind Flow.interrupt
                return drained, streamedValues, isShut, isInterrupted latePublish, isInterrupted lateTake
            }

        test <@ Flow.runSync () workflow = Exit.Success([ 1; 2; 3 ], [ 1; 2; 3 ], true, true, true) @>

    [<Fact>]
    let ``Hub: an interrupted subscription take never loses an element`` () =
        let attempt () : Flow<unit, Never, bool> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                let! subscription = hub |> Hub.subscribe SubscriberStrategy.Unbounded
                let! taker = Subscription.take subscription |> Flow.fork
                do! waitUntil (fun () -> suspendedTakers subscription () = 1)
                let! interrupted, _ = Flow.zipPar (Flow.interrupt taker) (hub |> Hub.publish 7)
                let! remaining = Subscription.poll subscription

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
        let workflow : Flow<unit, Never, Subscription<int>> =
            flow {
                let! (hub: Hub<int>) = Hub.make ()
                return! hub |> Hub.subscribe (SubscriberStrategy.BackPressure 0)
            }

        match Flow.runSync () workflow with
        | Exit.Failure(Cause.Die error) -> test <@ error :? ArgumentOutOfRangeException @>
        | other -> failwith $"Expected a defect, got {other}"
