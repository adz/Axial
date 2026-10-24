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

let private testAttributeNames = set [ "Fact"; "Theory"; "Property" ]

let private attributeShortName (attr: SynAttribute) : string =
    let name = attr.TypeName.LongIdent |> List.last |> (fun i -> i.idText)
    if name.EndsWith("Attribute") then name.Substring(0, name.Length - "Attribute".Length) else name

let private hasTestAttribute (attributeLists: SynAttributeList list) : bool =
    attributeLists
    |> List.collect (fun list -> list.Attributes)
    |> List.exists (fun a -> Set.contains (attributeShortName a) testAttributeNames)

/// A module-level binding is a "fixture" risk when it takes no arguments (so it's a value,
/// computed once and shared, not a function re-invoked per call) and its right-hand side isn't a
/// bare constant (`let tolerance = 0.0001` isn't the shared-mutable-state hazard this targets) or
/// a point-free function (`let f = function | ... -> ...` has no parameter in its binding
/// pattern, so SynValInfo reports arity 0, but it's a stateless function value like any other).
let private isFixtureRisk (binding: SynBinding) : bool =
    let (SynBinding(valData = valData; expr = expr; attributes = attrs)) = binding
    let arity = valData.SynValInfo.CurriedArgInfos.Length

    let isSafeShape =
        match expr with
        | SynExpr.Const _
        | SynExpr.Lambda _
        | SynExpr.MatchLambda _ -> true
        | _ -> false

    arity = 0 && not isSafeShape && not (hasTestAttribute attrs)

let private message (range: range) : Message =
    { Type = "Axial Test Fixture"
      Message =
        "This module-level `let` value is shared by every test in this module (it's computed once, "
        + "not per test), which is unsafe under xUnit's parallel test execution. Move the fixture "
        + "inside each test, or replace the value with a function that returns a fresh fixture."
      Code = "AXG004"
      Severity = Severity.Error
      Range = range
      Fixes = [] }

let rec private checkDecls (ctx: CliContext) (decls: SynModuleDecl list) : Message list =
    let isTestModule =
        decls
        |> List.exists (fun decl ->
            match decl with
            | SynModuleDecl.Let(bindings = bindings) ->
                bindings
                |> List.exists (fun (SynBinding(attributes = attrs)) -> hasTestAttribute attrs)
            | _ -> false)

    let hereFindings =
        if not isTestModule then
            []
        else
            decls
            |> List.collect (fun decl ->
                match decl with
                | SynModuleDecl.Let(bindings = bindings) ->
                    bindings
                    |> List.filter isFixtureRisk
                    |> List.choose (fun (SynBinding(range = range)) ->
                        let config = GuardrailConfig.load ()
                        if isAllowed config.BoundaryFiles ctx.FileName ctx.SourceText range.StartLine "AXG004" then None
                        else Some(message range))
                | _ -> [])

    let nestedFindings =
        decls
        |> List.collect (fun decl ->
            match decl with
            | SynModuleDecl.NestedModule(decls = nested) -> checkDecls ctx nested
            | _ -> [])

    hereFindings @ nestedFindings

let rec private rawDeclRanges (decls: SynModuleDecl list) =
    let isTestModule =
        decls
        |> List.exists (function
            | SynModuleDecl.Let(bindings = bindings) ->
                bindings |> List.exists (fun (SynBinding(attributes = attrs)) -> hasTestAttribute attrs)
            | _ -> false)
    [ if isTestModule then
          for declaration in decls do
              match declaration with
              | SynModuleDecl.Let(bindings = bindings) ->
                  for (SynBinding(range = range) as binding) in bindings do
                      if isFixtureRisk binding then yield range
              | _ -> ()
      for declaration in decls do
          match declaration with
          | SynModuleDecl.NestedModule(decls = nested) -> yield! rawDeclRanges nested
          | _ -> () ]

let rawRanges (ctx: CliContext) =
    match ctx.ParseFileResults.ParseTree with
    | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
        modules |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> rawDeclRanges decls)
    | _ -> []

[<CliAnalyzer("Fixture",
              "Flags a module-level `let` value (not a function) in a module that also contains an "
              + "xUnit Fact/Theory or FsCheck Property test - shared, computed-once state that's unsafe "
              + "under parallel test execution.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/04-guardrail-diagnostics.md#axg004-create-fresh-test-fixtures")>]
let fixtureAnalyzer: Analyzer<CliContext> =
    fun (ctx: CliContext) ->
        async {
            let modules =
                match ctx.ParseFileResults.ParseTree with
                | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) -> modules
                | _ -> []

            return
                modules
                |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> checkDecls ctx decls)
                |> GuardrailConfig.applyCore ctx
        }
