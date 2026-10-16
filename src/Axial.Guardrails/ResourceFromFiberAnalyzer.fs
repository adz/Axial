/// The `ResourceFromFiber` analyzer: flags a forked flow whose value is a scoped resource it acquired itself.
///
/// A forked fiber owns the scoped resources it acquires and releases them when it settles, so
/// `Flow.scopeResource r |> Flow.fork` followed by `Fiber.join` hands back a resource that has already been released.
/// The code compiles and reads like ZIO, where a forked fiber shares its parent's `Scope`, so the mistake is easy to
/// make and only shows up as use-after-dispose at runtime. `Flow.forkResource` is the form that keeps the resource.
///
/// The check is syntactic and narrow: it reports the flow passed to a `fork` variant when that flow is a
/// direct `scopeResource`/`scopeAcquireRelease` call, or a `flow { }` whose final `return`/`return!` hands back such
/// an acquisition. A flow built elsewhere and passed by name is not followed, which only costs a missed finding.
module Axial.Guardrails.ResourceFromFiberAnalyzer

open System.Text.RegularExpressions
open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

let private forkNames =
    set [ "fork"; "forkNamed"; "forkGraceful"; "forkDetached"; "forkReplacing"; "forkReplacingKey" ]

let private acquisitionNames = set [ "scopeResource"; "scopeAcquireRelease" ]

let private allowDirective =
    Regex(@"axial-allow-resource-from-fiber\b", RegexOptions.Compiled)

let private isAllowed (sourceText: ISourceText) (line1: int) : bool =
    let textOfLine1 (n: int) =
        if n >= 1 && n <= sourceText.GetLineCount() then
            sourceText.GetLineString(n - 1)
        else
            ""

    allowDirective.IsMatch(textOfLine1 line1) || allowDirective.IsMatch(textOfLine1 (line1 - 1))

/// The identifier an application chain ultimately applies: `fork` in `Flow.forkNamed "x" body` is `forkNamed`.
let rec private headIdent (expr: SynExpr) : string option =
    match expr with
    | SynExpr.Ident ident -> Some ident.idText
    | SynExpr.LongIdent(_, longIdent, _, _) -> longIdent.LongIdent |> List.tryLast |> Option.map (fun i -> i.idText)
    | SynExpr.App(_, _, funcExpr, _, _) -> headIdent funcExpr
    | SynExpr.TypeApp(funcExpr, _, _, _, _, _, _) -> headIdent funcExpr
    | SynExpr.Paren(inner, _, _, _) -> headIdent inner
    | SynExpr.Typed(inner, _, _) -> headIdent inner
    | _ -> None

let private headIn (names: Set<string>) (expr: SynExpr) : bool =
    headIdent expr |> Option.map (fun name -> Set.contains name names) |> Option.defaultValue false

/// `x |> f` parses as `App(App(op_PipeRight, x), f)`. Returns the piped value and the function it is piped into.
let private pipe (expr: SynExpr) : (SynExpr * SynExpr) option =
    match expr with
    | SynExpr.App(_, false, SynExpr.App(_, true, operator, value, _), target, _) when headIdent operator = Some "op_PipeRight" ->
        Some(value, target)
    | _ -> None

/// True when `expr` evaluates directly to a scoped acquisition: `Flow.scopeResource r`, `r |> Flow.scopeResource`.
let rec private isAcquisition (expr: SynExpr) : bool =
    match expr with
    | SynExpr.Paren(inner, _, _, _)
    | SynExpr.Typed(inner, _, _) -> isAcquisition inner
    | _ ->
        match pipe expr with
        | Some(_, target) -> headIn acquisitionNames target
        | None ->
            match expr with
            | SynExpr.App _ -> headIn acquisitionNames expr
            | _ -> false

let rec private boundName (pattern: SynPat) : string option =
    match pattern with
    | SynPat.Named(ident = SynIdent(ident, _)) -> Some ident.idText
    | SynPat.Paren(inner, _) -> boundName inner
    | SynPat.Typed(inner, _, _) -> boundName inner
    | _ -> None

/// Walks a computation expression body to its final expression, remembering which names were bound by `let!` to a
/// scoped acquisition along the way, and reports whether that final expression returns one of them.
let rec private returnsAcquisition (acquired: Set<string>) (body: SynExpr) : bool =
    match body with
    | SynExpr.LetOrUse letOrUse ->
        let acquiredHere =
            if letOrUse.IsBang then
                letOrUse.Bindings
                |> List.choose (fun (SynBinding(headPat = pattern; expr = rhs)) ->
                    if isAcquisition rhs then boundName pattern else None)
                |> Set.ofList
            else
                Set.empty

        returnsAcquisition (Set.union acquired acquiredHere) letOrUse.Body
    | SynExpr.Sequential(_, _, _, next, _, _) -> returnsAcquisition acquired next
    | SynExpr.YieldOrReturnFrom(expr = returned) -> isAcquisition returned
    | SynExpr.YieldOrReturn(expr = returned) ->
        match returned with
        | SynExpr.Ident ident -> Set.contains ident.idText acquired
        | _ -> false
    | _ -> false

/// True when `expr` is a flow whose value is a scoped resource it acquires: an acquisition itself, or a
/// `flow { }` that returns one.
let rec private returnsScopedResource (expr: SynExpr) : bool =
    match expr with
    | SynExpr.Paren(inner, _, _, _)
    | SynExpr.Typed(inner, _, _) -> returnsScopedResource inner
    | SynExpr.App(_, false, builder, SynExpr.ComputationExpr(_, body, _), _) when headIdent builder = Some "flow" ->
        returnsAcquisition Set.empty body
    | _ -> isAcquisition expr

let private childExprs (expr: SynExpr) : SynExpr list =
    match expr with
    | SynExpr.App(_, _, funcExpr, argExpr, _) -> [ funcExpr; argExpr ]
    | SynExpr.TypeApp(funcExpr, _, _, _, _, _, _) -> [ funcExpr ]
    | SynExpr.Paren(inner, _, _, _) -> [ inner ]
    | SynExpr.Typed(inner, _, _) -> [ inner ]
    | SynExpr.Tuple(_, exprs, _, _) -> exprs
    | SynExpr.ArrayOrList(_, exprs, _) -> exprs
    | SynExpr.ArrayOrListComputed(_, inner, _) -> [ inner ]
    | SynExpr.Sequential(_, _, expr1, expr2, _, _) -> [ expr1; expr2 ]
    | SynExpr.IfThenElse(cond, thenExpr, elseExpr, _, _, _, _) ->
        [ yield cond
          yield thenExpr
          yield! Option.toList elseExpr ]
    | SynExpr.Match(expr = matchExpr; clauses = clauses)
    | SynExpr.MatchBang(expr = matchExpr; clauses = clauses) ->
        matchExpr
        :: (clauses
            |> List.collect (fun (SynMatchClause(resultExpr = resultExpr; whenExpr = whenExpr)) ->
                resultExpr :: Option.toList whenExpr))
    | SynExpr.LetOrUse letOrUse -> (letOrUse.Bindings |> List.map (fun (SynBinding(expr = e)) -> e)) @ [ letOrUse.Body ]
    | SynExpr.TryWith(tryExpr, clauses, _, _, _, _) ->
        tryExpr
        :: (clauses
            |> List.collect (fun (SynMatchClause(resultExpr = resultExpr; whenExpr = whenExpr)) ->
                resultExpr :: Option.toList whenExpr))
    | SynExpr.TryFinally(tryExpr, finallyExpr, _, _, _, _) -> [ tryExpr; finallyExpr ]
    | SynExpr.Lambda(body = body) -> [ body ]
    | SynExpr.While(_, cond, body, _) -> [ cond; body ]
    | SynExpr.ForEach(enumExpr = enumExpr; bodyExpr = bodyExpr) -> [ enumExpr; bodyExpr ]
    | SynExpr.Do(inner, _) -> [ inner ]
    | SynExpr.DoBang(inner, _, _) -> [ inner ]
    | SynExpr.YieldOrReturn(expr = inner) -> [ inner ]
    | SynExpr.YieldOrReturnFrom(expr = inner) -> [ inner ]
    | SynExpr.ComputationExpr(_, inner, _) -> [ inner ]
    | _ -> []

/// Every forked flow that returns a scoped resource, as `Flow.fork body` (the flow is the last argument of a fork
/// application) or `body |> Flow.fork`.
let rec private findForkedResources (expr: SynExpr) : range list =
    let self =
        match pipe expr with
        | Some(value, target) when headIn forkNames target && returnsScopedResource value -> [ expr.Range ]
        | Some _ -> []
        | None ->
            match expr with
            | SynExpr.App(_, false, funcExpr, argExpr, range) when headIn forkNames funcExpr && returnsScopedResource argExpr ->
                [ range ]
            | _ -> []

    self @ (childExprs expr |> List.collect findForkedResources)

let rec private allExprsInModule (decls: SynModuleDecl list) : SynExpr list =
    decls
    |> List.collect (fun decl ->
        match decl with
        | SynModuleDecl.Let(bindings = bindings) -> bindings |> List.map (fun (SynBinding(expr = e)) -> e)
        | SynModuleDecl.Expr(expr, _) -> [ expr ]
        | SynModuleDecl.NestedModule(decls = nested) -> allExprsInModule nested
        | SynModuleDecl.Types(typeDefns, _) ->
            typeDefns
            |> List.collect (fun (SynTypeDefn(typeRepr = repr; members = extraMembers)) ->
                let memberExprs =
                    match repr with
                    | SynTypeDefnRepr.ObjectModel(members = members) -> members
                    | _ -> []

                (memberExprs @ extraMembers)
                |> List.choose (fun m ->
                    match m with
                    | SynMemberDefn.Member(SynBinding(expr = e), _) -> Some e
                    | _ -> None))
        | _ -> [])

let private message (range: range) : Message =
    { Type = "Axial Resource From Fiber"
      Message =
        "This fiber returns a scoped resource it acquired itself. A forked fiber releases its scoped resources when "
        + "it settles, so Fiber.join would hand back a resource that is already released. Use "
        + "`Flow.forkResource resource` to acquire it in the background and keep it in the caller's scope, or "
        + "`Flow.zipPar`/`Flow.sequencePar` to acquire several in parallel. If the fiber uses the resource itself "
        + "and the returned value is never used after the fiber settles, mark it: "
        + "`// axial-allow-resource-from-fiber` on this line or the line above."
      Code = "AXG007"
      Severity = Severity.Warning
      Range = range
      Fixes = [] }

/// Runs the check without applying guardrail configuration, for tests.
let analyze (ctx: CliContext) : Message list =
    let moduleExprs =
        match ctx.ParseFileResults.ParseTree with
        | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
            modules
            |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> allExprsInModule decls)
        | _ -> []

    moduleExprs
    |> List.collect findForkedResources
    |> List.distinctBy (fun range -> range.StartLine, range.StartColumn, range.EndLine, range.EndColumn)
    |> List.filter (fun range -> not (isAllowed ctx.SourceText range.StartLine))
    |> List.map message

[<CliAnalyzer("ResourceFromFiber",
              "Flags a forked flow whose value is a scoped resource it acquired itself, which the fiber releases "
              + "before Fiber.join returns it.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/03-guardrails.md")>]
let resourceFromFiberAnalyzer: Analyzer<CliContext> =
    fun ctx -> async { return analyze ctx |> GuardrailConfig.applyCore }
