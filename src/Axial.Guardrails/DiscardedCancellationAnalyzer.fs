/// The `DiscardedCancellation` analyzer: flags `ColdTask`/`ColdTask.create`/`Flow.fromTask`/
/// `Flow.fromTaskResult` given a lambda that discards its `CancellationToken` parameter
/// (`fun _ -> legacyCall ()`).
///
/// `docs/01-getting-started/03-existing-task-application.md` already calls this out in prose:
/// "Some legacy APIs do not accept a cancellation token. You can adapt one with
/// `ColdTask(fun _ -> legacyCall ())`, but cancelling the Flow cannot stop the underlying
/// operation." That's a real, easy-to-miss trap - the code compiles, looks like every other
/// adapter, and only shows up as a hang or a leaked operation under cancellation, which is hard
/// to reproduce and easy to blame on something else.
module Axial.Guardrails.DiscardedCancellationAnalyzer

open System.Text.RegularExpressions
open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

let private targetNames =
    set [ "ColdTask"; "ColdTask.create"; "Flow.fromTask"; "Flow.fromTaskResult" ]

let private allowDirective =
    Regex(@"axial-allow-discarded-cancellation\b", RegexOptions.Compiled)

let private isAllowed (sourceText: ISourceText) (line1: int) : bool =
    let textOfLine1 (n: int) =
        if n >= 1 && n <= sourceText.GetLineCount() then
            sourceText.GetLineString(n - 1)
        else
            ""

    allowDirective.IsMatch(textOfLine1 line1) || allowDirective.IsMatch(textOfLine1 (line1 - 1))

let rec private qualifiedName (expr: SynExpr) : string option =
    match expr with
    | SynExpr.Ident ident -> Some ident.idText
    | SynExpr.LongIdent(_, longIdent, _, _) ->
        Some(longIdent.LongIdent |> List.map (fun i -> i.idText) |> String.concat ".")
    | SynExpr.Paren(inner, _, _, _) -> qualifiedName inner
    | _ -> None

/// A `fun _ -> ...` parameter desugars to a compiler-generated identifier (its source text, `_`,
/// isn't preserved) - `isCompilerGenerated` is what's left to tell "the pattern wasn't a plain
/// name" apart from "the pattern was a plain name", which is the discard signal this needs.
let private isSingleDiscardedParam (pats: SynSimplePats) : bool =
    match pats with
    | SynSimplePats.SimplePats(pats = [ SynSimplePat.Id(isCompilerGenerated = true) ]) -> true
    | _ -> false

let rec private childExprs (expr: SynExpr) : SynExpr list =
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
    | SynExpr.LetOrUse(letOrUse) -> (letOrUse.Bindings |> List.map (fun (SynBinding(expr = e)) -> e)) @ [ letOrUse.Body ]
    | SynExpr.TryWith(tryExpr, clauses, _, _, _, _) ->
        tryExpr
        :: (clauses
            |> List.collect (fun (SynMatchClause(resultExpr = resultExpr; whenExpr = whenExpr)) ->
                resultExpr :: Option.toList whenExpr))
    | SynExpr.TryFinally(tryExpr, finallyExpr, _, _, _, _) -> [ tryExpr; finallyExpr ]
    | SynExpr.Lambda(body = body) -> [ body ]
    | SynExpr.ComputationExpr(_, inner, _) -> [ inner ]
    | SynExpr.YieldOrReturn(expr = inner) -> [ inner ]
    | SynExpr.YieldOrReturnFrom(expr = inner) -> [ inner ]
    | SynExpr.DoBang(inner, _, _) -> [ inner ]
    | SynExpr.Do(inner, _) -> [ inner ]
    | _ -> []

/// Finds every ColdTask/Flow.fromTask/Flow.fromTaskResult application whose argument is a lambda
/// discarding its single parameter, anywhere in `expr`.
let rec private findDiscards (expr: SynExpr) : range list =
    match expr with
    | SynExpr.App(_, _, funcExpr, argExpr, range) ->
        let isTarget =
            qualifiedName funcExpr
            |> Option.map (fun name -> Set.contains name targetNames)
            |> Option.defaultValue false

        let self =
            match argExpr with
            | SynExpr.Lambda(args = args) when isTarget && isSingleDiscardedParam args -> [ range ]
            | SynExpr.Paren(SynExpr.Lambda(args = args), _, _, _) when isTarget && isSingleDiscardedParam args ->
                [ range ]
            | _ -> []

        self @ findDiscards funcExpr @ findDiscards argExpr
    | _ -> childExprs expr |> List.collect findDiscards

let rec private allExprsInModule (decls: SynModuleDecl list) : SynExpr list =
    decls
    |> List.collect (fun decl ->
        match decl with
        | SynModuleDecl.Let(bindings = bindings) -> bindings |> List.map (fun (SynBinding(expr = e)) -> e)
        | SynModuleDecl.Expr(expr, _) -> [ expr ]
        | SynModuleDecl.NestedModule(decls = nested) -> allExprsInModule nested
        | _ -> [])

let private message (range: range) : Message =
    { Type = "Axial Discarded Cancellation"
      Message =
        "This lambda discards its CancellationToken parameter (`fun _ -> ...`), so cancelling the "
        + "Flow cannot stop the underlying operation - it keeps running until it finishes on its own. "
        + "If the wrapped call has a cancellation-aware overload, thread the token through instead. "
        + "If it's a legacy API that genuinely doesn't accept one, mark it: "
        + "`// axial-allow-discarded-cancellation` on this line or the line above."
      Code = "AXG005"
      Severity = Severity.Warning
      Range = range
      Fixes = [] }

[<CliAnalyzer("DiscardedCancellation",
              "Flags ColdTask/Flow.fromTask/Flow.fromTaskResult given a lambda that discards its "
              + "CancellationToken parameter, so cancelling the Flow cannot stop the underlying "
              + "operation.",
              "https://github.com/adz/Axial/blob/main/docs/guardrails.md")>]
let discardedCancellationAnalyzer: Analyzer<CliContext> =
    fun (ctx: CliContext) ->
        async {
            let sourceText = ctx.SourceText

            let moduleExprs =
                match ctx.ParseFileResults.ParseTree with
                | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
                    modules
                    |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> allExprsInModule decls)
                | _ -> []

            return
                moduleExprs
                |> List.collect findDiscards
                |> List.filter (fun range -> not (isAllowed sourceText range.StartLine))
                |> List.map message
        }
