/// Syntactic check for scope-owned services returned by a short-lived run.
/// Named flows, aliases and computations that acquire then return a service are not followed.
module Axial.Guardrails.ScopeFromRunAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open Axial.Guardrails.ResourceFromFiberAnalyzer
open Axial.Guardrails.Suppressions

let private constructors =
    Map.ofList [ "Cache", set [ "make"; "makeBounded"; "makeBoundedWithExpiry"; "makeRec" ]
                 "Hub", set [ "make"; "bounded"; "unbounded" ]
                 "Queue", set [ "bounded"; "unbounded" ] ]

let rec private owner (expr: SynExpr) =
    match expr with
    | SynExpr.Ident id -> Some id.idText
    | SynExpr.LongIdent(_, ids, _, _) ->
        ids.LongIdent |> List.map _.idText |> List.rev |> List.tryItem 1
    | SynExpr.App(_, _, f, _, _) -> owner f
    | SynExpr.Paren(e, _, _, _) -> owner e
    | SynExpr.Typed(e, _, _) -> owner e
    | _ -> None

let private isConstructor expr =
    match owner expr, headIdent expr with
    | Some moduleName, Some name ->
        constructors |> Map.tryFind moduleName |> Option.exists (Set.contains name)
    | _ -> false

let rec private returnsService expr =
    match expr with
    | SynExpr.Paren(e, _, _, _)
    | SynExpr.Typed(e, _, _) -> returnsService e
    | _ ->
        match pipe expr with
        | Some(_, target) -> isConstructor target
        | None ->
            match expr with
            | SynExpr.App _ -> isConstructor expr
            | _ -> false

let private isRunner expr =
    match owner expr, headIdent expr with
    | Some "Flow", Some ("run" | "runSync" | "RunSynchronously" | "StartAsTask") -> true
    | _ -> false

let rec private find expr : range list =
    let here =
        match pipe expr with
        | Some(value, target) when isRunner target && returnsService value -> [ expr.Range ]
        | _ ->
            match expr with
            | SynExpr.App(_, false, runner, argument, r) when isRunner runner && returnsService argument -> [ r ]
            | _ -> []
    here @ (childExprs expr |> List.collect find)

let rawRanges (ctx: CliContext) : range list =
    let expressions =
        match ctx.ParseFileResults.ParseTree with
        | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
            modules |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> allExprsInModule decls)
        | _ -> []
    expressions
    |> List.collect find
    |> List.distinctBy (fun r -> r.StartLine, r.StartColumn, r.EndLine, r.EndColumn)

let analyze (ctx: CliContext) : Message list =
    let allowed = fileLevelAllowedCategories ctx.SourceText
    rawRanges ctx
    |> List.filter (fun r -> not (isAllowed ctx.SourceText allowed r.StartLine "scope"))
    |> List.map (fun r ->
        { Type = "Axial Scope From Run"
          Message = "This run returns a scope-owned service that is closed when the run ends. Use the service inside the same flow, or keep its owning scope open. At an intentional boundary use `// axial-allow-effect: scope`."
          Code = "AXG008"
          Severity = Severity.Warning
          Range = r
          Fixes = [] })

[<CliAnalyzer("ScopeFromRun", "Flags scope-owned services returned by a completed Flow run.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/03-guardrails.md")>]
let scopeFromRunAnalyzer: Analyzer<CliContext> =
    fun ctx -> async { return analyze ctx |> GuardrailConfig.applyCore }
