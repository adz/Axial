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

    let private realFindings body =
        task {
            let! ctx = AnalyzerTestContext.createWithAxial true ("module M\nopen Axial\n" + body)
            return ScopeFromRunAnalyzer.analyze ctx |> List.map (fun m -> m.Code)
        }

    [<Theory>]
    [<InlineData("Cache.make (fun k -> Flow.ok k)")>]
    [<InlineData("Hub.make ()")>]
    [<InlineData("Queue.unbounded ()")>]
    let ``real constructors outlive run`` constructor =
        task {
            let! found = realFindings ("let result () = Flow.run (Unchecked.defaultof<IHasClock>) (" + constructor + ")")
            test <@ found = [ "AXG008" ] @>
        }

    [<Theory>]
    [<InlineData("flow { let! c = Cache.make (fun k -> Flow.ok k)\n             return c }")>]
    [<InlineData("Cache.make (fun k -> Flow.ok k) |> Flow.map id")>]
    let ``composed cache result outlives run`` expression =
        task {
            let! found = realFindings ("let result () = Flow.run (Unchecked.defaultof<IHasClock>) (" + expression + ")")
            test <@ found = [ "AXG008" ] @>
        }

    [<Fact>]
    let ``integer result is not a scope service`` () =
        task {
            let! found = realFindings "let result () = Flow.run () (Flow.ok 42)"
            test <@ found = [] @>
        }

    [<Fact>]
    let ``unrelated lookalike modules do not trigger scope findings`` () =
        task {
            let! found = findings ("let a = Flow.run (Cache.make 4)\n"
                                   + "let b = Hub.bounded 2 |> Flow.runSync\n"
                                   + "let c = Flow.RunSynchronously (Queue.unbounded ())\n"
                                   + "let d = Flow.StartAsTask (Cache.make 2)\n")
            test <@ found = [] @>
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
