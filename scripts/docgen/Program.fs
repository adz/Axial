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
        name.Replace("FsFlow.", "").Replace("Caps.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> s.Replace("`", "").Replace("'", "").Split('(').[0].Trim('.'))

let cleanName (name: string) =
    if String.IsNullOrEmpty name then ""
    else
        name.Replace("FsFlow.", "").Replace("Caps.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> s.Trim('.'))
        |> (fun s -> 
            let idx = s.IndexOf('`')
            if idx > 0 then s.Substring(0, idx) else s
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
        namePart.Replace("FsFlow.", "").Replace("Caps.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> s.Replace("`", "-").Replace("'", "").Trim('.'))
        
    let finalName = sanitizeFilename clean
    $"{kind}-{finalName}.md"

let safeFullName (sym: FSharp.Compiler.Symbols.FSharpSymbol) =
    match sym with
    | :? FSharp.Compiler.Symbols.FSharpEntity as e ->
        try e.FullName with _ -> e.DisplayName
    | _ -> 
        try sym.FullName with _ -> sym.DisplayName

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

let memberUsageName (m: ApiDocMember) =
    match enclosingEntity m.Symbol with
    | Some ent ->
        let moduleName = cleanName ent.FullName
        let isAutoOpen = hasAttribute "AutoOpenAttribute" ent.Attributes
        let isRequireQualifiedAccess = hasAttribute "RequireQualifiedAccessAttribute" ent.Attributes

        if isAutoOpen && not isRequireQualifiedAccess then m.Name
        else $"{moduleName}.{m.Name}"
    | None -> cleanName (safeFullName m.Symbol)

let qualifyUsageHtml usageName (html: string) =
    let encodedUsageName = WebUtility.HtmlEncode usageName
    let encodedShortName = WebUtility.HtmlEncode(usageName.Split('.').[usageName.Split('.').Length - 1])

    if encodedUsageName = encodedShortName then html
    else
        let withArgs = $"<span>{encodedShortName}&#32;"
        let qualifiedWithArgs = $"<span>{encodedUsageName}&#32;"
        let withoutArgs = $"<span>{encodedShortName}</span>"
        let qualifiedWithoutArgs = $"<span>{encodedUsageName}</span>"

        if html.Contains withArgs then html.Replace(withArgs, qualifiedWithArgs)
        elif html.Contains withoutArgs then html.Replace(withoutArgs, qualifiedWithoutArgs)
        else html

let renderMemberPage (m: ApiDocMember) =
    let fullName = safeFullName m.Symbol
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
        $"---\ntitle: \"{qualifiedName}\"\nlinkTitle: \"{linkTitle}\"\n---\n\n"
    
    // Description
    content <- content + m.Comment.Summary.HtmlText + "\n\n"

    // Signature
    let usageHtml =
        m.UsageHtml.HtmlText
        |> qualifyUsageHtml (memberUsageName m)

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

let renderEntityPage (e: ApiDocEntity) =
    let fullName = safeFullName e.Symbol
    let qualifiedName = cleanName fullName
    let shortName = cleanName e.Name
    
    let mutable content = 
        $"---\ntitle: \"{qualifiedName}\"\nlinkTitle: \"{shortName}\"\n---\n\n"
    
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
                // FSharpEntity.GenericParameters doesn't easily give us the XML doc for the type parameter
                // without more work, but DisplayName is a start.
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
        Intro = "This page shows the `Flow<'env, 'error, 'value>` surface, the central workflow type in FsFlow. A flow is a cold description of work that reads an explicit environment, can fail with a typed error, and only runs when you call an execution function such as `Flow.run`. Use this page as the API map for building fail-fast workflows, reading dependencies from `env`, reshaping environments with `localEnv`, composing typed failures, and introducing concurrency with fibers, `zipPar`, or `race`. Start with `flow { }`, `Flow.read`, `Flow.bind`, and `Flow.map`; reach for runtime helpers and parallel orchestration only at the boundary where the workflow actually needs them."
        SymbolIds = [
            "Core type", ["T:FsFlow.Flow`3"]
            "Module functions", ["M:FsFlow.Flow.run"; "M:FsFlow.Flow.ok"; "M:FsFlow.Flow.error"; "M:FsFlow.Flow.succeed"; "M:FsFlow.Flow.value"; "M:FsFlow.Flow.fail"; "M:FsFlow.Flow.fromResult"; "M:FsFlow.Flow.fromOption"; "M:FsFlow.Flow.fromValueOption"; "M:FsFlow.Flow.orElseFlow"; "M:FsFlow.Flow.env"; "M:FsFlow.Flow.read"; "M:FsFlow.Flow.service"; "M:FsFlow.Flow.inject"; "M:FsFlow.Flow.map"; "M:FsFlow.Flow.bind"; "M:FsFlow.Flow.tap"; "M:FsFlow.Flow.tapError"; "M:FsFlow.Flow.mapError"; "M:FsFlow.Flow.catch"; "M:FsFlow.Flow.orElseWith"; "M:FsFlow.Flow.orElse"; "M:FsFlow.Flow.zip"; "M:FsFlow.Flow.map2"; "M:FsFlow.Flow.map3"; "M:FsFlow.Flow.apply"; "M:FsFlow.Flow.ignore"; "M:FsFlow.Flow.localEnv"; "M:FsFlow.Flow.provideLayer"; "M:FsFlow.Flow.delay"; "M:FsFlow.Flow.traverse"; "M:FsFlow.Flow.sequence"]
            "Runtime helpers", ["M:FsFlow.Flow.Runtime.cancellationToken"; "M:FsFlow.Flow.Runtime.catchCancellation"; "M:FsFlow.Flow.Runtime.ensureNotCanceled"; "M:FsFlow.Flow.Runtime.sleep"; "M:FsFlow.Flow.Runtime.now"; "M:FsFlow.Flow.Runtime.log"; "M:FsFlow.Flow.Runtime.newGuid"; "M:FsFlow.Flow.Runtime.nextInt"; "M:FsFlow.Flow.Runtime.tryGetEnvironmentVariable"; "M:FsFlow.Flow.Runtime.useWithAcquireRelease"; "M:FsFlow.Flow.Runtime.timeout"; "M:FsFlow.Flow.Runtime.timeoutToOk"; "M:FsFlow.Flow.Runtime.timeoutToError"; "M:FsFlow.Flow.Runtime.timeoutWith"; "M:FsFlow.Flow.Runtime.retry"]
            "Concurrency", ["T:FsFlow.Fiber`2"; "M:FsFlow.Flow.fork"; "M:FsFlow.Flow.join"; "M:FsFlow.Flow.interrupt"]
            "Parallel orchestration", ["M:FsFlow.Flow.zipPar"; "M:FsFlow.Flow.race"]
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
            "Flow extensions", ["M:FsFlow.FlowScheduleExtensions.Retry"; "M:FsFlow.FlowScheduleExtensions.Repeat"]
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
        Intro = "This page shows the STM surface for composable atomic state transitions. STM is for cases where several transactional references must be read and updated as one operation, or where a workflow should wait until state satisfies a condition. Build transactions with `TRef` reads and writes, compose them before execution, then cross back into `Flow` with `STM.atomically`. Use `Ref` for one independent mutable value; use STM when correctness depends on a group of values changing together."
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
            "Module functions", ["M:FsFlow.Check.fromPredicate"; "M:FsFlow.Check.not"; "M:FsFlow.Check.and"; "M:FsFlow.Check.or"; "M:FsFlow.Check.all"; "M:FsFlow.Check.any"; "M:FsFlow.Check.okIf"; "M:FsFlow.Check.failIf"; "M:FsFlow.Check.okIfSome"; "M:FsFlow.Check.okIfNone"; "M:FsFlow.Check.failIfSome"; "M:FsFlow.Check.failIfNone"; "M:FsFlow.Check.okIfValueSome"; "M:FsFlow.Check.okIfValueNone"; "M:FsFlow.Check.failIfValueSome"; "M:FsFlow.Check.failIfValueNone"; "M:FsFlow.Check.okIfNotNull"; "M:FsFlow.Check.okIfNull"; "M:FsFlow.Check.failIfNotNull"; "M:FsFlow.Check.failIfNull"; "M:FsFlow.Check.okIfNotEmpty"; "M:FsFlow.Check.okIfEmpty"; "M:FsFlow.Check.failIfNotEmpty"; "M:FsFlow.Check.failIfEmpty"; "M:FsFlow.Check.okIfEqual"; "M:FsFlow.Check.okIfNotEqual"; "M:FsFlow.Check.failIfEqual"; "M:FsFlow.Check.failIfNotEqual"; "M:FsFlow.Check.okIfNonEmptyStr"; "M:FsFlow.Check.okIfEmptyStr"; "M:FsFlow.Check.failIfNonEmptyStr"; "M:FsFlow.Check.failIfEmptyStr"; "M:FsFlow.Check.okIfNotBlank"; "M:FsFlow.Check.notBlank"; "M:FsFlow.Check.okIfBlank"; "M:FsFlow.Check.blank"; "M:FsFlow.Check.failIfNotBlank"; "M:FsFlow.Check.failIfBlank"; "M:FsFlow.Check.orError"; "M:FsFlow.Check.orErrorWith"; "M:FsFlow.Check.notNull"; "M:FsFlow.Check.notEmpty"; "M:FsFlow.Check.equal"; "M:FsFlow.Check.notEqual"]
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
        OutPath = ["capability"; "_index.md"]
        Title = "Capability"
        Description = "Source-documented capability contracts and dependency access helpers for FsFlow."
        Intro = "This page shows the capability helpers around FsFlow's environment model. In FsFlow, a capability is a named interface that describes what a flow needs from `env`; the workflow still receives an explicit environment, but the interface gives that dependency surface a stable name. Prefer plain records plus `Flow.read` for local workflow code, use `IHas<'T>` plus `Flow.service` when reusable helpers need statically checked dependency contracts, and keep `Flow.inject` at .NET host boundaries where `IServiceProvider` interop is useful. Runtime-owned services such as clock, logging, random, GUID generation, and environment-variable lookup stay in `FsFlow.Capabilities.Core`, where they can be read through runtime helpers and overridden with `Flow.withClock`, `Flow.withLog`, `Flow.withRandom`, `Flow.withGuid`, and `Flow.withEnvironmentVariables`."
        SymbolIds = [
            "Capability contracts", ["T:FsFlow.IHas`1"]
            "Flow accessors", ["M:FsFlow.Flow.read"; "M:FsFlow.Flow.service"; "M:FsFlow.Flow.inject"]
            "Edge helpers", ["T:FsFlow.MissingCapability"]
        ]
        Alias = None
    }
    {
        OutPath = ["caps-core"; "_index.md"]
        Title = "Capabilities Core"
        Description = "Source-documented synchronous capability primitives for FsFlow.Capabilities.Core."
        Intro = "This page shows the core runtime capability package: clock, logging, random numbers, GUID generation, and environment-variable lookup. These services are operational concerns, so ordinary application workflows should usually read them through helpers such as `Clock.now`, `Log.info`, `Random.nextInt`, `Guid.newGuid`, and `EnvironmentVariable.get` instead of adding them to every app environment record. Tests and scoped workflows can replace them with deterministic providers via `Flow.withClock`, `Flow.withLog`, `Flow.withRandom`, `Flow.withGuid`, and `Flow.withEnvironmentVariables`."
        SymbolIds = [
            "Capability types", ["T:FsFlow.Capabilities.Core.IClock"; "T:FsFlow.Capabilities.Core.IRandom"; "T:FsFlow.Capabilities.Core.IGuid"; "T:FsFlow.Capabilities.Core.IEnvironmentVariables"; "T:FsFlow.Capabilities.Core.EnvironmentVariableError"]
            "Clock", ["M:FsFlow.Capabilities.Core.Clock.now"; "M:FsFlow.Capabilities.Core.Clock.live"; "M:FsFlow.Capabilities.Core.Clock.fromValue"]
            "Random", ["M:FsFlow.Capabilities.Core.Random.nextInt"; "M:FsFlow.Capabilities.Core.Random.live"; "M:FsFlow.Capabilities.Core.Random.fromValue"]
            "GUID", ["M:FsFlow.Capabilities.Core.Guid.newGuid"; "M:FsFlow.Capabilities.Core.Guid.live"; "M:FsFlow.Capabilities.Core.Guid.fromValue"]
            "Environment variables", ["M:FsFlow.Capabilities.Core.EnvironmentVariables.tryGet"; "M:FsFlow.Capabilities.Core.EnvironmentVariables.live"; "M:FsFlow.Capabilities.Core.EnvironmentVariables.fromPairs"; "M:FsFlow.Capabilities.Core.EnvironmentVariable.tryGet"; "M:FsFlow.Capabilities.Core.EnvironmentVariable.get"; "M:FsFlow.Capabilities.Core.EnvironmentVariable.getInt"; "M:FsFlow.Capabilities.Core.EnvironmentVariable.getGuid"; "M:FsFlow.Capabilities.Core.EnvironmentVariable.getBool"; "M:FsFlow.Capabilities.Core.EnvironmentVariableErrors.describe"]
        ]
        Alias = None
    }
    {
        OutPath = ["caps-console"; "_index.md"]
        Title = "Capabilities Console"
        Description = "Source-documented console I/O capability for FsFlow.Capabilities.Console."
        Intro = "This page shows the console capability package. `IConsole` is a small app capability for workflows that need standard input or output without depending directly on `System.Console`. Use it at command-line boundaries, examples, and simple interactive tools. Keep business logic typed against the interface, provide `Console.live` only at the edge, and replace it with a test implementation when you need deterministic input or captured output."
        SymbolIds = [
            "Capability", ["T:FsFlow.Capabilities.Console.IConsole"]
            "Helpers", ["M:FsFlow.Capabilities.Console.Console.readLine"; "M:FsFlow.Capabilities.Console.Console.writeLine"; "M:FsFlow.Capabilities.Console.Console.live"]
        ]
        Alias = None
    }
    {
        OutPath = ["caps-filesystem"; "_index.md"]
        Title = "Capabilities FileSystem"
        Description = "Source-documented file system capability for FsFlow.Capabilities.FileSystem."
        Intro = "This page shows the file-system capability package. `IFileSystem` names the small set of file operations currently supported by FsFlow examples and app workflows: reading text, writing text, and existence checks. Use it when file access is part of a workflow boundary but you still want tests to provide an in-memory or fake implementation. The live provider belongs at the composition root; reusable workflow code should depend on the interface."
        SymbolIds = [
            "Capability", ["T:FsFlow.Capabilities.FileSystem.IFileSystem"]
            "Helpers", ["M:FsFlow.Capabilities.FileSystem.FileSystem.readAllText"; "M:FsFlow.Capabilities.FileSystem.FileSystem.writeAllText"; "M:FsFlow.Capabilities.FileSystem.FileSystem.exists"; "M:FsFlow.Capabilities.FileSystem.FileSystem.live"]
        ]
        Alias = None
    }
    {
        OutPath = ["caps-http"; "_index.md"]
        Title = "Capabilities Http"
        Description = "Source-documented HTTP client capability for FsFlow.Capabilities.Http."
        Intro = "This page shows the HTTP capability package. `IHttp` is intentionally narrow: it models a workflow that needs to fetch a string from a URL without binding the workflow to a concrete `HttpClient` setup. Use it for small integrations and examples where a single `getString` operation is enough. For production clients with richer behavior, define an app-specific interface and keep FsFlow responsible for orchestration, typed failure, and environment threading."
        SymbolIds = [
            "Capability", ["T:FsFlow.Capabilities.Http.IHttp"]
            "Helpers", ["M:FsFlow.Capabilities.Http.Http.getString"; "M:FsFlow.Capabilities.Http.Http.live"]
        ]
        Alias = None
    }
    {
        OutPath = ["caps-process"; "_index.md"]
        Title = "Capabilities Process"
        Description = "Source-documented external process capability for FsFlow.Capabilities.Process."
        Intro = "This page shows the external-process capability package. `IProcess` models command execution as an asynchronous workflow dependency and returns a `ProcessResult` with exit code, standard output, and standard error. Use it for tooling, build automation, and integration boundaries where spawning a process is part of the application behavior. Keep process execution behind this interface so tests can return deterministic results without shelling out."
        SymbolIds = [
            "Capability", ["T:FsFlow.Capabilities.Process.IProcess"; "T:FsFlow.Capabilities.Process.ProcessResult"]
            "Helpers", ["M:FsFlow.Capabilities.Process.Process.execute"; "M:FsFlow.Capabilities.Process.Process.live"]
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
        Path.Combine(artifactsDir, "FsFlow.Capabilities.Core/debug_netstandard2.1/FsFlow.Capabilities.Core.dll")
        Path.Combine(artifactsDir, "FsFlow.Capabilities.Console/debug_netstandard2.1/FsFlow.Capabilities.Console.dll")
        Path.Combine(artifactsDir, "FsFlow.Capabilities.FileSystem/debug_netstandard2.1/FsFlow.Capabilities.FileSystem.dll")
        Path.Combine(artifactsDir, "FsFlow.Capabilities.Http/debug_netstandard2.1/FsFlow.Capabilities.Http.dll")
        Path.Combine(artifactsDir, "FsFlow.Capabilities.Process/debug_netstandard2.1/FsFlow.Capabilities.Process.dll")
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

    for spec in pageSpecs do
        let outPath = Path.Combine(outRoot, Path.Combine(Array.ofList spec.OutPath))
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)) |> ignore
        
        let mutable indexContent = 
            $"---\ntitle: \"{spec.Title}\"\n---\n\n{spec.Intro}\n\n"
            
        for sectionTitle, ids in spec.SymbolIds do
            indexContent <- indexContent + $"## {sectionTitle}\n\n"
            for id in ids do
                let idNorm = normalize (id.Substring(2))
                
                let foundFinal = 
                    allEntities |> Seq.tryPick (fun e ->
                        let eNorm = normalize (safeFullName e.Symbol)
                        
                        if id.[0] = 'T' && (eNorm = idNorm || eNorm.EndsWith("." + idNorm) || idNorm.EndsWith("." + eNorm)) then
                            Some (e :> obj)
                        else
                            e.AllMembers |> Seq.tryPick (fun m ->
                                let mNorm = normalize (safeFullName m.Symbol)
                                if mNorm = idNorm || mNorm.EndsWith("." + idNorm) || idNorm.EndsWith("." + mNorm) then
                                    Some (m :> obj)
                                else None
                            )
                    )

                match foundFinal with
                | Some (:? ApiDocMember as m) ->
                    let pageName = getPageName id
                    let mFullName = safeFullName m.Symbol
                    let linkText = cleanName mFullName
                    indexContent <- indexContent + $"- [`{linkText}`](./{pageName}): {m.Comment.Summary.HtmlText}\n"
                    let memberPageContent = renderMemberPage m
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), pageName), memberPageContent)
                    
                    match spec.Alias with
                    | Some a -> File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), a), memberPageContent)
                    | None -> ()

                | Some (:? ApiDocEntity as e) ->
                    let pageName = getPageName id
                    let eFullName = safeFullName e.Symbol
                    let linkText = cleanName eFullName
                    indexContent <- indexContent + $"- [`{linkText}`](./{pageName}): {e.Comment.Summary.HtmlText}\n"
                    let entityPageContent = renderEntityPage e
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), pageName), entityPageContent)
                | _ -> 
                    printfn "Warning: symbol not found: %s" id
            indexContent <- indexContent + "\n"
            
        File.WriteAllText(outPath, indexContent)

    0
