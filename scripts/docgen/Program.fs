open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Templating
open FSharp.Compiler.Symbols
open System
open System.IO
open System.Reflection
open System.Collections.Generic
open System.Net

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
        name.Replace("FsFlow.", "").Replace("Services.", "").Replace("Caps.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> s.Replace("`", "").Replace("'", "").Split('(').[0].Trim('.'))

let cleanName (name: string) =
    if String.IsNullOrEmpty name then ""
    else
        name.Replace("FsFlow.", "").Replace("Services.", "").Replace("Caps.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> s.Trim('.'))
        |> (fun s -> 
            // Surgical removal of generic backticks like `1, `2, etc.
            System.Text.RegularExpressions.Regex.Replace(s, @"`[0-9]+", "")
        )
        |> (fun s -> s.Replace("'", ""))
        |> (fun s -> if s.EndsWith(".Static") then s.Substring(0, s.Length - 7) else s)

let sanitizeFilename (name: string) =
    name.Replace("`", "-").Replace("'", "-").Replace(" ", "-").Replace(".", "-").ToLower()
    |> (fun s -> s.Trim('-'))

let getPageName (id: string) =
    let kind = id.[0].ToString().ToLower()
    let namePart = id.Substring(2).Split('(').[0]
    let clean = 
        namePart.Replace("FsFlow.", "").Replace("Services.", "").Replace("Caps.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
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

let renderMemberPage (weight: int) (m: ApiDocMember) =
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
    content <- content + m.Comment.Summary.HtmlText + "\n\n"

    // Signature
    let qualifier = memberQualifier m
    let usageName = if String.IsNullOrEmpty qualifier then m.Name else qualifier + "." + m.Name
    let usageHtml =
        m.UsageHtml.HtmlText
        |> qualifyUsageHtml usageName

    content <- content + "## Signature\n\n"
    content <- content + "<div class=\"fsdocs-usage\">\n" + usageHtml + "\n</div>\n\n"

    if not m.Parameters.IsEmpty then
        content <- content + "## Parameters\n\n"
        content <- content + "| Name | Type | Description |\n"
        content <- content + "| --- | --- | --- |\n"
        for p in m.Parameters do
            let docs =
                match p.ParameterDocs with
                | Some html -> html.HtmlText
                | None -> ""

            content <- content + $"| `{p.ParameterNameText}` | {p.ParameterType.HtmlText} | {docs} |\n"
        content <- content + "\n"

    content <- content + "## Returns\n\n"
    content <- content + "| Type | Description |\n"
    content <- content + "| --- | --- |\n"
    let returnDocs =
        match m.ReturnInfo.ReturnDocs with
        | Some html -> html.HtmlText
        | None -> ""

    let returnType =
        match m.ReturnInfo.ReturnType with
        | Some (_, html) -> html.HtmlText
        | None -> "<code>unit</code>"

    content <- content + $"| {returnType} | {returnDocs} |\n\n"

    match m.Comment.Remarks with
    | Some r -> content <- content + "## Remarks\n\n" + r.HtmlText + "\n\n"
    | None -> ()

    if not m.Comment.Examples.IsEmpty then
        content <- content + "## Examples\n\n"
        for e in m.Comment.Examples do
            content <- content + e.HtmlText + "\n\n"

    match m.SourceLocation with
    | Some url -> content <- content + $"\n[Source]({url})\n\n"
    | None -> ()

    content

let renderEntityPage (weight: int) (e: ApiDocEntity) =
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

    content <- content + e.Comment.Summary.HtmlText + "\n\n"

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
                let summary = c.Comment.Summary.HtmlText
                content <- content + $"| `{c.Name}` | {summary} |\n"
            content <- content + "\n"

        if e.RecordFields.Length > 0 then
            content <- content + "## Record Fields\n\n"
            content <- content + "| Field | Description |\n"
            content <- content + "| --- | --- |\n"
            for f in e.RecordFields do
                let summary = f.Comment.Summary.HtmlText
                content <- content + $"| `{f.Name}` | {summary} |\n"
            content <- content + "\n"
    | _ -> ()

    match e.Comment.Remarks with
    | Some r -> content <- content + "## Remarks\n\n" + r.HtmlText + "\n\n"
    | None -> ()

    if not e.Comment.Examples.IsEmpty then
        content <- content + "## Examples\n\n"
        for ex in e.Comment.Examples do
            content <- content + ex.HtmlText + "\n\n"
    
    content

let pageSpecs = [
    {
        OutPath = ["flow"; "_index.md"]
        Title = "Flow"
        Description = "Source-documented workflow surface in FsFlow."
        Intro = "This page shows the `Flow<'env, 'error, 'value>` surface, the central workflow type in FsFlow. A flow is a cold description of work that reads an explicit environment, can fail with a typed error, and only runs when you call an execution function such as `Flow.run`. Use this page as the API map for building fail-fast workflows, reading dependencies from `env`, reshaping environments with `localEnv`, composing typed failures, and introducing concurrency with fibers, `zipPar`, or `race`. Start with `flow { }`, `Flow.read`, `Flow.bind`, and `Flow.map`; reach for [runtime helpers](./runtime/) and parallel orchestration only at the boundary where the workflow actually needs them. \n\nNote that common extensions such as `Flow.Retry` and `Flow.Repeat` are available as soon as you `open FsFlow` because their modules are marked with `[<AutoOpen>]`."
        SymbolIds = [
            "Core type", ["T:FsFlow.Flow`3"]
            "Fiber operations", ["M:FsFlow.Flow.fork"; "M:FsFlow.Flow.join"; "M:FsFlow.Flow.interrupt"]
            "Execution", ["M:FsFlow.Flow.run"; "M:FsFlow.Flow.runFull"; "M:FsFlow.Flow.toAsync"; "M:FsFlow.Flow.toAsyncResult"; "M:FsFlow.Flow.toTask"; "M:FsFlow.Flow.toTaskResult"; "M:FsFlow.Flow.toTaskWithToken"; "M:FsFlow.Flow.toTaskResultWithToken"; "M:FsFlow.Flow.toValueTaskResult"; "M:FsFlow.Flow.toValueTaskResultWithToken"; "M:FsFlow.Flow.toResult"]
            "Module functions", ["M:FsFlow.Flow.ok"; "M:FsFlow.Flow.error"; "M:FsFlow.Flow.succeed"; "M:FsFlow.Flow.value"; "M:FsFlow.Flow.fail"; "M:FsFlow.Flow.fromResult"; "M:FsFlow.Flow.fromOption"; "M:FsFlow.Flow.fromValueOption"; "M:FsFlow.Flow.orElseFlow"; "M:FsFlow.Flow.env"; "M:FsFlow.Flow.read"; "M:FsFlow.Flow.map"; "M:FsFlow.Flow.bind"; "M:FsFlow.Flow.tap"; "M:FsFlow.Flow.tapError"; "M:FsFlow.Flow.mapError"; "M:FsFlow.Flow.catch"; "M:FsFlow.Flow.orElseWith"; "M:FsFlow.Flow.orElse"; "M:FsFlow.Flow.zip"; "M:FsFlow.Flow.map2"; "M:FsFlow.Flow.map3"; "M:FsFlow.Flow.apply"; "M:FsFlow.Flow.ignore"; "M:FsFlow.Flow.localEnv"; "M:FsFlow.Flow.provide"; "M:FsFlow.Flow.delay"; "M:FsFlow.Flow.traverse"; "M:FsFlow.Flow.sequence"]
            "Parallel orchestration", ["M:FsFlow.Flow.zipPar"; "M:FsFlow.Flow.race"]
            "Scheduling", ["M:FsFlow.FlowScheduleExtensions.Retry"; "M:FsFlow.FlowScheduleExtensions.Repeat"]
        ]
        Alias = None
    }
    {
        OutPath = ["fiber"; "_index.md"]
        Title = "Fiber"
        Description = "Source-documented handle for running workflows."
        Intro = "This page shows the `Fiber<'error, 'value>` handle used by FsFlow concurrency. A fiber represents a flow that has already been started in the background; it keeps the workflow's typed error and success values attached to the running work. The operations that create and consume fibers are still part of the [`Flow`](../flow/) API: use [`Flow.fork`](../flow/m-flow-fork/), [`Flow.join`](../flow/m-flow-join/), and [`Flow.interrupt`](../flow/m-flow-interrupt/) when a workflow needs explicit child execution. Prefer higher-level helpers such as `Flow.zipPar` or `Flow.race` when the code only needs parallel composition."
        SymbolIds = [
            "Core type", ["T:FsFlow.Fiber`2"]
        ]
        Alias = None
    }
    {
        OutPath = ["exit"; "_index.md"]
        Title = "Exit"
        Description = "Documentation for the Exit workflow outcome."
        Intro = "This page shows the `Exit<'value, 'error>` type, which represents the final outcome of an FsFlow execution. Every flow eventually resolves to either a success or a failure cause. Use the `Exit` module functions to transform outcomes without manually pattern matching at every boundary."
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
        Intro = "This page shows the `Cause<'error>` type, which distinguishes between expected domain failures, unexpected technical defects (exceptions), and administrative interruptions (cancellation). Understanding the cause allows FsFlow's runtime to make smart decisions about retries, cleanup, and observability."
        SymbolIds = [
            "Core type", ["T:FsFlow.Cause`1"]
            "Module functions", ["M:FsFlow.Cause.map"]
        ]
        Alias = None
    }
    {
        OutPath = ["effect"; "_index.md"]
        Title = "Effect"
        Description = "Documentation for the Effect execution shape."
        Intro = "This page shows the `Effect<'value, 'error>` shape and the `EffectFlow` module. An effect is the deferred execution handle returned by `Flow.run`; on .NET it is a `ValueTask<Exit<'v, 'e>>` and on Fable it is an `Async<Exit<'v, 'e>>`. Use the `EffectFlow` functions for low-level algebra and for bridging between the unified flow surface and platform-native async primitives."
        SymbolIds = [
            "Core type", ["T:FsFlow.Effect"]
            "Module functions", ["M:FsFlow.EffectFlow.ofValue"; "M:FsFlow.EffectFlow.ofError"; "M:FsFlow.EffectFlow.ofExit"; "M:FsFlow.EffectFlow.ofCause"; "M:FsFlow.EffectFlow.ofDie"; "M:FsFlow.EffectFlow.ofInterrupt"; "M:FsFlow.EffectFlow.ofResult"; "M:FsFlow.EffectFlow.fold"; "M:FsFlow.EffectFlow.mapBoth"]
        ]
        Alias = None
    }
    {
        OutPath = ["flow"; "runtime"; "_index.md"]
        Title = "Flow.Runtime"
        Description = "Runtime helpers for operational concerns like logging, timeout, retry, and cleanup."
        Intro = "This page shows the `Flow.Runtime` helpers for closed executor mechanics. These functions expose cancellation, scope ownership, runtime annotations, timeout handling, retry, and resource cleanup. They are intentionally separate from explicit application services such as clock, logging, HTTP, or file-system access."
        SymbolIds = [
            "Runtime helpers", ["M:FsFlow.Flow.Runtime.cancellationToken"; "M:FsFlow.Flow.Runtime.catchCancellation"; "M:FsFlow.Flow.Runtime.ensureNotCanceled"; "M:FsFlow.Flow.Runtime.sleep"; "M:FsFlow.Flow.Runtime.scope"; "M:FsFlow.Flow.Runtime.annotations"; "M:FsFlow.Flow.Runtime.traceId"; "M:FsFlow.Flow.Runtime.useWithAcquireRelease"; "M:FsFlow.Flow.Runtime.timeout"; "M:FsFlow.Flow.Runtime.timeoutToOk"; "M:FsFlow.Flow.Runtime.timeoutToError"; "M:FsFlow.Flow.Runtime.timeoutWith"; "M:FsFlow.Flow.Runtime.retry"]
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
        Description = "Source-documented pure predicate helpers for FsFlow."
        Intro = "This page shows the `Check` surface for reusable, pure predicates. A check is a `Result<unit, unit>`-style decision: it says whether a condition passed, without deciding the final domain error yet. This makes checks easy to compose, negate, reuse, and convert into typed failures with `orError` or `orErrorWith`. Use `Check` for local facts such as non-empty strings, equality, null checks, and option presence. When you need to collect several named failures, move to `Validation`; when you need environment or async work, lift the result into `Flow`."
        SymbolIds = [
            "Core type", ["T:Check"]
            "Structured errors", ["T:FsFlow.CheckError"; "T:FsFlow.CardinalityFailure"]
            "Module functions", ["M:FsFlow.Check.fromPredicate"; "M:FsFlow.Check.fromTry"; "M:FsFlow.Check.fromChoice"; "M:FsFlow.Check.okIfTrueTuple"; "M:FsFlow.Check.not"; "M:FsFlow.Check.and"; "M:FsFlow.Check.or"; "M:FsFlow.Check.all"; "M:FsFlow.Check.any"; "M:FsFlow.Check.okIf"; "M:FsFlow.Check.failIf"; "M:FsFlow.Check.okIfSome"; "M:FsFlow.Check.okIfNone"; "M:FsFlow.Check.failIfSome"; "M:FsFlow.Check.failIfNone"; "M:FsFlow.Check.okIfValueSome"; "M:FsFlow.Check.okIfValueNone"; "M:FsFlow.Check.failIfValueSome"; "M:FsFlow.Check.failIfValueNone"; "M:FsFlow.Check.okIfNotNullable"; "M:FsFlow.Check.okIfNullable"; "M:FsFlow.Check.failIfNotNullable"; "M:FsFlow.Check.failIfNullable"; "M:FsFlow.Check.notNullable"; "M:FsFlow.Check.okIfNotNull"; "M:FsFlow.Check.okIfNull"; "M:FsFlow.Check.failIfNotNull"; "M:FsFlow.Check.failIfNull"; "M:FsFlow.Check.okIfNotEmpty"; "M:FsFlow.Check.okIfEmpty"; "M:FsFlow.Check.failIfNotEmpty"; "M:FsFlow.Check.failIfEmpty"; "M:FsFlow.Check.okIfExactlyOne"; "M:FsFlow.Check.failIfExactlyOne"; "M:FsFlow.Check.okIfAtMostOne"; "M:FsFlow.Check.failIfAtMostOne"; "M:FsFlow.Check.okIfCountIs"; "M:FsFlow.Check.okIfContains"; "M:FsFlow.Check.okIfEqual"; "M:FsFlow.Check.okIfNotEqual"; "M:FsFlow.Check.failIfEqual"; "M:FsFlow.Check.failIfNotEqual"; "M:FsFlow.Check.okIfNonEmptyStr"; "M:FsFlow.Check.okIfEmptyStr"; "M:FsFlow.Check.failIfNonEmptyStr"; "M:FsFlow.Check.failIfEmptyStr"; "M:FsFlow.Check.okIfNotBlank"; "M:FsFlow.Check.notBlank"; "M:FsFlow.Check.okIfBlank"; "M:FsFlow.Check.blank"; "M:FsFlow.Check.failIfNotBlank"; "M:FsFlow.Check.failIfBlank"; "M:FsFlow.Check.orError"; "M:FsFlow.Check.orErrorWith"; "M:FsFlow.Check.notNull"; "M:FsFlow.Check.notEmpty"; "M:FsFlow.Check.equal"; "M:FsFlow.Check.notEqual"]
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
        Intro = "This page shows the `Layer<'input, 'error, 'output>` surface used to provision explicit services and environments. Layers build service values inside a `Scope`, can fail during provisioning, and are consumed through `Flow.provide`."
        SymbolIds = [
            "Core type", ["T:FsFlow.Layer`3"]
            "Module functions", ["M:FsFlow.Layer.effect"; "M:FsFlow.Layer.succeed"; "M:FsFlow.Layer.read"; "M:FsFlow.Layer.map"; "M:FsFlow.Layer.bind"; "M:FsFlow.Layer.zip"]
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
            "Methods", ["M:FsFlow.Scope.AddFinalizer(Microsoft.FSharp.Core.FSharpFunc{System.Threading.CancellationToken,System.Threading.Tasks.Task})"; "M:FsFlow.Scope.AddDisposable(System.IDisposable)"; "M:FsFlow.Scope.AddAsyncDisposable(System.IAsyncDisposable)"; "M:FsFlow.Scope.Close(System.Threading.CancellationToken)"]
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
            "Clock", ["M:FsFlow.Services.Core.Clock.now"; "P:FsFlow.Services.Core.Clock.live"; "P:FsFlow.Services.Core.Clock.layer"; "M:FsFlow.Services.Core.Clock.fromValue"]
            "Logging", ["M:FsFlow.Services.Core.Log.info"; "P:FsFlow.Services.Core.Log.live"; "P:FsFlow.Services.Core.Log.layer"]
            "Random", ["M:FsFlow.Services.Core.Random.nextInt"; "P:FsFlow.Services.Core.Random.live"; "P:FsFlow.Services.Core.Random.layer"; "M:FsFlow.Services.Core.Random.fromValue"]
            "GUID", ["M:FsFlow.Services.Core.Guid.newGuid"; "P:FsFlow.Services.Core.Guid.live"; "P:FsFlow.Services.Core.Guid.layer"; "M:FsFlow.Services.Core.Guid.fromValue"]
            "Environment variables", ["M:FsFlow.Services.Core.EnvironmentVariables.tryGet"; "P:FsFlow.Services.Core.EnvironmentVariables.live"; "P:FsFlow.Services.Core.EnvironmentVariables.layer"; "M:FsFlow.Services.Core.EnvironmentVariables.fromPairs"; "M:FsFlow.Services.Core.EnvironmentVariable.tryGet"; "M:FsFlow.Services.Core.EnvironmentVariable.get"; "M:FsFlow.Services.Core.EnvironmentVariable.getInt"; "M:FsFlow.Services.Core.EnvironmentVariable.getGuid"; "M:FsFlow.Services.Core.EnvironmentVariable.getBool"; "M:FsFlow.Services.Core.EnvironmentVariableErrors.describe"]
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
        Intro = "This page shows the file-system service package. `IFileSystem` names the small set of file operations currently supported by FsFlow examples and workflows: reading text, writing text, and existence checks. Keep workflow code typed against the service contract and choose the live or test implementation at provisioning time."
        SymbolIds = [
            "Service", ["T:FsFlow.Services.FileSystem.IFileSystem"]
            "Helpers", ["M:FsFlow.Services.FileSystem.FileSystem.readAllText"; "M:FsFlow.Services.FileSystem.FileSystem.writeAllText"; "M:FsFlow.Services.FileSystem.FileSystem.exists"; "P:FsFlow.Services.FileSystem.FileSystem.live"; "P:FsFlow.Services.FileSystem.FileSystem.layer"]
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
                let idNorm = normalize (id.Substring(2))
                
                let foundFinal = 
                    allEntities |> Seq.tryPick (fun e ->
                        let eNorm = normalize (safeFullName e.Symbol)
                        
                        if id.[0] = 'T' && (eNorm = idNorm || eNorm.EndsWith("." + idNorm) || idNorm.EndsWith("." + eNorm)) then
                            Some (e :> obj)
                        else
                            e.AllMembers |> Seq.tryPick (fun m ->
                                let mNorm = normalize (logicalName m.Symbol)
                                if mNorm = idNorm || mNorm.EndsWith("." + idNorm) || idNorm.EndsWith("." + mNorm) then
                                    Some (m :> obj)
                                else None
                            )
                    )

                match foundFinal with
                | Some (:? ApiDocMember as m) ->
                    let pageName = getPageName id
                    let qualifier = memberQualifier m
                    let linkText = if String.IsNullOrEmpty qualifier then m.Name else qualifier + "." + m.Name
                    indexContent <- indexContent + $"- [`{linkText}`](./{pageName}): {m.Comment.Summary.HtmlText}\n"
                    let memberPageContent = renderMemberPage (childPageWeight id sectionOrdinal itemOrdinal) m
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), pageName), memberPageContent)
                    
                    match spec.Alias with
                    | Some a -> File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), a), memberPageContent)
                    | None -> ()

                | Some (:? ApiDocEntity as e) ->
                    let pageName = getPageName id
                    let eFullName = safeFullName e.Symbol
                    let linkText = cleanName eFullName
                    indexContent <- indexContent + $"- [`{linkText}`](./{pageName}): {e.Comment.Summary.HtmlText}\n"
                    let entityPageContent = renderEntityPage (childPageWeight id sectionOrdinal itemOrdinal) e
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), pageName), entityPageContent)
                | _ -> 
                    printfn "Warning: symbol not found: %s" id
            indexContent <- indexContent + "\n"
            
        File.WriteAllText(outPath, indexContent)

    0
