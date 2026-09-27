/// Fibers: latest-wins slots, inherited annotations and environment, and the diagnostics that watch them.
module Axial.TortureTest.Fibers

open System
open System.Threading
open Axial
open Axial.State
open Axial.TortureTest.Scenario

type private Tenant = { Tenant: string }

let private settledOk (exit: Exit<'value, 'error> option) =
    match exit with
    | Some(Exit.Success _) -> true
    | Some exit -> isInterrupted exit
    | None -> false

// <snippet:torture-fibers>
let run (round: Round) : Flow<unit, Never, Check list> =
    let requests = round.Size 60

    flow {
        // Latest wins: each request replaces the one before it. Only the last is sure to finish; every other one
        // either finished first or was interrupted, and every one of them cleaned up.
        let! cleanedUp = Ref.make 0
        let! slot = FiberSlot.make ()

        let search query =
            flow {
                do! Flow.sleep (TimeSpan.FromMilliseconds(float (round.Next 3)))
                return query
            }
            |> Flow.ensuring (cleanedUp |> Ref.update ((+) 1))

        let! searches = [ 1..requests ] |> Flow.traverse (fun query -> slot |> Flow.forkReplacing <| search query)
        let! lastResult = searches |> List.last |> Fiber.join
        do! FiberSlot.interrupt slot
        let! searchExits = searches |> Flow.traverse Fiber.await
        let! polled = searches |> Flow.traverse Fiber.poll
        let! cleaned = Ref.get cleanedUp

        // Keyed slots: the same rule per key, and interruptAll leaves nothing running.
        let! keyed = FiberSlot.makeKeyed ()

        let! previews =
            [ for index in 1..requests -> index % 5, index ]
            |> Flow.traverse (fun (key, index) -> keyed |> Flow.forkReplacingKey key <| search index)

        let lastPerKey = [ for key in 0..4 -> previews |> List.indexed |> List.filter (fun (index, _) -> (index + 1) % 5 = key) |> List.last |> snd ]
        let! lastPreviews = lastPerKey |> Flow.traverse Fiber.await
        do! FiberSlot.interruptAll keyed
        let! remaining = FiberSlot.count keyed
        let! previewPolls = previews |> Flow.traverse Fiber.poll

        // Annotations, the trace id, and the environment are inherited by forked fibers, and siblings do not see
        // each other's annotations.
        // Observer and sink callbacks are synchronous, so they count with plain atomic counters.
        let sinkCount = ref 0

        let! inherited =
            [ for worker in 1..10 ->
                  flow {
                      let! annotations = Flow.annotations
                      let! trace = Flow.traceId
                      let! id = Flow.fiberId
                      let! tenant = Flow.envWith _.Tenant
                      return worker, annotations, trace, id, tenant
                  }
                  |> Flow.annotate "worker" (string worker)
                  |> Flow.fork ]
            |> Flow.sequence
            |> Flow.bind (Flow.traverse Fiber.join)
            |> Flow.annotate "request" "r1"
            |> Flow.withTraceId "trace-1"
            // addAnnotationSink composes with the sink installed outside it; withAnnotationSink replaces it.
            |> Flow.addAnnotationSink (fun _ _ -> increment sinkCount |> ignore)
            |> Flow.withAnnotationSink (fun _ _ -> ())
            |> Flow.localEnv (fun () -> { Tenant = "acme" })

        let annotationsHeld =
            inherited
            |> List.forall (fun (worker, annotations, trace, _, tenant) ->
                annotations.TryFind "request" = Some "r1"
                && annotations.TryFind "worker" = Some(string worker)
                && trace = Some "trace-1"
                && tenant = "acme")

        let distinctIds = inherited |> List.map (fun (_, _, _, id, _) -> id) |> List.distinct |> List.length
        let annotated = sinkCount.Value

        return
            [ check "the last request's result is delivered" (lastResult = requests)
              check "every replaced request finished or was interrupted, and none is left running" (polled |> List.forall settledOk)
              check "every request cleaned up" (cleaned = requests && searchExits.Length = requests)
              check "the last preview per key finished, and interruptAll left none running" (lastPreviews |> List.forall (function Exit.Success _ -> true | _ -> false) && remaining = 0 && previewPolls |> List.forall settledOk)
              check "forked fibers inherit annotations, the trace id, and the environment, but not their siblings' annotations" annotationsHeld
              check "every fiber has its own id" (distinctIds = 10)
              check "annotation sinks saw the annotations" (annotated >= 10) ]
    }

/// The fiber registry and observers under a burst of named fibers that succeed, fail, and are interrupted.
let diagnostics (round: Round) : Flow<unit, Never, Check list> =
    let workers = round.Size 40

    flow {
        let registry = FiberRegistry(10)
        let starts = ref 0
        let ends = ref 0
        let startDumps = ResizeArray<FiberDump>()

        let counting =
            { FiberObserver.none with
                OnStart =
                    fun metadata ->
                        increment starts |> ignore
                        lock startDumps (fun () -> startDumps.Add(FiberDump.ofMetadata metadata))
                OnEnd = fun _ _ -> increment ends |> ignore }

        let body =
            flow {
                let! fibers =
                    [ for index in 1..workers ->
                          match index % 3 with
                          | 0 -> Flow.sleep (TimeSpan.FromMinutes 5.0) |> Flow.forkNamed "sleeper" |> Flow.map Choice1Of2
                          | 1 -> (Flow.fail "expected" : Flow<unit, string, unit>) |> Flow.forkNamed "failer" |> Flow.map Choice2Of2
                          | _ -> Flow.ok () |> Flow.forkNamed "worker" |> Flow.map Choice1Of2 ]
                    |> Flow.sequence

                let sleepers = fibers |> List.choose (function Choice1Of2 fiber when (Fiber.dump fiber).Name = Some "sleeper" -> Some fiber | _ -> None)
                let firstSleeper = List.head sleepers
                let interruptedOne = registry.Interrupt (Fiber.dump firstSleeper).Id
                let interruptedByName = registry.InterruptByName "sleeper"
                let! exits = fibers |> Flow.traverse (function Choice1Of2 fiber -> Fiber.await fiber |> Flow.map ignore | Choice2Of2 fiber -> Fiber.await fiber |> Flow.map ignore)

                // A discarded fiber that dies is reported when its scope closes; a detached one is not.
                do!
                    flow {
                        let! _ = Flow.die (InvalidOperationException "lost") |> Flow.fork
                        let! _ = Flow.die (InvalidOperationException "deliberate") |> Flow.forkDetached
                        do! Flow.sleep (TimeSpan.FromMilliseconds 5.0)
                    }
                    |> Flow.scoped

                return interruptedOne, interruptedByName, exits.Length
            }

        let! interruptedOne, interruptedByName, settled =
            body
            |> Flow.withFiberRegistry registry
            |> Flow.withFiberObserver counting
            |> Flow.addFiberObserver (FiberObserver.compose FiberObserver.none FiberObserver.none)

        let stats = registry.Stats() |> List.map (fun stats -> stats.Name, stats) |> Map.ofList
        let sleepers = workers / 3
        let started, ended = starts.Value, ends.Value
        let renderedAt = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        let dump = registry.DumpAt renderedAt
        let observedDumps = lock startDumps (fun () -> List.ofSeq startDumps)
        let tree = FiberDump.renderTreeAt renderedAt observedDumps
        let lines = observedDumps |> List.map (FiberDump.renderAt renderedAt)

        return
            // The fiber interrupted by id may not have settled yet when InterruptByName runs, so it can be counted twice.
            [ check "interrupting by id and by name reached every sleeper" (interruptedOne && interruptedByName >= sleepers - 1)
              check "the registry counted every named fiber and how each ended" (stats["sleeper"].Interrupted = sleepers && stats["failer"].Failed = stats["failer"].Count && settled = workers)
              check "nothing is live afterwards, and history stays within its capacity" (registry.LiveFiberCount = 0 && registry.Snapshot().IsEmpty && registry.Settled().Length <= registry.HistoryCapacity)
              check "the registry saw every fork, and dumps render" (registry.StartedCount >= int64 workers && not (isNull dump) && not (isNull (registry.Dump())))
              check "the observer saw a dump of every start, and each renders" (observedDumps.Length = started && lines |> List.forall (fun line -> tree.Contains line) && lines |> List.exists (fun line -> line.Contains "\"sleeper\""))
              check "a discarded fiber's defect was reported, a detached one's was not" (registry.UnobservedDefects() |> List.map _.Defect |> List.exists (fun text -> text.Contains "lost") && not (registry.UnobservedDefects() |> List.exists (fun defect -> defect.Defect.Contains "deliberate")))
              check "the observer saw every fiber start and end" (started = ended && started >= workers) ]
    }
// </snippet:torture-fibers>

let scenario : Scenario =
    { Name = "fibers"
      Title = "Fibers: latest-wins slots, inherited context, the fiber registry and observers"
      Run = fun round -> Flow.map2 (@) (run round) (diagnostics round) }
