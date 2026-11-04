/// The `Fixture` analyzer: flags a module-level `let` value (not a function) in a module that
/// also contains an xUnit `[<Fact>]`/`[<Theory>]`/FsCheck `[<Property>]` test.
///
/// A module-level `let` binding with no parameters compiles to a value computed once, shared by
/// every test in the module. AGENTS.md's Test Authoring section prohibits shared fixtures as
/// module-level values in xUnit test modules; build them inside each test or expose functions instead.
/// The analyzer catches violations before review.
module Axial.Guardrails.FixtureAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open Axial.Guardrails.Suppressions

let message (range: range) : Message =
    { Type = "Axial Test Fixture"
      Message =
        "This module-level `let` value is shared by every test in this module (it's computed once, "
        + "not per test), which is unsafe under xUnit's parallel test execution. Move the fixture "
        + "inside each test, or replace the value with a function that returns a fresh fixture."
      Code = "AXG004"
      Severity = Severity.Error
      Range = range
      Fixes = [] }

let rawRanges (ctx: CliContext) = FixtureSyntax.ranges ctx.ParseFileResults.ParseTree

[<CliAnalyzer("Fixture",
              "Flags a module-level `let` value (not a function) in a module that also contains an "
              + "xUnit Fact/Theory or FsCheck Property test - shared, computed-once state that's unsafe "
              + "under parallel test execution.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/04-guardrail-diagnostics.md#axg004-create-fresh-test-fixtures")>]
let fixtureAnalyzer (ctx: CliContext) =
    let run (ctx: CliContext) =
        async {
            let config = GuardrailConfig.load ()
            return
                rawRanges ctx
                |> List.filter (fun range -> not (isAllowed config.BoundaryFiles ctx.FileName ctx.SourceText range.StartLine "AXG004"))
                |> List.map message
                |> GuardrailConfig.applyCore ctx
        }
    Execution.track "Fixture" run ctx
