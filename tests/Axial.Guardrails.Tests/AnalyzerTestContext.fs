namespace Axial.Guardrails.Tests

#nowarn "57" // DocumentSource is experimental FCS API; it lets fixtures live in memory.

open System
open System.Collections.Concurrent
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text

/// Typed analyzer contexts for unit tests. One checker and one set of project options serve the whole run: creating
/// an FSharpChecker (and its framework imports) per test made the suite's test host peak near 5 GB.
module AnalyzerTestContext =
    // Sources are supplied in memory, so fixtures need not exist on disk.
    let private sources = ConcurrentDictionary<string, string>(StringComparer.Ordinal)

    let private checker =
        lazy (
            let read (path: string) =
                async {
                    match sources.TryGetValue path with
                    | true, text -> return Some(SourceText.ofString text)
                    | _ -> return None
                }
            FSharpChecker.Create(keepAssemblyContents = true, projectCacheSize = 1, documentSource = DocumentSource.Custom read))

    // The SDK fixture's references can resolve to obsolete framework assemblies in CI.
    // Typecheck against the assemblies loaded by this net10 test process instead.
    let private baseOptions =
        lazy (
            task {
                let! options = mkOptionsFromProject "net10.0" []
                let runtimeAssemblies =
                    (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") :?> string).Split(Path.PathSeparator)
                    |> Array.map (fun path -> "-r:" + path)
                let compilerOptions =
                    options.OtherOptions
                    |> Array.filter (fun option -> not (option.StartsWith("-r:") || option.StartsWith("--reference:")))
                return Array.concat [ compilerOptions; [| "--noframework"; "--targetprofile:netcore" |]; runtimeAssemblies ], options
            })

    let private axialReferences =
        [| "-r:" + typeof<Axial.Flow<unit, unit, int>>.Assembly.Location
           "-r:" + typeof<Axial.ElmishFlows.ElmishRuntimeOptions>.Assembly.Location |]

    // A stable project path keeps at most one cached incremental builder per reference set.
    let private projectFile withAxial =
        Path.Combine(Path.GetTempPath(), "Axial.Guardrails.Tests", (if withAxial then "AxialFixture.fsproj" else "Fixture.fsproj"))

    // Each call is serialized so that clearing the builder cache never races another test's check.
    let private gate = new Threading.SemaphoreSlim(1, 1)

    let private getContext withAxial (fileName: string) (source: string) =
        task {
            let! otherOptions, template = baseOptions.Value
            let options: FSharpProjectOptions =
                { template with
                    ProjectFileName = projectFile withAxial
                    SourceFiles = [| fileName |]
                    OtherOptions = Array.append otherOptions (if withAxial then axialReferences else [||])
                    ReferencedProjects = [||]
                    Stamp = None }
            do! gate.WaitAsync()
            try
                sources[fileName] <- source
                try
                    let sourceText = SourceText.ofString source
                    let fcs = checker.Value
                    let! projectResults = fcs.ParseAndCheckProject options
                    let! parseResults, answer = fcs.ParseAndCheckFileInProject(fileName, 0, sourceText, options)
                    match answer with
                    | FSharpCheckFileAnswer.Succeeded checkResults ->
                        return Utils.createContext projectResults fileName sourceText (parseResults, checkResults) (BackgroundCompilerOptions options)
                    | FSharpCheckFileAnswer.Aborted -> return failwith $"Type checking aborted for {fileName}"
                finally
                    sources.TryRemove fileName |> ignore
                    // The context keeps its own results; drop the builder so the next fixture starts from fresh sources.
                    checker.Value.ClearCache [ options ]
            finally
                gate.Release() |> ignore
        }

    let createAt (fileName: string) (source: string) = getContext false fileName source

    let createWithAxialAt (fileName: string) (source: string) = getContext true fileName source

    let createWithAxial (withAxial: bool) (source: string) =
        let directory = Path.Combine(Path.GetTempPath(), "Axial.Guardrails.Tests", Guid.NewGuid().ToString("N"))
        getContext withAxial (Path.Combine(directory, "Fixture.fs")) source

    let create source = createWithAxial false source
