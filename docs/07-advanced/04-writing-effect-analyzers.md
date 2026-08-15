---
title: Writing your own effect-boundary analyzer
linkTitle: Writing your own effect-boundary analyzer
description: Package your own ambient-effect check the same way Axial.Guardrails does, wired into a consumer's build the moment they install your package.
---

# Writing your own effect-boundary analyzer

[Effect-boundary guardrails](/notes/guardrails.html) covers `Axial.Guardrails`, which flags direct calls to
ambient .NET effects such as `System.Random` and `DateTime.UtcNow`. The same rule applies to any effect your own
package hides behind a service interface. If you publish an HTTP client, a message queue, or a database driver
with an explicit, mockable contract, a caller can still bypass that contract by reaching for the underlying .NET
type directly. This page builds a small analyzer that catches that, and packages it so installing your package is
the only setup step a caller needs.

This is an advanced topic. Most packages never need it — the value only shows up once your package has an
explicit-service contract you want callers to actually use.

## What's reusable

`FSharp.Analyzers.SDK` analyzers are independent, individually discovered assemblies. `Axial.Guardrails` does not
expose a plugin API, and you don't reference it. Instead, you build a second, unrelated analyzer with the same
shape, and ship it the same way. Three pieces of `Axial.Guardrails`' source are worth copying as a starting point:

- `EffectCatalog.fs` — the `SymbolMatch` type (`ConstructorOf`, `MembersOf`, `AnyMemberOf`) and the `EffectRule`
  record (`Category`, `Match`, `Message`, `Replacement`) that describes one banned call.
- `Suppressions.fs` — regex-based parsing for a `// your-prefix-allow: category` line comment and a
  `// your-prefix-allow-file: category` file-header comment.
- `EffectBoundaryAnalyzer.fs` — resolves each symbol use in the file against your rule table and reports a
  `Message` for every match that isn't suppressed.

Together, these are roughly 150 lines. None of it is Axial-specific: the matching is against
`FSharpMemberOrFunctionOrValue`, a type from `FSharp.Compiler.Symbols`, not from Axial.

## Worked example: flagging raw HttpClient

Say your package publishes `IHttp`, the way `Axial.HttpClient` does, and you want to catch a caller constructing
`System.Net.Http.HttpClient` directly instead of going through it.

Define your rule table:

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

let knownCategories: Set<string> = rules |> List.map (fun r -> r.Category) |> Set.ofList
```

Reuse `Axial.Guardrails`' `Suppressions.fs` as-is, renaming the directive prefix from `axial-allow-effect` to
whatever fits your package (`contoso-http-allow`, for example — pick something a call site can't confuse with
`Axial.Guardrails`' own suppressions). Reuse `EffectBoundaryAnalyzer.fs` as-is, changing only the `[<CliAnalyzer>]`
name, code (`CHG001` rather than `AXG001`), and help URL:

```fsharp no-check reason="Illustrative analyzer wiring; ruleFor/isAllowed/toMessage come from the copied Suppressions.fs and EffectBoundaryAnalyzer.fs, not shown in full here"
[<CliAnalyzer("ContosoHttpBoundary",
              "Flags direct construction of System.Net.Http.HttpClient that bypasses IHttp.",
              "https://github.com/contoso/http/blob/main/docs/guardrails.md")>]
let httpBoundaryAnalyzer: Analyzer<CliContext> =
    fun ctx -> async {
        let fileCategories = fileLevelAllowedCategories ctx.SourceText

        return
            ctx.GetAllSymbolUsesOfFile()
            |> Seq.choose (fun symbolUse -> ruleFor symbolUse.Symbol |> Option.map (fun rule -> rule, symbolUse.Range))
            |> Seq.filter (fun (rule, range) -> not (isAllowed ctx.SourceText fileCategories range.StartLine rule.Category))
            |> Seq.map (fun (rule, range) -> toMessage rule range)
            |> Seq.toList
    }
```

A caller who bypasses `IHttp` sees a build warning naming the exact call and the exact replacement, the same as
any `Axial.Guardrails` finding.

## Packaging it as a self-installing NuGet package

The install experience described in [Installation](/getting-started/installation.html#add-effect-boundary-guardrails-optional)
— `dotnet add package`, and the check runs on the next build, with no MSBuild editing — comes from three things in
the package, not from anything Axial-specific. Copy this recipe from `src/Axial.Guardrails/Axial.Guardrails.fsproj`
and `src/Axial.Guardrails/build/`:

1. **Bundle the analyzer's dependency closure.** Set `CopyLocalLockFileAssemblies` to `true` so your build output
   folder contains every transitive dependency next to your analyzer DLL, not just your own assembly. Pack that
   whole folder under `analyzers/` in the `.nupkg`, and set `IncludeBuildOutput` to `false` so consumers never
   compile against it directly.
2. **Bundle the `fsharp-analyzers` CLI.** It's packaged as a .NET tool, which NuGet won't let you reference as an
   ordinary `PackageReference` (`NU1212`). Use `PackageDownload` instead, at your own build time only, and copy the
   downloaded CLI's files into your package under `cli/` at pack time.
3. **Ship `build/{YourPackageId}.props` and `build/{YourPackageId}.targets`.** NuGet auto-imports these into any
   project that references your package. The `.props` file sets your severity and enabled/disabled default
   properties; the `.targets` file adds an `AfterTargets="Build"` step that runs the bundled CLI against the
   consumer's project, pointed at your bundled `analyzers/` and `cli/` folders.

The one subtlety worth calling out: don't use `FSharp.Analyzers.Build`'s own `AfterBuild` target for this. It
hardcodes `IgnoreExitCode="true"`, so it can never fail a build regardless of severity. Call the CLI directly with
a plain MSBuild `<Exec>` instead, and let its normal exit-code handling do the work — the CLI itself exits
non-zero only when a finding is `--treat-as-error`.

## Should this be a shared package instead of a copy?

Given how small and self-contained the reusable part is, start by copying the three files and the packaging
recipe into your own repository. Don't add a dependency on `Axial.Guardrails` or any other `Axial.*` package to do
it — the pattern is a template, not a plugin surface, and an effect-ban analyzer for your own package shouldn't
require anyone to adopt Axial. Revisit extracting a shared "analyzer authoring" package only if you find yourself
maintaining several of these and the duplication starts to hurt.
