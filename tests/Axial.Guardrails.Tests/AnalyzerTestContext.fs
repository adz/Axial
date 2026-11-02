namespace Axial.Guardrails.Tests

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing

module AnalyzerTestContext =
    let private getContext withAxial (options: FSharp.Compiler.CodeAnalysis.FSharpProjectOptions) (fileName: string) (source: string) =
        task {
            let file = { FileName = fileName; Source = source }
            // The SDK fixture's references can resolve to obsolete framework assemblies in CI.
            // Typecheck against the assemblies loaded by this net10 test process instead.
            let runtimeAssemblies =
                (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") :?> string).Split(Path.PathSeparator)
                |> Array.map (fun path -> "-r:" + path)
            let compilerOptions =
                options.OtherOptions
                |> Array.filter (fun option -> not (option.StartsWith("-r:") || option.StartsWith("--reference:")))
            let directory = Path.GetDirectoryName fileName
            let options =
                { options with
                    ProjectFileName = Path.Combine(directory, "Fixture.fsproj")
                    OtherOptions =
                        Array.concat [ compilerOptions
                                       [| "--noframework"; "--targetprofile:netcore" |]
                                       runtimeAssemblies
                                       (if withAxial then
                                            [| "-r:" + typeof<Axial.Flow<unit, unit, int>>.Assembly.Location
                                               "-r:" + typeof<Axial.ElmishFlows.ElmishRuntimeOptions>.Assembly.Location |]
                                        else [||]) ] }
            return! getContextFor (BackgroundCompilerOptions options) [ file ] file
        }

    let createAt (fileName: string) (source: string) =
        task {
            let! options = mkOptionsFromProject "net10.0" []
            return! getContext false options fileName source
        }

    let createWithAxialAt (fileName: string) (source: string) =
        task {
            let! options = mkOptionsFromProject "net10.0" []
            return! getContext true options fileName source
        }

    let createWithAxial (withAxial: bool) (source: string) =
        task {
            let! options = mkOptionsFromProject "net10.0" []
            let directory = Path.Combine(Path.GetTempPath(), "Axial.Guardrails.Tests", Guid.NewGuid().ToString("N"))
            return! getContext withAxial options (Path.Combine(directory, "Fixture.fs")) source
        }

    let create source = createWithAxial false source
