/// The `TaskSurface` analyzer (AXG014, guide): keeps raw Task out of domain and service code and keeps task bridges
/// thin elsewhere. Typed: the builder of a computation expression is resolved to FSharp.Core's TaskBuilder or
/// BackgroundTaskBuilder (so shadowing and aliases are exact), and declared return types are resolved through
/// abbreviations to Task, Task<'T>, ValueTask or ValueTask<'T>.
module Axial.Guardrails.TaskSurfaceAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

let private builderTypes = set [ "Microsoft.FSharp.Control.TaskBuilder"; "Microsoft.FSharp.Control.BackgroundTaskBuilder" ]

let private taskTypes =
    Map [ "System.Threading.Tasks.Task", "Task"; "System.Threading.Tasks.Task`1", "Task<'T>"
          "System.Threading.Tasks.ValueTask", "ValueTask"; "System.Threading.Tasks.ValueTask`1", "ValueTask<'T>" ]

let rec private definition (t: FSharpType) =
    if t.IsAbbreviation then definition t.AbbreviatedType
    elif t.HasTypeDefinition then t.TypeDefinition.TryFullName
    else None

let private isTaskBuilder (ctx: CliContext) (builder: SynExpr) =
    (SymbolIndex.forContext ctx).ContainedIn builder.Range
    |> Array.exists (fun site ->
        match site.Symbol with
        | :? FSharpMemberOrFunctionOrValue as value ->
            (try definition value.FullType with _ -> None) |> Option.exists builderTypes.Contains
        | _ -> false)

let rec private publicEntity (entity: FSharpEntity) =
    entity.Accessibility.IsPublic && (entity.DeclaringEntity |> Option.forall publicEntity)

let private returnedTask (value: FSharpMemberOrFunctionOrValue) =
    try definition value.ReturnParameter.Type |> Option.bind taskTypes.TryFind with _ -> None

let private publicSignature (value: FSharpMemberOrFunctionOrValue) =
    not value.IsCompilerGenerated
    && value.Accessibility.IsPublic
    && (value.DeclaringEntity |> Option.forall publicEntity)
    && not value.IsOverrideOrExplicitInterfaceImplementation
    && not value.IsExplicitInterfaceImplementation

/// Declared public functions, values and members, plus abstract slots of public interfaces and classes.
let private signatures (ctx: CliContext) =
    let rec declarations declaration =
        match declaration with
        | FSharpImplementationFileDeclaration.Entity(entity, nested) ->
            let slots =
                if publicEntity entity then
                    entity.MembersFunctionsAndValues |> Seq.filter (fun value -> value.IsDispatchSlot && publicSignature value) |> Seq.toList
                else []
            slots @ List.collect declarations nested
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(value, _, _) when publicSignature value -> [ value ]
        | _ -> []
    match (try ctx.TypedTree with _ -> None) with
    | Some tree ->
        (try tree.Declarations |> List.collect declarations with _ -> [])
        |> List.choose (fun value -> returnedTask value |> Option.map (fun taskType -> value, taskType))
        |> List.distinctBy (fun (value, _) -> value.DeclarationLocation.StartLine, value.DeclarationLocation.StartColumn)
    | None -> []

let private nodes (ctx: CliContext) =
    ParsedInput.fold (fun nodes path node -> (path, node) :: nodes) [] ctx.ParseFileResults.ParseTree |> List.rev

/// Raw candidate ranges for every tier, for note validation.
let private collect (tier: TaskSurfaceSyntax.Tier) (ctx: CliContext) =
    let kind = "Axial Task Surface"
    let builder (expression: TaskSurfaceSyntax.Expression) = TaskSurfaceSyntax.builderName expression.Builder |> Option.defaultValue "task"
    let expressions =
        TaskSurfaceSyntax.expressions (nodes ctx)
        |> List.filter (fun expression -> isTaskBuilder ctx expression.Builder)
        |> List.choose (fun expression ->
            match tier with
            | TaskSurfaceSyntax.Strict -> Some(expression.Builder.Range, TaskSurfaceSyntax.expressionMessage (builder expression))
            | TaskSurfaceSyntax.Bridge when expression.Composes -> Some(expression.Builder.Range, TaskSurfaceSyntax.bridgeMessage (builder expression) expression)
            | _ -> None)
    let declared =
        match tier with
        | TaskSurfaceSyntax.Strict ->
            signatures ctx |> List.map (fun (value, taskType) -> value.DeclarationLocation, TaskSurfaceSyntax.signatureMessage value.DisplayName taskType)
        | _ -> []
    (expressions @ declared)
    |> List.map (fun (range, text) -> TaskSurfaceSyntax.finding ctx.SourceText kind range text)

let analyzeWithConfig (config: GuardrailConfig.Config) (ctx: CliContext) : Message list =
    if GuardrailConfig.isSkippedSource config ctx.FileName then [] else
    match TaskSurfaceSyntax.tier config ctx.FileName with
    | TaskSurfaceSyntax.Off -> []
    | tier ->
        SymbolIndex.memo ctx ("AXG014.findings", tier) (fun () -> collect tier ctx)
        |> GuardrailConfig.applyWith config EffectCatalog.CoreGuardrail ctx

/// Ranges of current findings, for `axial-note AXG014` validation.
let liveRanges (config: GuardrailConfig.Config) (ctx: CliContext) = analyzeWithConfig config ctx |> List.map _.Range

[<CliAnalyzer("TaskSurface", "Guides raw Task out of domain code and keeps task bridges thin.",
              "https://github.com/adz/Axial/blob/main/docs/guardrails/02-diagnostics.md#axg014-keep-raw-task-at-the-edge")>]
let taskSurfaceAnalyzer: Analyzer<CliContext> =
    fun ctx -> Execution.track "TaskSurface" (fun ctx -> async { return analyzeWithConfig (GuardrailConfig.load ()) ctx }) ctx
