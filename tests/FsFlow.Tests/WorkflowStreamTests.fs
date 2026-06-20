namespace FsFlow.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Result
open Axial.Validation
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowStreamTests =
    [<Fact>]
    let ``FlowStream: consumes sequence correctly`` () =
        let mutable sum = 0
        let stream = FlowStream.fromSeq [1; 2; 3; 4; 5]
        let workflow = 
            stream 
            |> FlowStream.map (fun v -> v * 2)
            |> FlowStream.runForEach () (fun v -> sum <- sum + v)

        let result = Flow.runSync () workflow
        test <@ result = Exit.Success () @>
        test <@ sum = 30 @>
