/// The `RaiseInFlow` analyzer: flags `raise`/`failwith`/`failwithf`/`invalidOp`/`invalidArg`
/// called directly inside a `flow { }` computation expression.
///
/// A `Flow<'env, 'error, 'value>` already has a typed `'error` channel for expected failures.
/// Reaching for an ordinary F# exception idiom inside `flow { }` is the single most common
/// mistake when writing Axial code for the first time - it's the natural instinct carried over
/// from plain F#/C#, and it silently turns what should be a `'error` value into a defect
/// (`Cause.Die`) instead, which callers matching on the `'error` type never see coming.
module Axial.Guardrails.RaiseInFlowAnalyzer

open System.Text.RegularExpressions
open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

let private bannedIdents =
    set [ "raise"; "failwith"; "failwithf"; "invalidOp"; "invalidArg"; "reraise" ]

let private allowDirective =
    Regex(@"axial-allow-raise\b", RegexOptions.Compiled)

let private isAllowed (sourceText: ISourceText) (line1: int) : bool =
    let textOfLine1 (n: int) =
        if n >= 1 && n <= sourceText.GetLineCount() then
            sourceText.GetLineString(n - 1)
        else
            ""

    allowDirective.IsMatch(textOfLine1 line1) || allowDirective.IsMatch(textOfLine1 (line1 - 1))

/// The identifier a `SynExpr.App` chain is ultimately applying, e.g. `raise` in `raise (Foo x)`
/// or `failwithf "%s" x`. F# curries applications, so `SynExpr.App` nests: unwrap to the head.
let rec private headIdent (expr: SynExpr) : string option =
    match expr with
    | SynExpr.Ident ident -> Some ident.idText
    | SynExpr.LongIdent(_, longIdent, _, _) -> longIdent.LongIdent |> List.tryLast |> Option.map (fun i -> i.idText)
    | SynExpr.App(_, _, funcExpr, _, _) -> headIdent funcExpr
    | SynExpr.TypeApp(funcExpr, _, _, _, _, _, _) -> headIdent funcExpr
    | SynExpr.Paren(inner, _, _, _) -> headIdent inner
    | _ -> None

/// Finds every `raise`/`failwith`/... application inside `expr`, recursing into the expression
/// tree generically. Missing a rarer SynExpr case only means a false negative (a raise this
/// walker doesn't reach), never a false positive, which is the safe direction to fail in for a
/// heuristic like this.
///
/// A curried call `invalidArg "x" "y"` parses as nested `App(App(invalidArg, "x"), "y")`; both
/// App nodes share the same head identifier, so this only recurses into the argument (never the
/// function position) once a match is found, to report each call once instead of once per arg.
let rec private findRaises (expr: SynExpr) : (range * string) list =
    match expr with
    | SynExpr.App(_, _, funcExpr, argExpr, range) ->
        match headIdent expr with
        | Some ident when Set.contains ident bannedIdents -> (range, ident) :: findRaises argExpr
        | _ -> findRaises funcExpr @ findRaises argExpr
    | _ -> childExprs expr |> List.collect findRaises

and private childExprs (expr: SynExpr) : SynExpr list =
    match expr with
    | SynExpr.App(_, _, funcExpr, argExpr, _) -> [ funcExpr; argExpr ]
    | SynExpr.TypeApp(funcExpr, _, _, _, _, _, _) -> [ funcExpr ]
    | SynExpr.Paren(inner, _, _, _) -> [ inner ]
    | SynExpr.Typed(inner, _, _) -> [ inner ]
    | SynExpr.Tuple(_, exprs, _, _) -> exprs
    | SynExpr.ArrayOrList(_, exprs, _) -> exprs
    | SynExpr.ArrayOrListComputed(_, inner, _) -> [ inner ]
    | SynExpr.New(_, _, inner, _) -> [ inner ]
    | SynExpr.Sequential(_, _, expr1, expr2, _, _) -> [ expr1; expr2 ]
    | SynExpr.IfThenElse(cond, thenExpr, elseExpr, _, _, _, _) ->
        [ yield cond
          yield thenExpr
          yield! Option.toList elseExpr ]
    | SynExpr.Match(expr = matchExpr; clauses = clauses) ->
        matchExpr
        :: (clauses
            |> List.collect (fun (SynMatchClause(resultExpr = resultExpr; whenExpr = whenExpr)) ->
                resultExpr :: Option.toList whenExpr))
    | SynExpr.MatchBang(expr = matchExpr; clauses = clauses) ->
        matchExpr
        :: (clauses
            |> List.collect (fun (SynMatchClause(resultExpr = resultExpr; whenExpr = whenExpr)) ->
                resultExpr :: Option.toList whenExpr))
    | SynExpr.LetOrUse(letOrUse) -> (letOrUse.Bindings |> List.map (fun (SynBinding(expr = e)) -> e)) @ [ letOrUse.Body ]
    | SynExpr.TryWith(tryExpr, clauses, _, _, _, _) ->
        tryExpr
        :: (clauses
            |> List.collect (fun (SynMatchClause(resultExpr = resultExpr; whenExpr = whenExpr)) ->
                resultExpr :: Option.toList whenExpr))
    | SynExpr.TryFinally(tryExpr, finallyExpr, _, _, _, _) -> [ tryExpr; finallyExpr ]
    | SynExpr.Lambda(body = body) -> [ body ]
    | SynExpr.While(_, cond, body, _) -> [ cond; body ]
    | SynExpr.For(identBody = identBody; toBody = toBody; doBody = doBody) -> [ identBody; toBody; doBody ]
    | SynExpr.ForEach(enumExpr = enumExpr; bodyExpr = bodyExpr) -> [ enumExpr; bodyExpr ]
    | SynExpr.Do(inner, _) -> [ inner ]
    | SynExpr.DoBang(inner, _, _) -> [ inner ]
    | SynExpr.YieldOrReturn(expr = inner) -> [ inner ]
    | SynExpr.YieldOrReturnFrom(expr = inner) -> [ inner ]
    | SynExpr.ComputationExpr(_, inner, _) -> [ inner ]
    | SynExpr.Record(_, _, fields, _) ->
        fields
        |> List.choose (fun (SynExprRecordField(expr = e)) -> e)
    | SynExpr.ObjExpr(bindings = bindings) -> bindings |> List.map (fun (SynBinding(expr = e)) -> e)
    | SynExpr.Assert(inner, _) -> [ inner ]
    | SynExpr.AddressOf(_, inner, _, _) -> [ inner ]
    | SynExpr.Downcast(inner, _, _) -> [ inner ]
    | SynExpr.Upcast(inner, _, _) -> [ inner ]
    | SynExpr.InferredDowncast(inner, _) -> [ inner ]
    | SynExpr.InferredUpcast(inner, _) -> [ inner ]
    | _ -> []

/// True when `expr` is the body of a `flow { ... }` computation expression: an application whose
/// function is the `flow` identifier and whose argument is a `ComputationExpr`.
let private isFlowCe (expr: SynExpr) : bool =
    match expr with
    | SynExpr.App(_, false, funcExpr, SynExpr.ComputationExpr _, _) ->
        match headIdent funcExpr with
        | Some "flow" -> true
        | _ -> false
    | _ -> false

let rec private flowCeBodies (expr: SynExpr) : SynExpr list =
    match expr with
    | SynExpr.App(_, false, _, SynExpr.ComputationExpr(_, inner, _), _) when isFlowCe expr ->
        // Don't also fall through to the generic childExprs recursion below: that would walk
        // into this same App's ComputationExpr argument a second time and double-count `inner`.
        // Recursing into `inner` here still finds a flow { } nested inside this one.
        inner :: flowCeBodies inner
    | _ -> childExprs expr |> List.collect flowCeBodies

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

let private message (range: range) (ident: string) : Message =
    { Type = "Axial Raise In Flow"
      Message =
        $"`{ident}` is called directly inside a `flow {{ }}` block. Flow already has a typed 'error "
        + "channel for expected failures - an ordinary exception here silently becomes a defect "
        + "(Cause.Die) instead, which callers matching on 'error never see. Use `return! Flow.fail err` "
        + "(or `Flow.die`, if this really is meant to be an unrecoverable defect). If this call is "
        + "intentional, mark it: `// axial-allow-raise` on this line or the line above."
      Code = "AXG003"
      Severity = Severity.Warning
      Range = range
      Fixes = [] }

[<CliAnalyzer("RaiseInFlow",
              "Flags raise/failwith/failwithf/invalidOp/invalidArg called directly inside a flow { } "
              + "block, where it silently becomes an unhandled defect instead of routing through the "
              + "typed 'error channel.",
              "https://github.com/adz/Axial/blob/main/docs/guardrails.md")>]
let raiseInFlowAnalyzer: Analyzer<CliContext> =
    fun (ctx: CliContext) ->
        async {
            let sourceText = ctx.SourceText

            let moduleExprs =
                match ctx.ParseFileResults.ParseTree with
                | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
                    modules
                    |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> allExprsInModule decls)
                | _ -> []

            let messages =
                moduleExprs
                |> List.collect flowCeBodies
                |> List.collect findRaises
                |> List.choose (fun (range, ident) ->
                    if isAllowed sourceText range.StartLine then
                        None
                    else
                        Some(message range ident))

            return messages
        }
