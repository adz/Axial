namespace FsFlow.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowConcurrencyTests =
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
