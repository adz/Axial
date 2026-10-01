namespace Axial.Guardrails.Tests

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing

module AnalyzerTestContext =
    let create (source: string) =
        task {
            let! options = mkOptionsFromProject "net10.0" []
            let directory = Path.Combine(Path.GetTempPath(), "Axial.Guardrails.Tests", Guid.NewGuid().ToString("N"))
            let file = { FileName = Path.Combine(directory, "Fixture.fs"); Source = source }
            // The SDK fixture's references can resolve to obsolete framework assemblies in CI.
            // Typecheck against the assemblies loaded by this net10 test process instead.
            let runtimeAssemblies =
                (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") :?> string).Split(Path.PathSeparator)
                |> Array.map (fun path -> "-r:" + path)
            let compilerOptions =
                options.OtherOptions
                |> Array.filter (fun option -> not (option.StartsWith("-r:") || option.StartsWith("--reference:")))
            let options =
                { options with
                    ProjectFileName = Path.Combine(directory, "Fixture.fsproj")
                    OtherOptions = Array.concat [ compilerOptions; [| "--noframework"; "--targetprofile:netcore" |]; runtimeAssemblies ] }
            return! getContextFor (BackgroundCompilerOptions options) [ file ] file
        }
