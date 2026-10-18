namespace Axial.Tests

open System
open Axial
open Swensen.Unquote
open Xunit

module FiberLocalTests =
    [<Fact>]
    let ``get returns the initial value`` () =
        let local = FiberLocal.make 10
        test <@ Flow.runSync (TestSupport.clockEnv ()) (FiberLocal.get local) = Exit.Success 10 @>

    [<Fact>]
    let ``locally overrides and restores`` () =
        let local = FiberLocal.named "count" 10
        let workflow = flow {
            let! before = FiberLocal.get local
            let! inside = FiberLocal.get local |> FiberLocal.locally 20 local
            let! after = FiberLocal.get local
            return before, inside, after
        }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success (10, 20, 10) @>

    [<Fact>]
    let ``nested locally restores outer value`` () =
        let local = FiberLocal.make 0
        let workflow =
            flow {
                let! nested = FiberLocal.get local |> FiberLocal.locally 2 local
                let! outer = FiberLocal.get local
                return nested, outer
            }
            |> FiberLocal.locally 1 local
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success (2, 1) @>

    [<Fact>]
    let ``fork inherits locally value`` () =
        let local = FiberLocal.make 0
        let workflow =
            flow {
                let! fiber = Flow.fork (FiberLocal.get local)
                return! Fiber.join fiber
            }
            |> FiberLocal.locally 7 local
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success 7 @>

    [<Fact>]
    let ``typed failure restores the outer local`` () =
        let local = FiberLocal.make 0
        let workflow = flow {
            let! outcome = Flow.fail "expected" |> FiberLocal.locally 1 local |> Flow.fold (Exit.Success >> Flow.ok) (Exit.Failure >> Flow.ok)
            let! restored = FiberLocal.get local
            return outcome, restored
        }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success (Exit.Failure (Cause.Fail "expected"), 0) @>

    [<Fact>]
    let ``defect restores the outer local`` () =
        let local = FiberLocal.make 0
        let defect = InvalidOperationException "boom"
        let workflow = flow {
            let! outcome = Flow.die defect |> FiberLocal.locally 1 local |> Flow.fold (Exit.Success >> Flow.ok) (Exit.Failure >> Flow.ok)
            let! restored = FiberLocal.get local
            return outcome, restored
        }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success (Exit.Failure (Cause.Die defect), 0) @>

    [<Fact>]
    let ``timeout interruption restores the outer local`` () =
        let local = FiberLocal.make 0
        let workflow = flow {
            let! outcome =
                Flow.sleep (TimeSpan.FromSeconds 30.0)
                |> FiberLocal.locally 1 local
                |> Flow.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
                |> Flow.fold (Exit.Success >> Flow.ok) (Exit.Failure >> Flow.ok)
            let! restored = FiberLocal.get local
            return outcome, restored
        }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success (Exit.Failure (Cause.Fail "timed out"), 0) @>

    [<Fact>]
    let ``local survives asynchronous suspension and restores afterward`` () =
        let local = FiberLocal.make 0
        let workflow = flow {
            let! inside =
                flow {
                    do! Flow.sleep (TimeSpan.FromMilliseconds 10.0)
                    return! FiberLocal.get local
                }
                |> FiberLocal.locally 1 local
            let! restored = FiberLocal.get local
            return inside, restored
        }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success (1, 0) @>

    [<Fact>]
    let ``fork retains inherited local after parent leaves scope`` () =
        let local = FiberLocal.make 0
        let workflow = flow {
            let! gate = Deferred.make<ClockEnvironment, string, unit> ()
            let! child =
                Flow.fork (flow {
                    do! Deferred.await gate
                    return! FiberLocal.get local
                })
                |> FiberLocal.locally 7 local
            let! parent = FiberLocal.get local
            do! Deferred.succeed () gate |> Flow.ignore
            let! inherited = Fiber.join child
            return parent, inherited
        }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success (0, 7) @>

    [<Fact>]
    let ``parallel locally values remain isolated`` () =
        let local = FiberLocal.make 0
        let workflow =
            Flow.zipPar
                (FiberLocal.get local |> FiberLocal.locally 1 local)
                (FiberLocal.get local |> FiberLocal.locally 2 local)
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success (1, 2) @>
