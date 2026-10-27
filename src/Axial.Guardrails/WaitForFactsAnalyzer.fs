/// Test-only checks for guessed waits, without guessing the clock supplied to a Flow.
module Axial.Guardrails.WaitForFactsAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open Axial.Guardrails.ResourceFromFiberAnalyzer

let rec private headRange expr =
    match expr with
    | SynExpr.Ident id -> Some id.idRange
    | SynExpr.LongIdent(_, ids, _, _) -> ids.LongIdent |> List.tryLast |> Option.map _.idRange
    | SynExpr.App(_, _, f, _, _)
    | SynExpr.TypeApp(f, _, _, _, _, _, _)
    | SynExpr.Paren(f, _, _, _)
    | SynExpr.Typed(f, _, _) -> headRange f
    | _ -> None

let private matches (ctx: CliContext) entity memberName expr =
    match headRange expr with
    | None -> false
    | Some location ->
        ctx.GetAllSymbolUsesOfFile()
        |> Seq.exists (fun site ->
            site.Range.End = location.End && site.Range.StartLine = location.StartLine
            && site.Range.StartColumn <= location.StartColumn
            && (match site.Symbol with
                | :? FSharpMemberOrFunctionOrValue as value ->
                    value.CompiledName = memberName
                    && (value.DeclaringEntity |> Option.exists (fun owner -> owner.TryFullName = Some entity))
                | _ -> false))

let rec private directDelay ctx expr =
    match expr with
    | SynExpr.Paren(inner, _, _, _)
    | SynExpr.Typed(inner, _, _) -> directDelay ctx inner
    | SynExpr.App _ ->
        matches ctx "System.Threading.Tasks.Task" "Delay" expr
        || matches ctx "Microsoft.FSharp.Control.FSharpAsync" "Sleep" expr
    | _ -> false

let rec private infiniteArgument (ctx: CliContext) expr =
    match expr with
    | SynExpr.Paren(inner, _, _, _)
    | SynExpr.Typed(inner, _, _) -> infiniteArgument ctx inner
    | SynExpr.Tuple(exprs = first :: _) -> infiniteArgument ctx first
    | SynExpr.Const(SynConst.Int32 -1, _) -> true
    | _ ->
        match headRange expr with
        | None -> false
        | Some location ->
            ctx.GetAllSymbolUsesOfFile()
            |> Seq.exists (fun site ->
                site.Range.End = location.End && site.Range.StartLine = location.StartLine
                && site.Range.StartColumn <= location.StartColumn
                && (match site.Symbol with
                    | :? FSharpField as field ->
                        (field.Name = "Infinite" || field.Name = "InfiniteTimeSpan")
                        && (field.DeclaringEntity |> Option.exists (fun owner -> owner.TryFullName = Some "System.Threading.Timeout"))
                    | _ -> false))

let rec private waitsForCancellation ctx expr =
    match expr with
    | SynExpr.Paren(inner, _, _, _)
    | SynExpr.Typed(inner, _, _) -> waitsForCancellation ctx inner
    | SynExpr.App(argExpr = argument) -> infiniteArgument ctx argument
    | _ -> false

// Only this shared helper owns the bounded polling implementation. A same-named local or another module
// must not turn arbitrary sleeps into fact waits.
let rec private approvedHelperBodies (ctx: CliContext) decls =
    decls
    |> List.collect (function
        | SynModuleDecl.NestedModule(decls = nested) -> approvedHelperBodies ctx nested
        | SynModuleDecl.Let(bindings = bindings) ->
            bindings |> List.choose (fun (SynBinding(headPat = pattern; expr = body)) ->
                let approved =
                    ctx.GetAllSymbolUsesOfFile()
                    |> Seq.exists (fun site ->
                        site.IsFromDefinition && Range.rangeContainsRange pattern.Range site.Range
                        && (match site.Symbol with
                            | :? FSharpMemberOrFunctionOrValue as value ->
                                value.DisplayName = "waitUntil"
                                && (value.DeclaringEntity |> Option.exists (fun owner -> owner.TryFullName = Some "Axial.Tests.TestSupport"))
                            | _ -> false))
                if approved then Some body.Range else None)
        | _ -> [])

let rec private find ctx approvedBodies expr : range list =
    let report (delay: SynExpr) =
        not (approvedBodies |> List.exists (fun body -> Range.rangeContainsRange body delay.Range))
        && not (waitsForCancellation ctx delay)
    let here =
        match expr with
        | SynExpr.App _ when matches ctx "System.Threading.Thread" "Sleep" expr && report expr -> [ expr.Range ]
        | SynExpr.DoBang(inner, _, _) when directDelay ctx inner && report inner -> [ inner.Range ]
        | SynExpr.LetOrUse bindings when bindings.IsBang ->
            bindings.Bindings
            |> List.choose (fun (SynBinding(expr = rhs)) -> if directDelay ctx rhs && report rhs then Some rhs.Range else None)
        | _ -> []
    let nested =
        match expr with
        | SynExpr.For(identBody = first; toBody = last; doBody = body) ->
            [ first; last; body ] |> List.collect (find ctx approvedBodies)
        | _ -> childExprs expr |> List.collect (find ctx approvedBodies)
    here @ nested

/// Raw sites also serve AXG002's stale-suppression validation, independently of policy role.
let rawRanges (ctx: CliContext) =
    match ctx.ParseFileResults.ParseTree with
    | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
        modules
        |> List.collect (fun (SynModuleOrNamespace(decls = decls)) ->
            let approvedBodies = approvedHelperBodies ctx decls
            allExprsInModule decls |> List.collect (find ctx approvedBodies))
        |> List.distinctBy (fun r -> r.StartLine, r.StartColumn, r.EndLine, r.EndColumn)
    | _ -> []

let analyzeWithConfig (config: GuardrailConfig.Config) (ctx: CliContext) : Message list =
    match GuardrailConfig.resolvedPolicy config ctx with
    | Some policy when policy.Role = Policy.Test ->
        let level = policy.Rules.TryFind "test.wait-for-facts" |> Option.defaultValue Policy.Deny
        if level = Policy.Allow then []
        else
            rawRanges ctx
            |> List.filter (fun r ->
                not (Suppressions.isAllowed config.BoundaryFiles ctx.FileName ctx.SourceText r.StartLine "AXG013"))
            |> List.map (fun r ->
                { Type = "Axial Wait For Facts"
                  Code = "AXG013"
                  Message = "Wait for a fact, not elapsed time: complete a Deferred or TaskCompletionSource when the work reaches the point you care about, then wait on it with a failure deadline; or step the test clock."
                  Severity = Severity.Error
                  Range = r
                  Fixes = [] })
            |> GuardrailConfig.applyWith config EffectCatalog.CoreGuardrail ctx
            |> List.map (fun message ->
                if level = Policy.Warn then { message with Severity = Severity.Warning } else message)
    | _ -> []

[<CliAnalyzer("WaitForFacts", "Flags guessed waits in test-role code.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/04-guardrail-diagnostics.md#axg013-wait-for-facts-not-time")>]
let waitForFactsAnalyzer: Analyzer<CliContext> =
    fun ctx -> async { return analyzeWithConfig (GuardrailConfig.load ()) ctx }
