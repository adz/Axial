/// BCL-only XML contract across the isolated load context. Never changes process environment variables.
module Axial.Guardrails.FastRunner

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Xml.Linq
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Text

let private checker = lazy (FSharpChecker.Create(projectCacheSize = 1, keepAssemblyContents = false))
let private xn name = XName.Get name
let private attribute name value = XAttribute(xn name, value)
let private digest (text: string) = SHA256.HashData(Encoding.UTF8.GetBytes text) |> Convert.ToHexString
let private atomic (path: string) (doc: XDocument) =
    Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
    let tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"
    try
        doc.Save tmp
        File.Move(tmp, path, true)
    finally
        if File.Exists tmp then File.Delete tmp

let private parse (path: string) (source: ISourceText) (options: FSharpParsingOptions) =
    let parsed = checker.Value.ParseFile(path, source, options) |> Async.RunSynchronously
    let errors = parsed.Diagnostics |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)
    if errors.Length <> 0 then invalidOp ("Parse failed: " + (errors |> Array.map _.Message |> String.concat "; "))
    parsed.ParseTree

let private parsingOptions (input: XElement) paths =
    let arguments = input.Elements(xn "argument") |> Seq.map _.Value |> Seq.toList
    if arguments.IsEmpty then
        let defines = input.Elements(xn "define") |> Seq.map _.Value |> Seq.toList
        { FSharpParsingOptions.Default with SourceFiles = paths; ConditionalDefines = defines; LangVersionText = input.Attribute(xn "langVersion").Value; IsInteractive = false }
    else
        let options, diagnostics = checker.Value.GetParsingOptionsFromCommandLineArgs arguments
        let errors = diagnostics |> List.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)
        if not errors.IsEmpty then invalidOp ("Compiler parsing options failed: " + (errors |> List.map _.Message |> String.concat "; "))
        { options with SourceFiles = paths }

// XML 1.0 cannot carry every character an unusable path or exception message may contain.
let private xmlSafe (value: string) = String(value |> Seq.map (fun ch -> if System.Xml.XmlConvert.IsXmlChar ch then ch else '\uFFFD') |> Seq.toArray)

/// The seams exercise the production failure boundary without process-wide fault-injection settings.
let runWith
    (parseFile: string -> ISourceText -> FSharpParsingOptions -> FSharp.Compiler.Syntax.ParsedInput)
    (visit: string option -> GuardrailConfig.Config -> string -> ISourceText -> FSharp.Compiler.Syntax.ParsedInput -> (string * FSharp.Analyzers.SDK.Message) list)
    (request: string) : string =
    let input = XElement.Parse request
    let get name = input.Attribute(xn name).Value
    let timing = input.Attribute(xn "timings") |> Option.ofObj |> Option.exists (fun a -> a.Value = "true")
    let result = XElement(xn "fast")
    let phases = System.Collections.Generic.Dictionary<string, float>()
    let record name milliseconds =
        if timing then lock phases (fun () -> phases[name] <- (match phases.TryGetValue name with | true, v -> v | _ -> 0.0) + milliseconds)
    let timed name (work: unit -> 'T) =
        if not timing then work () else
        let started = System.Diagnostics.Stopwatch.GetTimestamp()
        try work () finally record name (System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds)
    // Policy parsing does not depend on the parser, so overlap it with cache checks and the first (JIT-bound) parse.
    let config = System.Threading.Tasks.Task.Run(fun () -> timed "config" (fun () -> GuardrailConfig.parse (File.ReadAllLines(get "config"))))
    let paths = input.Elements(xn "file") |> Seq.map _.Value |> Seq.toArray
    let parsing = lazy (timed "options" (fun () -> parsingOptions input paths))
    let cacheDirectory = get "cache"
    let current = System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
    for path in paths do
        try
            let text = File.ReadAllText path
            let key = digest (get "context" + "\n" + path + "\n" + text)
            current.Add key |> ignore
            let cachePath = Path.Combine(cacheDirectory, key + ".xml")
            let cached =
                try
                    let envelope = XDocument.Load cachePath
                    let body = envelope.Root.Element(xn "file")
                    if envelope.Root.Attribute(xn "key").Value = key && envelope.Root.Attribute(xn "digest").Value = digest (body.ToString()) then Some body else None
                with _ -> None
            let body, hit =
                match cached with
                | Some body -> XElement(body), true
                // A source the build generated is not parsed: its coverage is complete and has no findings.
                | None when GuardrailConfig.isGeneratedSource (config.GetAwaiter().GetResult()) path ->
                    let body = XElement(xn "file")
                    for rule in FastSyntax.rules do
                        body.Add(XElement(xn "coverage", attribute "file" path, attribute "rule" rule, attribute "status" GuardrailConfig.ExcludedGeneratedStatus))
                    body, false
                | None ->
                    let source = SourceText.ofString text
                    let options = parsing.Value
                    let started = System.Diagnostics.Stopwatch.GetTimestamp()
                    let tree = parseFile path source options
                    let parseMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds
                    record "parse" parseMilliseconds
                    let config = config.GetAwaiter().GetResult()
                    let body = XElement(xn "file")
                    let mutable complete = true
                    for rule in FastSyntax.rules do
                        let entry = XElement(xn "coverage", attribute "file" path, attribute "rule" rule, attribute "status" "ran")
                        if timing then entry.SetAttributeValue(xn "parseMilliseconds", parseMilliseconds)
                        let started = System.Diagnostics.Stopwatch.GetTimestamp()
                        try
                            for _, finding in visit (Some rule) config path source tree do
                                entry.Add(XElement(xn "finding", attribute "code" finding.Code, attribute "severity" (string finding.Severity), attribute "line" finding.Range.StartLine, attribute "column" finding.Range.StartColumn, finding.Message))
                            if rule = "TaskSurface" then entry.Add(FastSyntax.taskSites config path source tree)
                        with error ->
                            complete <- false
                            entry.SetAttributeValue(xn "status", "skipped")
                            entry.SetAttributeValue(xn "reason", error.Message)
                            result.Add(XElement(xn "failure", attribute "file" path, rule + ": " + error.Message))
                        let milliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds
                        record "visit" milliseconds
                        if timing then entry.SetAttributeValue(xn "milliseconds", milliseconds)
                        body.Add entry
                    // Cached with the findings so that every build, including replays, warns about the skipped source.
                    match GuardrailConfig.skipReason config path with
                    | Some reason -> body.Add(XElement(xn "skip", attribute "file" path, attribute "reason" (xmlSafe reason)))
                    | None -> ()
                    if complete then
                        let envelope = XDocument(XElement(xn "cached", attribute "key" key, attribute "digest" (digest (body.ToString())), XElement(body)))
                        atomic cachePath envelope
                    body, false
            for entry in body.Elements(xn "coverage") do
                entry.SetAttributeValue(xn "cacheHit", if hit then "true" else "false")
                if not timing then
                    entry.SetAttributeValue(xn "milliseconds", null)
                    entry.SetAttributeValue(xn "parseMilliseconds", null)
                elif hit then
                    entry.SetAttributeValue(xn "milliseconds", 0)
                    entry.SetAttributeValue(xn "parseMilliseconds", 0)
                result.Add(XElement entry)
            for skip in body.Elements(xn "skip") do result.Add(XElement skip)
        with error ->
            result.Add(XElement(xn "failure", attribute "file" path, error.Message))
    // Policy errors are infrastructure failures even when every file was a cache hit.
    config.GetAwaiter().GetResult() |> ignore
    // The per-file cache holds only the current request's entries: edited, removed and renamed files leave nothing.
    try
        if Directory.Exists cacheDirectory then
            for file in Directory.GetFiles(cacheDirectory, "*.xml") do
                if not (current.Contains(Path.GetFileNameWithoutExtension file)) then
                    try File.Delete file with :? IOException | :? UnauthorizedAccessException -> ()
    with :? IOException | :? UnauthorizedAccessException -> ()
    for KeyValue(name, milliseconds) in phases do result.SetAttributeValue(xn (name + "Milliseconds"), milliseconds)
    result.ToString()

let private requireIsolation () =
    let contextOf (assembly: System.Reflection.Assembly) = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext assembly
    let own = contextOf typeof<GuardrailConfig.Config>.Assembly
    let dependencies = [ typeof<FSharpChecker>.Assembly; typeof<int list>.Assembly; typeof<FSharp.Analyzers.SDK.Message>.Assembly ]
    if isNull own || not own.IsCollectible || (dependencies |> List.exists (fun assembly -> not (obj.ReferenceEquals(own, contextOf assembly)))) then
        invalidOp "Fast isolation failed: FCS, FSharp.Core and the analyzer SDK must share the analyzer's collectible load context, not MSBuild's default context."

/// Latency only, started before CoreCompile: JIT the parser, policy and visitors on the newest source file while the
/// compiler runs. Writes nothing; the synchronous Run repeats all work and reports every failure.
let Warm (request: string) : unit =
    requireIsolation ()
    let input = XElement.Parse request
    let paths = input.Elements(xn "file") |> Seq.map _.Value |> Seq.toArray
    let existing = paths |> Array.filter File.Exists
    if existing.Length <> 0 then
        // The newest source is the likely edit; identical options let the Run reuse FCS's parse cache.
        let newest = existing |> Array.maxBy File.GetLastWriteTimeUtc
        let source = SourceText.ofString (File.ReadAllText newest)
        let tree = parse newest source (parsingOptions input paths)
        let configPath = input.Attribute(xn "config").Value
        if File.Exists configPath then
            FastSyntax.analyze (GuardrailConfig.parse (File.ReadAllLines configPath)) newest source tree |> ignore

/// BCL-only entry point for the MSBuild reflection bridge.
let Run (request: string) : string =
    requireIsolation ()
    let result = runWith parse FastSyntax.analyzeRule request |> XElement.Parse
    result.SetAttributeValue(xn "isolated", "true")
    result.ToString()
