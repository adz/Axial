namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowParallelTests =
    [<Fact>]
    let ``Flow: zipPar combines results concurrently`` () =
        let workflow =
            Flow.zipPar
                (flow { 
                    do! Flow.sleep (TimeSpan.FromMilliseconds 100.0)
                    return 1 
                })
                (flow { 
                    do! Flow.sleep (TimeSpan.FromMilliseconds 100.0)
                    return 2 
                })

        test <@ Flow.runSync () workflow = Exit.Success (1, 2) @>

    [<Fact>]
    let ``Flow: zipPar interrupts on failure`` () =
        let mutable executed = false
        let workflow =
            Flow.zipPar
                (flow { 
                    do! Flow.sleep (TimeSpan.FromMilliseconds 500.0)
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
                    do! Flow.sleep (TimeSpan.FromMilliseconds 100.0)
                    return 1 
                })
                (flow { 
                    do! Flow.sleep (TimeSpan.FromMilliseconds 500.0)
                    loserExecuted <- true
                    return 2 
                })

        test <@ Flow.runSync () workflow = Exit.Success 1 @>
        // Give it a bit more time to potentially execute (though it shouldn't)
        Thread.Sleep(500)
        test <@ loserExecuted = false @>

    [<Fact>]
    let ``traversePar keeps input order and never exceeds the bound`` () =
        let active = ref 0
        let peak = ref 0
        let gate = obj ()

        let work (value: int) : Flow<unit, string, int> =
            flow {
                lock gate (fun () ->
                    active.Value <- active.Value + 1
                    peak.Value <- max peak.Value active.Value)

                do! Flow.sleep (TimeSpan.FromMilliseconds(float (10 - value % 5)))
                lock gate (fun () -> active.Value <- active.Value - 1)
                return value * 10
            }

        let traversal = [ 1..20 ] |> Flow.traversePar (Parallelism.bounded 3) work

        test <@ Flow.runSync () traversal = Exit.Success [ for value in 1..20 -> value * 10 ] @>
        test <@ peak.Value <= 3 && peak.Value >= 2 @>
        // The flow value is a description: running it again starts from the first item.
        test <@ Flow.runSync () traversal = Exit.Success [ for value in 1..20 -> value * 10 ] @>

    [<Fact>]
    let ``traversePar fails fast and interrupts running siblings`` () =
        let interrupted = ref 0
        let started = ref 0

        let work (value: int) : Flow<unit, string, int> =
            flow {
                System.Threading.Interlocked.Increment(&started.contents) |> ignore

                if value = 2 then
                    do! Flow.sleep (TimeSpan.FromMilliseconds 20.0)
                    return! Flow.fail "boom"
                else
                    do!
                        Flow.sleep (TimeSpan.FromSeconds 30.0)
                        |> Flow.fold Flow.ok (fun cause ->
                            if Cause.isInterrupted cause then
                                System.Threading.Interlocked.Increment(&interrupted.contents) |> ignore

                            Flow.ofExit (Exit.Failure cause))

                    return value
            }

        let stopwatch = Diagnostics.Stopwatch.StartNew()
        let result = [ 1..10 ] |> Flow.traversePar (Parallelism.bounded 3) work |> Flow.runSync ()

        test <@ result = Exit.Failure(Cause.Fail "boom") @>
        test <@ stopwatch.Elapsed < TimeSpan.FromSeconds 10.0 @>
        test <@ started.Value = 3 @>
        test <@ interrupted.Value = 2 @>

    [<Fact>]
    let ``forEachPar runs every item and handles empty input`` () =
        let seen = System.Collections.Concurrent.ConcurrentBag<int>()

        let result =
            [ 1..50 ]
            |> Flow.forEachPar (Parallelism.bounded 4) (fun value -> Flow.delay (fun () -> seen.Add value; Flow.ok ()))
            |> Flow.runSync ()

        let empty : Exit<unit, string> = [] |> Flow.forEachPar (Parallelism.bounded 4) (fun (_: int) -> Flow.ok ()) |> Flow.runSync ()
        let emptyTraversal : Exit<int list, string> = [] |> Flow.traversePar (Parallelism.bounded 4) Flow.ok |> Flow.runSync ()

        test <@ result = Exit.Success () @>
        test <@ seen |> Seq.sort |> List.ofSeq = [ 1..50 ] @>
        test <@ empty = Exit.Success () @>
        test <@ emptyTraversal = Exit.Success [] @>
