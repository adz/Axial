/// Checks direct scope-owned service constructors returned by a short-lived run.
module Axial.Guardrails.ScopeFromRunAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open Axial.Guardrails.ResourceFromFiberAnalyzer
open Axial.Guardrails.Suppressions

let private constructors =
    Map.ofList [ "Cache", set [ "make"; "makeBounded"; "makeBoundedWithExpiry"; "makeRec" ]
                 "Hub", set [ "makeScoped"; "subscribe" ]
                 "Queue", set [ "makeScoped" ] ]

let rec private headRange expr =
    match expr with
    | SynExpr.Ident id -> Some id.idRange
    | SynExpr.LongIdent(_, ids, _, _) -> ids.LongIdent |> List.tryLast |> Option.map _.idRange
    | SynExpr.App(_, _, f, _, _)
    | SynExpr.TypeApp(f, _, _, _, _, _, _)
    | SynExpr.Paren(f, _, _, _)
    | SynExpr.Typed(f, _, _) -> headRange f
    | _ -> None

let private symbolMatches (ctx: CliContext) entity names expr =
    match headRange expr with
    | None -> false
    | Some location ->
        ctx.GetAllSymbolUsesOfFile()
        |> Seq.exists (fun useSite ->
            (useSite.Range.End = location.End && useSite.Range.StartLine = location.StartLine
             && useSite.Range.StartColumn <= location.StartColumn)
            && (match useSite.Symbol with
                | :? FSharpMemberOrFunctionOrValue as memberValue ->
                    Set.contains memberValue.CompiledName names
                    && (memberValue.DeclaringEntity
                        |> Option.exists (fun declaring -> declaring.TryFullName = Some entity || declaring.TryFullName = Some(entity + "Module")))
                | _ -> false))

let private isConstructor ctx expr =
    constructors |> Map.exists (fun moduleName names -> symbolMatches ctx ("Axial." + moduleName) names expr)

let rec private returnsService ctx expr =
    match expr with
    | SynExpr.Paren(e, _, _, _)
    | SynExpr.Typed(e, _, _) -> returnsService ctx e
    | _ ->
        match pipe expr with
        | Some(_, target) -> isConstructor ctx target
        | None ->
            match expr with
            | SynExpr.App _ -> isConstructor ctx expr
            | _ -> false

let private isRunner ctx expr =
    symbolMatches ctx "Axial.Flow" (set [ "run"; "runSync" ]) expr
    || symbolMatches ctx "Axial.Flow`3" (set [ "RunSynchronously"; "StartAsTask" ]) expr

let rec private find ctx expr : range list =
    let here =
        match pipe expr with
        | Some(value, target) when isRunner ctx target && returnsService ctx value -> [ expr.Range ]
        | _ ->
            match expr with
            | SynExpr.App(_, false, runner, argument, r) when isRunner ctx runner && returnsService ctx argument -> [ r ]
            | _ -> []
    here @ (childExprs expr |> List.collect (find ctx))

// A result type cannot distinguish make from makeScoped for Queue or Hub.
// Only Cache is always tied to its creating runtime, so type-based detection is Cache-only.
let private serviceTypes = set [ "Axial.Cache`3" ]

let rec private isServiceType (t: FSharpType) =
    if t.IsAbbreviation then isServiceType t.AbbreviatedType
    else t.HasTypeDefinition && (t.TypeDefinition.TryFullName |> Option.exists serviceTypes.Contains)

let private returnsServiceType (t: FSharpType) =
    if t.HasTypeDefinition && t.TypeDefinition.TryFullName = Some "Axial.Flow`3" then
        t.GenericArguments |> Seq.tryItem 2 |> Option.exists isServiceType
    else false

let rec private typedFindings (expr: FSharpExpr) =
    let here =
        match expr with
        | Call(None, memberValue, _, _, arguments)
            when (memberValue.DeclaringEntity
                  |> Option.exists (fun entity -> entity.TryFullName = Some "Axial.Flow"))
                 && Set.contains memberValue.CompiledName (set [ "run"; "runSync" ])
                 && (arguments |> List.exists (fun argument -> returnsServiceType argument.Type)) -> [ expr.Range ]
        | _ -> []
    here @ (expr.ImmediateSubExpressions |> List.collect typedFindings)

let rec private typedDeclarations declaration =
    match declaration with
    | FSharpImplementationFileDeclaration.Entity(_, declarations) -> declarations |> List.collect typedDeclarations
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(_, _, body)
    | FSharpImplementationFileDeclaration.InitAction body ->
        try typedFindings body with _ -> [] // axial-allow-AXG010: An unreadable recovered typed declaration is skipped; syntax run-boundary checks still run.

let rawRanges (ctx: CliContext) : range list =
    let expressions =
        match ctx.ParseFileResults.ParseTree with
        | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
            modules |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> allExprsInModule decls)
        | _ -> []
    let typed =
        match (try ctx.TypedTree with _ -> None) with // axial-allow-AXG010: FCS may not expose a typed tree for a recovered file; syntax run-boundary checks are retained.
        | Some tree -> (try tree.Declarations |> List.collect typedDeclarations with _ -> []) // axial-allow-AXG010: Recovered typed declarations can be unreadable; syntax run-boundary findings are retained.
        | None -> []
    (expressions |> List.collect (find ctx)) @ typed
    |> List.distinctBy (fun r -> r.StartLine, r.StartColumn, r.EndLine, r.EndColumn)

let analyze (ctx: CliContext) : Message list =
    let config = GuardrailConfig.load ()
    rawRanges ctx
    |> List.filter (fun r ->
        not (isAllowed config.BoundaryFiles ctx.FileName ctx.SourceText r.StartLine "AXG008"))
    |> List.map (fun r ->
        { Type = "Axial Scope From Run"
          Message = "This run returns a scope-owned service that is closed when the run ends. Move use of the service inside the same flow, or keep its owning scope open."
          Code = "AXG008"
          Severity = Severity.Error
          Range = r
          Fixes = [] })

[<CliAnalyzer("ScopeFromRun", "Flags scope-owned services returned by a completed Flow run.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/04-guardrail-diagnostics.md#axg008-keep-scope-owned-services-inside-their-run")>]
let scopeFromRunAnalyzer: Analyzer<CliContext> =
    fun ctx -> async { return analyze ctx |> GuardrailConfig.applyCore ctx }
