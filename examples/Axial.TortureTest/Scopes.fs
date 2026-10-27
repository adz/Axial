/// Nested scopes and forked children acquiring every kind of resource, then failing, dying, or being interrupted.
module Axial.TortureTest.Scopes

open System
open System.Threading
open Axial
open Axial.State
open Axial.TortureTest.Scenario

/// What happened to one resource, in the order it happened.
type private Event =
    | Acquired of scope: int * id: int
    | Released of scope: int * id: int

type private Disposable(onDispose: unit -> unit) =
    interface IDisposable with
        member _.Dispose() = onDispose ()

#if !FABLE_COMPILER
    interface IAsyncDisposable with
        member _.DisposeAsync() =
            onDispose ()
            Tasks.ValueTask()
#endif

let private throwingObserver (starts: int ref) (ends: int ref) =
    { FiberObserver.none with
        OnStart = fun _ -> increment starts |> ignore; raise (InvalidOperationException "observer start")
        OnEnd = fun _ _ -> increment ends |> ignore; raise (InvalidOperationException "observer end") }

// Gates force interruption into suspended cleanup rather than relying on a scheduling delay.
let private interruptedCleanup () : Flow<ClockEnvironment, Never, Check list> =
    [ 0; 1; 2 ]
    |> Flow.traverse (fun mode ->
        flow {
            let! started = Deferred.make<ClockEnvironment, Never, unit> ()
            let! resume = Deferred.make<ClockEnvironment, Never, unit> ()
            let! acquired = Deferred.make<ClockEnvironment, Never, unit> ()
            let! finished = Ref.make 0
            let starts, ends = ref 0, ref 0
            let observer = throwingObserver starts ends
            let cleanup =
                flow {
                    do! Deferred.succeed () started |> Flow.ignore
                    do! Deferred.await resume
                    do! Flow.ensureNotCanceled
                    do! finished |> Ref.update ((+) 1)
                }
            let resource : Flow<ClockEnvironment, string, unit> =
                flow {
                    let! _ = Flow.scopeAcquireRelease (Flow.ok ()) (fun _ -> cleanup)
                    if mode = 2 then
                        do! Deferred.succeed () acquired |> Flow.ignore
                        return! Flow.never
                    else
                        return! Flow.fail "original"
                }
                |> Flow.scoped
            let source =
                match mode with
                | 0 -> Flow.fail "original" |> Flow.ensuring cleanup
                | 1 -> resource
                | _ -> Flow.race resource (Deferred.await acquired |> Flow.widenError)
            let! owner = source |> Flow.fork |> Flow.withFiberObserver observer
            let bounded source =
                source |> Flow.timeoutWith (TimeSpan.FromSeconds 10.0) (fun () -> Flow.die (TimeoutException "cleanup gate"))
            let! waiting =
                flow {
                    do! Deferred.await started |> bounded
                    do! Fiber.requestInterrupt owner
                    return! Fiber.poll owner
                }
                |> Flow.ensuring (Deferred.succeed () resume |> Flow.ignore)
            let! exit = Fiber.awaitExit owner |> bounded
            let! completed = Ref.get finished
            let preserved =
                if mode = 2 then exit = Exit.Success ()
                else exit = Exit.Failure(Cause.Fail "original")
            return
                [ check $"cleanup {mode}: caller interruption waited for suspended cleanup" (waiting.IsNone && completed = 1)
                  check $"cleanup {mode}: throwing observers preserved the original exit" (preserved && starts.Value = ends.Value && starts.Value > 0) ]
        })
    |> Flow.map List.concat

// <snippet:torture-scopes>
let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    flow {
        let events = ResizeArray<Event>()
        let nextId = ref 0
        let fresh () = increment nextId
        let record event = lock events (fun () -> events.Add event)
        let started = ref 0
        let finished = ref 0
        let interrupted = ref 0

        // Acquires one resource in the current scope, in one of the ways Axial offers, and logs its release.
        let acquire (scope: int) : Flow<Axial.ClockEnvironment, string, unit> =
            let id = fresh ()
            let release () = record (Released(scope, id))
            let acquired value = Flow.delay (fun () -> record (Acquired(scope, id)); Flow.ok value)

            match round.Next 8 with
            | 0 ->
                let resource = Resource.create (acquired id) (fun _ -> finalizer release)

                // Acquired here or by a fiber; either way the release belongs to this scope.
                if round.Chance 50 then
                    Flow.scopeResource resource |> Flow.ignore
                else
                    Flow.forkResource resource |> Flow.bind Fiber.join |> Flow.ignore
            | 1 -> acquired () |> Flow.bind (fun () -> Flow.scopeFinalizer (finalizer release))
            | 2 -> acquired () |> Flow.bind (fun () -> Flow.scopeResource (Resource.finalizer (finalizer release)))
            | 3 ->
                Flow.scope
                |> Flow.bind (fun current ->
                    acquired ()
                    |> Flow.map (fun () ->
                        current.AddFinalizer(fun _ ->
                            release ()
#if FABLE_COMPILER
                            async.Return()))
#else
                            Tasks.Task.CompletedTask))
#endif
                )
            | 4 -> Flow.scopeAcquireRelease (acquired id) (fun _ -> finalizer release) |> Flow.ignore
            // A release that reads the environment it was acquired with.
            | 5 ->
                Flow.scopeAcquireRelease (acquired id) (fun _ ->
                    Flow.envWith (fun (env: ClockEnvironment) -> env.Clock) |> Flow.bind (fun _ -> finalizer release))
                |> Flow.ignore
#if FABLE_COMPILER
            | _ -> Flow.scopeResource (Resource.create (acquired id) (fun _ -> finalizer release)) |> Flow.ignore
#else
            | 6 -> acquired (new Disposable(release)) |> Flow.bind (fun resource -> Flow.scopeDisposable resource)
            | _ ->
                match round.Next 2 with
                | 0 -> acquired (new Disposable(release)) |> Flow.bind (fun resource -> Flow.scopeAsyncDisposable resource)
                | _ -> Flow.scopeResource (Resource.create (acquired id) (fun _ -> finalizer release)) |> Flow.ignore
#endif

        // A scope acquires a few resources, then may open child scopes, fork children (joined, interrupted, or left
        // running for the scope to interrupt), fail, or die. Every level counts itself in and out.
        let rec nest depth : Flow<Axial.ClockEnvironment, string, unit> =
            flow {
                let scope = fresh ()

                for _ in 1 .. round.Next 4 do
                    do! acquire scope

                if depth < 3 then
                    for _ in 1 .. round.Next 3 do
                        match round.Next 4 with
                        | 0 -> do! nest (depth + 1) |> Flow.orElse (Flow.ok ())
                        | 1 ->
                            let! child = nest (depth + 1) |> Flow.fork
                            let! _ = Fiber.awaitExit child
                            ()
                        | 2 ->
                            let! child = nest (depth + 1) |> Flow.fork
                            do! Flow.sleep (TimeSpan.FromMilliseconds(float (round.Next 2)))
                            let! _ = Fiber.interrupt child
                            ()
                        | _ ->
                            // Left running: closing this scope interrupts it.
                            let! _ = nest (depth + 1) |> Flow.fork
                            ()

                do! Flow.sleep (TimeSpan.FromMilliseconds(float (round.Next 2)))
                if round.Chance 10 then return! Flow.fail "boom"
                if round.Chance 5 then return! Flow.die (InvalidOperationException "crash")
            }
            |> Flow.scoped
            |> Flow.onInterrupt (Flow.delay (fun () -> increment interrupted |> ignore; Flow.ok ()))
            |> Flow.ensuring (Flow.delay (fun () -> increment finished |> ignore; Flow.ok ()))
            |> fun body -> Flow.delay (fun () -> increment started |> ignore; body)

        // The whole tree runs as a fiber that is sometimes interrupted part way.
        let! root = nest 0 |> Flow.fork
        if round.Chance 50 then do! Flow.sleep (TimeSpan.FromMilliseconds(float (round.Next 4)))
        let! _ = if round.Chance 40 then Fiber.interrupt root else Fiber.awaitExit root

        let log = lock events (fun () -> List.ofSeq events)
        let acquiredIds = log |> List.choose (function Acquired(_, id) -> Some id | _ -> None)
        let releasedIds = log |> List.choose (function Released(_, id) -> Some id | _ -> None)

        // Within one scope, resources are released in the reverse of the order they were acquired.
        let reverseOrderPerScope =
            log
            |> List.groupBy (function Acquired(scope, _) | Released(scope, _) -> scope)
            |> List.forall (fun (_, scopeEvents) ->
                let acquiredOrder = scopeEvents |> List.choose (function Acquired(_, id) -> Some id | _ -> None)
                let releasedOrder = scopeEvents |> List.choose (function Released(_, id) -> Some id | _ -> None)
                releasedOrder = List.rev acquiredOrder)

        let releasedAfterAcquired =
            let position event = log |> List.tryFindIndex ((=) event)
            log |> List.forall (function
                | Released(scope, id) -> position (Acquired(scope, id)) < position (Released(scope, id))
                | Acquired _ -> true)

        let! cleanupChecks = interruptedCleanup ()

        return
            cleanupChecks @
            [ check "every acquired resource was released exactly once" (List.sort acquiredIds = List.sort releasedIds && List.distinct releasedIds = releasedIds)
              check "each scope released its resources in reverse order" reverseOrderPerScope
              check "every resource was released after it was acquired" releasedAfterAcquired
              check "every scope that started ran its ensuring finalizer exactly once" (started.Value = finished.Value)
              check "no more scopes were interrupted than started" (interrupted.Value <= started.Value) ]
    }
// </snippet:torture-scopes>

let scenario : Scenario =
    { Name = "scopes"
      Title = "Scopes: nested scopes and forked children with every kind of resource, failing and interrupted"
      Run = run }
