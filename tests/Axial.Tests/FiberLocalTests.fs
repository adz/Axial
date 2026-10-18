namespace Axial.Tests

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
    let ``parallel locally values remain isolated`` () =
        let local = FiberLocal.make 0
        let workflow =
            Flow.zipPar
                (FiberLocal.get local |> FiberLocal.locally 1 local)
                (FiberLocal.get local |> FiberLocal.locally 2 local)
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success (1, 2) @>
