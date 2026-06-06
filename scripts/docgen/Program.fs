open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Templating
open FSharp.Compiler.Symbols
open System
open System.IO
open System.Reflection
open System.Collections.Generic
open System.Net
open System.Text.RegularExpressions

type PageSpec = {
    OutPath: string list
    Title: string
    Description: string
    Intro: string
    SymbolIds: (string * string list) list
    Alias: string option
}

let normalize (name: string) =
    if String.IsNullOrEmpty name then ""
    else
        name.Replace("FsFlow.", "").Replace("Services.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s ->
            s
                .Split('(').[0]
                .Trim('.')
                |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"``[0-9]+(?=$|[.])", "")
                |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"`[0-9]+(?=$|[.])", "")
                |> fun value -> value.Replace("`", "").Replace("'", ""))

let cleanName (name: string) =
    if String.IsNullOrEmpty name then ""
    else
        name.Replace("FsFlow.", "").Replace("Services.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> s.Trim('.'))
        |> (fun s -> 
            s
            |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"``[0-9]+(?=$|[.])", "")
            |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"`[0-9]+(?=$|[.])", "")
        )
        |> (fun s -> s.Replace("'", ""))
        |> (fun s -> if s.EndsWith(".Static") then s.Substring(0, s.Length - 7) else s)

let sanitizeFilename (name: string) =
    name.Replace("`", "-").Replace("'", "-").Replace(" ", "-").Replace(".", "-").ToLower()
    |> (fun s -> s.Trim('-'))

let formatterApiSlug (name: string) =
    name.Replace("`", "-").Replace("'", "").Replace(".", "-").Replace("+", "-").ToLowerInvariant()

let getPageName (id: string) =
    let kind = id.[0].ToString().ToLower()
    let namePart = id.Substring(2).Split('(').[0]
    let clean = 
        namePart.Replace("FsFlow.", "").Replace("Services.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> System.Text.RegularExpressions.Regex.Replace(s, @"`[0-9]+", ""))
        |> (fun s -> s.Replace("'", "").Trim('.'))
        
    let finalName = sanitizeFilename clean
    $"{kind}-{finalName}.md"

let safeFullName (sym: FSharp.Compiler.Symbols.FSharpSymbol) =
    match sym with
    | :? FSharp.Compiler.Symbols.FSharpEntity as e ->
        try e.FullName with _ -> e.DisplayName
    | _ -> 
        try sym.FullName with _ -> sym.DisplayName

let logicalName (sym: FSharp.Compiler.Symbols.FSharpSymbol) =
    match sym with
    | :? FSharpMemberOrFunctionOrValue as mfv when mfv.IsExtensionMember ->
        try 
            match mfv.ApparentEnclosingEntity with
            | Some ent -> $"{ent.FullName}.{mfv.DisplayName}"
            | None -> safeFullName sym
        with _ -> safeFullName sym
    | _ -> safeFullName sym

let hasAttribute named (attrs: seq<FSharpAttribute>) =
    attrs
    |> Seq.exists (fun attr ->
        attr.AttributeType.DisplayName = named
        || attr.AttributeType.FullName.EndsWith("." + named, StringComparison.Ordinal))

let enclosingEntity (sym: FSharp.Compiler.Symbols.FSharpSymbol) =
    match sym with
    | :? FSharpMemberOrFunctionOrValue as mfv -> mfv.DeclaringEntity
    | :? FSharpField as field -> field.DeclaringEntity
    | _ -> None

let memberQualifier (m: ApiDocMember) =
    match m.Symbol with
    | :? FSharpMemberOrFunctionOrValue as mfv when mfv.IsExtensionMember ->
        try 
            match mfv.ApparentEnclosingEntity with
            | Some ent -> cleanName ent.FullName
            | None -> ""
        with _ -> ""
    | _ ->
        match enclosingEntity m.Symbol with
        | Some ent ->
            let moduleName = cleanName ent.FullName
            let isAutoOpen = hasAttribute "AutoOpenAttribute" ent.Attributes
            let isRequireQualifiedAccess = hasAttribute "RequireQualifiedAccessAttribute" ent.Attributes

            if isAutoOpen && not isRequireQualifiedAccess then ""
            else moduleName
        | None -> ""

let qualifyUsageHtml usageName (html: string) =
    let encodedUsageName = WebUtility.HtmlEncode usageName
    let parts = usageName.Split('.')
    let shortName = parts.[parts.Length - 1]
    let encodedShortName = WebUtility.HtmlEncode shortName

    // We look for patterns like <span>log&#32; or <span>Runtime.log&#32;
    // or without trailing space for property-like access.
    
    let patterns = [
        $"<span>{encodedShortName}&#32;", $"<span>{encodedUsageName}&#32;"
        $"<span>{encodedShortName}</span>", $"<span>{encodedUsageName}</span>"
    ]

    let mutable result = html
    let mutable replaced = false

    for pat, rep in patterns do
        if not replaced && result.Contains pat then
            result <- result.Replace(pat, rep)
            replaced <- true
    
    if not replaced && parts.Length > 1 then
        let midName = parts.[parts.Length - 2] + "." + shortName
        let encodedMidName = WebUtility.HtmlEncode midName
        let midPatterns = [
            $"<span>{encodedMidName}&#32;", $"<span>{encodedUsageName}&#32;"
            $"<span>{encodedMidName}</span>", $"<span>{encodedUsageName}</span>"
        ]
        for pat, rep in midPatterns do
            if not replaced && result.Contains pat then
                result <- result.Replace(pat, rep)
                replaced <- true
                
    result

let platformLabel (qualifiedName: string) =
    let netOnly =
        [ ".ToTask"; ".ToValueTask"; ".RunSynchronously"; ".fromTask"; ".fromValueTask" ]

    let fableCompatible =
        [ ".ToAsync"; ".fromAsync" ]

    if netOnly |> List.exists qualifiedName.EndsWith then
        Some ".NET only"
    elif fableCompatible |> List.exists qualifiedName.EndsWith then
        Some "Fable compatible"
    else
        None

let renderMemberPage (rewriteHtml: string -> string) (weight: int) (m: ApiDocMember) =
    let fullName = logicalName m.Symbol
    let qualifiedName = cleanName fullName
    let shortName = cleanName m.Name
    
    // Better link title for CEs
    let linkTitle = 
        if m.Name = "flow" then "flow { }"
        elif m.Name = "validate" then "validate { }"
        elif m.Name = "result" then "result { }"
        elif m.Name = "stm" then "stm { }"
        else shortName

    let mutable content = 
        $"---\ntitle: \"{qualifiedName}\"\nlinkTitle: \"{linkTitle}\"\nweight: {weight}\n---\n\n"
    
    // Description
    content <- content + rewriteHtml m.Comment.Summary.HtmlText + "\n\n"

    match platformLabel qualifiedName with
    | Some label ->
        content <- content + $"**Platform:** {label}\n\n"
    | None -> ()

    // Signature
    let qualifier = memberQualifier m
    let usageName = if String.IsNullOrEmpty qualifier then m.Name else qualifier + "." + m.Name
    let usageHtml =
        m.UsageHtml.HtmlText
        |> qualifyUsageHtml usageName
        |> rewriteHtml

    content <- content + "## Signature\n\n"
    content <- content + "<div class=\"fsdocs-usage\">\n" + usageHtml + "\n</div>\n\n"

    if not m.Parameters.IsEmpty then
        content <- content + "## Parameters\n\n"
        content <- content + "| Name | Type | Description |\n"
        content <- content + "| --- | --- | --- |\n"
        for p in m.Parameters do
            let docs =
                match p.ParameterDocs with
                | Some html -> rewriteHtml html.HtmlText
                | None -> ""

            content <- content + $"| `{p.ParameterNameText}` | {rewriteHtml p.ParameterType.HtmlText} | {docs} |\n"
        content <- content + "\n"

    content <- content + "## Returns\n\n"
    content <- content + "| Type | Description |\n"
    content <- content + "| --- | --- |\n"
    let returnDocs =
        match m.ReturnInfo.ReturnDocs with
        | Some html -> rewriteHtml html.HtmlText
        | None -> ""

    let returnType =
        match m.ReturnInfo.ReturnType with
        | Some (_, html) -> rewriteHtml html.HtmlText
        | None -> "<code>unit</code>"

    content <- content + $"| {returnType} | {returnDocs} |\n\n"

    match m.Comment.Remarks with
    | Some r -> content <- content + "## Remarks\n\n" + rewriteHtml r.HtmlText + "\n\n"
    | None -> ()

    if not m.Comment.Examples.IsEmpty then
        content <- content + "## Examples\n\n"
        for e in m.Comment.Examples do
            content <- content + rewriteHtml e.HtmlText + "\n\n"

    match m.SourceLocation with
    | Some url -> content <- content + $"\n[Source]({url})\n\n"
    | None -> ()

    content

let renderEntityPage (rewriteHtml: string -> string) (weight: int) (e: ApiDocEntity) =
    let fullName = safeFullName e.Symbol
    let qualifiedName = cleanName fullName
    let shortName = cleanName e.Name
    
    let mutable content = 
        $"---\ntitle: \"{qualifiedName}\"\nlinkTitle: \"{shortName}\"\nweight: {weight}\n---\n\n"
    
    // Construct signature
    let signature = 
        match e.Symbol with
        | :? FSharp.Compiler.Symbols.FSharpEntity as ent ->
            let generics = 
                if ent.GenericParameters.Count > 0 then
                    "<" + (ent.GenericParameters |> Seq.map (fun p -> "'" + p.DisplayName) |> String.concat ", ") + ">"
                else ""
            $"type {ent.DisplayName}{generics}"
        | _ -> $"type {shortName}"

    content <- content + rewriteHtml e.Comment.Summary.HtmlText + "\n\n"

    content <- content + "## Signature\n\n"
    content <- content + "<div class=\"fsdocs-usage\">\n" + $"<code>{signature}</code>" + "\n</div>\n\n"
    
    match e.Symbol with
    | :? FSharp.Compiler.Symbols.FSharpEntity as ent ->
        if ent.GenericParameters.Count > 0 then
            content <- content + "## Type Parameters\n\n"
            content <- content + "| Name |\n"
            content <- content + "| --- |\n"
            for tp in ent.GenericParameters do
                content <- content + $"| `{tp.DisplayName}` |\n"
            content <- content + "\n"

        if e.UnionCases.Length > 0 then
            content <- content + "## Union Cases\n\n"
            content <- content + "| Case | Description |\n"
            content <- content + "| --- | --- |\n"
            for c in e.UnionCases do
                let summary = rewriteHtml c.Comment.Summary.HtmlText
                content <- content + $"| `{c.Name}` | {summary} |\n"
            content <- content + "\n"

        if e.RecordFields.Length > 0 then
            content <- content + "## Record Fields\n\n"
            content <- content + "| Field | Description |\n"
            content <- content + "| --- | --- |\n"
            for f in e.RecordFields do
                let summary = rewriteHtml f.Comment.Summary.HtmlText
                content <- content + $"| `{f.Name}` | {summary} |\n"
            content <- content + "\n"
    | _ -> ()

    match e.Comment.Remarks with
    | Some r -> content <- content + "## Remarks\n\n" + rewriteHtml r.HtmlText + "\n\n"
    | None -> ()

    if not e.Comment.Examples.IsEmpty then
        content <- content + "## Examples\n\n"
        for ex in e.Comment.Examples do
            content <- content + rewriteHtml ex.HtmlText + "\n\n"
    
    content

let pageSpecs = [
    {
        OutPath = ["flow"; "_index.md"]
        Title = "Flow"
        Description = "Source-documented workflow surface in FsFlow."
        Intro = "This page shows the `Flow<'env, 'error, 'value>` surface, the central workflow type in FsFlow. A flow is a cold description of work that reads an explicit environment, can fail with a typed error, and only starts when you call an execution member such as `workflow.ToTask(env)`, `workflow.ToValueTask(env)`, `workflow.ToAsync(env)`, or `workflow.RunSynchronously(env)`. Use this page as the API map for building fail-fast workflows, reading dependencies from `env`, reshaping environments with `localEnv`, composing typed failures, and introducing concurrency with fibers, `zipPar`, or `race`. Start with `flow { }`, `Flow.read`, `Flow.bind`, and `Flow.map`; reach for [runtime helpers](./runtime/) and parallel orchestration only at the boundary where the workflow actually needs them. \n\nNote that common extensions such as `Flow.Retry` and `Flow.Repeat` are available as soon as you `open FsFlow` because their modules are marked with `[<AutoOpen>]`."
        SymbolIds = [
            "Core type", ["T:FsFlow.Flow`3"]
            "Fiber operations", ["M:FsFlow.Flow.fork"; "M:FsFlow.Flow.join"; "M:FsFlow.Flow.interrupt"]
            "Execution", ["M:FsFlow.Flow.ToAsync"; "M:FsFlow.Flow.ToTask"; "M:FsFlow.Flow.ToValueTask"; "M:FsFlow.Flow.RunSynchronously"]
            "Module functions", ["M:FsFlow.Flow.ok"; "M:FsFlow.Flow.error"; "M:FsFlow.Flow.succeed"; "M:FsFlow.Flow.value"; "M:FsFlow.Flow.fail"; "M:FsFlow.Flow.fromResult"; "M:FsFlow.Flow.fromOption"; "M:FsFlow.Flow.fromValueOption"; "M:FsFlow.Flow.fromAsync"; "M:FsFlow.Flow.fromTask"; "M:FsFlow.Flow.fromValueTask"; "M:FsFlow.Flow.orElseFlow"; "M:FsFlow.Flow.env"; "M:FsFlow.Flow.read"; "M:FsFlow.Flow.map"; "M:FsFlow.Flow.bind"; "M:FsFlow.Flow.tap"; "M:FsFlow.Flow.tapError"; "M:FsFlow.Flow.mapError"; "M:FsFlow.Flow.catch"; "M:FsFlow.Flow.orElseWith"; "M:FsFlow.Flow.orElse"; "M:FsFlow.Flow.zip"; "M:FsFlow.Flow.map2"; "M:FsFlow.Flow.map3"; "M:FsFlow.Flow.apply"; "M:FsFlow.Flow.ignore"; "M:FsFlow.Flow.localEnv"; "M:FsFlow.Flow.provide"; "M:FsFlow.Flow.delay"; "M:FsFlow.Flow.traverse"; "M:FsFlow.Flow.sequence"]
            "Scoped resources", ["M:FsFlow.Flow.addFinalizer"; "M:FsFlow.Flow.addDisposable"; "M:FsFlow.Flow.addAsyncDisposable"; "M:FsFlow.Flow.acquireRelease"; "M:FsFlow.Flow.acquireReleaseWith"]
            "Parallel orchestration", ["M:FsFlow.Flow.zipPar"; "M:FsFlow.Flow.race"]
            "Scheduling", ["M:FsFlow.Flow`3.Retry"; "M:FsFlow.Flow`3.Repeat"]
        ]
        Alias = None
    }
    {
        OutPath = ["fiber"; "_index.md"]
        Title = "Fiber"
        Description = "Source-documented handle for running workflows."
        Intro = "This page shows the `Fiber<'error, 'value>` handle used by FsFlow concurrency. A fiber represents a flow that has already been started in the background; it keeps the workflow's typed error and success values attached to the running work, plus diagnostic metadata such as fiber id, parent id, start time, and lifecycle status. The operations that create and consume fibers are still part of the [`Flow`](../flow/) API: use [`Flow.fork`](../flow/concurrency/m-flow-fork.md), [`Flow.join`](../flow/concurrency/m-flow-join.md), and [`Flow.interrupt`](../flow/concurrency/m-flow-interrupt.md) when a workflow needs explicit child execution. Prefer higher-level helpers such as `Flow.zipPar` or `Flow.race` when the code only needs parallel composition."
        SymbolIds = [
            "Core types", ["T:FsFlow.Fiber`2"; "T:FsFlow.FiberId"; "T:FsFlow.FiberStatus"; "T:FsFlow.FiberMetadata"; "T:FsFlow.FiberDump"]
            "Module functions", ["M:FsFlow.Fiber.dump"]
        ]
        Alias = None
    }
    {
        OutPath = ["concurrency"; "_index.md"]
        Title = "Concurrency"
        Description = "Source-documented deferred and semaphore primitives for FsFlow."
        Intro = "This page shows the small Flow-native concurrency primitives added for coordination that needs FsFlow semantics rather than raw .NET behavior. `Deferred<'error, 'value>` is a one-shot typed handoff point backed by a full `Exit<'value, 'error>`. `FlowSemaphore` limits concurrent workflow sections through scoped `Semaphore.withPermit`, releasing permits after success, typed failure, defect, or interruption."
        SymbolIds = [
            "Deferred", ["T:FsFlow.Deferred`2"; "M:FsFlow.Deferred.make"; "M:FsFlow.Deferred.await"; "M:FsFlow.Deferred.complete"; "M:FsFlow.Deferred.succeed"; "M:FsFlow.Deferred.fail"; "M:FsFlow.Deferred.die"; "M:FsFlow.Deferred.interrupt"]
            "Semaphore", ["T:FsFlow.FlowSemaphore"; "M:FsFlow.Semaphore.make"; "M:FsFlow.Semaphore.create"; "M:FsFlow.Semaphore.withPermit"]
        ]
        Alias = None
    }
    {
        OutPath = ["exit"; "_index.md"]
        Title = "Exit"
        Description = "Documentation for the Exit workflow outcome."
        Intro = "This page shows the `Exit<'value, 'error>` type, which is FsFlow's name for `Result<'value, Cause<'error>>`. We name it `Exit` because it represents a completed workflow execution, not an ordinary domain result. Use the `Exit` module functions to transform completed outcomes without manually pattern matching at every boundary."
        SymbolIds = [
            "Core type", ["T:FsFlow.Exit`2"]
            "Module functions", ["M:FsFlow.Exit.map"; "M:FsFlow.Exit.bind"; "M:FsFlow.Exit.mapError"; "M:FsFlow.Exit.mapBoth"; "M:FsFlow.Exit.fromResult"; "M:FsFlow.Exit.toResult"]
        ]
        Alias = None
    }
    {
        OutPath = ["cause"; "_index.md"]
        Title = "Cause"
        Description = "Documentation for the Cause of workflow failure."
        Intro = "This page shows the `Cause<'error>` type, which distinguishes expected domain failures, unexpected technical defects, administrative interruptions, sequential failure composition, parallel failure composition, and diagnostic traces. Understanding the cause tree lets FsFlow preserve what happened during retries, cleanup, parallel execution, and observability boundaries without flattening everything into one exception or one typed error."
        SymbolIds = [
            "Core type", ["T:FsFlow.Cause`1"]
            "Module functions", ["M:FsFlow.Cause.map"; "M:FsFlow.Cause.thenCause"; "M:FsFlow.Cause.both"; "M:FsFlow.Cause.traced"; "M:FsFlow.Cause.failures"; "M:FsFlow.Cause.defects"; "M:FsFlow.Cause.isInterrupted"; "M:FsFlow.Cause.prettyPrint"]
        ]
        Alias = None
    }
    {
        OutPath = ["flow"; "runtime"; "_index.md"]
        Title = "Flow.Runtime"
        Description = "Runtime helpers for operational concerns like logging, timeout, retry, and cleanup."
        Intro = "This page shows the `Flow.Runtime` helpers for closed executor mechanics. These functions expose cancellation, scope ownership, runtime annotations, timeout handling, and retry. User-facing resource combinators such as `Flow.acquireRelease` live on the main `Flow` module; `Flow.Runtime.scope` remains available for advanced code that needs direct scope access."
        SymbolIds = [
            "Runtime helpers", ["M:FsFlow.Flow.Runtime.cancellationToken"; "M:FsFlow.Flow.Runtime.catchCancellation"; "M:FsFlow.Flow.Runtime.ensureNotCanceled"; "M:FsFlow.Flow.Runtime.sleep"; "M:FsFlow.Flow.Runtime.scope"; "M:FsFlow.Flow.Runtime.annotations"; "M:FsFlow.Flow.Runtime.traceId"; "M:FsFlow.Flow.Runtime.timeout"; "M:FsFlow.Flow.Runtime.timeoutToOk"; "M:FsFlow.Flow.Runtime.timeoutToError"; "M:FsFlow.Flow.Runtime.timeoutWith"; "M:FsFlow.Flow.Runtime.retry"]
        ]
        Alias = None
    }
    {
        OutPath = ["schedule"; "_index.md"]
        Title = "Schedule"
        Description = "Source-documented retry and repeat logic for FsFlow."
        Intro = "This page shows the `Schedule` surface for describing retry and repeat policies as values. A schedule decides when a workflow should run again, what delay should be used, and what output should be accumulated for each step. Use schedules when retry behavior is part of the workflow boundary and must stay explicit, testable, and separate from the domain operation being retried. The common entry points are `recurs` for bounded repetition, `spaced` for fixed delays, `exponential` for backoff, and `jittered` when several callers should not retry in lockstep."
        SymbolIds = [
            "Core type", ["T:FsFlow.Schedule`3"]
            "Module functions", ["M:FsFlow.Schedule.recurs"; "M:FsFlow.Schedule.spaced"; "M:FsFlow.Schedule.exponential"; "M:FsFlow.Schedule.jittered"]
        ]
        Alias = None
    }
    {
        OutPath = ["ref"; "_index.md"]
        Title = "Ref"
        Description = "Source-documented atomic mutable references for FsFlow."
        Intro = "This page shows the `Ref` surface for small pieces of shared mutable state inside flows. A `Ref<'T>` is an atomic handle that can be created, read, set, updated, or modified from workflow code without turning the whole environment into a mutable object. Use `Ref` for counters, flags, request-local caches, and coordination points where a single value is enough. For multi-value invariants that must change together, use STM instead."
        SymbolIds = [
            "Core type", ["T:FsFlow.Ref`1"]
            "Module functions", ["M:FsFlow.Ref.make"; "M:FsFlow.Ref.get"; "M:FsFlow.Ref.set"; "M:FsFlow.Ref.update"; "M:FsFlow.Ref.modify"]
        ]
        Alias = None
    }
    {
        OutPath = ["stm"; "_index.md"]
        Title = "STM"
        Description = "Source-documented Software Transactional Memory for FsFlow."
        Intro = "This page shows the STM surface for composable atomic state transitions. STM is for cases where several transactional references must be read and updated as one operation, or where a workflow should wait until state satisfies a condition. Build transactions with `TRef` reads and writes, compose them before execution, then cross back into `Flow` with `STM.atomically`. Use `Ref` for one independent mutable value; use STM when correctness depends on a group of values changing together. \n\n**Note**: The current implementation uses a global synchronizing lock for coordination and is available on .NET only."
        SymbolIds = [
            "Core types", ["T:FsFlow.TRef`1"; "T:FsFlow.STM`1"]
            "Module functions", ["M:FsFlow.TRef.make"; "M:FsFlow.TRef.get"; "M:FsFlow.TRef.set"; "M:FsFlow.TRef.update"; "M:FsFlow.STM.atomically"]
            "Builder", ["P:FsFlow.StmBuilders.stm"]
        ]
        Alias = None
    }
    {
        OutPath = ["stream"; "_index.md"]
        Title = "Stream"
        Description = "Source-documented effectful streams for FsFlow."
        Intro = "This page shows the `FlowStream` surface for cold, pull-based streams that still participate in FsFlow's environment and typed-error model. A stream can require `env`, emit values incrementally, and fail with the same error discipline as `Flow`. Use it when the boundary produces many values over time, such as file records, network messages, or paged API results. Keep per-item logic small and push final side effects through `runForEach` so cancellation and failure stay visible."
        SymbolIds = [
            "Core type", ["T:FsFlow.FlowStream`3"]
            "Module functions", ["M:FsFlow.FlowStream.fromSeq"; "M:FsFlow.FlowStream.map"; "M:FsFlow.FlowStream.runForEach"]
        ]
        Alias = None
    }
    {
        OutPath = ["flow"; "builders-flow.md"]
        Title = "flow { }"
        Description = "Documentation for the flow { } computation expression."
        Intro = "This page shows the `flow { }` computation expression, the primary syntax for writing FsFlow workflows. Inside the builder, ordinary values, `Result`, `Async`, `Task`, `Flow`, and guarded sources can be sequenced without manually unwrapping each layer. The builder preserves the important boundaries: expected errors stay typed, defects become `Cause.Die`, cancellation becomes interruption, and environment access remains explicit through `Flow.env` or `Flow.read`. Prefer `flow { }` for application orchestration; keep pure validation and simple predicates in `Check`, `Validation`, or `Result` until the code needs environment or effects."
        SymbolIds = [
            "Builder", ["P:FsFlow.Builders.flow"]
        ]
        Alias = None
    }
    {
        OutPath = ["check"; "_index.md"]
        Title = "Check"
        Description = "Source-documented pure validation helpers for FsFlow."
        Intro = "This page shows the `Check` surface for reusable, pure validation. Unprefixed helpers test a property, `when*` helpers preserve the original input on success, and `take*` helpers extract or narrow a useful value. Simple helpers carry a `unit` error and can be converted into typed failures with `Check.withError`. Helpers with useful built-in diagnostics return typed `Result` values such as `CardinalityFailure`, `StringLengthFailure`, or `RangeFailure`. Use `Check` before moving into `Result`, `Validation`, or `Flow`."
        SymbolIds = [
            "Core type", ["T:FsFlow.Check`1"]
            "Structured errors", ["T:FsFlow.CardinalityFailure"; "T:FsFlow.StringLengthFailure"; "T:FsFlow.RangeFailure`1"]
            "Construction", ["M:FsFlow.Check.fromPredicate"; "M:FsFlow.Check.fromTry"; "M:FsFlow.Check.fromChoice"]
            "Composition", ["M:FsFlow.Check.negate"; "M:FsFlow.Check.both"; "M:FsFlow.Check.either"; "M:FsFlow.Check.all"; "M:FsFlow.Check.any"]
            "Boolean and branch predicates", ["M:FsFlow.Check.isTrue"; "M:FsFlow.Check.isFalse"; "M:FsFlow.Check.isSome"; "M:FsFlow.Check.isNone"; "M:FsFlow.Check.isValueSome"; "M:FsFlow.Check.isValueNone"; "M:FsFlow.Check.isOk"; "M:FsFlow.Check.isError"]
            "Presence predicates", ["M:FsFlow.Check.hasValue"; "M:FsFlow.Check.hasNoValue"; "M:FsFlow.Check.notNull"; "M:FsFlow.Check.isNull"; "M:FsFlow.Check.notEmpty"; "M:FsFlow.Check.empty"]
            "String predicates", ["M:FsFlow.Check.notNullOrEmpty"; "M:FsFlow.Check.nullOrEmpty"; "M:FsFlow.Check.notEmptyString"; "M:FsFlow.Check.emptyString"; "M:FsFlow.Check.notBlank"; "M:FsFlow.Check.blank"; "M:FsFlow.Check.minLength"; "M:FsFlow.Check.maxLength"; "M:FsFlow.Check.exactLength"; "M:FsFlow.Check.matchesRegex"]
            "Collection predicates", ["M:FsFlow.Check.contains"; "M:FsFlow.Check.hasCount"; "M:FsFlow.Check.hasDuplicates"; "M:FsFlow.Check.hasNoDuplicates"; "M:FsFlow.Check.isSingle"; "M:FsFlow.Check.atMostOne"; "M:FsFlow.Check.atLeastOne"; "M:FsFlow.Check.moreThanOne"]
            "Equality and range predicates", ["M:FsFlow.Check.equalTo"; "M:FsFlow.Check.notEqualTo"; "M:FsFlow.Check.greaterThan"; "M:FsFlow.Check.lessThan"; "M:FsFlow.Check.atLeast"; "M:FsFlow.Check.atMost"; "M:FsFlow.Check.between"; "M:FsFlow.Check.positive"; "M:FsFlow.Check.nonNegative"; "M:FsFlow.Check.negative"; "M:FsFlow.Check.nonPositive"]
            "Preserving gates", ["M:FsFlow.Check.whenTrue"; "M:FsFlow.Check.whenFalse"; "M:FsFlow.Check.whenSome"; "M:FsFlow.Check.whenNone"; "M:FsFlow.Check.whenValueSome"; "M:FsFlow.Check.whenValueNone"; "M:FsFlow.Check.whenHasValue"; "M:FsFlow.Check.whenHasNoValue"; "M:FsFlow.Check.whenNotNull"; "M:FsFlow.Check.whenNull"; "M:FsFlow.Check.whenOk"; "M:FsFlow.Check.whenError"; "M:FsFlow.Check.whenNotEmpty"; "M:FsFlow.Check.whenEmpty"; "M:FsFlow.Check.whenNotNullOrEmpty"; "M:FsFlow.Check.whenNullOrEmpty"; "M:FsFlow.Check.whenNotEmptyString"; "M:FsFlow.Check.whenEmptyString"; "M:FsFlow.Check.whenNotBlank"; "M:FsFlow.Check.whenBlank"; "M:FsFlow.Check.whenMinLength"; "M:FsFlow.Check.whenMaxLength"; "M:FsFlow.Check.whenExactLength"; "M:FsFlow.Check.whenMatchesRegex"; "M:FsFlow.Check.whenEqualTo"; "M:FsFlow.Check.whenNotEqualTo"; "M:FsFlow.Check.whenContains"; "M:FsFlow.Check.whenCount"; "M:FsFlow.Check.whenHasDuplicates"; "M:FsFlow.Check.whenHasNoDuplicates"; "M:FsFlow.Check.whenSingle"; "M:FsFlow.Check.whenAtMostOne"; "M:FsFlow.Check.whenAtLeastOne"; "M:FsFlow.Check.whenMoreThanOne"; "M:FsFlow.Check.whenGreaterThan"; "M:FsFlow.Check.whenLessThan"; "M:FsFlow.Check.whenAtLeast"; "M:FsFlow.Check.whenAtMost"; "M:FsFlow.Check.whenBetween"; "M:FsFlow.Check.whenPositive"; "M:FsFlow.Check.whenNonNegative"; "M:FsFlow.Check.whenNegative"; "M:FsFlow.Check.whenNonPositive"]
            "Extraction helpers", ["M:FsFlow.Check.takeSome"; "M:FsFlow.Check.takeValueSome"; "M:FsFlow.Check.takeHasValue"; "M:FsFlow.Check.takeNotNull"; "M:FsFlow.Check.takeOk"; "M:FsFlow.Check.takeError"; "M:FsFlow.Check.takeHead"; "M:FsFlow.Check.takeSingle"; "M:FsFlow.Check.takeAtMostOne"]
            "Error attachment", ["M:FsFlow.Check.withError"]
        ]
        Alias = None
    }
    {
        OutPath = ["bind-error"; "_index.md"]
        Title = "BindError"
        Description = "Source-documented flow bind-site error adaptation for FsFlow."
        Intro = "This page shows the `BindError` marker used when a source needs its error assigned or mapped immediately before `flow { }` binds it. Use `BindError.withError` for missingness, falsehood, or `unit` failures such as `option`, `bool`, or `Result<'value, unit>`. Use `BindError.map` when the source already carries a meaningful error that must be wrapped or translated into the surrounding flow error. Do not use `BindError` as a general Result adapter; in pure code use `Check.withError`, `Result.mapError`, or `Validation.mapError`."
        SymbolIds = [
            "Core type", ["T:FsFlow.BindError`3"]
            "Module functions", ["M:FsFlow.BindError.withError"; "M:FsFlow.BindError.map"]
        ]
        Alias = None
    }
    {
        OutPath = ["validation"; "_index.md"]
        Title = "Validation"
        Description = "Source-documented accumulating validation for FsFlow."
        Intro = "This page shows the `Validation<'value, 'error>` surface for accumulating several failures into one diagnostics graph. Unlike `Result`, validation does not stop at the first independent error; functions such as `map2`, `map3`, `apply`, `collect`, and `traverseIndexed` combine sibling checks and preserve all reported problems. Use path helpers such as `name`, `key`, `index`, and `at` to attach errors to fields, map entries, list positions, or nested structures. Use `Validation` for input decoding, command validation, configuration checks, and any boundary where users need a complete error report."
        SymbolIds = [
            "Core type", ["T:FsFlow.Validation`2"]
            "Module functions", ["M:FsFlow.Validation.toResult"; "M:FsFlow.Validation.ok"; "M:FsFlow.Validation.error"; "M:FsFlow.Validation.succeed"; "M:FsFlow.Validation.fail"; "M:FsFlow.Validation.fromResult"; "M:FsFlow.Validation.map"; "M:FsFlow.Validation.bind"; "M:FsFlow.Validation.mapError"; "M:FsFlow.Validation.map2"; "M:FsFlow.Validation.map3"; "M:FsFlow.Validation.apply"; "M:FsFlow.Validation.ignore"; "M:FsFlow.Validation.orElse"; "M:FsFlow.Validation.orElseWith"; "M:FsFlow.Validation.collect"; "M:FsFlow.Validation.sequence"; "M:FsFlow.Validation.traverseIndexed"; "M:FsFlow.Validation.merge"]
            "Path scoping", ["M:FsFlow.Validation.at"; "M:FsFlow.Validation.key"; "M:FsFlow.Validation.index"; "M:FsFlow.Validation.name"]
        ]
        Alias = None
    }
    {
        OutPath = ["validation"; "builders-validate.md"]
        Title = "validate { }"
        Description = "Documentation for the validate { } computation expression."
        Intro = "This page shows the `validate { }` computation expression for writing validation logic with direct, sequential syntax. The builder is best for validation steps that read clearly as a block while still returning `Validation<'value, 'error>`. Use it when each bound step depends on earlier successful values. For independent sibling fields where you want maximum error accumulation, prefer `Validation.map2`, `map3`, `apply`, `collect`, or `traverseIndexed` so all branches are evaluated and all diagnostics are retained."
        SymbolIds = [
            "Builder", ["P:FsFlow.Builders.validate"]
        ]
        Alias = None
    }
    {
        OutPath = ["result"; "_index.md"]
        Title = "Result Builder"
        Description = "Documentation for the result { } computation expression."
        Intro = "This page shows the `result { }` computation expression for ordinary fail-fast `Result` workflows. It is the smallest effect in FsFlow's stack: no environment, no async boundary, and no runtime services. Use it for pure domain transformations where the first error should stop the computation. If the same logic later needs dependency access, async work, cancellation, logging, or typed execution outcomes, lift it into `Flow` without changing the underlying error model."
        SymbolIds = [
            "Builder", ["P:FsFlow.Builders.result"]
        ]
        Alias = Some "builders-result.md"
    }
    {
        OutPath = ["diagnostics"; "_index.md"]
        Title = "Diagnostics"
        Description = "Source-documented validation diagnostics graph for FsFlow."
        Intro = "This page shows the diagnostics graph used by `Validation`. A `Diagnostics<'error>` value stores errors at the current node and at named, keyed, or indexed child paths, so validation can report both what failed and where it failed. Use `Diagnostics.singleton` for one error, `merge` to combine sibling reports, `flatten` when callers need path-bearing diagnostics, and `toString` for compact human-readable output. Keep diagnostics at the validation boundary; convert them to domain responses or UI messages at the edge."
        SymbolIds = [
            "Graph types", ["T:FsFlow.PathSegment"; "T:Path"; "T:FsFlow.Diagnostic`1"; "T:FsFlow.Diagnostics`1"]
            "Module functions", ["M:FsFlow.Diagnostics.empty"; "M:FsFlow.Diagnostics.singleton"; "M:FsFlow.Diagnostics.merge"; "M:FsFlow.Diagnostics.toString"; "M:FsFlow.Diagnostics.flatten"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "_index.md"]
        Title = "Service"
        Description = "Source-documented service contracts and dependency access helpers for FsFlow."
        Intro = "This page shows the service helpers around FsFlow's explicit environment model. In FsFlow, a service is a named dependency contract such as `IClock`, `IConsole`, or `IHttp`. Prefer plain records plus `Flow.read` for local workflow code, use `IHas<'T>` plus `Service<'service>.get()` when reusable helpers need a nominal service contract, and keep `Service<'service>.resolve()` at .NET host boundaries where `IServiceProvider` interop is useful. Layers provision explicit services, while the ambient runtime is reserved for closed executor mechanics only.\n\nSee the standard service packages: [Core](./core/), [Console](./console/), [FileSystem](./filesystem/), [Http](./http/), and [Process](./process/)."
        SymbolIds = [
            "Service contracts", ["T:FsFlow.IHas`1"; "T:FsFlow.Service`1"]
            "Service accessors", ["M:FsFlow.Service.get"; "M:FsFlow.Service.resolve"]
            "Environment helpers", ["M:FsFlow.Flow.read"]
        ]
        Alias = None
    }
    {
        OutPath = ["layer"; "_index.md"]
        Title = "Layer"
        Description = "Source-documented service provisioning surface for FsFlow."
        Intro = "This page shows the `Layer<'input, 'error, 'output>` surface used to provision explicit services and environments. Layers build service values inside a `Scope`, can fail during provisioning, and are consumed through `Flow.provide`. Use `layer { }` for application environment construction: plain `let!` is dependent and sequential, while sibling `and!` bindings use `Layer.merge` / `Layer.zipPar` for independent parallel provisioning."
        SymbolIds = [
            "Core type", ["T:FsFlow.Layer`3"]
            "Builder", ["P:FsFlow.Builders.layer"]
            "Module functions", ["M:FsFlow.Layer.fromAsync"; "M:FsFlow.Layer.fromTask"; "M:FsFlow.Layer.fromValueTask"; "M:FsFlow.Layer.succeed"; "M:FsFlow.Layer.read"; "M:FsFlow.Layer.addFinalizer"; "M:FsFlow.Layer.acquireRelease"; "M:FsFlow.Layer.map"; "M:FsFlow.Layer.mapError"; "M:FsFlow.Layer.bind"; "M:FsFlow.Layer.zip"; "M:FsFlow.Layer.zipPar"; "M:FsFlow.Layer.merge"; "M:FsFlow.Layer.map2"; "M:FsFlow.Layer.apply"; "M:FsFlow.Layer.map3"]
            "Flow integration", ["M:FsFlow.Flow.provide"]
        ]
        Alias = None
    }
    {
        OutPath = ["scope"; "_index.md"]
        Title = "Scope"
        Description = "Source-documented resource scope for FsFlow."
        Intro = "This page shows the `Scope` surface used to own cleanup for resources acquired during provisioning and execution. Scopes register finalizers, disposables, and async disposables, and they close in reverse registration order."
        SymbolIds = [
            "Core type", ["T:FsFlow.Scope"]
            "Methods", ["M:FsFlow.Scope.AddFinalizer(Microsoft.FSharp.Core.FSharpFunc{System.Threading.CancellationToken,System.Threading.Tasks.Task})"; "M:FsFlow.Scope.AddDisposable(System.IDisposable)"; "M:FsFlow.Scope.AddAsyncDisposable(System.IAsyncDisposable)"; "M:FsFlow.Scope.AddChild"; "M:FsFlow.Scope.Close(System.Threading.CancellationToken)"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "core"; "_index.md"]
        Title = "Services Core"
        Description = "Source-documented synchronous service primitives for FsFlow.Services.Core."
        Intro = "This page shows the core service package: clock, logging, random numbers, GUID generation, and environment-variable lookup. These are explicit services, not ambient runtime slots. Use the helper modules when a workflow needs one of these services, and use `BaseRuntime` or custom environments to supply deterministic or live implementations."
        SymbolIds = [
            "Service types", ["T:FsFlow.Services.Core.IClock"; "T:FsFlow.Services.Core.ILog"; "T:FsFlow.Services.Core.IRandom"; "T:FsFlow.Services.Core.IGuid"; "T:FsFlow.Services.Core.IEnvironmentVariables"; "T:FsFlow.Services.Core.EnvironmentVariableError"; "T:FsFlow.Services.Core.BaseRuntimeError"; "T:FsFlow.Services.Core.BaseRuntime"]
            "Base runtime", ["P:FsFlow.Services.Core.BaseRuntimeModule.liveValue"; "P:FsFlow.Services.Core.BaseRuntimeModule.live"; "P:FsFlow.Services.Core.BaseRuntimeModule.fromServiceProvider"]
            "Clock", ["M:FsFlow.Services.Core.Clock.now"; "M:FsFlow.Services.Core.Clock.utcDateTime"; "M:FsFlow.Services.Core.Clock.unixTimeSeconds"; "M:FsFlow.Services.Core.Clock.unixTimeMilliseconds"; "P:FsFlow.Services.Core.Clock.live"; "P:FsFlow.Services.Core.Clock.layer"; "M:FsFlow.Services.Core.Clock.fromValue"]
            "Logging", ["M:FsFlow.Services.Core.Log.log"; "M:FsFlow.Services.Core.Log.trace"; "M:FsFlow.Services.Core.Log.debug"; "M:FsFlow.Services.Core.Log.info"; "M:FsFlow.Services.Core.Log.warning"; "M:FsFlow.Services.Core.Log.error"; "M:FsFlow.Services.Core.Log.critical"; "P:FsFlow.Services.Core.Log.live"; "P:FsFlow.Services.Core.Log.layer"; "M:FsFlow.Services.Core.Log.fromSink"]
            "Random", ["M:FsFlow.Services.Core.Random.next"; "M:FsFlow.Services.Core.Random.nextMax"; "M:FsFlow.Services.Core.Random.nextInt"; "M:FsFlow.Services.Core.Random.nextDouble"; "M:FsFlow.Services.Core.Random.nextBytes"; "M:FsFlow.Services.Core.Random.bytes"; "P:FsFlow.Services.Core.Random.live"; "P:FsFlow.Services.Core.Random.layer"; "M:FsFlow.Services.Core.Random.fromValue"; "M:FsFlow.Services.Core.Random.fromFixed"]
            "GUID", ["M:FsFlow.Services.Core.Guid.newGuid"; "P:FsFlow.Services.Core.Guid.live"; "P:FsFlow.Services.Core.Guid.layer"; "M:FsFlow.Services.Core.Guid.fromValue"]
            "Environment variables", ["M:FsFlow.Services.Core.EnvironmentVariables.tryGet"; "M:FsFlow.Services.Core.EnvironmentVariables.getAll"; "M:FsFlow.Services.Core.EnvironmentVariables.set"; "M:FsFlow.Services.Core.EnvironmentVariables.clear"; "M:FsFlow.Services.Core.EnvironmentVariables.expand"; "P:FsFlow.Services.Core.EnvironmentVariables.live"; "P:FsFlow.Services.Core.EnvironmentVariables.layer"; "M:FsFlow.Services.Core.EnvironmentVariables.fromPairs"; "M:FsFlow.Services.Core.EnvironmentVariable.tryGet"; "M:FsFlow.Services.Core.EnvironmentVariable.get"; "M:FsFlow.Services.Core.EnvironmentVariable.getInt"; "M:FsFlow.Services.Core.EnvironmentVariable.getInt64"; "M:FsFlow.Services.Core.EnvironmentVariable.getDouble"; "M:FsFlow.Services.Core.EnvironmentVariable.getDecimal"; "M:FsFlow.Services.Core.EnvironmentVariable.getGuid"; "M:FsFlow.Services.Core.EnvironmentVariable.getUri"; "M:FsFlow.Services.Core.EnvironmentVariable.getTimeSpan"; "M:FsFlow.Services.Core.EnvironmentVariable.getBool"; "M:FsFlow.Services.Core.EnvironmentVariableErrors.describe"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "console"; "_index.md"]
        Title = "Services Console"
        Description = "Source-documented console I/O service for FsFlow.Services.Console."
        Intro = "This page shows the console service package. `IConsole` models standard input and output as an explicit workflow service. Keep business logic typed against the service contract, provide `Console.live` only at the edge, and replace it with a test implementation when you need deterministic input or captured output."
        SymbolIds = [
            "Service", ["T:FsFlow.Services.Console.IConsole"]
            "Helpers", ["M:FsFlow.Services.Console.Console.readLine"; "M:FsFlow.Services.Console.Console.writeLine"; "P:FsFlow.Services.Console.Console.live"; "P:FsFlow.Services.Console.Console.layer"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "filesystem"; "_index.md"]
        Title = "Services FileSystem"
        Description = "Source-documented file-system service for FsFlow.Services.FileSystem."
        Intro = "This page shows the file-system service package. `IFileSystem` models common `System.IO.File`, `Directory`, `Path`, text, byte, stream, metadata, and timestamp operations as an explicit workflow service. Keep workflow code typed against the service contract, provide `FileSystem.live` only at the edge, and replace it with a deterministic implementation in tests. File-system helpers classify thrown platform exceptions into `FileSystemError` so workflow errors stay typed instead of escaping as ordinary exceptions."
        SymbolIds = [
            "Service", ["T:FsFlow.Services.FileSystem.IFileSystem"; "T:FsFlow.Services.FileSystem.FileSystemError"]
            "Errors", ["M:FsFlow.Services.FileSystem.FileSystemError.fromException"; "M:FsFlow.Services.FileSystem.FileSystemError.describe"]
            "Text and bytes",
                [ "M:FsFlow.Services.FileSystem.FileSystem.readAllText"
                  "M:FsFlow.Services.FileSystem.FileSystem.readAllTextWithEncoding"
                  "M:FsFlow.Services.FileSystem.FileSystem.readAllTextAsync"
                  "M:FsFlow.Services.FileSystem.FileSystem.readAllLines"
                  "M:FsFlow.Services.FileSystem.FileSystem.readAllLinesWithEncoding"
                  "M:FsFlow.Services.FileSystem.FileSystem.readAllLinesAsync"
                  "M:FsFlow.Services.FileSystem.FileSystem.readAllBytes"
                  "M:FsFlow.Services.FileSystem.FileSystem.readAllBytesAsync"
                  "M:FsFlow.Services.FileSystem.FileSystem.writeAllText"
                  "M:FsFlow.Services.FileSystem.FileSystem.writeAllTextWithEncoding"
                  "M:FsFlow.Services.FileSystem.FileSystem.writeAllTextAsync"
                  "M:FsFlow.Services.FileSystem.FileSystem.writeAllLines"
                  "M:FsFlow.Services.FileSystem.FileSystem.writeAllLinesWithEncoding"
                  "M:FsFlow.Services.FileSystem.FileSystem.writeAllLinesAsync"
                  "M:FsFlow.Services.FileSystem.FileSystem.writeAllBytes"
                  "M:FsFlow.Services.FileSystem.FileSystem.writeAllBytesAsync"
                  "M:FsFlow.Services.FileSystem.FileSystem.appendAllText"
                  "M:FsFlow.Services.FileSystem.FileSystem.appendAllTextWithEncoding"
                  "M:FsFlow.Services.FileSystem.FileSystem.appendAllTextAsync"
                  "M:FsFlow.Services.FileSystem.FileSystem.appendAllLines"
                  "M:FsFlow.Services.FileSystem.FileSystem.appendAllLinesWithEncoding" ]
            "Files and streams",
                [ "M:FsFlow.Services.FileSystem.FileSystem.fileExists"
                  "M:FsFlow.Services.FileSystem.FileSystem.exists"
                  "M:FsFlow.Services.FileSystem.FileSystem.deleteFile"
                  "M:FsFlow.Services.FileSystem.FileSystem.copyFile"
                  "M:FsFlow.Services.FileSystem.FileSystem.moveFile"
                  "M:FsFlow.Services.FileSystem.FileSystem.openFile"
                  "M:FsFlow.Services.FileSystem.FileSystem.openFileWithAccess"
                  "M:FsFlow.Services.FileSystem.FileSystem.openFileWithShare"
                  "M:FsFlow.Services.FileSystem.FileSystem.openRead"
                  "M:FsFlow.Services.FileSystem.FileSystem.openText"
                  "M:FsFlow.Services.FileSystem.FileSystem.openWrite"
                  "M:FsFlow.Services.FileSystem.FileSystem.createFile"
                  "M:FsFlow.Services.FileSystem.FileSystem.createText"
                  "M:FsFlow.Services.FileSystem.FileSystem.appendText" ]
            "File metadata",
                [ "M:FsFlow.Services.FileSystem.FileSystem.getFileAttributes"
                  "M:FsFlow.Services.FileSystem.FileSystem.setFileAttributes"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFileCreationTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFileCreationTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.setFileCreationTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.setFileCreationTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFileLastAccessTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFileLastAccessTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.setFileLastAccessTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.setFileLastAccessTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFileLastWriteTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFileLastWriteTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.setFileLastWriteTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.setFileLastWriteTimeUtc" ]
            "Directories",
                [ "M:FsFlow.Services.FileSystem.FileSystem.directoryExists"
                  "M:FsFlow.Services.FileSystem.FileSystem.createDirectory"
                  "M:FsFlow.Services.FileSystem.FileSystem.deleteDirectory"
                  "M:FsFlow.Services.FileSystem.FileSystem.moveDirectory"
                  "M:FsFlow.Services.FileSystem.FileSystem.enumerateFiles"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFiles"
                  "M:FsFlow.Services.FileSystem.FileSystem.enumerateDirectories"
                  "M:FsFlow.Services.FileSystem.FileSystem.getDirectories"
                  "M:FsFlow.Services.FileSystem.FileSystem.enumerateFileSystemEntries"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFileSystemEntries"
                  "M:FsFlow.Services.FileSystem.FileSystem.getLogicalDrives"
                  "M:FsFlow.Services.FileSystem.FileSystem.getDirectoryRoot"
                  "M:FsFlow.Services.FileSystem.FileSystem.getParent"
                  "M:FsFlow.Services.FileSystem.FileSystem.getCurrentDirectory"
                  "M:FsFlow.Services.FileSystem.FileSystem.setCurrentDirectory" ]
            "Directory metadata",
                [ "M:FsFlow.Services.FileSystem.FileSystem.getDirectoryCreationTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.getDirectoryCreationTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.setDirectoryCreationTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.setDirectoryCreationTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.getDirectoryLastAccessTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.getDirectoryLastAccessTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.setDirectoryLastAccessTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.setDirectoryLastAccessTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.getDirectoryLastWriteTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.getDirectoryLastWriteTimeUtc"
                  "M:FsFlow.Services.FileSystem.FileSystem.setDirectoryLastWriteTime"
                  "M:FsFlow.Services.FileSystem.FileSystem.setDirectoryLastWriteTimeUtc" ]
            "Paths",
                [ "M:FsFlow.Services.FileSystem.FileSystem.combine"
                  "M:FsFlow.Services.FileSystem.FileSystem.changeExtension"
                  "M:FsFlow.Services.FileSystem.FileSystem.getDirectoryName"
                  "M:FsFlow.Services.FileSystem.FileSystem.getInvalidFileNameChars"
                  "M:FsFlow.Services.FileSystem.FileSystem.getInvalidPathChars"
                  "M:FsFlow.Services.FileSystem.FileSystem.getExtension"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFileName"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFileNameWithoutExtension"
                  "M:FsFlow.Services.FileSystem.FileSystem.getFullPath"
                  "M:FsFlow.Services.FileSystem.FileSystem.getPathRoot"
                  "M:FsFlow.Services.FileSystem.FileSystem.getRelativePath"
                  "M:FsFlow.Services.FileSystem.FileSystem.getTempPath"
                  "M:FsFlow.Services.FileSystem.FileSystem.getTempFileName"
                  "M:FsFlow.Services.FileSystem.FileSystem.getRandomFileName"
                  "M:FsFlow.Services.FileSystem.FileSystem.hasExtension"
                  "M:FsFlow.Services.FileSystem.FileSystem.endsInDirectorySeparator"
                  "M:FsFlow.Services.FileSystem.FileSystem.trimEndingDirectorySeparator"
                  "M:FsFlow.Services.FileSystem.FileSystem.isPathFullyQualified"
                  "M:FsFlow.Services.FileSystem.FileSystem.isPathRooted" ]
            "Implementations", ["P:FsFlow.Services.FileSystem.FileSystem.live"; "P:FsFlow.Services.FileSystem.FileSystem.layer"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "http"; "_index.md"]
        Title = "Services Http"
        Description = "Source-documented HTTP client service for FsFlow.Services.Http."
        Intro = "This page shows the HTTP service package. `IHttp` is intentionally narrow: it models a workflow that needs to fetch a string from a URL without binding the workflow to a concrete `HttpClient` setup. For richer clients, define an app-specific service and keep FsFlow responsible for orchestration and failure handling."
        SymbolIds = [
            "Service", ["T:FsFlow.Services.Http.IHttp"]
            "Helpers", ["M:FsFlow.Services.Http.Http.getString"; "M:FsFlow.Services.Http.Http.live"; "M:FsFlow.Services.Http.Http.layer"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "process"; "_index.md"]
        Title = "Services Process"
        Description = "Source-documented external process service for FsFlow.Services.Process."
        Intro = "This page shows the external-process service package. `IProcess` models command execution as an asynchronous workflow service and returns a `ProcessResult` with exit code, standard output, and standard error. Keep process execution behind this service contract so tests can return deterministic results without shelling out."
        SymbolIds = [
            "Service", ["T:FsFlow.Services.Process.IProcess"; "T:FsFlow.Services.Process.ProcessResult"]
            "Helpers", ["M:FsFlow.Services.Process.Process.execute"; "P:FsFlow.Services.Process.Process.live"; "P:FsFlow.Services.Process.Process.layer"]
        ]
        Alias = None
    }
]

let flowSectionDirectories =
    dict [
        "Fiber operations", ("concurrency", "Forking, joining, and interrupting child workflows.")
        "Execution", ("execution", "Start a flow and choose the handle that matches the host boundary.")
        "Module functions", ("composition", "Construct, transform, compose, and adapt workflows.")
        "Scoped resources", ("resources", "Register cleanup and scope-owned resources inside a flow execution.")
        "Parallel orchestration", ("concurrency", "Run workflows concurrently or race them when independent work can overlap.")
        "Scheduling", ("scheduling", "Attach retry and repeat policies to an existing workflow.")
    ]

let groupedFlowEnvironmentMembers =
    set [
        "M:FsFlow.Flow.env"
        "M:FsFlow.Flow.read"
        "M:FsFlow.Flow.localEnv"
        "M:FsFlow.Flow.provide"
    ]

let groupedFlowConstructionMembers =
    set [
        "M:FsFlow.Flow.ok"
        "M:FsFlow.Flow.error"
        "M:FsFlow.Flow.succeed"
        "M:FsFlow.Flow.value"
        "M:FsFlow.Flow.fail"
        "M:FsFlow.Flow.fromResult"
        "M:FsFlow.Flow.fromOption"
        "M:FsFlow.Flow.fromValueOption"
        "M:FsFlow.Flow.fromAsync"
        "M:FsFlow.Flow.fromTask"
        "M:FsFlow.Flow.fromValueTask"
        "M:FsFlow.Flow.orElseFlow"
        "M:FsFlow.Flow.delay"
    ]

let serviceCoreSectionDirectories =
    dict [
        "Base runtime", ("base-runtime", "Base runtime", "This page shows the `Core.BaseRuntime` helpers for building the standard explicit service bundle used by FsFlow workflow hosts.")
        "Clock", ("clock", "Clock", "This page shows the `Core.Clock` helpers for reading time from an explicit clock service.")
        "Logging", ("log", "Logging", "This page shows the `Core.Log` helpers for writing messages through an explicit logging service.")
        "Random", ("random", "Random", "This page shows the `Core.Random` helpers for reading values from an explicit random-number service.")
        "GUID", ("guid", "GUID", "This page shows the `Core.Guid` helpers for reading GUID values from an explicit GUID service.")
        "Environment variables", ("environment-variables", "Environment variables", "This page shows the `Core.EnvironmentVariables`, `Core.EnvironmentVariable`, and `Core.EnvironmentVariableErrors` helpers for explicit environment-variable access.")
    ]

let serviceFileSystemSectionDirectories =
    dict [
        "Errors", ("errors", "Errors", "This page shows the `FileSystemError` helpers for classifying and describing file-system failures.")
        "Text and bytes", ("text-and-bytes", "Text and bytes", "This page shows the `FileSystem.FileSystem` helpers for reading and writing text and byte content through an explicit file-system service.")
        "Files and streams", ("files-and-streams", "Files and streams", "This page shows the `FileSystem.FileSystem` helpers for file existence, mutation, and stream access.")
        "File metadata", ("file-metadata", "File metadata", "This page shows the `FileSystem.FileSystem` helpers for reading and updating file metadata.")
        "Directories", ("directories", "Directories", "This page shows the `FileSystem.FileSystem` helpers for directory creation, discovery, and enumeration.")
        "Directory metadata", ("directory-metadata", "Directory metadata", "This page shows the `FileSystem.FileSystem` helpers for reading and updating directory metadata.")
        "Paths", ("paths", "Paths", "This page shows the `FileSystem.FileSystem` helpers for path manipulation and inspection.")
        "Implementations", ("implementations", "Implementations", "This page shows the live `FileSystem.FileSystem` implementations used to provide the explicit file-system service.")
    ]

let sectionDirectory (spec: PageSpec) (sectionTitle: string) (id: string) =
    match spec.OutPath, sectionTitle with
    | ["flow"; "_index.md"], "Core type" -> None
    | ["flow"; "_index.md"], "Module functions" when groupedFlowEnvironmentMembers.Contains id -> Some "environment"
    | ["flow"; "_index.md"], "Module functions" when groupedFlowConstructionMembers.Contains id -> Some "construction"
    | ["flow"; "_index.md"], "Module functions" -> Some "composition"
    | ["flow"; "_index.md"], _ when flowSectionDirectories.ContainsKey sectionTitle ->
        let dir, _ = flowSectionDirectories[sectionTitle]
        Some dir
    | ["service"; "core"; "_index.md"], _ when serviceCoreSectionDirectories.ContainsKey sectionTitle ->
        let dir, _, _ = serviceCoreSectionDirectories[sectionTitle]
        Some dir
    | ["service"; "filesystem"; "_index.md"], _ when serviceFileSystemSectionDirectories.ContainsKey sectionTitle ->
        let dir, _, _ = serviceFileSystemSectionDirectories[sectionTitle]
        Some dir
    | _ -> None

let sectionTitleForDirectory = function
    | "construction" -> "Construction"
    | "environment" -> "Environment"
    | "composition" -> "Composition"
    | "execution" -> "Execution"
    | "resources" -> "Resources"
    | "concurrency" -> "Concurrency"
    | "scheduling" -> "Scheduling"
    | "base-runtime" -> "Base runtime"
    | "clock" -> "Clock"
    | "log" -> "Logging"
    | "random" -> "Random"
    | "guid" -> "GUID"
    | "environment-variables" -> "Environment variables"
    | "errors" -> "Errors"
    | "text-and-bytes" -> "Text and bytes"
    | "files-and-streams" -> "Files and streams"
    | "file-metadata" -> "File metadata"
    | "directories" -> "Directories"
    | "directory-metadata" -> "Directory metadata"
    | "paths" -> "Paths"
    | "implementations" -> "Implementations"
    | other -> other

let sectionIntroForDirectory = function
    | "construction" -> "This page shows the helpers that create or adapt flows before you start composing them with domain logic."
    | "environment" -> "This page shows the helpers that read, reshape, and provide explicit environments for flows."
    | "composition" -> "This page shows the everyday Flow combinators for mapping, binding, zipping, and otherwise shaping workflow logic."
    | "execution" -> "This page shows the execution members that turn a cold flow description into a running handle or a blocking exit."
    | "resources" -> "This page shows the Flow helpers that register cleanup and manage scoped resources during execution."
    | "concurrency" -> "This page shows the Flow helpers that fork work, coordinate fibers, and run independent workflows in parallel."
    | "scheduling" -> "This page shows the Flow helpers that apply retry and repeat schedules."
    | "base-runtime" -> "This page shows the `Core.BaseRuntime` helpers for building the standard explicit service bundle used by FsFlow workflow hosts."
    | "clock" -> "This page shows the `Core.Clock` helpers for reading time from an explicit clock service."
    | "log" -> "This page shows the `Core.Log` helpers for writing messages through an explicit logging service."
    | "random" -> "This page shows the `Core.Random` helpers for reading values from an explicit random-number service."
    | "guid" -> "This page shows the `Core.Guid` helpers for reading GUID values from an explicit GUID service."
    | "environment-variables" -> "This page shows the `Core.EnvironmentVariables`, `Core.EnvironmentVariable`, and `Core.EnvironmentVariableErrors` helpers for explicit environment-variable access."
    | "errors" -> "This page shows the `FileSystemError` helpers for classifying and describing file-system failures."
    | "text-and-bytes" -> "This page shows the `FileSystem.FileSystem` helpers for reading and writing text and byte content through an explicit file-system service."
    | "files-and-streams" -> "This page shows the `FileSystem.FileSystem` helpers for file existence, mutation, and stream access."
    | "file-metadata" -> "This page shows the `FileSystem.FileSystem` helpers for reading and updating file metadata."
    | "directories" -> "This page shows the `FileSystem.FileSystem` helpers for directory creation, discovery, and enumeration."
    | "directory-metadata" -> "This page shows the `FileSystem.FileSystem` helpers for reading and updating directory metadata."
    | "paths" -> "This page shows the `FileSystem.FileSystem` helpers for path manipulation and inspection."
    | "implementations" -> "This page shows the live `FileSystem.FileSystem` implementations used to provide the explicit file-system service."
    | _ -> "This page shows the members in this reference subgroup."

let finalSegment (name: string) =
    let parts = name.Split('.')
    parts[parts.Length - 1]

let candidateNamesForMember (m: ApiDocMember) =
    let qualifier = memberQualifier m
    let rawNames =
        match m.Symbol with
        | :? FSharpMemberOrFunctionOrValue as mfv ->
            [
                mfv.DisplayName
                mfv.CompiledName
                if String.IsNullOrEmpty qualifier then mfv.DisplayName else qualifier + "." + mfv.DisplayName
                if String.IsNullOrEmpty qualifier then mfv.CompiledName else qualifier + "." + mfv.CompiledName
            ]
        | _ -> []

    [
        cleanName (logicalName m.Symbol)
        cleanName (safeFullName m.Symbol)
        if String.IsNullOrEmpty qualifier then cleanName m.Name else cleanName (qualifier + "." + m.Name)
        cleanName m.Name
        yield!
            rawNames
            |> List.map cleanName
    ]
    |> List.distinct

let candidateNamesForEntity (e: ApiDocEntity) =
    [ cleanName (safeFullName e.Symbol); cleanName e.Name ]
    |> List.distinct

let matchScore (idNorm: string) (candidate: string) =
    if String.IsNullOrEmpty candidate then 0
    elif candidate = idNorm then 1000
    elif candidate.EndsWith("." + idNorm, StringComparison.Ordinal) then 850
    elif idNorm.EndsWith("." + candidate, StringComparison.Ordinal) then
        if finalSegment candidate = finalSegment idNorm then 400 else 150
    elif finalSegment candidate = finalSegment idNorm then 75
    else 0

type ResolvedSymbol =
    | ResolvedMember of ApiDocMember
    | ResolvedEntity of ApiDocEntity

let findBestSymbol (allEntities: ApiDocEntity list) (id: string) =
    let rawId = id.Substring(2).Split('(').[0]
    let idNorm = cleanName rawId

    let candidates =
        seq {
            for e in allEntities do
                let entityScore =
                    candidateNamesForEntity e
                    |> List.map (matchScore idNorm)
                    |> List.max

                if id[0] = 'T' && entityScore > 0 then
                    yield entityScore, ResolvedEntity e

                for m in e.AllMembers do
                    let memberScore =
                        candidateNamesForMember m
                        |> List.map (matchScore idNorm)
                        |> List.max

                    if memberScore > 0 then
                        yield memberScore, ResolvedMember m
        }
        |> Seq.sortByDescending fst
        |> Seq.toList

    candidates
    |> List.tryHead
    |> Option.map snd

let relativeLinkFrom (fromFile: string) (toFile: string) =
    Path.GetRelativePath(Path.GetDirectoryName(fromFile), toFile).Replace("\\", "/")

let rewriteApiDocHtml (slugMap: IDictionary<string, string>) (filePath: string) (content: string) =
    let unresolved = ResizeArray<string>()

    let rewritten =
        Regex.Replace(
            content,
            "https://adz\\.github\\.io/FsFlow/reference/FsFlow/([a-z0-9\\-]+)\\.html",
            MatchEvaluator(fun m ->
                let slug = m.Groups[1].Value
                match slugMap.TryGetValue slug with
                | true, target ->
                    relativeLinkFrom filePath target
                | _ ->
                    unresolved.Add slug
                    m.Value))

    if unresolved.Count > 0 then
        let unique = unresolved |> Seq.distinct |> String.concat ", "
        printfn "Warning: unresolved generated reference links in %s -> %s" filePath unique

    rewritten

let rec collectAllEntities (e: ApiDocEntity) =
    seq {
        yield e
        for n in e.NestedEntities do
            yield! collectAllEntities n
    }

let pageWeight (spec: PageSpec) =
    match spec.OutPath with
    | ["flow"; "_index.md"] -> 10
    | ["flow"; "runtime"; "_index.md"] -> 10
    | ["flow"; "builders-flow.md"] -> 2000
    | ["fiber"; "_index.md"] -> 20
    | ["exit"; "_index.md"] -> 30
    | ["cause"; "_index.md"] -> 40
    | ["effect"; "_index.md"] -> 50
    | ["result"; "_index.md"] -> 60
    | ["check"; "_index.md"] -> 70
    | ["take"; "_index.md"] -> 75
    | ["bind-error"; "_index.md"] -> 76
    | ["validation"; "_index.md"] -> 80
    | ["diagnostics"; "_index.md"] -> 90
    | ["schedule"; "_index.md"] -> 100
    | ["ref"; "_index.md"] -> 110
    | ["stm"; "_index.md"] -> 120
    | ["stream"; "_index.md"] -> 130
    | ["service"; "_index.md"] -> 140
    | ["layer"; "_index.md"] -> 150
    | ["scope"; "_index.md"] -> 160
    | ["validation"; "builders-validate.md"] -> 2000
    | ["result"; "builders-result.md"] -> 2000
    | ["service"; "core"; "_index.md"] -> 10
    | ["service"; "console"; "_index.md"] -> 20
    | ["service"; "filesystem"; "_index.md"] -> 30
    | ["service"; "http"; "_index.md"] -> 40
    | ["service"; "process"; "_index.md"] -> 50
    | _ -> 500

let childPageWeight (id: string) (sectionOrdinal: int) (itemOrdinal: int) =
    let ordinal = sectionOrdinal * 100 + itemOrdinal
    match id.[0] with
    | 'T' -> 1000 + ordinal
    | _ -> 2000 + ordinal

let normalizeGeneratedMarkdown (content: string) =
    content.Replace("\r\n", "\n").Split('\n')
    |> Array.map (fun line -> line.TrimEnd())
    |> String.concat "\n"
    |> fun text -> text.TrimEnd() + "\n"

[<EntryPoint>]
let main argv =
    let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))
    let artifactsDir = Path.Combine(root, "artifacts/bin")
    
    let outRoot = Path.Combine(root, "docs/reference")
    
    if Directory.Exists outRoot then
        for d in Directory.GetDirectories(outRoot) do
            Directory.Delete(d, true)
        for f in Directory.GetFiles(outRoot) do
            if Path.GetFileName(f) <> "_index.md" then
                File.Delete(f)
    else
        Directory.CreateDirectory(outRoot) |> ignore

    let dllPaths = [
        Path.Combine(artifactsDir, "FsFlow/debug_netstandard2.1/FsFlow.dll")
        Path.Combine(artifactsDir, "FsFlow.Services.Core/debug_netstandard2.1/FsFlow.Services.Core.dll")
        Path.Combine(artifactsDir, "FsFlow.Services.Console/debug_netstandard2.1/FsFlow.Services.Console.dll")
        Path.Combine(artifactsDir, "FsFlow.Services.FileSystem/debug_netstandard2.1/FsFlow.Services.FileSystem.dll")
        Path.Combine(artifactsDir, "FsFlow.Services.Http/debug_netstandard2.1/FsFlow.Services.Http.dll")
        Path.Combine(artifactsDir, "FsFlow.Services.Process/debug_netstandard2.1/FsFlow.Services.Process.dll")
    ]

    let apiDocInputs = [
        for dll in dllPaths do
            if File.Exists dll then
                yield ApiDocInput.FromFile(dll)
    ]

    let substitutions = Substitutions.Empty
    let model = ApiDocs.GenerateModel(apiDocInputs, "FsFlow", substitutions, root="https://adz.github.io/FsFlow/", qualify=true)
    
    let allEntities = 
        model.EntityInfos 
        |> Seq.map (fun ei -> ei.Entity)
        |> Seq.collect collectAllEntities
        |> Seq.toList

    let referenceTargetMap = Dictionary<string, string>()

    let registerReferenceTarget (symbolFullName: string) (absolutePath: string) =
        if not (String.IsNullOrWhiteSpace symbolFullName) then
            referenceTargetMap[formatterApiSlug symbolFullName] <- absolutePath

    let registerReferenceId (id: string) (absolutePath: string) =
        let rawName = id.Substring(2).Split('(').[0]
        referenceTargetMap[formatterApiSlug rawName] <- absolutePath

    for spec in pageSpecs do
        let outPath = Path.Combine(outRoot, Path.Combine(Array.ofList spec.OutPath))

        for sectionTitle, ids in spec.SymbolIds do
            for id in ids do
                let targetDir =
                    match sectionDirectory spec sectionTitle id with
                    | Some dir -> Path.Combine(Path.GetDirectoryName(outPath), dir)
                    | None -> Path.GetDirectoryName outPath

                let pagePath = Path.Combine(targetDir, getPageName id)

                match findBestSymbol allEntities id with
                | Some (ResolvedMember m) ->
                    registerReferenceId id pagePath
                    registerReferenceTarget (safeFullName m.Symbol) pagePath
                    registerReferenceTarget (logicalName m.Symbol) pagePath
                | Some (ResolvedEntity e) ->
                    registerReferenceId id pagePath
                    registerReferenceTarget (safeFullName e.Symbol) pagePath
                | _ -> ()

    let canonicalAliases =
        dict [
            formatterApiSlug "FsFlow.CheckModule", Path.Combine(outRoot, "check", "_index.md")
            formatterApiSlug "FsFlow.BindErrorModule", Path.Combine(outRoot, "bind-error", "_index.md")
            formatterApiSlug "FsFlow.FlowModule", Path.Combine(outRoot, "flow", "_index.md")
            formatterApiSlug "FsFlow.LayerBuilder", Path.Combine(outRoot, "layer", "p-layer.md")
            formatterApiSlug "FsFlow.FlowBuilder", Path.Combine(outRoot, "flow", "builders-flow.md")
            formatterApiSlug "FsFlow.ValidateBuilder", Path.Combine(outRoot, "validation", "builders-validate.md")
            formatterApiSlug "FsFlow.ResultBuilder", Path.Combine(outRoot, "result", "builders-result.md")
            formatterApiSlug "FsFlow.StmBuilder", Path.Combine(outRoot, "stm", "p-stm-stm.md")
            formatterApiSlug "FsFlow.Check`1", Path.Combine(outRoot, "check", "t-check.md")
            formatterApiSlug "FsFlow.BindError`3", Path.Combine(outRoot, "bind-error", "t-binderror.md")
            formatterApiSlug "FsFlow.Path", Path.Combine(outRoot, "diagnostics", "t-path.md")
            formatterApiSlug "FsFlow.LogLevel", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "FsFlow.RetryPolicy`1", Path.Combine(outRoot, "flow", "runtime", "_index.md")
            formatterApiSlug "FsFlow.Never", Path.Combine(outRoot, "layer", "_index.md")
            formatterApiSlug "FsFlow.Services.Core.Clock", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "FsFlow.Services.Core.Log", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "FsFlow.Services.Core.Random", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "FsFlow.Services.Core.Guid", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "FsFlow.Services.Core.EnvironmentVariables", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "FsFlow.Services.Core.BaseRuntime", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "FsFlow.Services.Console.Console", Path.Combine(outRoot, "service", "console", "_index.md")
            formatterApiSlug "FsFlow.Services.FileSystem.FileSystem", Path.Combine(outRoot, "service", "filesystem", "_index.md")
            formatterApiSlug "FsFlow.Services.FileSystem.FileSystemError", Path.Combine(outRoot, "service", "filesystem", "_index.md")
            formatterApiSlug "FsFlow.Services.Http.Http", Path.Combine(outRoot, "service", "http", "_index.md")
            formatterApiSlug "FsFlow.Services.Process.Process", Path.Combine(outRoot, "service", "process", "_index.md")
        ]

    for KeyValue(slug, path) in canonicalAliases do
        if not (referenceTargetMap.ContainsKey slug) then
            referenceTargetMap[slug] <- path

    let sectionMembers = Dictionary<string, ResizeArray<string * string * string>>()
    
    // Debug: print all entity names
    // for e in allEntities do printfn "Entity: %s" (safeFullName e.Symbol)

    for spec in pageSpecs do
        let outPath = Path.Combine(outRoot, Path.Combine(Array.ofList spec.OutPath))
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)) |> ignore
        
        let mutable indexContent = 
            $"---\ntitle: \"{spec.Title}\"\nweight: {pageWeight spec}\n---\n\n{spec.Intro}\n\n"
            
        for sectionOrdinal, (sectionTitle, ids) in spec.SymbolIds |> List.indexed do
            indexContent <- indexContent + $"## {sectionTitle}\n\n"
            for itemOrdinal, id in ids |> List.indexed do
                let targetDir =
                    match sectionDirectory spec sectionTitle id with
                    | Some dir ->
                        let dirPath = Path.Combine(Path.GetDirectoryName(outPath), dir)
                        Directory.CreateDirectory dirPath |> ignore
                        dirPath
                    | None ->
                        Path.GetDirectoryName outPath

                match findBestSymbol allEntities id with
                | Some (ResolvedMember m) ->
                    let pageName = getPageName id
                    let qualifier = memberQualifier m
                    let linkText = if String.IsNullOrEmpty qualifier then m.Name else qualifier + "." + m.Name
                    let pagePath = Path.Combine(targetDir, pageName)
                    let relativeLink = relativeLinkFrom outPath pagePath
                    let rewriteHtml = rewriteApiDocHtml referenceTargetMap pagePath
                    let summaryHtml = rewriteHtml m.Comment.Summary.HtmlText
                    indexContent <- indexContent + $"- [`{linkText}`](./{relativeLink}): {summaryHtml}\n"
                    let memberPageContent = renderMemberPage rewriteHtml (childPageWeight id sectionOrdinal itemOrdinal) m
                    File.WriteAllText(pagePath, normalizeGeneratedMarkdown memberPageContent)

                    match sectionDirectory spec sectionTitle id with
                    | Some dir ->
                        let key = Path.Combine(Path.GetDirectoryName(outPath), dir, "_index.md")
                        let items =
                            match sectionMembers.TryGetValue key with
                            | true, existing -> existing
                            | _ ->
                                let created = ResizeArray()
                                sectionMembers[key] <- created
                                created

                        items.Add(linkText, pageName, summaryHtml)
                    | None -> ()
                    
                    match spec.Alias with
                    | Some a -> File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), a), normalizeGeneratedMarkdown memberPageContent)
                    | None -> ()

                | Some (ResolvedEntity e) ->
                    let pageName = getPageName id
                    let eFullName = safeFullName e.Symbol
                    let linkText = cleanName eFullName
                    let pagePath = Path.Combine(targetDir, pageName)
                    let relativeLink = relativeLinkFrom outPath pagePath
                    let rewriteHtml = rewriteApiDocHtml referenceTargetMap pagePath
                    let summaryHtml = rewriteHtml e.Comment.Summary.HtmlText
                    indexContent <- indexContent + $"- [`{linkText}`](./{relativeLink}): {summaryHtml}\n"
                    let entityPageContent = renderEntityPage rewriteHtml (childPageWeight id sectionOrdinal itemOrdinal) e
                    File.WriteAllText(pagePath, normalizeGeneratedMarkdown entityPageContent)

                    match sectionDirectory spec sectionTitle id with
                    | Some dir ->
                        let key = Path.Combine(Path.GetDirectoryName(outPath), dir, "_index.md")
                        let items =
                            match sectionMembers.TryGetValue key with
                            | true, existing -> existing
                            | _ ->
                                let created = ResizeArray()
                                sectionMembers[key] <- created
                                created

                        items.Add(linkText, pageName, summaryHtml)
                    | None -> ()
                | _ -> 
                    printfn "Warning: symbol not found: %s" id
            indexContent <- indexContent + "\n"
            
        File.WriteAllText(outPath, normalizeGeneratedMarkdown indexContent)

    for KeyValue(indexPath, members) in sectionMembers do
        let dirName = Path.GetFileName(Path.GetDirectoryName indexPath)
        let title = sectionTitleForDirectory dirName
        let intro = sectionIntroForDirectory dirName

        let mutable content =
            $"---\ntitle: \"{title}\"\n---\n\n{intro}\n\n"

        for linkText, pageName, summary in members do
            content <- content + $"- [`{linkText}`](./{pageName}): {summary}\n"

        File.WriteAllText(indexPath, normalizeGeneratedMarkdown content)

    0
