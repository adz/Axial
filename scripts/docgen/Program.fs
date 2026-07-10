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
        name.Replace("Axial.", "").Replace("Axial.", "").Replace("Services.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
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
        name.Replace("Axial.", "").Replace("Axial.", "").Replace("Services.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
        |> (fun s -> s.Trim('.'))
        |> (fun s -> 
            s
            |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"``[0-9]+(?=$|[.])", "")
            |> fun value -> System.Text.RegularExpressions.Regex.Replace(value, @"`[0-9]+(?=$|[.])", "")
        )
        |> (fun s -> s.Replace("'", ""))
        |> (fun s -> if s.EndsWith(".Static") then s.Substring(0, s.Length - 7) else s)

/// Collapses consecutive duplicate dotted segments, e.g. "Flow.Flow.ToAsync" -> "Flow.ToAsync",
/// "Schema.Schema" -> "Schema". Module names frequently coincide with their enclosing namespace's
/// last segment, which otherwise shows up doubled in generated titles and index links.
let dedupeAdjacentSegments (name: string) =
    if String.IsNullOrEmpty name then name
    else
        let parts = name.Split('.')
        let result = ResizeArray<string>()
        for part in parts do
            if result.Count = 0 || result.[result.Count - 1] <> part then
                result.Add part
        String.Join(".", result)

let sanitizeFilename (name: string) =
    name.Replace("`", "-").Replace("'", "-").Replace(" ", "-").Replace(".", "-").ToLower()
    |> (fun s -> s.Trim('-'))

let formatterApiSlug (name: string) =
    name.Replace("`", "-").Replace("'", "").Replace(".", "-").Replace("+", "-").ToLowerInvariant()

let getPageName (id: string) =
    let kind = id.[0].ToString().ToLower()
    let namePart = id.Substring(2).Split('(').[0]
    let clean = 
        namePart.Replace("Axial.", "").Replace("Axial.", "").Replace("Services.", "").Replace("Module", "").Replace("Extensions", "").Replace("Builders", "")
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
    let qualifiedName = cleanName fullName |> dedupeAdjacentSegments
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
    let qualifiedName = cleanName fullName |> dedupeAdjacentSegments
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
        OutPath = ["schema"; "_index.md"]
        Title = "Schema"
        Description = "Source-documented model schema definitions for Axial."
        Intro = "This page shows the core `Schema<'model>`, `ValueSchema<'value>`, and `Field<'model, 'value>` types. Schemas describe trusted model and value structure for interpreters such as input parsing, validation, codecs, JSON Schema, UI, and documentation. The core schema package stays independent of workflow execution, diagnostics, raw input, and validation interpreters."
        SymbolIds = [
            "Core types", ["T:Axial.Schema.Schema`1"; "T:Axial.Schema.ValueSchema`1"; "T:Axial.Schema.Field`2"; "T:Axial.Schema.UnionCase`1"]
            "Value schemas", ["P:Axial.Schema.Value.text"; "P:Axial.Schema.Value.int"; "P:Axial.Schema.Value.decimal"; "P:Axial.Schema.Value.bool"; "P:Axial.Schema.Value.dateTime"; "P:Axial.Schema.Value.guid"; "M:Axial.Schema.Value.manyOf"; "M:Axial.Schema.Value.optionOf"; "M:Axial.Schema.Value.union"; "M:Axial.Schema.UnionCase.create"]
            "Inspection", ["T:Axial.Schema.ValueShape"; "T:Axial.Schema.ValueDescription"; "T:Axial.Schema.FieldDescription"; "T:Axial.Schema.ModelDescription"; "T:Axial.Schema.UnionDescription"; "T:Axial.Schema.UnionCaseDescription"; "M:Axial.Schema.Inspect.model"; "M:Axial.Schema.Inspect.value"; "M:Axial.Schema.Inspect.field"]
            "JSON Schema generation", ["M:Axial.Schema.JsonSchema.generate"; "M:Axial.Schema.JsonSchema.generateValue"]
        ]
        Alias = None
    }
    {
        OutPath = ["schema"; "interpreters"; "_index.md"]
        Title = "Schema Interpreters"
        Description = "Source-documented schema input parsing, validation, and rules interpreters."
        Intro = "This page shows the `Axial.Schema` interpreter surface: raw boundary input, schema input parsing into `ParsedInput`, intrinsic validation of existing models, and contextual rule sets over already-trusted models. Core schema metadata stays in [Schema](../); these interpreters attach diagnostics, raw input, and redisplay behavior to it."
        SymbolIds = [
            "Raw input", ["T:Axial.Schema.RawInput"; "T:Axial.Schema.JsonLikeValue"; "M:Axial.Schema.RawInputModule.ofMap"; "M:Axial.Schema.RawInputModule.ofNameValues"; "M:Axial.Schema.RawInputModule.ofCliArgs"; "M:Axial.Schema.RawInputModule.ofJsonLikeValue"; "M:Axial.Schema.RawInputModule.ofJsonElement"; "M:Axial.Schema.RawInputModule.ofJsonDocument"; "M:Axial.Schema.RawInputModule.ofConfiguration"; "M:Axial.Schema.RawInputModule.redisplay"; "M:Axial.Schema.RawInputModule.redisplayPath"]
            "Input parsing", ["T:Axial.Schema.ParsedInput`2"; "M:Axial.Schema.Input.parse"; "M:Axial.Schema.Input.parseWith"; "T:Axial.Schema.Input.Options"; "M:Axial.Schema.ParsedInputModule.mapErrors"; "M:Axial.Schema.ParsedInputModule.renderErrors"]
            "Errors", ["T:Axial.Schema.SchemaError"]
            "Refined catalog schemas", ["M:Axial.Schema.RefinedSchema.nonBlankString"; "M:Axial.Schema.RefinedSchema.trimmedString"; "M:Axial.Schema.RefinedSchema.boundedString"; "M:Axial.Schema.RefinedSchema.slug"; "M:Axial.Schema.RefinedSchema.positiveInt"; "M:Axial.Schema.RefinedSchema.nonNegativeInt"; "M:Axial.Schema.RefinedSchema.nonZeroInt"; "M:Axial.Schema.RefinedSchema.negativeInt"; "M:Axial.Schema.RefinedSchema.nonPositiveInt"; "M:Axial.Schema.RefinedSchema.nonEmptyList"; "M:Axial.Schema.RefinedSchema.nonEmptyArray"; "M:Axial.Schema.RefinedSchema.distinctList"; "M:Axial.Schema.RefinedSchema.boundedList"; "M:Axial.Schema.RefinedSchema.boundedArray"; "M:Axial.Schema.RefinedSchema.dateTimeOffsetRange"]
            "Model validation", ["M:Axial.Schema.Validation.validate"]
            "Rules", ["T:Axial.Schema.RuleSet`2"; "M:Axial.Schema.Rules.create"; "M:Axial.Schema.Rules.concat"; "M:Axial.Schema.Rules.at"; "M:Axial.Schema.Rules.failAt"; "M:Axial.Schema.Rules.custom"; "M:Axial.Schema.Rules.validate"; "M:Axial.Schema.Rules.apply"]
        ]
        Alias = None
    }
    {
        OutPath = ["codec"; "_index.md"]
        Title = "Codec"
        Description = "Source-documented compiled JSON codecs over built model schemas."
        Intro = "This page shows the `Axial.Codec` surface: `Json.compile` turns a built `Schema<'model>` into a reusable `JsonCodec<'model>` with reflection-free, constructor-specialized encode and decode plans. The codec is the trusted hot path for serialization; parse untrusted boundary input with [schema input parsing](../schema/interpreters/) when path-aware diagnostics matter."
        SymbolIds = [
            "Core types", ["T:Axial.Codec.JsonCodec`1"; "T:Axial.Codec.JsonCodecException"]
            "Module functions", ["M:Axial.Codec.Json.compile"; "M:Axial.Codec.Json.serialize"; "M:Axial.Codec.Json.serializeBytes"; "M:Axial.Codec.Json.serializeToStream"; "M:Axial.Codec.Json.deserialize"; "M:Axial.Codec.Json.deserializeBytes"; "M:Axial.Codec.Json.deserializeStreamAsync"; "M:Axial.Codec.Json.tryDeserialize"]
        ]
        Alias = None
    }
    {
        OutPath = ["flow"; "_index.md"]
        Title = "Flow"
        Description = "Source-documented workflow surface in Axial."
        Intro = "This page shows the Flow surface for cold workflow descriptions that only start when you call an execution member such as `workflow.ToTask(env)`, `workflow.ToValueTask(env)`, `workflow.ToAsync(env)`, or `workflow.RunSynchronously(env)`. The smallest useful signature is `Flow<'value>`: no environment and no typed failure. `Flow<'error, 'value>` adds typed failure with no environment; `EnvFlow<'env, 'value>` adds an environment with no typed failure; `ExnFlow`/`ExnEnvFlow` put recoverable exceptions in the typed error channel; the full `Flow<'env, 'error, 'value>` form carries both. Use this page as the API map for building fail-fast workflows with `flow { }`, `Flow.read`, `Flow.bind`, and `Flow.map`; reading dependencies from `env`; reshaping environments with `localEnv`; composing typed failures; and introducing concurrency with fibers, `zipPar`, or `race`. Reach for [runtime helpers](./runtime/) and parallel orchestration only at the boundary where the workflow actually needs them."
        SymbolIds = [
            "Core type", ["T:Axial.Flow.Flow`3"; "T:Axial.Flow.Flow`2"; "T:Axial.Flow.Flow`1"; "T:Axial.Flow.EnvFlow`2"; "T:Axial.Flow.ExnFlow`1"; "T:Axial.Flow.ExnEnvFlow`2"; "T:Axial.Flow.Never"]
            "Fiber operations", ["M:Axial.Flow.Flow.fork"; "M:Axial.Flow.Flow.join"; "M:Axial.Flow.Flow.interrupt"]
            "Execution", ["M:Axial.Flow.Flow.ToAsync"; "M:Axial.Flow.Flow.ToTask"; "M:Axial.Flow.Flow.ToValueTask"; "M:Axial.Flow.Flow.RunSynchronously"]
            "Module functions", ["M:Axial.Flow.Flow.ok"; "M:Axial.Flow.Flow.error"; "M:Axial.Flow.Flow.succeed"; "M:Axial.Flow.Flow.value"; "M:Axial.Flow.Flow.fail"; "M:Axial.Flow.Flow.fromResult"; "M:Axial.Flow.Flow.fromOption"; "M:Axial.Flow.Flow.fromValueOption"; "M:Axial.Flow.Flow.fromAsync"; "M:Axial.Flow.Flow.attemptAsync"; "M:Axial.Flow.Flow.fromTask"; "M:Axial.Flow.Flow.attemptTask"; "M:Axial.Flow.Flow.fromValueTask"; "M:Axial.Flow.Flow.attemptValueTask"; "M:Axial.Flow.Flow.verify"; "M:Axial.Flow.Flow.orElseFlow"; "M:Axial.Flow.Flow.env"; "M:Axial.Flow.Flow.read"; "M:Axial.Flow.Flow.map"; "M:Axial.Flow.Flow.bind"; "M:Axial.Flow.Flow.tap"; "M:Axial.Flow.Flow.tapError"; "M:Axial.Flow.Flow.mapError"; "M:Axial.Flow.Flow.catch"; "M:Axial.Flow.Flow.orElseWith"; "M:Axial.Flow.Flow.orElse"; "M:Axial.Flow.Flow.zip"; "M:Axial.Flow.Flow.map2"; "M:Axial.Flow.Flow.map3"; "M:Axial.Flow.Flow.apply"; "M:Axial.Flow.Flow.ignore"; "M:Axial.Flow.Flow.localEnv"; "M:Axial.Flow.Flow.provide"; "M:Axial.Flow.Flow.delay"; "M:Axial.Flow.Flow.traverse"; "M:Axial.Flow.Flow.sequence"]
            "Policies", ["T:Axial.Flow.Policy`4"; "M:Axial.Flow.Policy.pure"; "M:Axial.Flow.Policy.withError"; "M:Axial.Flow.Policy.context"; "P:Axial.Flow.Policy.pass"; "M:Axial.Flow.Policy.compose"; "M:Axial.Flow.Policy.optional"]
            "Scoped resources", ["M:Axial.Flow.Flow.addFinalizer"; "M:Axial.Flow.Flow.addDisposable"; "M:Axial.Flow.Flow.addAsyncDisposable"; "M:Axial.Flow.Flow.acquireRelease"; "M:Axial.Flow.Flow.acquireReleaseWith"]
            "Parallel orchestration", ["M:Axial.Flow.Flow.zipPar"; "M:Axial.Flow.Flow.race"]
            "Scheduling", ["M:Axial.Flow.ScheduleModule.retry"; "M:Axial.Flow.ScheduleModule.repeat"]
        ]
        Alias = None
    }
    {
        OutPath = ["fiber"; "_index.md"]
        Title = "Fiber"
        Description = "Source-documented handle for running workflows."
        Intro = "This page shows the `Fiber<'error, 'value>` handle used by Axial concurrency. A fiber represents a flow that has already been started in the background; it keeps the workflow's typed error and success values attached to the running work, plus diagnostic metadata such as fiber id, parent id, start time, and lifecycle status. The operations that create and consume fibers are still part of the [`Flow`](../flow/) API: use [`Flow.fork`](../flow/concurrency/m-flow-fork.md), [`Flow.join`](../flow/concurrency/m-flow-join.md), and [`Flow.interrupt`](../flow/concurrency/m-flow-interrupt.md) when a workflow needs explicit child execution. Prefer higher-level helpers such as `Flow.zipPar` or `Flow.race` when the code only needs parallel composition."
        SymbolIds = [
            "Core types", ["T:Axial.Flow.Fiber`2"; "T:Axial.Flow.FiberId"; "T:Axial.Flow.FiberStatus"; "T:Axial.Flow.FiberMetadata"; "T:Axial.Flow.FiberDump"]
            "Module functions", ["M:Axial.Flow.Fiber.dump"]
        ]
        Alias = None
    }
    {
        OutPath = ["concurrency"; "_index.md"]
        Title = "Concurrency"
        Description = "Source-documented deferred and semaphore primitives for Axial."
        Intro = "This page shows the small Flow-native concurrency primitives added for coordination that needs Axial semantics rather than raw .NET behavior. `Deferred<'error, 'value>` is a one-shot typed handoff point backed by a full `Exit<'value, 'error>`. `FlowSemaphore` limits concurrent workflow sections through scoped `Semaphore.withPermit`, releasing permits after success, typed failure, defect, or interruption."
        SymbolIds = [
            "Deferred", ["T:Axial.Flow.Deferred`2"; "M:Axial.Flow.Deferred.make"; "M:Axial.Flow.Deferred.await"; "M:Axial.Flow.Deferred.complete"; "M:Axial.Flow.Deferred.succeed"; "M:Axial.Flow.Deferred.fail"; "M:Axial.Flow.Deferred.die"; "M:Axial.Flow.Deferred.interrupt"]
            "Semaphore", ["T:Axial.Flow.FlowSemaphore"; "M:Axial.Flow.Semaphore.make"; "M:Axial.Flow.Semaphore.create"; "M:Axial.Flow.Semaphore.withPermit"]
        ]
        Alias = None
    }
    {
        OutPath = ["exit"; "_index.md"]
        Title = "Exit"
        Description = "Documentation for the Exit workflow outcome."
        Intro = "This page shows the `Exit<'value, 'error>` type, which is Axial's name for `Result<'value, Cause<'error>>`. We name it `Exit` because it represents a completed workflow execution, not an ordinary domain result. Use the `Exit` module functions to transform completed outcomes without manually pattern matching at every boundary."
        SymbolIds = [
            "Core type", ["T:Axial.Flow.Exit`2"]
            "Module functions", ["M:Axial.Flow.Exit.map"; "M:Axial.Flow.Exit.bind"; "M:Axial.Flow.Exit.mapError"; "M:Axial.Flow.Exit.mapBoth"; "M:Axial.Flow.Exit.fromResult"; "M:Axial.Flow.Exit.toResult"]
        ]
        Alias = None
    }
    {
        OutPath = ["cause"; "_index.md"]
        Title = "Cause"
        Description = "Documentation for the Cause of workflow failure."
        Intro = "This page shows the `Cause<'error>` type, which distinguishes expected domain failures, unexpected technical defects, administrative interruptions, sequential failure composition, parallel failure composition, and diagnostic traces. Understanding the cause tree lets Axial preserve what happened during retries, cleanup, parallel execution, and observability boundaries without flattening everything into one exception or one typed error."
        SymbolIds = [
            "Core type", ["T:Axial.Flow.Cause`1"]
            "Module functions", ["M:Axial.Flow.Cause.map"; "M:Axial.Flow.Cause.thenCause"; "M:Axial.Flow.Cause.both"; "M:Axial.Flow.Cause.traced"; "M:Axial.Flow.Cause.failures"; "M:Axial.Flow.Cause.defects"; "M:Axial.Flow.Cause.isInterrupted"; "M:Axial.Flow.Cause.prettyPrint"]
        ]
        Alias = None
    }
    {
        OutPath = ["flow"; "runtime"; "_index.md"]
        Title = "Flow.Runtime"
        Description = "Runtime helpers for operational concerns like logging, timeout, retry, and cleanup."
        Intro = "This page shows the `Flow.Runtime` helpers for closed executor mechanics. These functions expose cancellation, scope ownership, runtime annotations, timeout handling, and retry. User-facing resource combinators such as `Flow.acquireRelease` live on the main `Flow` module; `Flow.Runtime.scope` remains available for advanced code that needs direct scope access."
        SymbolIds = [
            "Runtime types", ["T:Axial.Flow.RetryPolicy`1"]
            "Runtime helpers", ["M:Axial.Flow.Flow.Runtime.cancellationToken"; "M:Axial.Flow.Flow.Runtime.catchCancellation"; "M:Axial.Flow.Flow.Runtime.ensureNotCanceled"; "M:Axial.Flow.Flow.Runtime.sleep"; "M:Axial.Flow.Flow.Runtime.scope"; "M:Axial.Flow.Flow.Runtime.annotations"; "M:Axial.Flow.Flow.Runtime.traceId"; "M:Axial.Flow.Flow.Runtime.timeout"; "M:Axial.Flow.Flow.Runtime.timeoutToOk"; "M:Axial.Flow.Flow.Runtime.timeoutToError"; "M:Axial.Flow.Flow.Runtime.timeoutWith"; "M:Axial.Flow.Flow.Runtime.retry"]
        ]
        Alias = None
    }
    {
        OutPath = ["schedule"; "_index.md"]
        Title = "Schedule"
        Description = "Source-documented retry and repeat logic for Axial."
        Intro = "This page shows the `Schedule` surface for describing retry and repeat policies as values. A `Schedule` on its own does nothing — it is a definition of when to run again (recur or stop) and how long to wait, not an action. Build one with `recurs` (bounded repetition), `spaced` (fixed delay), `exponential` (backoff), and `jittered` (randomized delay, so callers don't retry in lockstep), then apply it to a flow with `Schedule.retry` (rerun on a typed failure) or `Schedule.repeat` (rerun on a success) — nothing happens until one of those two runs the schedule against an actual workflow. Use schedules when retry behavior is part of the workflow boundary and must stay explicit, testable, and separate from the domain operation being retried."
        SymbolIds = [
            "Core type", ["T:Axial.Flow.Schedule`3"]
            "Module functions", ["M:Axial.Flow.ScheduleModule.recurs"; "M:Axial.Flow.ScheduleModule.spaced"; "M:Axial.Flow.ScheduleModule.exponential"; "M:Axial.Flow.ScheduleModule.jittered"; "M:Axial.Flow.ScheduleModule.retry"; "M:Axial.Flow.ScheduleModule.repeat"]
        ]
        Alias = None
    }
    {
        OutPath = ["ref"; "_index.md"]
        Title = "Ref"
        Description = "Source-documented atomic mutable references for Axial."
        Intro = "This page shows the `Ref` surface for small pieces of shared mutable state inside flows. A `Ref<'T>` is an atomic handle that can be created, read, set, updated, or modified from workflow code without turning the whole environment into a mutable object. Use `Ref` for counters, flags, request-local caches, and coordination points where a single value is enough. For multi-value invariants that must change together, use STM instead."
        SymbolIds = [
            "Core type", ["T:Axial.Flow.Ref`1"]
            "Module functions", ["M:Axial.Flow.Ref.make"; "M:Axial.Flow.Ref.get"; "M:Axial.Flow.Ref.set"; "M:Axial.Flow.Ref.update"; "M:Axial.Flow.Ref.modify"]
        ]
        Alias = None
    }
    {
        OutPath = ["stm"; "_index.md"]
        Title = "STM"
        Description = "Source-documented Software Transactional Memory for Axial."
        Intro = "This page shows the STM surface for composable atomic state transitions. STM is for cases where several transactional references must be read and updated as one operation, or where a workflow should wait until state satisfies a condition. Build transactions with `TRef` reads and writes, compose them before execution, then cross back into `Flow` with `STM.atomically`. Use `Ref` for one independent mutable value; use STM when correctness depends on a group of values changing together. \n\n**Note**: The current implementation uses a global synchronizing lock for coordination and is available on .NET only."
        SymbolIds = [
            "Core types", ["T:Axial.Flow.TRef`1"; "T:Axial.Flow.STM`1"]
            "Module functions", ["M:Axial.Flow.TRef.make"; "M:Axial.Flow.TRef.get"; "M:Axial.Flow.TRef.set"; "M:Axial.Flow.TRef.update"; "M:Axial.Flow.STM.atomically"]
            "Builder", ["T:Axial.Flow.StmBuilder"; "P:Axial.Flow.StmBuilders.stm"]
        ]
        Alias = None
    }
    {
        OutPath = ["stream"; "_index.md"]
        Title = "Stream"
        Description = "Source-documented effectful streams for Axial."
        Intro = "This page shows the `FlowStream` surface for cold, pull-based streams that still participate in Axial's environment and typed-error model. A stream can require `env`, emit values incrementally, and fail with the same error discipline as `Flow`. Use it when the boundary produces many values over time, such as file records, network messages, or paged API results. Keep per-item logic small and push final side effects through `runForEach` so cancellation and failure stay visible."
        SymbolIds = [
            "Core type", ["T:Axial.Flow.FlowStream`3"]
            "Module functions", ["M:Axial.Flow.FlowStream.fromSeq"; "M:Axial.Flow.FlowStream.map"; "M:Axial.Flow.FlowStream.runForEach"]
        ]
        Alias = None
    }
    {
        OutPath = ["flow"; "builders-flow.md"]
        Title = "flow { }"
        Description = "Documentation for the flow { } computation expression."
        Intro = "This page shows the `flow { }` computation expression, the primary syntax for writing Axial workflows. Inside the builder, ordinary values, `Result`, `Async`, `Task`, `Flow`, and guarded sources can be sequenced without manually unwrapping each layer. The builder preserves the important boundaries: expected errors stay typed, defects become `Cause.Die`, cancellation becomes interruption, and environment access remains explicit through `Flow.env` or `Flow.read`. Prefer `flow { }` for application orchestration; keep pure validation and simple predicates in `Check`, `Validation`, or `Result` until the code needs environment or effects."
        SymbolIds = [
            "Builder", ["P:Axial.Flow.Builders.flow"]
        ]
        Alias = None
    }
    {
        OutPath = ["check"; "_index.md"]
        Title = "Check"
        Description = "Source-documented pure validation helpers for Axial."
        Intro = "This page shows the `Check` surface for reusable, path-free value constraints. `Check.*` helpers return `Result<'value, CheckFailure list>`: a passing check hands back the same value unchanged, so it pipes directly into the next step. They compose with `Check.all`, `Check.any`, `Check.not`, and `Check.mapFailure`. Use [`Predicate`](../predicate/) when a local branch needs a raw boolean instead of a structured result. `Axial.ErrorHandling.CheckDSL` opens the deduplicated root names unqualified for use inside a validation module; `not`, `contains`, `distinct`, `all`, `any`, `length`, and `between` stay reachable only as `Check.___` there, since they shadow FSharp.Core names."
        SymbolIds = [
            "Core types", ["T:Axial.ErrorHandling.Check`1"; "T:Axial.ErrorHandling.CheckFailure"; "T:Axial.ErrorHandling.CheckLengthExpectation"; "T:Axial.ErrorHandling.CheckRangeExpectation"; "T:Axial.ErrorHandling.CheckCountExpectation"]
            "Executable composition", ["M:Axial.ErrorHandling.CheckModule.all"; "M:Axial.ErrorHandling.CheckModule.any"; "M:Axial.ErrorHandling.CheckModule.not"; "M:Axial.ErrorHandling.CheckModule.mapFailure"]
            "Top-level executable checks", ["M:Axial.ErrorHandling.CheckModule.present"; "M:Axial.ErrorHandling.CheckModule.empty"; "M:Axial.ErrorHandling.CheckModule.notEmpty"; "M:Axial.ErrorHandling.CheckModule.length"; "M:Axial.ErrorHandling.CheckModule.minLength"; "M:Axial.ErrorHandling.CheckModule.maxLength"; "M:Axial.ErrorHandling.CheckModule.lengthBetween"; "M:Axial.ErrorHandling.CheckModule.email"; "M:Axial.ErrorHandling.CheckModule.matches"; "M:Axial.ErrorHandling.CheckModule.oneOf"; "M:Axial.ErrorHandling.CheckModule.between"; "M:Axial.ErrorHandling.CheckModule.greaterThan"; "M:Axial.ErrorHandling.CheckModule.lessThan"; "M:Axial.ErrorHandling.CheckModule.atLeast"; "M:Axial.ErrorHandling.CheckModule.atMost"; "M:Axial.ErrorHandling.CheckModule.positive"; "M:Axial.ErrorHandling.CheckModule.nonNegative"; "M:Axial.ErrorHandling.CheckModule.negative"; "M:Axial.ErrorHandling.CheckModule.nonPositive"; "M:Axial.ErrorHandling.CheckModule.count"; "M:Axial.ErrorHandling.CheckModule.minCount"; "M:Axial.ErrorHandling.CheckModule.maxCount"; "M:Axial.ErrorHandling.CheckModule.countBetween"; "M:Axial.ErrorHandling.CheckModule.distinct"; "M:Axial.ErrorHandling.CheckModule.contains"; "M:Axial.ErrorHandling.CheckModule.single"; "M:Axial.ErrorHandling.CheckModule.atMostOne"; "M:Axial.ErrorHandling.CheckModule.atLeastOne"; "M:Axial.ErrorHandling.CheckModule.moreThanOne"; "M:Axial.ErrorHandling.CheckModule.equalTo"; "M:Axial.ErrorHandling.CheckModule.notEqualTo"]
            "Executable string checks", ["M:Axial.ErrorHandling.CheckModule.String.present"; "M:Axial.ErrorHandling.CheckModule.String.empty"; "M:Axial.ErrorHandling.CheckModule.String.notEmpty"; "M:Axial.ErrorHandling.CheckModule.String.minLength"; "M:Axial.ErrorHandling.CheckModule.String.maxLength"; "M:Axial.ErrorHandling.CheckModule.String.lengthBetween"; "M:Axial.ErrorHandling.CheckModule.String.exactLength"; "M:Axial.ErrorHandling.CheckModule.String.email"; "M:Axial.ErrorHandling.CheckModule.String.matches"; "M:Axial.ErrorHandling.CheckModule.String.numeric"; "M:Axial.ErrorHandling.CheckModule.String.alphaNumeric"; "M:Axial.ErrorHandling.CheckModule.String.oneOf"]
            "Executable number checks", ["M:Axial.ErrorHandling.CheckModule.Number.between"; "M:Axial.ErrorHandling.CheckModule.Number.greaterThan"; "M:Axial.ErrorHandling.CheckModule.Number.lessThan"; "M:Axial.ErrorHandling.CheckModule.Number.atLeast"; "M:Axial.ErrorHandling.CheckModule.Number.atMost"; "M:Axial.ErrorHandling.CheckModule.Number.positive"; "M:Axial.ErrorHandling.CheckModule.Number.nonNegative"; "M:Axial.ErrorHandling.CheckModule.Number.negative"; "M:Axial.ErrorHandling.CheckModule.Number.nonPositive"]
            "Executable sequence checks", ["M:Axial.ErrorHandling.CheckModule.Seq.empty"; "M:Axial.ErrorHandling.CheckModule.Seq.notEmpty"; "M:Axial.ErrorHandling.CheckModule.Seq.count"; "M:Axial.ErrorHandling.CheckModule.Seq.minCount"; "M:Axial.ErrorHandling.CheckModule.Seq.maxCount"; "M:Axial.ErrorHandling.CheckModule.Seq.countBetween"; "M:Axial.ErrorHandling.CheckModule.Seq.noDuplicates"; "M:Axial.ErrorHandling.CheckModule.Seq.contains"; "M:Axial.ErrorHandling.CheckModule.Seq.single"; "M:Axial.ErrorHandling.CheckModule.Seq.atMostOne"; "M:Axial.ErrorHandling.CheckModule.Seq.atLeastOne"; "M:Axial.ErrorHandling.CheckModule.Seq.moreThanOne"]
            "Executable optional checks", ["M:Axial.ErrorHandling.CheckModule.Option.some"; "M:Axial.ErrorHandling.CheckModule.Option.none"; "M:Axial.ErrorHandling.CheckModule.ValueOption.some"; "M:Axial.ErrorHandling.CheckModule.ValueOption.none"; "M:Axial.ErrorHandling.CheckModule.Nullable.hasValue"; "M:Axial.ErrorHandling.CheckModule.Nullable.hasNoValue"; "M:Axial.ErrorHandling.CheckModule.Result.ok"; "M:Axial.ErrorHandling.CheckModule.Result.error"]
        ]
        Alias = None
    }
    {
        OutPath = ["predicate"; "_index.md"]
        Title = "Predicate"
        Description = "Source-documented boolean predicates for Axial."
        Intro = "This page shows the `Predicate` and `PredicateExtensions` surface: plain `bool` facts for local branching (`if`, `match`, guard clauses), as opposed to [`Check`](../check/), which returns a structured `Result`. `PredicateExtensions` is `AutoOpen`, adding members such as `IsBlank`, `IsPresent`, and `HasItems` directly onto the types they describe. `Predicate.present`, `Predicate.empty`, and `Predicate.notEmpty` are the `bool`-returning counterparts to `Check.present`/`Check.empty`/`Check.notEmpty`, using the same type-directed SRTP dispatch."
        SymbolIds = [
            "Type-directed presence facade", ["M:Axial.ErrorHandling.PredicateModule.present"; "M:Axial.ErrorHandling.PredicateModule.empty"; "M:Axial.ErrorHandling.PredicateModule.notEmpty"]
            "Option and result predicates", ["M:Axial.ErrorHandling.PredicateExtensions.Option`1.get_IsPresent``1(Microsoft.FSharp.Core.FSharpOption{``0})"; "M:Axial.ErrorHandling.PredicateExtensions.Option`1.get_IsAbsent``1(Microsoft.FSharp.Core.FSharpOption{``0})"; "M:Axial.ErrorHandling.PredicateExtensions.ValueOption`1.get_IsPresent``1(Microsoft.FSharp.Core.FSharpValueOption{``0})"; "M:Axial.ErrorHandling.PredicateExtensions.ValueOption`1.get_IsAbsent``1(Microsoft.FSharp.Core.FSharpValueOption{``0})"; "M:Axial.ErrorHandling.PredicateExtensions.Result.IsOk"; "M:Axial.ErrorHandling.PredicateExtensions.Result.IsError"]
            "Presence predicates", ["M:Axial.ErrorHandling.PredicateExtensions.Nullable`1.get_IsPresent``1(System.Nullable{``0})"; "M:Axial.ErrorHandling.PredicateExtensions.Nullable`1.get_IsAbsent``1(System.Nullable{``0})"; "M:Axial.ErrorHandling.PredicateModule.Reference.notNull"; "M:Axial.ErrorHandling.PredicateModule.Reference.isNull"]
            "String predicates", ["M:Axial.ErrorHandling.PredicateExtensions.String.IsEmpty"; "M:Axial.ErrorHandling.PredicateExtensions.String.IsNotEmpty"; "M:Axial.ErrorHandling.PredicateExtensions.String.IsBlank"; "M:Axial.ErrorHandling.PredicateExtensions.String.IsNotBlank"; "M:Axial.ErrorHandling.PredicateExtensions.String.HasMinLength"; "M:Axial.ErrorHandling.PredicateExtensions.String.HasMaxLength"; "M:Axial.ErrorHandling.PredicateExtensions.String.HasLength"; "M:Axial.ErrorHandling.PredicateExtensions.String.HasLengthBetween"; "M:Axial.ErrorHandling.PredicateExtensions.String.MatchesPattern"; "M:Axial.ErrorHandling.PredicateExtensions.String.IsEmail"; "M:Axial.ErrorHandling.PredicateExtensions.String.IsNumeric"; "M:Axial.ErrorHandling.PredicateExtensions.String.IsAlphaNumeric"]
            "Sequence predicates", ["M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasNoItems"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasItems"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasItem"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasCount"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasMinCount"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasMaxCount"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasCountBetween"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasSingleItem"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasAtMostOneItem"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasMoreThanOneItem"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.HasDuplicates"; "M:Axial.ErrorHandling.PredicateExtensions.IEnumerable.IsDistinct"]
            "Comparison predicates", ["M:Axial.ErrorHandling.PredicateModule.Number.greaterThan"; "M:Axial.ErrorHandling.PredicateModule.Number.lessThan"; "M:Axial.ErrorHandling.PredicateModule.Number.atLeast"; "M:Axial.ErrorHandling.PredicateModule.Number.atMost"; "M:Axial.ErrorHandling.PredicateModule.Number.between"; "M:Axial.ErrorHandling.PredicateModule.Number.positive"; "M:Axial.ErrorHandling.PredicateModule.Number.nonNegative"; "M:Axial.ErrorHandling.PredicateModule.Number.negative"; "M:Axial.ErrorHandling.PredicateModule.Number.nonPositive"]
        ]
        Alias = None
    }
    {
        OutPath = ["bind"; "_index.md"]
        Title = "Bind"
        Description = "Source-documented flow bind-site error adaptation for Axial."
        Intro = "This page shows the `Bind` helpers used when a source needs its error assigned or mapped immediately before `flow { }` binds it. Use `Bind.error` for option or value-option absence and unit-error failures such as `Result<'value, unit>` or `Flow<'env, unit, 'value>`. Use `Bind.mapError` when the source already carries a meaningful error that must be wrapped or translated into the surrounding flow error. The helpers return a `BindError` marker for the flow builder. Do not use `Bind` as a general Result adapter; in pure code use `Result.mapError`, `Result.orError`, or `Validation.mapError`."
        SymbolIds = [
            "Core type", ["T:Axial.Flow.BindError`3"]
            "Module functions", ["M:Axial.Flow.Bind.error"; "M:Axial.Flow.Bind.mapError"]
        ]
        Alias = None
    }
    {
        OutPath = ["validation"; "_index.md"]
        Title = "Validation"
        Description = "Source-documented accumulating validation for Axial."
        Intro = "This page shows the `Validation<'value, 'error>` surface for accumulating several failures into one diagnostics graph. Unlike `Result`, validation does not stop at the first independent error; functions such as `map2`, `map3`, `apply`, `collect`, and `traverseIndexed` combine sibling checks and preserve all reported problems. Use `Validation.fromResult` as the canonical bridge from fail-fast `Result` values into validation, and use `Validation.toResult` when a boundary expects ordinary `Result`. Use path helpers such as `name`, `key`, `index`, and `at` to attach errors to fields, map entries, list positions, or nested structures. Use `Validation` for input decoding, command validation, configuration checks, and any boundary where users need a complete error report."
        SymbolIds = [
            "Core type", ["T:Axial.Validation.Validation`2"]
            "Module functions", ["M:Axial.Validation.Validation.toResult"; "M:Axial.Validation.Validation.ok"; "M:Axial.Validation.Validation.error"; "M:Axial.Validation.Validation.succeed"; "M:Axial.Validation.Validation.fail"; "M:Axial.Validation.Validation.fromResult"; "M:Axial.Validation.Validation.map"; "M:Axial.Validation.Validation.bind"; "M:Axial.Validation.Validation.mapError"; "M:Axial.Validation.Validation.map2"; "M:Axial.Validation.Validation.map3"; "M:Axial.Validation.Validation.apply"; "M:Axial.Validation.Validation.ignore"; "M:Axial.Validation.Validation.orElse"; "M:Axial.Validation.Validation.orElseWith"; "M:Axial.Validation.Validation.collect"; "M:Axial.Validation.Validation.sequence"; "M:Axial.Validation.Validation.traverseIndexed"; "M:Axial.Validation.Validation.merge"]
            "Path scoping", ["M:Axial.Validation.Validation.at"; "M:Axial.Validation.Validation.key"; "M:Axial.Validation.Validation.index"; "M:Axial.Validation.Validation.name"]
        ]
        Alias = None
    }
    {
        OutPath = ["validation"; "builders-validate.md"]
        Title = "validate { }"
        Description = "Documentation for the validate { } computation expression."
        Intro = "This page shows the `validate { }` computation expression for writing validation logic with direct, sequential syntax. The builder is best for validation steps that read clearly as a block while still returning `Validation<'value, 'error>`. Use it when each bound step depends on earlier successful values. For independent sibling fields where you want maximum error accumulation, prefer `Validation.map2`, `map3`, `apply`, `collect`, or `traverseIndexed` so all branches are evaluated and all diagnostics are retained."
        SymbolIds = [
            "Builder", ["P:Axial.Validation.Builders.validate"]
        ]
        Alias = None
    }
    {
        OutPath = ["result"; "_index.md"]
        Title = "Result"
        Description = "Source-documented fail-fast Result helpers for Axial."
        Intro = "This page shows Axial's fail-fast helpers over the standard F# `Result<'value, 'error>` type. Use `Result.requireTrue` when a bare `bool` condition should become a `Result` (nothing to preserve). Use `Result.okIf`/`Result.failIf` (mirroring `Option.filter`) when a predicate over the value itself should keep that value on success, then attach the real error afterward with `Result.orError`. Extraction helpers such as `Result.someOr` change the success shape. For domain checks with a built-in error type, reach for `Check.*` directly — it is already value-preserving, so no separate `Result` wrapper is needed. Sequence cardinality extraction (`exactlyOne`, `atMostOne`) lives on [Refine]({{< relref \"/reference/refined/\" >}}) instead, since it is a structural refinement, not a generic Result concern. The `result { }` builder sequences ordinary fail-fast `Result` workflows."
        SymbolIds = [
            "Structured errors", ["T:Axial.ErrorHandling.CheckFailure"]
            "Core helpers", ["M:Axial.ErrorHandling.Result.ok"; "M:Axial.ErrorHandling.Result.error"; "M:Axial.ErrorHandling.Result.map"; "M:Axial.ErrorHandling.Result.mapError"; "M:Axial.ErrorHandling.Result.bind"; "M:Axial.ErrorHandling.Result.orElse"; "M:Axial.ErrorHandling.Result.orElseWith"]
            "Lifts and conversions", ["M:Axial.ErrorHandling.Result.requireTrue"; "M:Axial.ErrorHandling.Result.okIf"; "M:Axial.ErrorHandling.Result.failIf"; "M:Axial.ErrorHandling.Result.orError"; "M:Axial.ErrorHandling.Result.fromTry"; "M:Axial.ErrorHandling.Result.fromChoice"; "M:Axial.ErrorHandling.Result.toOption"; "M:Axial.ErrorHandling.Result.toValueOption"; "M:Axial.ErrorHandling.Result.defaultValue"]
            "Extraction helpers", ["M:Axial.ErrorHandling.Result.someOr"; "M:Axial.ErrorHandling.Result.noneOr"; "M:Axial.ErrorHandling.Result.valueSomeOr"; "M:Axial.ErrorHandling.Result.valueNoneOr"; "M:Axial.ErrorHandling.Result.nullableOr"; "M:Axial.ErrorHandling.Result.notNullOr"; "M:Axial.ErrorHandling.Result.okOr"; "M:Axial.ErrorHandling.Result.errorOr"; "M:Axial.ErrorHandling.Result.headOr"]
            "Traversal", ["M:Axial.ErrorHandling.Collection.traverseResult"; "M:Axial.ErrorHandling.Collection.sequenceResult"]
            "Builder", ["P:Axial.ErrorHandling.Builders.result"]
        ]
        Alias = Some "builders-result.md"
    }
    {
        OutPath = ["refined"; "_index.md"]
        Title = "Refined"
        Description = "Source-documented parsing and structural refinement helpers for Axial."
        Intro = "This page shows the `Axial.Refined` surface for turning untrusted boundary data into stronger structural values. Use `Parse` to convert serialized strings into primitive values, focused modules such as `Text`, `Numeric`, `Collection`, and `Temporal` to construct refined values such as `Slug`, `NonZeroInt`, and `DateTimeOffsetRange`, and `refine { }` to sequence fail-fast parsing and refinement before workflow execution."
        SymbolIds = [
            "Errors and refined types", [
                "T:Axial.Refined.ParseError"; "T:Axial.Refined.RefinementError"
                "T:Axial.Refined.NonBlankString"; "T:Axial.Refined.TrimmedString"; "T:Axial.Refined.BoundedString"; "T:Axial.Refined.Slug"
                "T:Axial.Refined.PositiveInt"; "T:Axial.Refined.NonNegativeInt"; "T:Axial.Refined.NonZeroInt"; "T:Axial.Refined.NegativeInt"; "T:Axial.Refined.NonPositiveInt"
                "T:Axial.Refined.NonEmptyList`1"; "T:Axial.Refined.NonEmptyArray`1"; "T:Axial.Refined.DistinctList`1"; "T:Axial.Refined.BoundedList`1"; "T:Axial.Refined.BoundedArray`1"
                "T:Axial.Refined.DateTimeOffsetRange"; "T:Axial.Refined.DateOnlyRange"
            ]
            "Parse", ["M:Axial.Refined.Parse.int"; "M:Axial.Refined.Parse.long"; "M:Axial.Refined.Parse.decimal"; "M:Axial.Refined.Parse.float"; "M:Axial.Refined.Parse.bool"; "M:Axial.Refined.Parse.guid"; "M:Axial.Refined.Parse.dateTime"; "M:Axial.Refined.Parse.dateTimeOffset"; "M:Axial.Refined.Parse.dateOnly"; "M:Axial.Refined.Parse.timeOnly"; "M:Axial.Refined.Parse.enum"; "M:Axial.Refined.Parse.intOption"; "M:Axial.Refined.Parse.boolOption"; "M:Axial.Refined.Parse.decimalOption"; "M:Axial.Refined.Parse.guidOption"; "M:Axial.Refined.Parse.intOrDefault"; "M:Axial.Refined.Parse.boolOrDefault"; "M:Axial.Refined.Parse.decimalOrDefault"]
            "Text", ["M:Axial.Refined.Text.nonBlankString"; "M:Axial.Refined.Text.trimmedString"; "M:Axial.Refined.Text.boundedString"; "M:Axial.Refined.Text.slug"]
            "Numeric", ["M:Axial.Refined.Numeric.positiveInt"; "M:Axial.Refined.Numeric.nonNegativeInt"; "M:Axial.Refined.Numeric.nonZeroInt"; "M:Axial.Refined.Numeric.negativeInt"; "M:Axial.Refined.Numeric.nonPositiveInt"]
            "Collection", ["M:Axial.Refined.Collection.nonEmptyList"; "M:Axial.Refined.Collection.nonEmptyArray"; "M:Axial.Refined.Collection.distinctList"; "M:Axial.Refined.Collection.boundedList"; "M:Axial.Refined.Collection.boundedArray"; "M:Axial.Refined.Collection.exactlyOne"; "M:Axial.Refined.Collection.atMostOne"]
            "Temporal", ["M:Axial.Refined.Temporal.dateTimeOffsetRange"; "M:Axial.Refined.Temporal.dateOnlyRange"]
            "Character", ["M:Axial.Refined.Character.isAsciiDigit"; "M:Axial.Refined.Character.isAsciiHexDigit"; "M:Axial.Refined.Character.isLowercase"; "M:Axial.Refined.Character.isUppercase"; "M:Axial.Refined.Character.isWhitespace"; "M:Axial.Refined.Character.isControl"; "M:Axial.Refined.Character.isNumeric"]
            "Choice", ["M:Axial.Refined.Choice.orElse"; "M:Axial.Refined.Choice.tryAny"]
            "Re-certifying helpers", ["M:Axial.Refined.NonBlankString.value"; "M:Axial.Refined.NonBlankString.create"; "M:Axial.Refined.NonBlankString.map"; "M:Axial.Refined.PositiveInt.value"; "M:Axial.Refined.PositiveInt.create"; "M:Axial.Refined.PositiveInt.map"; "M:Axial.Refined.PositiveInt.replace"; "M:Axial.Refined.NonEmptyList.toList"; "M:Axial.Refined.NonEmptyList.create"; "M:Axial.Refined.NonEmptyList.cons"; "M:Axial.Refined.NonEmptyList.map"; "M:Axial.Refined.NonEmptyList.filter"; "M:Axial.Refined.NonEmptyList.tryFilter"]
            "Refine facade", ["M:Axial.Refined.Refine.withCheck"; "M:Axial.Refined.Refine.withChecks"; "M:Axial.Refined.Refine.nonBlankString"; "M:Axial.Refined.Refine.trimmedString"; "M:Axial.Refined.Refine.boundedString"; "M:Axial.Refined.Refine.slug"; "M:Axial.Refined.Refine.positiveInt"; "M:Axial.Refined.Refine.nonNegativeInt"; "M:Axial.Refined.Refine.nonZeroInt"; "M:Axial.Refined.Refine.negativeInt"; "M:Axial.Refined.Refine.nonPositiveInt"; "M:Axial.Refined.Refine.nonEmptyList"; "M:Axial.Refined.Refine.nonEmptyArray"; "M:Axial.Refined.Refine.distinctList"; "M:Axial.Refined.Refine.boundedList"; "M:Axial.Refined.Refine.boundedArray"; "M:Axial.Refined.Refine.dateTimeOffsetRange"; "M:Axial.Refined.Refine.dateOnlyRange"; "M:Axial.Refined.Refine.exactlyOne"; "M:Axial.Refined.Refine.atMostOne"]
            "Builder", ["P:Axial.Refined.Builders.refine"]
        ]
        Alias = None
    }
    {
        OutPath = ["diagnostics"; "_index.md"]
        Title = "Diagnostics"
        Description = "Source-documented validation diagnostics graph for Axial."
        Intro = "This page shows the diagnostics graph used by `Validation`. A `Diagnostics<'error>` value stores errors at the current node and at named, keyed, or indexed child paths, so validation can report both what failed and where it failed. Use `Diagnostics.singleton` for one error, `merge` to combine sibling reports, `flatten` when callers need path-bearing diagnostics, and `toString` for compact human-readable output. Keep diagnostics at the validation boundary; convert them to domain responses or UI messages at the edge."
        SymbolIds = [
            "Graph types", ["T:Axial.Validation.PathSegment"; "T:Path"; "T:Axial.Validation.Diagnostic`1"; "T:Axial.Validation.Diagnostics`1"]
            "Module functions", ["M:Axial.Validation.Diagnostics.empty"; "M:Axial.Validation.Diagnostics.singleton"; "M:Axial.Validation.Diagnostics.merge"; "M:Axial.Validation.Diagnostics.toString"; "M:Axial.Validation.Diagnostics.flatten"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "_index.md"]
        Title = "Service"
        Description = "Source-documented service contracts and dependency access helpers for Axial."
        Intro = "This page shows the service helpers around Axial's explicit environment model. In Axial, a service is a named dependency contract such as `IClock`, `IConsole`, or `IHttp`. Prefer plain records plus `Flow.read` for local workflow code, use `IHas<'T>` plus `Service<'service>.get()` when reusable helpers need a nominal service contract, and keep `Service<'service>.resolve()` at .NET host boundaries where `IServiceProvider` interop is useful. Layers provision explicit services, while the ambient runtime is reserved for closed executor mechanics only.\n\nSee the standard service packages: [Core](./core/), [Console](./console/), [FileSystem](./filesystem/), [Http](./http/), and [Process](./process/)."
        SymbolIds = [
            "Service contracts", ["T:Axial.Flow.IHas`1"; "T:Axial.Flow.Service`1"]
            "Service accessors", ["M:Axial.Flow.Service.get"; "M:Axial.Flow.Service.resolve"]
            "Environment helpers", ["M:Axial.Flow.Flow.read"]
        ]
        Alias = None
    }
    {
        OutPath = ["layer"; "_index.md"]
        Title = "Layer"
        Description = "Source-documented service provisioning surface for Axial."
        Intro = "This page shows the `Layer<'input, 'error, 'output>` surface used to provision explicit services and environments. Layers build service values inside a `Scope`, can fail during provisioning, and are consumed through `Flow.provide`. Use `layer { }` for application environment construction: plain `let!` is dependent and sequential, while sibling `and!` bindings use `Layer.merge` / `Layer.zipPar` for independent parallel provisioning."
        SymbolIds = [
            "Core type", ["T:Axial.Flow.Layer`3"]
            "Builder", ["P:Axial.Flow.Builders.layer"]
            "Module functions", ["M:Axial.Flow.Layer.fromAsync"; "M:Axial.Flow.Layer.fromTask"; "M:Axial.Flow.Layer.fromValueTask"; "M:Axial.Flow.Layer.succeed"; "M:Axial.Flow.Layer.read"; "M:Axial.Flow.Layer.addFinalizer"; "M:Axial.Flow.Layer.acquireRelease"; "M:Axial.Flow.Layer.map"; "M:Axial.Flow.Layer.mapError"; "M:Axial.Flow.Layer.bind"; "M:Axial.Flow.Layer.zip"; "M:Axial.Flow.Layer.zipPar"; "M:Axial.Flow.Layer.merge"; "M:Axial.Flow.Layer.map2"; "M:Axial.Flow.Layer.apply"; "M:Axial.Flow.Layer.map3"]
            "Flow integration", ["M:Axial.Flow.Flow.provide"]
        ]
        Alias = None
    }
    {
        OutPath = ["scope"; "_index.md"]
        Title = "Scope"
        Description = "Source-documented resource scope for Axial."
        Intro = "This page shows the `Scope` surface used to own cleanup for resources acquired during provisioning and execution. Scopes register finalizers, disposables, and async disposables, and they close in reverse registration order."
        SymbolIds = [
            "Core type", ["T:Axial.Flow.Scope"]
            "Methods", ["M:Axial.Flow.Scope.AddFinalizer(Microsoft.FSharp.Core.FSharpFunc{System.Threading.CancellationToken,System.Threading.Tasks.Task})"; "M:Axial.Flow.Scope.AddDisposable(System.IDisposable)"; "M:Axial.Flow.Scope.AddAsyncDisposable(System.IAsyncDisposable)"; "M:Axial.Flow.Scope.AddChild"; "M:Axial.Flow.Scope.Close(System.Threading.CancellationToken)"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "core"; "_index.md"]
        Title = "Services Core"
        Description = "Source-documented synchronous service primitives for Axial.Flow.PlatformService."
        Intro = "This page shows the core service package: clock, logging, random numbers, GUID generation, and environment-variable lookup. These are explicit services, not ambient runtime slots. Use the helper modules when a workflow needs one of these services, and use `BaseRuntime` or custom environments to supply deterministic or live implementations."
        SymbolIds = [
            "Service types", ["T:Axial.Flow.PlatformService.IClock"; "T:Axial.Flow.PlatformService.ILog"; "T:Axial.Flow.LogLevel"; "T:Axial.Flow.PlatformService.IRandom"; "T:Axial.Flow.PlatformService.IGuid"; "T:Axial.Flow.PlatformService.IEnvironmentVariables"; "T:Axial.Flow.PlatformService.EnvironmentVariableError"; "T:Axial.Flow.PlatformService.BaseRuntimeError"; "T:Axial.Flow.PlatformService.BaseRuntime"]
            "Base runtime", ["P:Axial.Flow.PlatformService.BaseRuntimeModule.liveValue"; "P:Axial.Flow.PlatformService.BaseRuntimeModule.live"; "P:Axial.Flow.PlatformService.BaseRuntimeModule.fromServiceProvider"]
            "Clock", ["M:Axial.Flow.PlatformService.Clock.now"; "M:Axial.Flow.PlatformService.Clock.utcDateTime"; "M:Axial.Flow.PlatformService.Clock.unixTimeSeconds"; "M:Axial.Flow.PlatformService.Clock.unixTimeMilliseconds"; "P:Axial.Flow.PlatformService.Clock.live"; "P:Axial.Flow.PlatformService.Clock.layer"; "M:Axial.Flow.PlatformService.Clock.fromValue"]
            "Logging", ["M:Axial.Flow.PlatformService.Log.log"; "M:Axial.Flow.PlatformService.Log.trace"; "M:Axial.Flow.PlatformService.Log.debug"; "M:Axial.Flow.PlatformService.Log.info"; "M:Axial.Flow.PlatformService.Log.warning"; "M:Axial.Flow.PlatformService.Log.error"; "M:Axial.Flow.PlatformService.Log.critical"; "P:Axial.Flow.PlatformService.Log.live"; "P:Axial.Flow.PlatformService.Log.layer"; "M:Axial.Flow.PlatformService.Log.fromSink"]
            "Random", ["M:Axial.Flow.PlatformService.Random.next"; "M:Axial.Flow.PlatformService.Random.nextMax"; "M:Axial.Flow.PlatformService.Random.nextInt"; "M:Axial.Flow.PlatformService.Random.nextDouble"; "M:Axial.Flow.PlatformService.Random.nextBytes"; "M:Axial.Flow.PlatformService.Random.bytes"; "P:Axial.Flow.PlatformService.Random.live"; "P:Axial.Flow.PlatformService.Random.layer"; "M:Axial.Flow.PlatformService.Random.fromValue"; "M:Axial.Flow.PlatformService.Random.fromFixed"]
            "GUID", ["M:Axial.Flow.PlatformService.Guid.newGuid"; "P:Axial.Flow.PlatformService.Guid.live"; "P:Axial.Flow.PlatformService.Guid.layer"; "M:Axial.Flow.PlatformService.Guid.fromValue"]
            "Environment variables", ["M:Axial.Flow.PlatformService.EnvironmentVariables.tryGet"; "M:Axial.Flow.PlatformService.EnvironmentVariables.getAll"; "M:Axial.Flow.PlatformService.EnvironmentVariables.set"; "M:Axial.Flow.PlatformService.EnvironmentVariables.clear"; "M:Axial.Flow.PlatformService.EnvironmentVariables.expand"; "P:Axial.Flow.PlatformService.EnvironmentVariables.live"; "P:Axial.Flow.PlatformService.EnvironmentVariables.layer"; "M:Axial.Flow.PlatformService.EnvironmentVariables.fromPairs"; "M:Axial.Flow.PlatformService.EnvironmentVariable.tryGet"; "M:Axial.Flow.PlatformService.EnvironmentVariable.get"; "M:Axial.Flow.PlatformService.EnvironmentVariable.getInt"; "M:Axial.Flow.PlatformService.EnvironmentVariable.getInt64"; "M:Axial.Flow.PlatformService.EnvironmentVariable.getDouble"; "M:Axial.Flow.PlatformService.EnvironmentVariable.getDecimal"; "M:Axial.Flow.PlatformService.EnvironmentVariable.getGuid"; "M:Axial.Flow.PlatformService.EnvironmentVariable.getUri"; "M:Axial.Flow.PlatformService.EnvironmentVariable.getTimeSpan"; "M:Axial.Flow.PlatformService.EnvironmentVariable.getBool"; "M:Axial.Flow.PlatformService.EnvironmentVariableErrors.describe"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "console"; "_index.md"]
        Title = "Services Console"
        Description = "Source-documented console I/O service for Axial.Flow.Console."
        Intro = "This page shows the console service package. `IConsole` models standard input and output as an explicit workflow service. Keep business logic typed against the service contract, provide `Console.live` only at the edge, and replace it with a test implementation when you need deterministic input or captured output."
        SymbolIds = [
            "Service", ["T:Axial.Flow.Console.IConsole"]
            "Helpers", ["M:Axial.Flow.Console.Console.readLine"; "M:Axial.Flow.Console.Console.writeLine"; "P:Axial.Flow.Console.Console.live"; "P:Axial.Flow.Console.Console.layer"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "filesystem"; "_index.md"]
        Title = "Services FileSystem"
        Description = "Source-documented file-system service for Axial.Flow.FileSystem."
        Intro = "This page shows the file-system service package. `IFileSystem` models common `System.IO.File`, `Directory`, `Path`, text, byte, stream, metadata, and timestamp operations as an explicit workflow service. Keep workflow code typed against the service contract, provide `FileSystem.live` only at the edge, and replace it with a deterministic implementation in tests. File-system helpers classify thrown platform exceptions into `FileSystemError` so workflow errors stay typed instead of escaping as ordinary exceptions."
        SymbolIds = [
            "Service", ["T:Axial.Flow.FileSystem.IFileSystem"; "T:Axial.Flow.FileSystem.FileSystemError"]
            "Errors", ["M:Axial.Flow.FileSystem.FileSystemError.fromException"; "M:Axial.Flow.FileSystem.FileSystemError.describe"]
            "Text and bytes",
                [ "M:Axial.Flow.FileSystem.FileSystem.readAllText"
                  "M:Axial.Flow.FileSystem.FileSystem.readAllTextWithEncoding"
                  "M:Axial.Flow.FileSystem.FileSystem.readAllTextAsync"
                  "M:Axial.Flow.FileSystem.FileSystem.readAllLines"
                  "M:Axial.Flow.FileSystem.FileSystem.readAllLinesWithEncoding"
                  "M:Axial.Flow.FileSystem.FileSystem.readAllLinesAsync"
                  "M:Axial.Flow.FileSystem.FileSystem.readAllBytes"
                  "M:Axial.Flow.FileSystem.FileSystem.readAllBytesAsync"
                  "M:Axial.Flow.FileSystem.FileSystem.writeAllText"
                  "M:Axial.Flow.FileSystem.FileSystem.writeAllTextWithEncoding"
                  "M:Axial.Flow.FileSystem.FileSystem.writeAllTextAsync"
                  "M:Axial.Flow.FileSystem.FileSystem.writeAllLines"
                  "M:Axial.Flow.FileSystem.FileSystem.writeAllLinesWithEncoding"
                  "M:Axial.Flow.FileSystem.FileSystem.writeAllLinesAsync"
                  "M:Axial.Flow.FileSystem.FileSystem.writeAllBytes"
                  "M:Axial.Flow.FileSystem.FileSystem.writeAllBytesAsync"
                  "M:Axial.Flow.FileSystem.FileSystem.appendAllText"
                  "M:Axial.Flow.FileSystem.FileSystem.appendAllTextWithEncoding"
                  "M:Axial.Flow.FileSystem.FileSystem.appendAllTextAsync"
                  "M:Axial.Flow.FileSystem.FileSystem.appendAllLines"
                  "M:Axial.Flow.FileSystem.FileSystem.appendAllLinesWithEncoding" ]
            "Files and streams",
                [ "M:Axial.Flow.FileSystem.FileSystem.fileExists"
                  "M:Axial.Flow.FileSystem.FileSystem.exists"
                  "M:Axial.Flow.FileSystem.FileSystem.deleteFile"
                  "M:Axial.Flow.FileSystem.FileSystem.copyFile"
                  "M:Axial.Flow.FileSystem.FileSystem.moveFile"
                  "M:Axial.Flow.FileSystem.FileSystem.openFile"
                  "M:Axial.Flow.FileSystem.FileSystem.openFileWithAccess"
                  "M:Axial.Flow.FileSystem.FileSystem.openFileWithShare"
                  "M:Axial.Flow.FileSystem.FileSystem.openRead"
                  "M:Axial.Flow.FileSystem.FileSystem.openText"
                  "M:Axial.Flow.FileSystem.FileSystem.openWrite"
                  "M:Axial.Flow.FileSystem.FileSystem.createFile"
                  "M:Axial.Flow.FileSystem.FileSystem.createText"
                  "M:Axial.Flow.FileSystem.FileSystem.appendText" ]
            "File metadata",
                [ "M:Axial.Flow.FileSystem.FileSystem.getFileAttributes"
                  "M:Axial.Flow.FileSystem.FileSystem.setFileAttributes"
                  "M:Axial.Flow.FileSystem.FileSystem.getFileCreationTime"
                  "M:Axial.Flow.FileSystem.FileSystem.getFileCreationTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.setFileCreationTime"
                  "M:Axial.Flow.FileSystem.FileSystem.setFileCreationTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.getFileLastAccessTime"
                  "M:Axial.Flow.FileSystem.FileSystem.getFileLastAccessTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.setFileLastAccessTime"
                  "M:Axial.Flow.FileSystem.FileSystem.setFileLastAccessTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.getFileLastWriteTime"
                  "M:Axial.Flow.FileSystem.FileSystem.getFileLastWriteTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.setFileLastWriteTime"
                  "M:Axial.Flow.FileSystem.FileSystem.setFileLastWriteTimeUtc" ]
            "Directories",
                [ "M:Axial.Flow.FileSystem.FileSystem.directoryExists"
                  "M:Axial.Flow.FileSystem.FileSystem.createDirectory"
                  "M:Axial.Flow.FileSystem.FileSystem.deleteDirectory"
                  "M:Axial.Flow.FileSystem.FileSystem.moveDirectory"
                  "M:Axial.Flow.FileSystem.FileSystem.enumerateFiles"
                  "M:Axial.Flow.FileSystem.FileSystem.getFiles"
                  "M:Axial.Flow.FileSystem.FileSystem.enumerateDirectories"
                  "M:Axial.Flow.FileSystem.FileSystem.getDirectories"
                  "M:Axial.Flow.FileSystem.FileSystem.enumerateFileSystemEntries"
                  "M:Axial.Flow.FileSystem.FileSystem.getFileSystemEntries"
                  "M:Axial.Flow.FileSystem.FileSystem.getLogicalDrives"
                  "M:Axial.Flow.FileSystem.FileSystem.getDirectoryRoot"
                  "M:Axial.Flow.FileSystem.FileSystem.getParent"
                  "M:Axial.Flow.FileSystem.FileSystem.getCurrentDirectory"
                  "M:Axial.Flow.FileSystem.FileSystem.setCurrentDirectory" ]
            "Directory metadata",
                [ "M:Axial.Flow.FileSystem.FileSystem.getDirectoryCreationTime"
                  "M:Axial.Flow.FileSystem.FileSystem.getDirectoryCreationTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.setDirectoryCreationTime"
                  "M:Axial.Flow.FileSystem.FileSystem.setDirectoryCreationTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.getDirectoryLastAccessTime"
                  "M:Axial.Flow.FileSystem.FileSystem.getDirectoryLastAccessTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.setDirectoryLastAccessTime"
                  "M:Axial.Flow.FileSystem.FileSystem.setDirectoryLastAccessTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.getDirectoryLastWriteTime"
                  "M:Axial.Flow.FileSystem.FileSystem.getDirectoryLastWriteTimeUtc"
                  "M:Axial.Flow.FileSystem.FileSystem.setDirectoryLastWriteTime"
                  "M:Axial.Flow.FileSystem.FileSystem.setDirectoryLastWriteTimeUtc" ]
            "Paths",
                [ "M:Axial.Flow.FileSystem.FileSystem.combine"
                  "M:Axial.Flow.FileSystem.FileSystem.changeExtension"
                  "M:Axial.Flow.FileSystem.FileSystem.getDirectoryName"
                  "M:Axial.Flow.FileSystem.FileSystem.getInvalidFileNameChars"
                  "M:Axial.Flow.FileSystem.FileSystem.getInvalidPathChars"
                  "M:Axial.Flow.FileSystem.FileSystem.getExtension"
                  "M:Axial.Flow.FileSystem.FileSystem.getFileName"
                  "M:Axial.Flow.FileSystem.FileSystem.getFileNameWithoutExtension"
                  "M:Axial.Flow.FileSystem.FileSystem.getFullPath"
                  "M:Axial.Flow.FileSystem.FileSystem.getPathRoot"
                  "M:Axial.Flow.FileSystem.FileSystem.getRelativePath"
                  "M:Axial.Flow.FileSystem.FileSystem.getTempPath"
                  "M:Axial.Flow.FileSystem.FileSystem.getTempFileName"
                  "M:Axial.Flow.FileSystem.FileSystem.getRandomFileName"
                  "M:Axial.Flow.FileSystem.FileSystem.hasExtension"
                  "M:Axial.Flow.FileSystem.FileSystem.endsInDirectorySeparator"
                  "M:Axial.Flow.FileSystem.FileSystem.trimEndingDirectorySeparator"
                  "M:Axial.Flow.FileSystem.FileSystem.isPathFullyQualified"
                  "M:Axial.Flow.FileSystem.FileSystem.isPathRooted" ]
            "Implementations", ["P:Axial.Flow.FileSystem.FileSystem.live"; "P:Axial.Flow.FileSystem.FileSystem.layer"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "http"; "_index.md"]
        Title = "Services Http"
        Description = "Source-documented HTTP client service for Axial.Flow.Http."
        Intro = "This page shows the HTTP service package. `IHttp` is intentionally narrow: it models a workflow that needs to fetch a string from a URL without binding the workflow to a concrete `HttpClient` setup. For richer clients, define an app-specific service and keep Axial responsible for orchestration and failure handling."
        SymbolIds = [
            "Service", ["T:Axial.Flow.Http.IHttp"]
            "Helpers", ["M:Axial.Flow.Http.Http.getString"; "M:Axial.Flow.Http.Http.live"; "M:Axial.Flow.Http.Http.layer"]
        ]
        Alias = None
    }
    {
        OutPath = ["service"; "process"; "_index.md"]
        Title = "Services Process"
        Description = "Source-documented external process service for Axial.Flow.Process."
        Intro = "This page shows the external-process service package. `IProcess` models command execution as an asynchronous workflow service and returns a `ProcessResult` with exit code, standard output, and standard error. Keep process execution behind this service contract so tests can return deterministic results without shelling out."
        SymbolIds = [
            "Service", ["T:Axial.Flow.Process.IProcess"; "T:Axial.Flow.Process.ProcessResult"]
            "Helpers", ["M:Axial.Flow.Process.Process.execute"; "P:Axial.Flow.Process.Process.live"; "P:Axial.Flow.Process.Process.layer"]
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
        "M:Axial.Flow.Flow.env"
        "M:Axial.Flow.Flow.read"
        "M:Axial.Flow.Flow.localEnv"
        "M:Axial.Flow.Flow.provide"
    ]

let groupedFlowConstructionMembers =
    set [
        "M:Axial.Flow.Flow.ok"
        "M:Axial.Flow.Flow.error"
        "M:Axial.Flow.Flow.succeed"
        "M:Axial.Flow.Flow.value"
        "M:Axial.Flow.Flow.fail"
        "M:Axial.Flow.Flow.fromResult"
        "M:Axial.Flow.Flow.fromOption"
        "M:Axial.Flow.Flow.fromValueOption"
        "M:Axial.Flow.Flow.fromAsync"
        "M:Axial.Flow.Flow.attemptAsync"
        "M:Axial.Flow.Flow.fromTask"
        "M:Axial.Flow.Flow.attemptTask"
        "M:Axial.Flow.Flow.fromValueTask"
        "M:Axial.Flow.Flow.attemptValueTask"
        "M:Axial.Flow.Flow.orElseFlow"
        "M:Axial.Flow.Flow.delay"
    ]

let serviceCoreSectionDirectories =
    dict [
        "Base runtime", ("base-runtime", "Base runtime", "This page shows the `Core.BaseRuntime` helpers for building the standard explicit service bundle used by Axial workflow hosts.")
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
    | "base-runtime" -> "This page shows the `Core.BaseRuntime` helpers for building the standard explicit service bundle used by Axial workflow hosts."
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
                    if safeFullName e.Symbol = rawId || logicalName e.Symbol = rawId then
                        5000
                    else
                        candidateNamesForEntity e
                        |> List.map (matchScore idNorm)
                        |> List.max

                if id[0] = 'T' && entityScore > 0 then
                    yield entityScore, ResolvedEntity e

                for m in e.AllMembers do
                    let memberScore =
                        if safeFullName m.Symbol = rawId || logicalName m.Symbol = rawId then
                            5000
                        else
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
            "(?:https://adz\\.github\\.io/Axial)?/reference/Axial/([a-z0-9\\-]+)\\.html",
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
    | ["predicate"; "_index.md"] -> 72
    | ["take"; "_index.md"] -> 75
    | ["bind"; "_index.md"] -> 76
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

    // All inputs load their net8.0 build so the reference always reflects the widest TFM-gated
    // surface (e.g. Value.date, Value.dateOnly, ofJsonElement); netstandard2.1-only builds would
    // silently drop those members from the docs instead of describing them as unavailable there.
    let dllPaths = [
        Path.Combine(artifactsDir, "Axial.Flow/debug_net8.0/Axial.Flow.dll")
        Path.Combine(artifactsDir, "Axial.ErrorHandling/debug_net8.0/Axial.ErrorHandling.dll")
        Path.Combine(artifactsDir, "Axial.Schema/debug_net8.0/Axial.Schema.dll")
        Path.Combine(artifactsDir, "Axial.Codec/debug_net8.0/Axial.Codec.dll")
        Path.Combine(artifactsDir, "Axial.Flow.PlatformService/debug_net8.0/Axial.Flow.PlatformService.dll")
        Path.Combine(artifactsDir, "Axial.Flow.Console/debug_net8.0/Axial.Flow.Console.dll")
        Path.Combine(artifactsDir, "Axial.Flow.FileSystem/debug_net8.0/Axial.Flow.FileSystem.dll")
        Path.Combine(artifactsDir, "Axial.Flow.Http/debug_net8.0/Axial.Flow.Http.dll")
        Path.Combine(artifactsDir, "Axial.Flow.Process/debug_net8.0/Axial.Flow.Process.dll")
    ]

    let apiDocInputs = [
        for dll in dllPaths do
            if File.Exists dll then
                yield ApiDocInput.FromFile(dll)
    ]

    let substitutions = Substitutions.Empty
    let model = ApiDocs.GenerateModel(apiDocInputs, "Axial", substitutions, root="/", qualify=true)
    
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
            formatterApiSlug "Axial.CheckModule", Path.Combine(outRoot, "check", "_index.md")
            formatterApiSlug "Axial.BindModule", Path.Combine(outRoot, "bind", "_index.md")
            formatterApiSlug "Axial.BindErrorModule", Path.Combine(outRoot, "bind", "_index.md")
            formatterApiSlug "Axial.FlowModule", Path.Combine(outRoot, "flow", "_index.md")
            formatterApiSlug "Axial.PolicyModule", Path.Combine(outRoot, "flow", "_index.md")
            formatterApiSlug "Axial.LayerBuilder", Path.Combine(outRoot, "layer", "p-layer.md")
            formatterApiSlug "Axial.FlowBuilder", Path.Combine(outRoot, "flow", "builders-flow.md")
            formatterApiSlug "Axial.ValidateBuilder", Path.Combine(outRoot, "validation", "builders-validate.md")
            formatterApiSlug "Axial.ResultBuilder", Path.Combine(outRoot, "result", "builders-result.md")
            formatterApiSlug "Axial.RefineBuilder", Path.Combine(outRoot, "refined", "p-refined--refine.md")
            formatterApiSlug "Axial.Flow.LayerBuilder", Path.Combine(outRoot, "layer", "p-flow--layer.md")
            formatterApiSlug "Axial.Flow.FlowBuilder", Path.Combine(outRoot, "flow", "builders-flow.md")
            formatterApiSlug "Axial.Validation.ValidateBuilder", Path.Combine(outRoot, "validation", "builders-validate.md")
            formatterApiSlug "Axial.ErrorHandling.ResultBuilder", Path.Combine(outRoot, "result", "builders-result.md")
            formatterApiSlug "Axial.Refined.RefineBuilder", Path.Combine(outRoot, "refined", "p-refined--refine.md")
            formatterApiSlug "Axial.StmBuilder", Path.Combine(outRoot, "stm", "t-flow-stmbuilder.md")
            formatterApiSlug "Axial.BindError`3", Path.Combine(outRoot, "bind", "t-binderror.md")
            formatterApiSlug "Axial.Path", Path.Combine(outRoot, "diagnostics", "t-path.md")
            formatterApiSlug "Axial.LogLevel", Path.Combine(outRoot, "service", "core", "t-flow-loglevel.md")
            formatterApiSlug "Axial.RetryPolicy`1", Path.Combine(outRoot, "flow", "runtime", "t-flow-retrypolicy.md")
            formatterApiSlug "Axial.Never", Path.Combine(outRoot, "flow", "t-flow-never.md")
            formatterApiSlug "Axial.Flow.PlatformService.Clock", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "Axial.Flow.PlatformService.Log", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "Axial.Flow.PlatformService.Random", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "Axial.Flow.PlatformService.Guid", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "Axial.Flow.PlatformService.EnvironmentVariables", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "Axial.Flow.PlatformService.BaseRuntime", Path.Combine(outRoot, "service", "core", "_index.md")
            formatterApiSlug "Axial.Flow.Console.Console", Path.Combine(outRoot, "service", "console", "_index.md")
            formatterApiSlug "Axial.Flow.FileSystem.FileSystem", Path.Combine(outRoot, "service", "filesystem", "_index.md")
            formatterApiSlug "Axial.Flow.FileSystem.FileSystemError", Path.Combine(outRoot, "service", "filesystem", "_index.md")
            formatterApiSlug "Axial.Flow.Http.Http", Path.Combine(outRoot, "service", "http", "_index.md")
            formatterApiSlug "Axial.Flow.Process.Process", Path.Combine(outRoot, "service", "process", "_index.md")
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
                    let linkText = (if String.IsNullOrEmpty qualifier then m.Name else qualifier + "." + m.Name) |> dedupeAdjacentSegments
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
                    let linkText = cleanName eFullName |> dedupeAdjacentSegments
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
