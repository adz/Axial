namespace Axial.Guardrails.Tests

open Axial.Guardrails
open Swensen.Unquote
open Xunit

module ResourceFromFiberAnalyzerTests =
    // The rule is syntactic, but the fixture must typecheck, so it declares identity-typed stand-ins for the Axial
    // API rather than referencing it.
    let private prelude =
        [ "module M"
          "type FlowBuilder() ="
          "    member _.Bind(value: 'a, next: 'a -> 'b) = next value"
          "    member _.Return(value: 'a) = value"
          "    member _.ReturnFrom(value: 'a) = value"
          "let flow = FlowBuilder()"
          "module Flow ="
          "    let scopeResource (resource: int) = resource"
          "    let scopeAcquireRelease (acquire: int) (_: int) = acquire"
          "    let fork (body: int) = body"
          "    let forkNamed (_: string) (body: int) = body"
          "    let forkResource (resource: int) = resource"
          "    let zipPar (left: int) (_: int) = left"
          "let db, cache, openDb, closeDb = 0, 0, 0, 0"
          "let ping (_: int) = ()"
          "let query (connection: int) = connection" ]

    let private findings (body: string) =
        task {
            let source = String.concat "\n" prelude + "\n" + body
            let! ctx = AnalyzerTestContext.create source

            return
                ResourceFromFiberAnalyzer.analyze ctx
                |> List.map (fun message -> message.Range.StartLine - prelude.Length, message.Code)
        }

    [<Fact>]
    let ``flags a forked acquisition, piped or applied`` () =
        task {
            let! found =
                findings (
                    "let a = Flow.scopeResource db |> Flow.fork\n"
                    + "let b = Flow.fork (Flow.scopeResource db)\n"
                    + "let c = Flow.forkNamed \"db\" (Flow.scopeAcquireRelease openDb closeDb)\n"
                    + "let d = db |> Flow.scopeResource |> Flow.fork\n")

            test <@ found = [ 1, "AXG007"; 2, "AXG007"; 3, "AXG007"; 4, "AXG007" ] @>
        }

    [<Fact>]
    let ``flags a forked flow that returns what it acquired`` () =
        task {
            let! found =
                findings (
                    "let a =\n"
                    + "    flow {\n"
                    + "        let! connection = Flow.scopeResource db\n"
                    + "        do! ping connection\n"
                    + "        return connection\n"
                    + "    }\n"
                    + "    |> Flow.fork\n"
                    + "let b = flow { return! Flow.scopeResource db } |> Flow.fork\n")

            // A piped finding starts where the piped flow starts.
            test <@ found = [ 2, "AXG007"; 8, "AXG007" ] @>
        }

    [<Fact>]
    let ``does not flag a fiber that uses its resource and returns something else`` () =
        task {
            let! found =
                findings (
                    "let a =\n"
                    + "    flow {\n"
                    + "        let! connection = Flow.scopeResource db\n"
                    + "        return! query connection\n"
                    + "    }\n"
                    + "    |> Flow.fork\n"
                    + "let b = Flow.forkResource db\n"
                    + "let c = Flow.zipPar (Flow.scopeResource db) (Flow.scopeResource cache)\n")

            test <@ found = [] @>
        }

    [<Fact>]
    let ``honours the allow directive`` () =
        task {
            let! found =
                findings (
                    "// axial-allow-AXG007: fixture intentionally demonstrates fiber ownership\n"
                    + "let a = Flow.scopeResource db |> Flow.fork\n")

            test <@ found = [] @>
        }
