/// The `RaiseInFlow` analyzer: flags `raise`/`failwith`/`failwithf`/`invalidOp`/`invalidArg`
/// called directly inside a `flow { }` computation expression.
///
/// A `Flow<'env, 'error, 'value>` already has a typed `'error` channel for expected failures.
/// Reaching for an ordinary F# exception idiom inside `flow { }` is the single most common
/// mistake when writing Axial code for the first time - it's the natural instinct carried over
/// from plain F#/C#, and it silently turns what should be a `'error` value into a defect
/// (`Cause.Die`) instead, which callers matching on the `'error` type never see coming.
module Axial.Guardrails.RaiseInFlowAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open Axial.Guardrails.Suppressions

let private bannedIdents =
    set [ "raise"; "failwith"; "failwithf"; "invalidOp"; "invalidArg"; "reraise" ]

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
type private RaiseFinding =
    { Range: range
      Ident: string
      ArgumentRange: range
      CaughtLocally: bool }

let rec private findRaises caughtLocally (expr: SynExpr) : RaiseFinding list =
    match expr with
    | SynExpr.TryWith(tryExpr, clauses, _, _, _, _) ->
        findRaises true tryExpr
        @ (clauses
           |> List.collect (fun (SynMatchClause(resultExpr = resultExpr; whenExpr = whenExpr)) ->
               findRaises caughtLocally resultExpr
               @ (whenExpr |> Option.toList |> List.collect (findRaises caughtLocally))))
    | SynExpr.App(_, _, funcExpr, argExpr, range) ->
        match headIdent expr with
        | Some ident when Set.contains ident bannedIdents ->
            { Range = range; Ident = ident; ArgumentRange = argExpr.Range; CaughtLocally = caughtLocally }
            :: findRaises caughtLocally argExpr
        | _ -> findRaises caughtLocally funcExpr @ findRaises caughtLocally argExpr
    | _ -> childExprs expr |> List.collect (findRaises caughtLocally)

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

/// The ranges of every `flow { }` body in the file, for rules that apply only inside a flow.
let flowBodyRanges (ctx: CliContext) : range list =
    match ctx.ParseFileResults.ParseTree with
    | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
        modules
        |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> allExprsInModule decls)
        |> List.collect flowCeBodies
        |> List.map _.Range
    | _ -> []

let private textAtRange (sourceText: ISourceText) (range: range) =
    if range.StartLine <> range.EndLine then None
    else
        let line = sourceText.GetLineString(range.StartLine - 1)
        if range.StartColumn < 0 || range.EndColumn > line.Length || range.EndColumn <= range.StartColumn then None
        else Some(line.Substring(range.StartColumn, range.EndColumn - range.StartColumn), line)

let private contains (outer: range) (inner: range) = FSharp.Compiler.Text.Range.rangeContainsRange outer inner

let private allTypedExpressions (ctx: CliContext) =
    let rec expressions (expression: FSharpExpr) =
        expression :: (expression.ImmediateSubExpressions |> List.collect expressions)
    let rec declarations (declaration: FSharpImplementationFileDeclaration) =
        match declaration with
        | FSharpImplementationFileDeclaration.Entity(_, nested) -> nested |> List.collect declarations
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(_, _, body)
        | FSharpImplementationFileDeclaration.InitAction body -> expressions body
    match (try ctx.TypedTree with _ -> None) with // axial-allow-AXG010: FCS may have no translatable typed tree during error recovery; syntax raise checks still run.
    | Some tree -> try tree.Declarations |> List.collect declarations with _ -> [] // axial-allow-AXG010: Unreadable recovered typed declarations are skipped while syntax raise checks remain available.
    | None -> []

let rec private entityFullName (typ: FSharpType) =
    if typ.HasTypeDefinition then typ.TypeDefinition.TryFullName
    elif typ.IsAbbreviation then entityFullName typ.AbbreviatedType
    else None

let rec private isExceptionEntity (entity: FSharpEntity) =
    entity.TryFullName = Some "System.Exception"
    || (entity.BaseType |> Option.exists (fun typ -> typ.HasTypeDefinition && isExceptionEntity typ.TypeDefinition))

let private typeAtRange (typed: FSharpExpr list) (target: range) =
    typed
    |> List.filter (fun expression -> contains expression.Range target)
    |> List.sortBy (fun expression ->
        expression.Range.EndLine - expression.Range.StartLine,
        expression.Range.EndColumn - expression.Range.StartColumn)
    |> List.tryHead
    |> Option.map _.Type

let rec private flowErrorType (typ: FSharpType) =
    match entityFullName typ with
    | Some name when name.StartsWith("Axial.Flow", System.StringComparison.Ordinal) && typ.GenericArguments.Count = 3 ->
        Some typ.GenericArguments[1]
    | Some "Microsoft.FSharp.Core.FSharpFunc`2" when typ.GenericArguments.Count = 2 ->
        flowErrorType typ.GenericArguments[1]
    | _ -> None

let private flowErrorTypeAtRange (typed: FSharpExpr list) (target: range) =
    typed
    |> List.choose (fun expression ->
        flowErrorType expression.Type |> Option.map (fun errorType -> expression.Range, errorType))
    |> List.filter (fun (range, _) -> contains range target)
    |> List.sortBy (fun (range, _) -> range.EndLine - range.StartLine, range.EndColumn - range.StartColumn)
    |> List.tryHead
    |> Option.map snd

let private mechanicalFix (ctx: CliContext) (finding: RaiseFinding) =
    let sourceText = ctx.SourceText
    let range = finding.Range
    let ident = finding.Ident
    let typed = allTypedExpressions ctx
    let replacement =
        if finding.CaughtLocally then None
        elif ident = "raise" then
            typeAtRange typed finding.ArgumentRange
            |> Option.filter (fun typ -> typ.HasTypeDefinition && isExceptionEntity typ.TypeDefinition)
            |> Option.map (fun _ -> "die")
        elif Set.contains ident (set [ "failwith"; "failwithf"; "invalidOp" ]) then
            flowErrorTypeAtRange typed range
            |> Option.filter (fun typ ->
                entityFullName typ = Some "System.String"
                || string typ = "type Microsoft.FSharp.Core.string")
            |> Option.map (fun _ -> "fail")
        else None
    match replacement, textAtRange sourceText range with
    | Some operation, Some(fromText, line)
        when line.Substring(0, range.StartColumn).Trim() = ""
             && (let suffix = line.Substring(range.EndColumn).TrimStart()
                 suffix = "" || suffix.StartsWith("//"))
             ->
        let argument = fromText.Substring(ident.Length).Trim()
        let value = if ident = "failwithf" then $"(sprintf {argument})" else argument
        Some { FromRange = range; FromText = fromText; ToText = $"return! Flow.{operation} {value}" }
    | _ -> None

let private message (ctx: CliContext) (finding: RaiseFinding) : Message =
    let range = finding.Range
    let ident = finding.Ident
    { Type = "Axial Raise In Flow"
      Message =
        $"`{ident}` is called directly inside a `flow {{ }}` block. Flow already has a typed 'error "
        + "channel for expected failures - an ordinary exception here silently becomes a defect "
        + "(Cause.Die) instead, which callers matching on 'error never see. Replace it with "
        + "`return! Flow.fail err` for an expected failure, or `return! Flow.die defect` for an "
        + "unrecoverable defect."
      Code = "AXG003"
      Severity = Severity.Error
      Range = range
      Fixes = mechanicalFix ctx finding |> Option.toList }

let rawRanges (ctx: CliContext) =
    match ctx.ParseFileResults.ParseTree with
    | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
        modules
        |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> allExprsInModule decls)
        |> List.collect flowCeBodies
        |> List.collect (findRaises false)
        |> List.map _.Range
    | _ -> []

[<CliAnalyzer("RaiseInFlow",
              "Flags raise/failwith/failwithf/invalidOp/invalidArg called directly inside a flow { } "
              + "block, where it silently becomes an unhandled defect instead of routing through the "
              + "typed 'error channel.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/04-guardrail-diagnostics.md#axg003-return-typed-failures-from-workflows")>]
let raiseInFlowAnalyzer: Analyzer<CliContext> =
    fun (ctx: CliContext) ->
        async {
            let sourceText = ctx.SourceText

            let messages =
                let moduleExprs =
                    match ctx.ParseFileResults.ParseTree with
                    | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
                        modules |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> allExprsInModule decls)
                    | _ -> []
                moduleExprs |> List.collect flowCeBodies |> List.collect (findRaises false)
                |> List.choose (fun finding ->
                    let config = GuardrailConfig.load ()
                    if isAllowed config.BoundaryFiles ctx.FileName sourceText finding.Range.StartLine "AXG003" then None
                    else Some(message ctx finding))

            return GuardrailConfig.applyCore ctx messages
        }
