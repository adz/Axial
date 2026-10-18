namespace Axial.Guardrails.Tests

open Axial.Guardrails
open Swensen.Unquote
open Xunit

module ScopeFromRunAnalyzerTests =
    let private prelude =
        [ "module M"
          "module Flow ="
          "    let run (value: 'a) = value"
          "    let runSync (value: 'a) = value"
          "    let RunSynchronously (value: 'a) = value"
          "    let StartAsTask (value: 'a) = value"
          "module Cache ="
          "    let make (capacity: int) = capacity"
          "module Hub ="
          "    let bounded (capacity: int) = capacity"
          "module Queue ="
          "    let unbounded () = 1"
          "module Ref ="
          "    let make (value: int) = value" ]

    let private findings body =
        task {
            let! ctx = AnalyzerTestContext.create (String.concat "\n" prelude + "\n" + body)
            return ScopeFromRunAnalyzer.analyze ctx |> List.map (fun m -> m.Range.StartLine - prelude.Length, m.Code)
        }

    [<Fact>]
    let ``direct and piped constructors escape their run`` () =
        task {
            let! found = findings ("let a = Flow.run (Cache.make 4)\n"
                                   + "let b = Hub.bounded 2 |> Flow.runSync\n"
                                   + "let c = Flow.RunSynchronously (Queue.unbounded ())\n"
                                   + "let d = Flow.StartAsTask (Cache.make 2)\n")
            test <@ found = [ 1, "AXG008"; 2, "AXG008"; 3, "AXG008"; 4, "AXG008" ] @>
        }

    [<Fact>]
    let ``named flows refs and intentional boundaries are not reported`` () =
        task {
            let! found = findings ("let a = Flow.run (Ref.make 2)\n"
                                   + "let source = Cache.make 2\n"
                                   + "let b = Flow.run source\n"
                                   + "let c = Flow.run (Cache.make 4) // axial-allow-effect: scope\n")
            test <@ found = [] @>
        }
