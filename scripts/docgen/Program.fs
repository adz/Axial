open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Templating
open System
open System.IO
open System.Reflection
open System.Collections.Generic

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
    
    // Usage / Signature
    content <- content + "<div class=\"fsdocs-usage\">\n" + m.UsageHtml.HtmlText + "\n</div>\n\n"
    
    content <- content + m.Comment.Summary.HtmlText + "\n\n"
    
    match m.SourceLocation with
    | Some url -> content <- content + $"\n[Source]({url})\n\n"
    | None -> ()

    match m.Comment.Remarks with
    | Some r -> content <- content + "## Remarks\n\n" + r.HtmlText + "\n\n"
    | None -> ()

    if not m.Parameters.IsEmpty then
        content <- content + "## Parameters\n\n"
        for p in m.Parameters do
            content <- content + $"- `{p.ParameterNameText}`: {p.ParameterType.HtmlText}\n"
            match p.ParameterDocs with
            | Some html -> content <- content + $"  {html.HtmlText}\n"
            | None -> ()
        content <- content + "\n"

    match m.Comment.Returns with
    | Some r -> content <- content + "## Returns\n\n" + r.HtmlText + "\n\n"
    | None -> ()

    if not m.Comment.Examples.IsEmpty then
        content <- content + "## Examples\n\n"
        for e in m.Comment.Examples do
            content <- content + e.HtmlText + "\n\n"

    content

let pageSpecs = [
    {
        OutPath = ["flow"; "_index.md"]
        Title = "Flow"
        Description = "Source-documented workflow surface in FsFlow."
        Intro = "This page shows the source-documented `Flow` surface: the core type and module functions."
        SymbolIds = [
            "Core type", ["T:FsFlow.Flow`3"]
            "Module functions", ["M:FsFlow.Flow.run"; "M:FsFlow.Flow.ok"; "M:FsFlow.Flow.error"; "M:FsFlow.Flow.succeed"; "M:FsFlow.Flow.value"; "M:FsFlow.Flow.fail"; "M:FsFlow.Flow.fromResult"; "M:FsFlow.Flow.fromOption"; "M:FsFlow.Flow.fromValueOption"; "M:FsFlow.Flow.orElseFlow"; "M:FsFlow.Flow.env"; "M:FsFlow.Flow.read"; "M:FsFlow.Flow.map"; "M:FsFlow.Flow.bind"; "M:FsFlow.Flow.tap"; "M:FsFlow.Flow.tapError"; "M:FsFlow.Flow.mapError"; "M:FsFlow.Flow.catch"; "M:FsFlow.Flow.orElseWith"; "M:FsFlow.Flow.orElse"; "M:FsFlow.Flow.zip"; "M:FsFlow.Flow.map2"; "M:FsFlow.Flow.map3"; "M:FsFlow.Flow.apply"; "M:FsFlow.Flow.ignore"; "M:FsFlow.Flow.localEnv"; "M:FsFlow.Flow.provideLayer"; "M:FsFlow.Flow.delay"; "M:FsFlow.Flow.traverse"; "M:FsFlow.Flow.sequence"]
            "Concurrency", ["T:FsFlow.Fiber`2"; "M:FsFlow.Flow.fork"; "M:FsFlow.Flow.join"; "M:FsFlow.Flow.interrupt"]
            "Parallel orchestration", ["M:FsFlow.Flow.zipPar"; "M:FsFlow.Flow.race"]
        ]
        Alias = None
    }
    {
        OutPath = ["schedule"; "_index.md"]
        Title = "Schedule"
        Description = "Source-documented retry and repeat logic for FsFlow."
        Intro = "The `Schedule` module provides a DSL for describing execution policies."
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
        Intro = "The `Ref` module provides thread-safe mutable state handles."
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
        Intro = "The `STM` module provides composable atomic transactions with `retry` / `orElse` coordination."
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
        Intro = "The `FlowStream` module provides asynchronous, pull-based streams."
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
        Intro = "The `flow { }` builder is the primary entry point for orchestrating synchronous, async, and task-based work."
        SymbolIds = [
            "Builder", ["P:FsFlow.Builders.flow"]
        ]
        Alias = None
    }
    {
        OutPath = ["check"; "_index.md"]
        Title = "Check"
        Description = "Source-documented pure predicate helpers for FsFlow."
        Intro = "This page shows the source-documented `Check` surface: the unit-failure result type and reusable predicate helpers."
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
        Intro = "This page shows the source-documented `Validation` surface: the accumulating result type, module functions, and path-scoping helpers."
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
        Intro = "The `validate { }` builder is used for accumulating sibling failures into a structured diagnostics graph."
        SymbolIds = [
            "Builder", ["P:FsFlow.Builders.validate"]
        ]
        Alias = None
    }
    {
        OutPath = ["result"; "_index.md"]
        Title = "Result Builder"
        Description = "Documentation for the result { } computation expression."
        Intro = "The `result { }` builder provides a fail-fast computation expression for standard F# Result values."
        SymbolIds = [
            "Builder", ["P:FsFlow.Builders.result"]
        ]
        Alias = Some "builders-result.md"
    }
    {
        OutPath = ["diagnostics"; "_index.md"]
        Title = "Diagnostics"
        Description = "Source-documented validation diagnostics graph for FsFlow."
        Intro = "The `Diagnostics` type represents a structured graph of validation failures."
        SymbolIds = [
            "Graph types", ["T:FsFlow.PathSegment"; "T:Path"; "T:FsFlow.Diagnostic`1"; "T:FsFlow.Diagnostics`1"]
            "Module functions", ["M:FsFlow.Diagnostics.empty"; "M:FsFlow.Diagnostics.singleton"; "M:FsFlow.Diagnostics.merge"; "M:FsFlow.Diagnostics.toString"; "M:FsFlow.Diagnostics.flatten"]
        ]
        Alias = None
    }
    {
        OutPath = ["capability"; "_index.md"]
        Title = "Capability"
        Description = "Source-documented dependency resolution and layers for FsFlow."
        Intro = "In FsFlow, a capability is a named interface that describes what a flow needs from env. A capability contract puts that interface in the environment surface type. Using an interface through a capability contract makes the dependency visible in the type, so the compiler can check it, refactoring can move safely, and reusable helpers can advertise what they need. This page shows the source-documented capability and layer surface, with `Requires` and `Resolve` as the binding surface."
        SymbolIds = [
            "Binding tokens", ["T:FsFlow.Requires`1"; "T:FsFlow.Resolve`1"; "T:FsFlow.Resolve`2"]
            "Edge helpers", ["T:FsFlow.MissingCapability"; "M:FsFlow.Resolver.resolve"; "M:FsFlow.Resolver.runtime"; "M:FsFlow.Resolver.environment"; "M:FsFlow.Resolver.fromProvider"]
            "Layers", ["M:FsFlow.Layer.provideLayer"]
        ]
        Alias = None
    }
    {
        OutPath = ["runtime"; "_index.md"]
        Title = "RuntimeContext"
        Description = "Source-documented runtime/application split for FsFlow."
        Intro = "The `RuntimeContext` type and module split host services from application dependencies and carry the cancellation token for task-based execution. In the current foundation it is the execution carrier above the adapter layer, not the runtime storage engine."
        SymbolIds = [
            "Core type", ["T:FsFlow.RuntimeContext`2"]
            "Module functions", ["M:FsFlow.RuntimeContext.create"; "M:FsFlow.RuntimeContext.runtime"; "M:FsFlow.RuntimeContext.environment"; "M:FsFlow.RuntimeContext.cancellationToken"; "M:FsFlow.RuntimeContext.mapRuntime"; "M:FsFlow.RuntimeContext.mapEnvironment"; "M:FsFlow.RuntimeContext.withRuntime"; "M:FsFlow.RuntimeContext.withEnvironment"]
        ]
        Alias = None
    }
    {
        OutPath = ["caps-core"; "_index.md"]
        Title = "Capabilities Core"
        Description = "Source-documented synchronous capability primitives for FsFlow.Capabilities.Core."
        Intro = "`FsFlow.Capabilities.Core` is the smallest shared capability package in the FsFlow capabilities story. It keeps the surface synchronous and explicit: clock, random, GUID, and environment-variable capabilities."
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
        Intro = "This page shows the source-documented `FsFlow.Capabilities.Console` surface: the console interface and its helpers."
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
        Intro = "This page shows the source-documented `FsFlow.Capabilities.FileSystem` surface: the file system interface and its helpers."
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
        Intro = "This page shows the source-documented `FsFlow.Capabilities.Http` surface: the HTTP interface and its helpers."
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
        Intro = "This page shows the source-documented `FsFlow.Capabilities.Process` surface: the process runner interface and its helpers."
        SymbolIds = [
            "Capability", ["T:FsFlow.Capabilities.Process.IProcess"; "T:FsFlow.Capabilities.Process.ProcessResult"]
            "Helpers", ["M:FsFlow.Capabilities.Process.Process.execute"; "M:FsFlow.Capabilities.Process.Process.live"]
        ]
        Alias = None
    }
    {
        OutPath = ["hosting"; "_index.md"]
        Title = "Hosting"
        Description = "Source-documented .NET host integration for FsFlow.Hosting."
        Intro = "This page shows the source-documented `FsFlow.Hosting` surface: the host adapters, runtime composition, and startup validation helpers."
        SymbolIds = [
            "Startup", ["M:FsFlow.Hosting.Startup.validateEnvironment"]
        ]
        Alias = None
    }
    {
        OutPath = ["telemetry"; "_index.md"]
        Title = "Telemetry"
        Description = "Source-documented observability integration for FsFlow.Runtime.Telemetry."
        Intro = "This page shows the source-documented `FsFlow.Runtime.Telemetry` surface: Activity.trace integration."
        SymbolIds = [
            "Tracing", ["M:FsFlow.Runtime.Telemetry.Activity.trace"]
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
        Path.Combine(artifactsDir, "FsFlow.Hosting/debug_netstandard2.1/FsFlow.Hosting.dll")
        Path.Combine(artifactsDir, "FsFlow.Runtime.Telemetry/debug_netstandard2.1/FsFlow.Runtime.Telemetry.dll")
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
                    let cleanShort = cleanName e.Name
                    indexContent <- indexContent + $"- [`{linkText}`](./{pageName}): {e.Comment.Summary.HtmlText}\n"
                    let remarksText = match e.Comment.Remarks with Some r -> "## Remarks\n\n" + r.HtmlText | None -> ""
                    let memberPageContent = 
                        $"---\ntitle: \"{cleanName eFullName}\"\nlinkTitle: \"{cleanShort}\"\n---\n\n" + 
                        e.Comment.Summary.HtmlText + "\n\n" +
                        remarksText
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), pageName), memberPageContent)
                | _ -> 
                    printfn "Warning: symbol not found: %s" id
            indexContent <- indexContent + "\n"
            
        File.WriteAllText(outPath, indexContent)

    0
