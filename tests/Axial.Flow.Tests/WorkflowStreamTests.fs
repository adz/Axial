namespace Axial.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Tests.TestSupport
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
            |> FlowStream.runForEach (fun v -> sum <- sum + v)

        let result = Flow.runSync () workflow
        test <@ result = Exit.Success () @>
        test <@ sum = 30 @>

    [<Fact>]
    let ``FlowStream: transforms bounds and collects without leaving Flow`` () =
        let result =
            FlowStream.fromSeq [ 1..8 ]
            |> FlowStream.filter (fun value -> value % 2 = 0)
            |> FlowStream.map (fun value -> value * 10)
            |> FlowStream.skip 1
            |> FlowStream.take 2
            |> FlowStream.runCollect
            |> Flow.runSync ()

        test <@ result = Exit.Success [ 40; 60 ] @>

    [<Fact>]
    let ``FlowStream: effectful unfold map tap and fold preserve typed effects`` () =
        let seen = ResizeArray<int>()
        let stream =
            FlowStream.unfoldFlow (fun value -> Flow.ok (if value > 3 then None else Some(value, value + 1))) 1
            |> FlowStream.mapFlow (fun value -> Flow.ok (value * 2))
            |> FlowStream.tapFlow (fun value -> flow { seen.Add value })

        let result = stream |> FlowStream.runFold (+) 0 |> Flow.runSync ()
        test <@ result = Exit.Success 12 @>
        test <@ seen |> Seq.toList = [ 2; 4; 6 ] @>

    [<Fact>]
    let ``FlowStream: append collect and zip compose lazily`` () =
        let expanded =
            FlowStream.fromSeq [ 1; 2 ]
            |> FlowStream.append (FlowStream.singleton 3)
            |> FlowStream.collect (fun value -> FlowStream.fromSeq [ value; value * 10 ])

        let result =
            expanded
            |> FlowStream.zip (FlowStream.fromSeq [ "a"; "b"; "c"; "d"; "e"; "f" ])
            |> FlowStream.runCollect
            |> Flow.runSync ()

        test <@ result = Exit.Success [ (1, "a"); (10, "b"); (2, "c"); (20, "d"); (3, "e"); (30, "f") ] @>
