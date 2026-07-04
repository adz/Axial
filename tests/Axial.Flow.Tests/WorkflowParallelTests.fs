namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowParallelTests =
    [<Fact>]
    let ``Flow: zipPar combines results concurrently`` () =
        let workflow =
            Flow.zipPar
                (flow { 
                    do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 100.0)
                    return 1 
                })
                (flow { 
                    do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 100.0)
                    return 2 
                })

        test <@ Flow.runSync () workflow = Exit.Success (1, 2) @>

    [<Fact>]
    let ``Flow: zipPar interrupts on failure`` () =
        let mutable executed = false
        let workflow =
            Flow.zipPar
                (flow { 
                    do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 500.0)
                    executed <- true
                    return 1 
                })
                (Flow.fail "boom")

        let outcome = Flow.runSync () workflow
        test <@ outcome = Exit.Failure (Cause.Fail "boom") @>
        test <@ executed = false @>

    [<Fact>]
    let ``Flow: race returns first result and interrupts loser`` () =
        let mutable loserExecuted = false
        let workflow =
            Flow.race
                (flow { 
                    do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 100.0)
                    return 1 
                })
                (flow { 
                    do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 500.0)
                    loserExecuted <- true
                    return 2 
                })

        test <@ Flow.runSync () workflow = Exit.Success 1 @>
        // Give it a bit more time to potentially execute (though it shouldn't)
        Thread.Sleep(500)
        test <@ loserExecuted = false @>
