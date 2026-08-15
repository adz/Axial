/// The `Fixture` analyzer: flags a module-level `let` value (not a function) in a module that
/// also contains an xUnit `[<Fact>]`/`[<Theory>]`/FsCheck `[<Property>]` test.
///
/// A module-level `let` binding with no parameters compiles to a value computed once, shared by
/// every test in the module - exactly the xUnit-parallelism hazard AGENTS.md's Test Authoring
/// section already documents in prose ("Do not define shared fixtures as module-level `let`
/// values in xUnit test modules. Build fixtures inside each test or expose them as functions.").
/// This makes that rule mechanically checked instead of relying on review to catch it.
module Axial.Guardrails.FixtureAnalyzer

open System.Text.RegularExpressions
open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

let private testAttributeNames = set [ "Fact"; "Theory"; "Property" ]

let private allowDirective =
    Regex(@"axial-allow-fixture\b", RegexOptions.Compiled)

let private isAllowed (sourceText: ISourceText) (line1: int) : bool =
    let textOfLine1 (n: int) =
        if n >= 1 && n <= sourceText.GetLineCount() then
            sourceText.GetLineString(n - 1)
        else
            ""

    allowDirective.IsMatch(textOfLine1 line1) || allowDirective.IsMatch(textOfLine1 (line1 - 1))

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
        + "not per test), which is unsafe under xUnit's parallel test execution. Build the fixture "
        + "inside each test, or expose it as a function so each caller gets a fresh value. If it's "
        + "genuinely immutable and safe to share, mark it: `// axial-allow-fixture` on this line or "
        + "the line above."
      Code = "AXG004"
      Severity = Severity.Warning
      Range = range
      Fixes = [] }

let rec private checkDecls (sourceText: ISourceText) (decls: SynModuleDecl list) : Message list =
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
                        if isAllowed sourceText range.StartLine then None else Some(message range))
                | _ -> [])

    let nestedFindings =
        decls
        |> List.collect (fun decl ->
            match decl with
            | SynModuleDecl.NestedModule(decls = nested) -> checkDecls sourceText nested
            | _ -> [])

    hereFindings @ nestedFindings

[<CliAnalyzer("Fixture",
              "Flags a module-level `let` value (not a function) in a module that also contains an "
              + "xUnit Fact/Theory or FsCheck Property test - shared, computed-once state that's unsafe "
              + "under parallel test execution.",
              "https://github.com/adz/Axial/blob/main/docs/guardrails.md")>]
let fixtureAnalyzer: Analyzer<CliContext> =
    fun (ctx: CliContext) ->
        async {
            let modules =
                match ctx.ParseFileResults.ParseTree with
                | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) -> modules
                | _ -> []

            return
                modules
                |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> checkDecls ctx.SourceText decls)
        }
