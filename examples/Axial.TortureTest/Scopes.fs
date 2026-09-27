/// Nested scopes and forked children acquiring every kind of resource, then failing, dying, or being interrupted.
module Axial.TortureTest.Scopes

open System
open System.Threading
open Axial
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

// <snippet:torture-scopes>
let run (round: Round) : Flow<unit, Never, Check list> =
    flow {
        let events = ResizeArray<Event>()
        let nextId = ref 0
        let fresh () = increment nextId
        let record event = lock events (fun () -> events.Add event)
        let started = ref 0
        let finished = ref 0
        let interrupted = ref 0

        // Acquires one resource in the current scope, in one of the ways Axial offers, and logs its release.
        let acquire (scope: int) : Flow<unit, string, unit> =
            let id = fresh ()
            let release () = record (Released(scope, id))
            let acquired value = Flow.delay (fun () -> record (Acquired(scope, id)); Flow.ok value)

            match round.Next 8 with
            | 0 -> Flow.scopeResource (Resource.ofAsync (acquired id) (fun _ _ -> async { release () })) |> Flow.ignore
            | 1 -> acquired () |> Flow.bind (fun () -> Flow.scopeAsyncFinalizer (fun _ -> async { release () }))
            | 2 -> acquired () |> Flow.bind (fun () -> Flow.scopeResource (Resource.asyncFinalizer (fun _ -> async { release () })))
            | 3 -> Flow.scope |> Flow.bind (fun current -> acquired () |> Flow.map (fun () -> current.AddFinalizer(finalizer release)))
#if FABLE_COMPILER
            | _ -> Flow.scopeResource (Resource.ofAsync (acquired id) (fun _ _ -> async { release () })) |> Flow.ignore
#else
            | 4 -> Flow.scopeAcquireRelease (acquired id) (fun _ _ -> release (); Tasks.Task.CompletedTask) |> Flow.ignore
            | 5 -> acquired () |> Flow.bind (fun () -> Flow.scopeFinalizer (finalizer release))
            | 6 -> acquired (new Disposable(release)) |> Flow.bind (fun resource -> Flow.scopeDisposable resource)
            | _ ->
                match round.Next 3 with
                | 0 -> acquired (new Disposable(release)) |> Flow.bind (fun resource -> Flow.scopeAsyncDisposable resource)
                | 1 -> Flow.scopeResource (Resource.create (acquired id) (fun _ _ -> release (); Tasks.Task.CompletedTask)) |> Flow.ignore
                | _ -> acquired () |> Flow.bind (fun () -> Flow.scopeResource (Resource.finalizer (finalizer release)))
#endif

        // A scope acquires a few resources, then may open child scopes, fork children (joined, interrupted, or left
        // running for the scope to interrupt), fail, or die. Every level counts itself in and out.
        let rec nest depth : Flow<unit, string, unit> =
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
                            let! _ = Fiber.await child
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
        let! _ = if round.Chance 40 then Fiber.interrupt root else Fiber.await root

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

        return
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
