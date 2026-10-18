namespace Axial.Tests

open System
open System.Threading.Tasks
open Axial
open Swensen.Unquote
open Xunit

/// Who owns a scoped resource acquired inside a concurrent combinator: a branch whose value the caller receives
/// hands its scoped resources to the caller's scope; any other branch releases them at once; a forked fiber releases
/// its own when it settles, and `Flow.forkResource` acquires in a fiber for the caller.
module ScopedResourceOwnershipTests =
    let private record (events: ResizeArray<string>) (event: string) = lock events (fun () -> events.Add event)

    let private snapshot (events: ResizeArray<string>) = lock events (fun () -> List.ofSeq events)

    let private released (events: ResizeArray<string>) =
        snapshot events |> List.filter (fun event -> event.EndsWith ":released") |> List.sort

    let private connection (events: ResizeArray<string>) (name: string) : Resource<ClockEnvironment, string, string> =
        Resource.create
            (Flow.delay (fun () ->
                record events $"{name}:acquired"
                Flow.ok name))
            (fun _ ->
                Flow.delay (fun () ->
                    record events $"{name}:released"
                    Flow.succeed ()))

    let private acquire events name = connection events name |> Flow.scopeResource

    /// Waits on the real clock until `condition` holds, for work the combinator does not wait for (a race loser).
    let rec private eventually (condition: unit -> bool) (attempts: int) : Flow<ClockEnvironment, string, unit> =
        flow {
            if not (condition ()) && attempts > 0 then
                do! Flow.sleep (TimeSpan.FromMilliseconds 5.0)
                return! eventually condition (attempts - 1)
        }

    let private registeredInCurrentScope () : Flow<ClockEnvironment, 'error, int> =
        Flow(fun _ _ -> Execution.ofValue (RuntimeState.current().Scope.RegisteredCount))

    [<Fact>]
    let ``zipPar keeps both branches' scoped resources until the caller's scope closes`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string * string * string list> =
            flow {
                let! left, right = Flow.zipPar (acquire events "left") (acquire events "right")
                return left, right, released events
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success("left", "right", []) @>
        test <@ released events = [ "left:released"; "right:released" ] @>

    [<Fact>]
    let ``A failing zipPar releases its sibling's scoped resources before it returns`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string list> =
            flow {
                let! acquired = Deferred.make<ClockEnvironment, string, unit> ()

                let kept =
                    flow {
                        let! name = acquire events "sibling"
                        do! Deferred.succeed () acquired |> Flow.ignore
                        return name
                    }

                let failing =
                    flow {
                        do! Deferred.await acquired
                        return! Flow.fail "boom"
                    }

                do! Flow.zipPar kept failing |> Flow.ignore |> Flow.orElse (Flow.ok ())
                return released events
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success [ "sibling:released" ] @>

    [<Fact>]
    let ``sequencePar keeps every flow's scoped resources until the caller's scope closes`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string list * string list> =
            flow {
                let! names = [ "a"; "b"; "c" ] |> List.map (acquire events) |> Flow.sequencePar
                return names, released events
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success([ "a"; "b"; "c" ], []) @>
        test <@ released events = [ "a:released"; "b:released"; "c:released" ] @>

    [<Fact>]
    let ``race keeps the winner's scoped resources and releases the loser's`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string * string list> =
            flow {
                let! loserAcquired = Deferred.make<ClockEnvironment, string, unit> ()

                let loser =
                    flow {
                        let! _ = acquire events "loser"
                        do! Deferred.succeed () loserAcquired |> Flow.ignore
                        do! Flow.never
                        return "loser"
                    }

                let winner =
                    flow {
                        do! Deferred.await loserAcquired
                        return! acquire events "winner"
                    }

                let! won = Flow.race loser winner
                // The race returns without waiting for the interrupted loser's cleanup.
                do! eventually (fun () -> released events = [ "loser:released" ]) 400
                return won, released events
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success("winner", [ "loser:released" ]) @>
        test <@ released events = [ "loser:released"; "winner:released" ] @>

    [<Fact>]
    let ``traversePar keeps each item's scoped resources after its worker finishes`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string list * string list> =
            flow {
                let! names = [ 1..6 ] |> Flow.traversePar (Parallelism.bounded 2) (fun index -> acquire events $"item{index}")
                return names, released events
            }
            |> Flow.scoped

        let expectedNames = [ for index in 1..6 -> $"item{index}" ]

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(expectedNames, []) @>
        test <@ released events = (expectedNames |> List.map (fun name -> name + ":released") |> List.sort) @>

    [<Fact>]
    let ``A single failing flow in sequencePar releases its scoped resources before it returns`` () =
        let events = ResizeArray<string>()

        let failsAfterAcquiring name : Flow<ClockEnvironment, string, string> =
            acquire events name |> Flow.bind (fun (_: string) -> Flow.fail "boom")

        let workflow : Flow<ClockEnvironment, string, string list * string list> =
            flow {
                do! Flow.sequencePar [ failsAfterAcquiring "only" ] |> Flow.ignore |> Flow.orElse (Flow.ok ())
                let afterSequence = released events

                do!
                    [ "first"; "second" ]
                    |> Flow.forEachPar (Parallelism.bounded 1) (fun name -> failsAfterAcquiring name |> Flow.ignore)
                    |> Flow.orElse (Flow.ok ())

                return afterSequence, released events
            }
            |> Flow.scoped

        test
            <@
                Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(
                    [ "only:released" ],
                    [ "first:released"; "only:released" ]
                )
            @>

    [<Fact>]
    let ``traverseParUsing releases each worker's resource but keeps what its items acquired`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string list * string list> =
            flow {
                let! names =
                    [ 1..4 ]
                    |> Flow.traverseParUsing
                        (Parallelism.bounded 2)
                        (connection events "worker")
                        (fun _ index -> acquire events $"item{index}")

                return names, released events
            }
            |> Flow.scoped

        let expectedNames = [ for index in 1..4 -> $"item{index}" ]

        let workersReleased = [ "worker:released"; "worker:released" ]

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(expectedNames, workersReleased) @>

        test
            <@
                released events = List.sort (
                    [ "worker:released"; "worker:released" ]
                    @ (expectedNames |> List.map (fun name -> name + ":released"))
                )
            @>

    [<Fact>]
    let ``A timed-out flow releases its scoped resources before the fallback, and one in time keeps them`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string * string * string list> =
            flow {
                let! fast = acquire events "fast" |> Flow.timeout (TimeSpan.FromSeconds 30.0) "timed out"

                let slow =
                    flow {
                        let! _ = acquire events "slow"
                        do! Flow.never
                        return "slow"
                    }

                let! fallback = slow |> Flow.timeoutToOk (TimeSpan.FromMilliseconds 50.0) "fallback"
                return fast, fallback, released events
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success("fast", "fallback", [ "slow:released" ]) @>
        test <@ released events = [ "fast:released"; "slow:released" ] @>

    [<Fact>]
    let ``Parallel calls do not leave empty branch scopes in a long-lived scope`` () =
        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                for _ in 1..500 do
                    let! _ = Flow.zipPar (Flow.ok 1) (Flow.ok 2)
                    let! _ = [ 1..4 ] |> Flow.traversePar (Parallelism.bounded 2) Flow.ok
                    let! _ = Flow.ok 3 |> Flow.timeout (TimeSpan.FromSeconds 30.0) "timed out"
                    ()

                return! registeredInCurrentScope ()
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success 0 @>

    [<Fact>]
    let ``Concurrent combinators still run when the caller's scope has already closed`` () =
        // Public code cannot reach a closed scope on demand, so run the flow under one directly.
        let underClosedScope (inner: Flow<ClockEnvironment, string, 'value>) : Flow<ClockEnvironment, string, 'value> =
            Flow(fun environment cancellationToken ->
                let closed = new Scope()
                (closed :> IDisposable).Dispose()
                let runtime = RuntimeState.current () |> RuntimeContext.withScope closed
                RuntimeState.withRuntime runtime (fun () -> FlowInternal.invoke inner environment cancellationToken))

        let workflow : Flow<ClockEnvironment, string, int * int * int * int> =
            flow {
                let! left, right = Flow.zipPar (Flow.ok 1) (Flow.ok 2)
                let! raced = Flow.race (Flow.ok 3) (Flow.never)
                let! fiber = Flow.fork (Flow.ok 4)
                let! joined = Fiber.join fiber
                return left, right, raced, joined
            }
            |> underClosedScope

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(1, 2, 3, 4) @>

    [<Fact>]
    let ``A resource acquired after its scope closed is released at once and the flow is interrupted`` () =
        // A race loser can finish acquiring after the race has returned and closed its scope. Nobody can receive the
        // resource then, so it is released instead of leaking, here or in a branch.
        let underClosedScope (inner: Flow<ClockEnvironment, string, 'value>) : Flow<ClockEnvironment, string, 'value> =
            Flow(fun environment cancellationToken ->
                let closed = new Scope()
                (closed :> IDisposable).Dispose()
                let runtime = RuntimeState.current () |> RuntimeContext.withScope closed
                RuntimeState.withRuntime runtime (fun () -> FlowInternal.invoke inner environment cancellationToken))

        let events = ResizeArray<string>()
        let finalized = ref 0
        let finalizer = Flow.scopeFinalizer (Flow.delay (fun () -> finalized.Value <- finalized.Value + 1; Flow.succeed ()))

        let run inner = Flow.runSync (TestSupport.clockEnv ()) (underClosedScope inner)
        let direct = run (acquire events "direct")
        let inBranch = run (Flow.zipPar (acquire events "branch") (Flow.ok 1))
        let registered = run finalizer

        test <@ direct = Exit.Failure Cause.Interrupt @>
        test <@ inBranch = Exit.Failure Cause.Interrupt @>
        test <@ registered = Exit.Failure Cause.Interrupt @>
        test <@ snapshot events = [ "direct:acquired"; "direct:released"; "branch:acquired"; "branch:released" ] @>
        test <@ finalized.Value = 1 @>

    [<Fact>]
    let ``Nested parallel branches hand their scoped resources up to the outermost caller`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string list * int> =
            flow {
                let! _ =
                    Flow.zipPar
                        (Flow.zipPar (acquire events "inner-left") (acquire events "inner-right"))
                        (Flow.race (acquire events "raced") Flow.never)

                let! registered = registeredInCurrentScope ()
                return released events, registered
            }
            |> Flow.scoped

        // zipPar's two flows share one branch scope, which holds the inner zipPar's scope and the race winner's, so
        // the caller's scope gains a single registration and nothing is released until it closes.
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success([], 1) @>
        test <@ released events = [ "inner-left:released"; "inner-right:released"; "raced:released" ] @>

    [<Fact>]
    let ``A fiber forked in a parallel branch outlives the branch when the caller keeps its value`` () =
        // A race cancels its branches once it is decided, and a timeout disposes its own token; neither may interrupt
        // a fiber that the winning branch forked and handed to the caller.
        let forkSeven () : Flow<ClockEnvironment, string, Fiber<string, int>> =
            Flow.sleep (TimeSpan.FromMilliseconds 50.0) |> Flow.map (fun () -> 7) |> Flow.fork

        let workflow : Flow<ClockEnvironment, string, int list> =
            flow {
                let! raced = Flow.race (forkSeven ()) Flow.never
                let! zipped, _ = Flow.zipPar (forkSeven ()) (Flow.ok 1)
                let! sequenced = Flow.sequencePar [ forkSeven () ]
                let! timed = forkSeven () |> Flow.timeout (TimeSpan.FromSeconds 30.0) "timed out"
                return! raced :: zipped :: timed :: sequenced |> Flow.traverse Fiber.join
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success [ 7; 7; 7; 7 ] @>

    [<Fact>]
    let ``A fiber forked in a race loser is interrupted with the loser`` () =
        let workflow : Flow<ClockEnvironment, string, Exit<unit, string>> =
            flow {
                let! forked = Deferred.make<ClockEnvironment, string, Fiber<string, unit>> ()

                let loser =
                    flow {
                        let! fiber = Flow.fork Flow.never
                        do! Deferred.succeed fiber forked |> Flow.ignore
                        do! Flow.never
                        return 0
                    }

                let winner =
                    flow {
                        let! _ = Deferred.await forked
                        return 1
                    }

                let! _ = Flow.race loser winner
                let! fiber = Deferred.await forked
                return! Fiber.await fiber
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(Exit.Failure Cause.Interrupt) @>

    [<Fact>]
    let ``A resource returned from a plain fork is already released when join returns it`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string * string list> =
            flow {
                let! fiber = acquire events "db" |> Flow.fork // axial-allow-resource-from-fiber
                let! name = Fiber.join fiber
                return name, released events
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success("db", [ "db:released" ]) @>

    [<Fact>]
    let ``forkResource hands join a live resource that the caller's scope releases`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, string * string list> =
            flow {
                let! connecting = Flow.forkResource (connection events "db")
                let! name = Fiber.join connecting
                return name, released events
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success("db", []) @>
        test <@ snapshot events = [ "db:acquired"; "db:released" ] @>

    [<Fact>]
    let ``forkResource releases a resource nobody joined when the caller's scope closes`` () =
        let events = ResizeArray<string>()

        let workflow : Flow<ClockEnvironment, string, unit> =
            flow {
                let! _ = Flow.forkResource (connection events "db")
                do! eventually (fun () -> snapshot events = [ "db:acquired" ]) 400
            }
            |> Flow.scoped

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success() @>
        test <@ snapshot events = [ "db:acquired"; "db:released" ] @>
