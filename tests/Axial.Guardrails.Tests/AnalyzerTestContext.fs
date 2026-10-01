namespace Axial.Guardrails.Tests

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing

module AnalyzerTestContext =
    let create (options: FSharp.Compiler.CodeAnalysis.FSharpProjectOptions) (source: string) =
        // The SDK fixture uses relative names; FCS can fail to resolve them under newer .NET SDKs.
        let directory = Path.Combine(Path.GetTempPath(), "Axial.Guardrails.Tests", Guid.NewGuid().ToString("N"))
        let file = { FileName = Path.Combine(directory, "Fixture.fs"); Source = source }
        let options = { options with ProjectFileName = Path.Combine(directory, "Fixture.fsproj") }
        getContextFor (BackgroundCompilerOptions options) [ file ] file
