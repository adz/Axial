---
title: Write an effect-boundary analyzer
linkTitle: Write an effect-boundary analyzer
description: Build and package an analyzer that detects direct access to effects hidden behind your service interface.
---

# Write an effect-boundary analyzer

[Effect-boundary guardrails](/notes/guardrails.html) describes how `Axial.Guardrails` detects direct calls to ambient .NET effects. You can apply the same pattern to an effect owned by your package.

For example, callers can bypass an HTTP, queue, or database service interface by using the underlying .NET API. A package-specific analyzer can detect that access during the build.

This guide shows how to create and package such an analyzer. Use this approach only after your package defines an explicit service boundary that you want consumers to follow.

## Copy the reusable components

FSharp.Analyzers.SDK discovers each analyzer assembly independently. `Axial.Guardrails` does not provide a plug-in API, and your analyzer does not need to reference it.

Use these files from `src/Axial.Guardrails` as a starting point:

- `EffectCatalog.fs` defines symbol-match rules and diagnostic details.
- `Suppressions.fs` parses line-level and file-level suppression directives.
- `EffectBoundaryAnalyzer.fs` resolves symbols and reports unsuppressed matches.

Together, these files contain about 150 lines. They use `FSharpMemberOrFunctionOrValue` from `FSharp.Compiler.Symbols` and do not depend on Axial types.

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

A matching call now produces a diagnostic that identifies both the direct call and the service that should replace it.

## Package the analyzer

Package the analyzer so that `dotnet add package` enables it without requiring consumers to edit MSBuild files.

Use `src/Axial.Guardrails/Axial.Guardrails.fsproj` and `src/Axial.Guardrails/build/` as references for the following steps.

### Include analyzer dependencies

Set `CopyLocalLockFileAssemblies` to `true`. This setting places the analyzer's transitive dependencies beside its assembly in the build output.

Pack the complete output under `analyzers/` in the NuGet package. Set `IncludeBuildOutput` to `false` so consumer projects do not compile against the analyzer assembly.

### Include the analyzer CLI

The `fsharp-analyzers` CLI is a .NET tool. NuGet does not allow a normal `PackageReference` to a tool package and reports `NU1212` if you add one.

Use `PackageDownload` during your build instead. At pack time, copy the downloaded CLI files into the NuGet package under `cli/`.

### Add automatic build targets

Add `build/{YourPackageId}.props` and `build/{YourPackageId}.targets` to the package. NuGet imports these files automatically into projects that reference the package.

Use the props file to define the default severity and enabled state. Use the targets file to run the bundled CLI after `Build`, passing the bundled `analyzers/` and `cli/` directories.

Run the CLI with an MSBuild `<Exec>` task. Do not use the `AfterBuild` target from `FSharp.Analyzers.Build`, because that target sets `IgnoreExitCode="true"` and cannot fail the build.

The CLI exits with a nonzero code only when a diagnostic uses `--treat-as-error`. A normal `<Exec>` task therefore preserves warning and error behavior.

## Keep the analyzer independent

Start by copying the three source files and packaging configuration into your repository. Do not add an `Axial.Guardrails` or other `Axial.*` dependency.

The implementation is a template, not a plug-in API. Keeping the analyzer independent also prevents consumers from acquiring an unrelated Axial dependency.

Consider extracting shared analyzer infrastructure only after several analyzers need the same code and the maintenance cost becomes significant.
