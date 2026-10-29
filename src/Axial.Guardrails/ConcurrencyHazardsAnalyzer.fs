/// Concurrency escape hatches outside the runtime and silently abandoned work.
module Axial.Guardrails.ConcurrencyHazardsAnalyzer

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTrivia
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open Axial.Guardrails.ResourceFromFiberAnalyzer

let private expressions (ctx: CliContext) =
    match ctx.ParseFileResults.ParseTree with
    | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
        modules |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> allExprsInModule decls)
    | _ -> []

let rec private walk expr = expr :: (childExprs expr |> List.collect walk)

let rec private handlerExpressions expr =
    let children =
        match expr with
        // Recording an exception in mutable state is handling it, too.
        | SynExpr.Record(_, _, fields, _) ->
            fields |> List.choose (fun (SynExprRecordField(expr = value)) -> value)
        | SynExpr.DotGet(target, _, _, _) -> [ target ]
        | SynExpr.TypeTest(value, _, _) -> [ value ]
        | SynExpr.LongIdentSet(_, value, _) -> [ value ]
        | SynExpr.DotSet(target, _, value, _) | SynExpr.Set(target, value, _) -> [ target; value ]
        | _ -> childExprs expr
    expr :: (children |> List.collect handlerExpressions)

let private key (r: range) = r.StartLine, r.StartColumn, r.EndLine, r.EndColumn

let private symbolRanges code (ctx: CliContext) =
    ctx.GetAllSymbolUsesOfFile()
    |> Seq.choose (fun site ->
        match site.Symbol with
        | :? FSharpMemberOrFunctionOrValue as value ->
            let owner = value.DeclaringEntity |> Option.bind _.TryFullName |> Option.defaultValue ""
            let name = value.CompiledName
            let taskType = Set.contains owner (set [ "System.Threading.Tasks.Task"; "System.Threading.Tasks.Task`1" ])
            let taskFactory = Set.contains owner (set [ "System.Threading.Tasks.TaskFactory"; "System.Threading.Tasks.TaskFactory`1" ])
            let raw =
                (Set.contains owner (set [ "System.Threading.Tasks.TaskCompletionSource"; "System.Threading.Tasks.TaskCompletionSource`1" ]) && value.IsConstructor)
                || (taskType && value.IsConstructor)
                || (owner = "System.Threading.CancellationTokenSource" && value.IsConstructor)
                || (owner = "System.Threading.Thread" && value.IsConstructor)
                || owner = "System.Threading.ThreadPool"
                || (taskType && Set.contains name (set [ "Run"; "ContinueWith" ]))
                || (taskFactory && name = "StartNew")
            let matches =
                match code with
                | "AXG009" -> raw
                | "AXG011" -> owner = "System.Threading.CancellationToken" && (name = "get_None" || name = "None")
                | _ -> false
            if matches then Some site.Range else None
        | _ -> None)
    |> Seq.toList

let rec private boundNames pat =
    match pat with
    | SynPat.Named(ident = SynIdent(id, _)) -> [ id.idText ]
    | SynPat.Paren(inner, _) | SynPat.Typed(inner, _, _) -> boundNames inner
    | SynPat.As(left, right, _) -> boundNames left @ boundNames right
    | _ -> []

let rec private baseExceptionType (t: FSharpType) =
    if t.IsAbbreviation then baseExceptionType t.AbbreviatedType
    else t.HasTypeDefinition && t.TypeDefinition.TryFullName = Some "System.Exception"

let rec private catchAll (ctx: CliContext) pat =
    match pat with
    | SynPat.Wild _ | SynPat.Named _ -> true
    | SynPat.Paren(inner, _) | SynPat.Typed(inner, _, _)
    | SynPat.Attrib(inner, _, _) | SynPat.As(inner, _, _) -> catchAll ctx inner
    | SynPat.Or(left, right, _, _) -> catchAll ctx left || catchAll ctx right
    | SynPat.Ands(patterns, _) -> patterns |> List.forall (catchAll ctx)
    | SynPat.IsInst(targetType, _) ->
        ctx.GetAllSymbolUsesOfFile()
        |> Seq.exists (fun site ->
            Range.rangeContainsRange targetType.Range site.Range
            && (match site.Symbol with
                | :? FSharpEntity as entity ->
                    if entity.IsFSharpAbbreviation then baseExceptionType entity.AbbreviatedType
                    else entity.TryFullName = Some "System.Exception"
                | _ -> false))
    | _ -> false

let private swallowed ctx expr =
    match expr with
    | SynExpr.TryWith(_, clauses, _, _, _, _) ->
        clauses |> List.choose (fun (SynMatchClause(pat = pat; resultExpr = body; whenExpr = guard)) ->
            let names = boundNames pat |> Set.ofList
            let handled =
                (handlerExpressions body @ (guard |> Option.toList |> List.collect handlerExpressions)) |> List.exists (fun node ->
                    match node with
                    | SynExpr.Ident id when names.Contains id.idText -> true
                    | SynExpr.LongIdent(_, ids, _, _) when ids.LongIdent |> List.exists (fun id -> names.Contains id.idText) -> true
                    | SynExpr.App _ ->
                        headIdent node |> Option.exists (fun name ->
                            name = "raise" || name = "reraise")
                    | _ -> false)
            if not (catchAll ctx pat) || handled then None else Some body.Range)
    | _ -> []

let rec private concurrentType (t: FSharpType) =
    if t.IsAbbreviation then concurrentType t.AbbreviatedType
    else t.HasTypeDefinition &&
         (t.TypeDefinition.TryFullName |> Option.exists (fun name ->
             Set.contains name (set [ "System.Threading.Tasks.Task"; "System.Threading.Tasks.Task`1";
                                       "System.Threading.Tasks.ValueTask"; "System.Threading.Tasks.ValueTask`1"; "Axial.Fiber`2" ])))

let private typedConcurrentRanges (ctx: CliContext) =
    let rec visit (expr: FSharpExpr) =
        [ if concurrentType expr.Type then yield expr.Range
          yield! expr.ImmediateSubExpressions |> List.collect visit ]
    let rec declarations declaration =
        match declaration with
        | FSharpImplementationFileDeclaration.Entity(_, nested) -> nested |> List.collect declarations
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(_, _, body)
        | FSharpImplementationFileDeclaration.InitAction body -> visit body
    match ctx.TypedTree with
    | Some tree -> tree.Declarations |> List.collect declarations |> List.map key |> Set.ofList
    | None -> Set.empty

let rawRanges code (ctx: CliContext) =
    let nodes = expressions ctx |> List.collect walk
    let syntactic =
        match code with
        | "AXG010" -> nodes |> List.collect (swallowed ctx)
        | "AXG012" ->
            let typed = typedConcurrentRanges ctx
            // Array/list syntax uses Sequential for retained elements, not discarded statements.
            let rec elements expr =
                match expr with
                | SynExpr.Sequential(_, _, first, next, _, _) -> elements first @ elements next
                | _ -> [ key expr.Range ]
            let collectionElements =
                nodes |> List.collect (function SynExpr.ArrayOrListComputed(_, inner, _) -> elements inner | _ -> [])
                |> Set.ofList
            let rec discarded (expr: SynExpr) =
                typed.Contains(key expr.Range)
                || (match expr with
                    // Typed lowering can erase the source range of an explicit task cast.
                    | SynExpr.Paren(inner, _, _, _) | SynExpr.Typed(inner, _, _)
                    | SynExpr.Upcast(inner, _, _) | SynExpr.Downcast(inner, _, _)
                    | SynExpr.InferredUpcast(inner, _) | SynExpr.InferredDowncast(inner, _) -> discarded inner
                    | _ -> false)
            let rec ignores (target: SynExpr) =
                match target with
                | SynExpr.Paren(inner, _, _, _) | SynExpr.Typed(inner, _, _)
                | SynExpr.TypeApp(inner, _, _, _, _, _, _) -> ignores inner
                | SynExpr.Ident _ | SynExpr.LongIdent _ ->
                    ctx.GetAllSymbolUsesOfFile()
                    |> Seq.exists (fun site ->
                        Range.rangeContainsRange target.Range site.Range
                        && (match site.Symbol with
                            | :? FSharpMemberOrFunctionOrValue as value ->
                                value.CompiledName = "Ignore"
                                && (value.DeclaringEntity |> Option.bind _.TryFullName) = Some "Microsoft.FSharp.Core.Operators"
                            | _ -> false))
                | _ -> false
            nodes |> List.collect (fun node ->
                match pipe node with
                | Some(value, target) when ignores target && discarded value -> [ value.Range ]
                | _ ->
                    match node with
                    | SynExpr.LetOrUse binding ->
                        binding.Bindings |> List.choose (fun (SynBinding(headPat = pat; expr = rhs; trivia = trivia)) ->
                            match trivia.LeadingKeyword with
                            | SynLeadingKeyword.LetBang _ | SynLeadingKeyword.UseBang _ | SynLeadingKeyword.AndBang _ -> None
                            | _ ->
                                match pat with
                                | SynPat.Wild _ when discarded rhs -> Some rhs.Range
                                | _ -> None)
                    | SynExpr.Sequential(_, _, first, _, _, _) when not (collectionElements.Contains(key first.Range)) && discarded first -> [ first.Range ]
                    | SynExpr.Do(inner, _) when discarded inner -> [ inner.Range ]
                    | SynExpr.App(_, false, target, value, _) when ignores target && discarded value -> [ value.Range ]
                    | _ -> [])
        | _ -> []
    symbolRanges code ctx @ syntactic |> List.distinctBy key

let private descriptions =
    Map.ofList [ "AXG009", "Use Flow concurrency rather than raw TPL coordination; move an OS bridge into a capability-role module."
                 "AXG010", "Consider propagating, recording, or reporting this exception; this catch-all handler ignores it."
                 "AXG011", "Use the runtime's uninterruptible cleanup primitive instead of CancellationToken.None."
                 "AXG012", "Concurrent work is discarded. Await it or retain and join its owning fiber." ]

let analyzeWithConfig (config: GuardrailConfig.Config) code (ctx: CliContext) =
    let role = GuardrailConfig.resolvedPolicy config ctx |> Option.map _.Role
    let exempt =
        match role, config.Policy, config.PolicyRoot, config.ProjectPath with
        | Some Policy.Host, Some policy, Some root, Some project ->
            let relative = Path.GetRelativePath(root, Path.GetFullPath project).Replace('\\', '/')
            policy.Exemptions |> Map.tryFind code |> Option.defaultValue []
            |> List.exists (fun pattern -> Policy.matchesGlob pattern relative)
        | _ -> false
    let runtime =
        match config.Policy, config.PolicyRoot with
        | Some policy, Some root -> policy.Runtime |> List.exists (fun path -> Path.GetFullPath(Path.Combine(root, path)) = Path.GetFullPath ctx.FileName)
        | _ -> false
    // The one definition that owns the uncancellable token: nothing else in src may name CancellationToken.None.
    let rec primitiveDefinitions declaration =
        match declaration with
        | FSharpImplementationFileDeclaration.Entity(_, nested) -> List.collect primitiveDefinitions nested
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(value, _, body)
            when value.CompiledName = "never"
                 && (value.DeclaringEntity |> Option.bind _.TryFullName) = Some "Axial.Platform.Cancellation" -> [ body.Range ]
        | _ -> []
    let primitives = ctx.TypedTree |> Option.map (fun tree -> List.collect primitiveDefinitions tree.Declarations) |> Option.defaultValue []
    if (code = "AXG009" || code = "AXG012") && runtime then []
    else
        rawRanges code ctx
        |> List.filter (fun r ->
            not (code = "AXG011" && primitives |> List.exists (fun definition -> Range.rangeContainsRange definition r))
            && role <> Some Policy.Test && not exempt && (code <> "AXG009" || role <> Some Policy.Capabilities))
        |> List.map (fun r ->
            { Type = "Axial Concurrency Hazard"; Code = code; Message = descriptions[code]
              Severity = Severity.Error; Range = r; Fixes = [] })
        |> List.map (fun message -> if code = "AXG010" then Suppressions.guideMessage config ctx message else message)
        |> GuardrailConfig.applyWith config EffectCatalog.CoreGuardrail ctx

[<CliAnalyzer("RawConcurrency", "Flags raw concurrency outside the runtime.", "https://github.com/adz/Axial")>]
let rawConcurrencyAnalyzer: Analyzer<CliContext> = fun ctx -> async { return analyzeWithConfig (GuardrailConfig.load ()) "AXG009" ctx }

[<CliAnalyzer("SwallowedException", "Flags unreported exceptions.", "https://github.com/adz/Axial")>]
let swallowedExceptionAnalyzer: Analyzer<CliContext> = fun ctx -> async { return analyzeWithConfig (GuardrailConfig.load ()) "AXG010" ctx }

[<CliAnalyzer("UncancellableWork", "Flags CancellationToken.None outside runtime cleanup.", "https://github.com/adz/Axial")>]
let uncancellableWorkAnalyzer: Analyzer<CliContext> = fun ctx -> async { return analyzeWithConfig (GuardrailConfig.load ()) "AXG011" ctx }

[<CliAnalyzer("DiscardedWork", "Flags discarded concurrent work.", "https://github.com/adz/Axial")>]
let discardedWorkAnalyzer: Analyzer<CliContext> = fun ctx -> async { return analyzeWithConfig (GuardrailConfig.load ()) "AXG012" ctx }
