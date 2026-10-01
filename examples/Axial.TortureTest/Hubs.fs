/// One publisher, subscribers joining and leaving throughout, and every publish style.
module Axial.TortureTest.Hubs

open System
open Axial
open Axial.TortureTest.Scenario

// <snippet:torture-hubs>
let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    let values = round.Size 2000
    let churners = round.Size 40

    let strategies =
        [| QueueStrategy.Unbounded; QueueStrategy.BackPressure 8; QueueStrategy.Sliding 2; QueueStrategy.Dropping 2 |]

    let isLossless strategy =
        match strategy with
        | QueueStrategy.Unbounded
        | QueueStrategy.BackPressure _ -> true
        | _ -> false

    flow {
        let! (feed: Hub<int>) = Hub.make ()
        let totals = ref PublishResult.empty
        let add result = lock totals (fun () -> totals.Value <- PublishResult.add totals.Value result)
        let visits = ResizeArray<QueueStrategy * int list * QueueStats>()

        // Two subscribers stay for the whole run: one reads its subscription directly, one through FlowStream.fromHub.
        let! lifetime = feed |> Hub.subscribe (QueueStrategy.BackPressure 16)
        let! direct = lifetime |> FlowStream.fromDequeue |> FlowStream.runCollect |> Flow.fork
        let! streamed = feed |> FlowStream.fromHub QueueStrategy.Unbounded |> FlowStream.runCollect |> Flow.fork

        // FlowStream.fromHub subscribes when its stream starts; wait for both before publishing.
        let mutable subscribers = 0

        while subscribers <> 2 do
            let! current = Hub.subscriberCount feed
            subscribers <- current

            if current <> 2 then
                do! Flow.sleep (TimeSpan.FromMilliseconds 1.0)

        // Short-lived subscribers join at random, read a few values, and leave by shutting their subscription down
        // or by closing their scope.
        let visitor () =
            flow {
                let strategy = strategies[round.Next strategies.Length]
                let quota = round.Next 40 + 1

                let! visit =
                    flow {
                        let! subscription = feed |> Hub.subscribe strategy
                        let seen = ResizeArray<int>()
                        let mutable running = true

                        while running && seen.Count < quota do
                            let! next = subscription |> Dequeue.take |> exitOf

                            match next with
                            | Exit.Success value -> seen.Add value
                            | Exit.Failure _ -> running <- false

                        if round.Chance 50 then
                            do! Dequeue.shutdown subscription

                        // Leave for good before reading the counters, so they are final.
                        do! Dequeue.shutdown subscription
                        let! unread = Dequeue.takeAll subscription
                        let! stats = Dequeue.stats subscription
                        return strategy, List.ofSeq seen @ unread, stats
                    }
                    |> Flow.scoped

                lock visits (fun () -> visits.Add visit)
            }

        let publisher =
            flow {
                let mutable next = 1

                while next <= values do
                    match round.Next 4 with
                    | 0 ->
                        let! result = feed |> Hub.publish next
                        add result
                        next <- next + 1
                    | 1 ->
                        let batch = [ next .. min values (next + round.Next 5) ]
                        let! result = feed |> Hub.publishAll batch
                        add result
                        next <- next + batch.Length
                    | 2 ->
                        // tryPublish delivers to every subscription or to none; publish anyway if it refused.
                        let! attempt = feed |> Hub.tryPublish next

                        match attempt with
                        | Some result -> add result
                        | None ->
                            let! result = feed |> Hub.publish next
                            add result

                        next <- next + 1
                    | 3 ->
                        match feed |> Hub.tryPublishNow next with
                        | HubTryPublishResult.Published result -> add result
                        | HubTryPublishResult.Busy
                        | HubTryPublishResult.Full ->
                            let! result = feed |> Hub.publish next
                            add result
                        | HubTryPublishResult.Shutdown ->
                            return! Flow.die (InvalidOperationException "Publisher found hub shut down early")

                        next <- next + 1
                    | _ -> return! Flow.die (InvalidOperationException "Invalid publisher choice")
            }

        // Visitors still waiting when the publisher finishes are released by the shutdown and drain what they have.
        let! churn = [ for _ in 1..churners -> visitor () ] |> Flow.sequencePar |> Flow.fork
        do! publisher
        do! Hub.shutdown feed
        do! Fiber.join churn |> Flow.ignore
        do! Hub.awaitShutdown feed
        let! isShut = Hub.isShutdown feed
        let! directValues = Fiber.join direct
        let! streamedValues = Fiber.join streamed

        // The lifetime subscription is shut down with the hub, so its counters are final too.
        let! lifetimeStats = Dequeue.stats lifetime
        let! streamedAfter = Hub.subscriberCount feed

        let visited = List.ofSeq visits
        let consecutive (seen: int list) = seen |> List.pairwise |> List.forall (fun (a, b) -> b = a + 1)
        let increasing (seen: int list) = seen |> List.pairwise |> List.forall (fun (a, b) -> b > a)

        let subscriptionTotal =
            visited
            |> List.map (fun (_, _, stats) -> stats)
            |> List.append [ lifetimeStats ]
            |> List.fold
                (fun (accepted, dropped, evicted) stats -> accepted + stats.Accepted, dropped + stats.Dropped, evicted + stats.Evicted)
                (0L, 0L, 0L)

        // The streamed subscription is not visible here; its values are all accepted, so add them.
        let accepted, dropped, evicted = subscriptionTotal
        let accepted = accepted + int64 streamedValues.Length

        return
            [ check "the lifetime subscribers each saw every value, in order" (directValues = [ 1..values ] && streamedValues = [ 1..values ])
              check
                  "every lossless visitor saw an unbroken run of values"
                  (visited |> List.forall (fun (strategy, seen, _) -> not (isLossless strategy) || consecutive seen))
              check "every lossy visitor saw values in publish order" (visited |> List.forall (fun (_, seen, _) -> increasing seen))
              check
                  "publish results add up to what the subscriptions accepted, dropped, and evicted"
                  (int64 totals.Value.Delivered = accepted
                   && int64 totals.Value.Dropped = dropped
                   && int64 totals.Value.Evicted = evicted)
              check "shutdown ended the streams and emptied the hub" (isShut && streamedAfter = 0) ]
    }

/// A hub created under a scope is shut down when the scope closes, and its subscribers drain what is left.
let runScoped (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    flow {
        let values = round.Size 200

        // The subscription belongs to the same scope, so it is shut down too, but keeps its backlog for draining.
        let! hub, subscription =
            flow {
                let! (hub: Hub<int>) = Hub.makeScoped ()
                let! subscription = hub |> Hub.subscribe QueueStrategy.Unbounded
                do! hub |> Hub.publishAll [ 1 .. values / 2 ] |> Flow.ignore
                do! FlowStream.fromSeq [ values / 2 + 1 .. values ] |> FlowStream.runIntoHub hub
                return hub, subscription
            }
            |> Flow.scoped

        let! isShut = Hub.isShutdown hub
        let! seen = subscription |> FlowStream.fromDequeue |> FlowStream.runCollect
        return [ check "a scoped hub shuts down with its scope, and its subscriber drains everything" (isShut && seen = [ 1..values ]) ]
    }
// </snippet:torture-hubs>

let scenario : Scenario =
    { Name = "hubs"
      Title = "Hub churn: subscribers joining and leaving while every publish style runs"
      Run = fun round -> Flow.map2 (@) (run round) (runScoped round) }
