---
title: Write an effect-boundary analyzer
linkTitle: Write an effect-boundary analyzer
description: Build and package an analyzer that detects direct access to effects hidden behind your service interface.
---

# Write an effect-boundary analyzer

[Effect-boundary guardrails](/notes/guardrails.html) describes how `Axial.Guardrails` detects direct calls to ambient .NET effects. You can apply the same pattern to an effect owned by your package.

For example, callers can bypass an HTTP, queue, or database service interface by using the underlying .NET API. A package-specific analyzer can detect that access during the build.

Create an analyzer after your package defines an explicit service boundary for consumers.

## Copy the reusable components

FSharp.Analyzers.SDK discovers each analyzer assembly independently. `Axial.Guardrails` does not provide a plug-in API, and your analyzer does not need to reference it.

Copy these files from the [`src/Axial.Guardrails`](https://github.com/adz/Axial/tree/main/src/Axial.Guardrails) directory in the Axial source tree. The published docs and NuGet package do not ship these files, so fetch them from the repository:

- [`EffectCatalog.fs`](https://github.com/adz/Axial/blob/main/src/Axial.Guardrails/EffectCatalog.fs) defines symbol-match rules and diagnostic details.
- [`Suppressions.fs`](https://github.com/adz/Axial/blob/main/src/Axial.Guardrails/Suppressions.fs) parses line-level and file-level suppression directives.
- [`EffectBoundaryAnalyzer.fs`](https://github.com/adz/Axial/blob/main/src/Axial.Guardrails/EffectBoundaryAnalyzer.fs) resolves symbols and reports unsuppressed matches.

Together, these files contain about 150 lines. They use `FSharpMemberOrFunctionOrValue` from `FSharp.Compiler.Symbols` and do not depend on Axial types. They form a complete, compilable template together with [`tests/Axial.Guardrails.Tests`](https://github.com/adz/Axial/tree/main/tests/Axial.Guardrails.Tests), the analyzer project and its consumer-verification loop. Fetch both from a release tag such as [`v0.11.0`](https://github.com/adz/Axial/tree/v0.11.0) when you need a stable snapshot instead of `main`.

## Define a symbol rule

Suppose your package exposes `IHttp` and must detect direct construction of `System.Net.Http.HttpClient`.

Define a rule for the constructor:

```fsharp no-check reason="Illustrative package, Contoso.Http, does not exist in this repository"
module Contoso.Http.Guardrails.EffectCatalog

type SymbolMatch =
    | ConstructorOf of entityFullName: string

type EffectRule =
    { Category: string
      Match: SymbolMatch
      Message: string
      Replacement: string }

let rules: EffectRule list =
    [ { Category = "http"
        Match = ConstructorOf "System.Net.Http.HttpClient"
        Message = "constructs System.Net.Http.HttpClient directly, bypassing the explicit HTTP service"
        Replacement = "Contoso.Http's IHttp service" } ]

let knownCategories: Set<string> =
    rules |> List.map _.Category |> Set.ofList
```

Copy `Suppressions.fs`, then replace the Axial directive prefix with a package-specific prefix such as `contoso-http-allow`. A distinct prefix prevents confusion with suppressions from other analyzers.

Copy `EffectBoundaryAnalyzer.fs`, then change the analyzer name, diagnostic code, and help URL:

```fsharp no-check reason="Illustrative analyzer wiring; helper functions come from files omitted from this example"
[<CliAnalyzer("ContosoHttpBoundary",
              "Flags direct construction of System.Net.Http.HttpClient that bypasses IHttp.",
              "https://github.com/contoso/http/blob/main/docs/guardrails.md")>]
let httpBoundaryAnalyzer: Analyzer<CliContext> =
    fun ctx -> async {
        let fileCategories = fileLevelAllowedCategories ctx.SourceText

        return
            ctx.GetAllSymbolUsesOfFile()
            |> Seq.choose (fun symbolUse ->
                ruleFor symbolUse.Symbol
                |> Option.map (fun rule -> rule, symbolUse.Range))
            |> Seq.filter (fun (rule, range) ->
                not (isAllowed ctx.SourceText fileCategories range.StartLine rule.Category))
            |> Seq.map (fun (rule, range) -> toMessage rule range)
            |> Seq.toList
    }
```

A matching call produces a diagnostic identifying the direct call and the replacement service.

## Verify a consumer build

After packaging, confirm three behaviors in one consumer project: the analyzer flags the direct call, a suppression
silences it, and error severity fails the build. Create a consumer that references the analyzer package and builds
the direct call:

```fsharp no-check reason="Illustrative consumer source for a package analyzer that does not exist in this repository"
module Consumer
let makeClient () = new System.Net.Http.HttpClient()
```

Build with severity `error`. The build exits non-zero and reports the `ContosoHttpBoundary` diagnostic naming the
`System.Net.Http.HttpClient` constructor and the `IHttp` replacement. Then mark the boundary and rebuild; the build
now succeeds:

```fsharp no-check reason="Illustrative consumer source for a package analyzer that does not exist in this repository"
module Consumer
let makeClient () = new System.Net.Http.HttpClient() // contoso-http-allow: http
```

Run the same pair for every rule the analyzer ships, once at `warning` (build succeeds, finding reported) and once
at `error` (build fails). `tests/Axial.Guardrails.Tests/PackagePropsTests.fs` is the reference implementation of this
loop: it writes a consumer into a temp directory, runs `dotnet build`, and asserts on the exit code and the reported
`AXG001`/`AXG002` findings.

## Package the analyzer

Packaging — the NuGet layout, `PackageDownload` for the CLI, and the MSBuild props/targets that run it — is
documented in the [analyzer packaging reference](analyzer-packaging.html). In short: pack the analyzer's
`analyzers/` and `cli/` directories into the package, add `build/{YourPackageId}.props` and
`build/{YourPackageId}.targets`, and run the CLI with an `<Exec>` task that can fail the build.

## Keep the analyzer independent

Start by copying the three source files and packaging configuration into your repository. Do not add an `Axial.Guardrails` or other `Axial.*` dependency.

The implementation is a template, not a plug-in API. Keeping the analyzer independent also prevents consumers from acquiring an unrelated Axial dependency.

Extract shared analyzer infrastructure if several analyzers need the same code and maintaining separate copies becomes costly.
