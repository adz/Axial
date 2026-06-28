namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowConcurrencyTests =
    [<Fact>]
    let ``Deferred: await receives successful completion`` () =
        let workflow : Flow<unit, string, bool * int> =
            flow {
                let! deferred = Deferred.make<unit, string, int> ()
                let! fiber = Deferred.await deferred |> Flow.fork
                let! completed = Deferred.succeed 42 deferred
                let! value = Flow.join fiber
                return completed, value
            }

        test <@ Flow.runSync () workflow = Exit.Success(true, 42) @>

    [<Fact>]
    let ``Deferred: completion is idempotent`` () =
        let workflow : Flow<unit, string, bool * bool * int> =
            flow {
                let! deferred = Deferred.make<unit, string, int> ()
                let! first = Deferred.succeed 1 deferred
                let! second = Deferred.fail "late" deferred
                let! value = Deferred.await deferred
                return first, second, value
            }

        test <@ Flow.runSync () workflow = Exit.Success(true, false, 1) @>

    [<Fact>]
    let ``Deferred: await preserves typed failure defect and interruption`` () =
        let typedFailure : Flow<unit, string, int> =
            flow {
                let! deferred = Deferred.make<unit, string, int> ()
                let! _ = Deferred.fail "boom" deferred
                return! Deferred.await deferred
            }

        let defect = InvalidOperationException("defect")

        let defective : Flow<unit, string, int> =
            flow {
                let! deferred = Deferred.make<unit, string, int> ()
                let! _ = Deferred.die defect deferred
                return! Deferred.await deferred
            }

        let interrupted : Flow<unit, string, int> =
            flow {
                let! deferred = Deferred.make<unit, string, int> ()
                let! _ = Deferred.interrupt deferred
                return! Deferred.await deferred
            }

        test <@ Flow.runSync () typedFailure = Exit.Failure(Cause.Fail "boom") @>
        test <@ Flow.runSync () defective = Exit.Failure(Cause.Die defect) @>
        test <@ Flow.runSync () interrupted = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Deferred: await respects runtime cancellation`` () =
        use cts = new CancellationTokenSource()

        let workflow : Flow<unit, string, int> =
            flow {
                let! deferred = Deferred.make<unit, string, int> ()
                return! Deferred.await deferred
            }

        cts.Cancel()

        test <@ Flow.runSyncWithToken () cts.Token workflow = Exit.Failure Cause.Interrupt @>

    [<Fact>]
    let ``Deferred: await composes with race`` () =
        let workflow : Flow<unit, string, int> =
            flow {
                let! deferred = Deferred.make<unit, string, int> ()
                let! _ = Deferred.succeed 7 deferred

                return!
                    Flow.race
                        (Deferred.await deferred)
                        (flow {
                            do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 100.0)
                            return 99
                        })
            }

        test <@ Flow.runSync () workflow = Exit.Success 7 @>

    [<Fact>]
    let ``Semaphore: withPermit serializes concurrent sections`` () =
        let gate = obj()
        let mutable active = 0
        let mutable maxActive = 0

        let enter () =
            lock gate (fun () ->
                active <- active + 1
                maxActive <- max maxActive active)

        let leave () =
            lock gate (fun () -> active <- active - 1)

        let workflow : Flow<unit, string, int> =
            flow {
                let! semaphore = Semaphore.make 1

                let protectedFlow =
                    Semaphore.withPermit semaphore (
                        flow {
                            enter ()
                            do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 40.0)
                            leave ()
                        })

                let! left = Flow.fork protectedFlow
                let! right = Flow.fork protectedFlow
                do! Flow.join left
                do! Flow.join right
                return maxActive
            }

        test <@ Flow.runSync () workflow = Exit.Success 1 @>

    [<Fact>]
    let ``Semaphore: withPermit releases after typed failure and defect`` () =
        let semaphore =
            match Flow.runSync () (Semaphore.make 1) with
            | Exit.Success semaphore -> semaphore
            | other -> failwithf "Expected semaphore creation, got %A" other

        let afterTypedFailure : Flow<unit, string, string> =
            Semaphore.withPermit semaphore (Flow.fail "boom")
            |> Flow.orElseWith (fun error ->
                flow {
                    let! next = Semaphore.withPermit semaphore (Flow.succeed "next")
                    return $"{error}:{next}"
                })

        let defect = InvalidOperationException("defect")
        let afterDefect = Semaphore.withPermit semaphore (Flow.die defect)
        let afterDefectRelease = Semaphore.withPermit semaphore (Flow.succeed "released")

        test <@ Flow.runSync () afterTypedFailure = Exit.Success "boom:next" @>
        test <@ Flow.runSync () afterDefect = Exit.Failure(Cause.Die defect) @>
        test <@ Flow.runSync () afterDefectRelease = Exit.Success "released" @>

    [<Fact>]
    let ``Semaphore: make rejects non-positive permit counts`` () =
        let result : Exit<FlowSemaphore, string> =
            Flow.runSync () (Semaphore.make 0)

        match result with
        | Exit.Failure(Cause.Die(:? ArgumentOutOfRangeException as error)) ->
            test <@ error.ParamName = "permits" @>
        | other ->
            failwithf "Expected permit count defect, got %A" other

    [<Fact>]
    let ``Fiber: fork and join success`` () =
        let workflow : Flow<unit, string, int> =
            flow {
                let! (fiber: Fiber<string, int>) = Flow.ok 42 |> Flow.fork
                let! result = fiber |> Flow.join
                return result
            }

        test <@ Flow.runSync () workflow = Exit.Success 42 @>

    [<Fact>]
    let ``Fiber: fork and join failure`` () =
        let workflow : Flow<unit, string, int> =
            flow {
                let! (fiber: Fiber<string, int>) = Flow.fail "boom" |> Flow.fork
                let! result = fiber |> Flow.join
                return result
            }

        test <@ Flow.runSync () workflow = Exit.Failure (Cause.Fail "boom") @>

    [<Fact>]
    let ``Fiber: interrupt stops execution`` () =
        let mutable executed = false
        let workflow =
            flow {
                let! (fiber: Fiber<string, int>) = 
                    flow {
                        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 500.0)
                        executed <- true
                        return 42
                    }
                    |> Flow.fork
                
                do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 100.0)
                let! exit = fiber |> Flow.interrupt
                return exit
            }

        let outcome = Flow.runSync () workflow
        
        match outcome with
        | Exit.Success (Exit.Failure Cause.Interrupt) -> 
            test <@ executed = false @>
        | _ -> failwithf "Expected interrupted exit, got %A" outcome
